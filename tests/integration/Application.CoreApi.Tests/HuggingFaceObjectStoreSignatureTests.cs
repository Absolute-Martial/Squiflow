using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.CoreApi.Storage;
using Application.ObjectStorage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.CoreApi.Tests;

// Controlled HTTP regressions verify emitted SigV4 requests and streamed-content lifetime.
// They do not qualify a live provider or real credentials.
public sealed class HuggingFaceObjectStoreSignatureTests
{
    private const string AccessKeyId = "HFAKPROBEPROBEPROBEPROBE";
    private const string SecretAccessKey = "probe-secret-not-a-real-credential";

    private sealed class CapturedRequest
    {
        internal required HttpMethod Method { get; init; }
        internal required Uri Uri { get; init; }
        internal required Dictionary<string, string> Headers { get; init; }
        internal required byte[] Body { get; init; }
    }

    private sealed class RecordingHandler(HttpStatusCode status, List<CapturedRequest> captured)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            // Host is a strongly typed property on HttpRequestMessage and is not enumerated
            // from Headers, yet it is always signed, so record it explicitly.
            headers["host"] = request.RequestUri!.Host;
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(',', header.Value);
            }

            if (request.Content is not null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers[header.Key] = string.Join(',', header.Value);
                }
            }

            captured.Add(new CapturedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Headers = headers,
                Body = body,
            });

            return new HttpResponseMessage(status) { Content = new StringContent(string.Empty) };
        }
    }

    private static HuggingFaceObjectStoreConfiguration Configuration() =>
        HuggingFaceObjectStoreConfiguration.From(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Enabled"] = "true",
                ["ObjectStorage:Endpoint"] = "https://s3.hf.co",
                ["ObjectStorage:Namespace"] = "godetret",
                ["ObjectStorage:Bucket"] = "SDF_Test",
                ["ObjectStorage:AccessKeyId"] = AccessKeyId,
                ["ObjectStorage:SecretAccessKey"] = SecretAccessKey,
                ["ObjectStorage:ProviderScope"] = "probe",
                ["ObjectStorage:MaximumRetainedBytes"] = "1048576",
                ["ObjectStorage:RequestTimeoutSeconds"] = "30",
            })
            .Build());

    [Fact]
    public async Task StreamedPutPreservesItsHashWhenHttpContentDisposesTheReadWrapper()
    {
        var payload = Payload();
        using var handler = new SuccessfulStreamingHandler(payload);
        using var http = new HttpClient(handler);
        var store = new HuggingFaceObjectStore(http, Configuration());
        using var content = new System.IO.MemoryStream(payload);

        var result = await store.PutAsync(new ObjectStorePutRequest(
            ObjectStoreKey.Create("qualification/streamed-put.txt"), content,
            payload.Length, Sha256Hex(payload), "text/plain"), CancellationToken.None);

        Assert.Equal(ObjectStorePutOutcome.Created, result.Outcome);
        Assert.Equal(payload, handler.UploadedBytes);
        Assert.Contains(HttpMethod.Put, handler.Methods);
        Assert.Contains(HttpMethod.Head, handler.Methods);
        Assert.Contains(HttpMethod.Get, handler.Methods);
        Assert.True(content.CanRead);
    }

    private sealed class SuccessfulStreamingHandler(byte[] payload) : HttpMessageHandler
    {
        internal byte[]? UploadedBytes { get; private set; }
        internal List<HttpMethod> Methods { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            if (request.Method == HttpMethod.Put)
            {
                using var uploaded = new System.IO.MemoryStream();
                await request.Content!.CopyToAsync(uploaded, cancellationToken).ConfigureAwait(false);
                UploadedBytes = uploaded.ToArray();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) };
        }
    }

    private static byte[] Payload() => Encoding.UTF8.GetBytes("object-store-provider-probe\n");

    private static string Sha256Hex(byte[] body) =>
        Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();

    private static async Task<CapturedRequest> CaptureAsync(
        string assertedSha256,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var captured = new List<CapturedRequest>();
        using var http = new HttpClient(new RecordingHandler(status, captured));
        var store = new HuggingFaceObjectStore(http, Configuration());
        var body = Payload();

        _ = assertedSha256;
        await store.GetMetadataAsync(
            ObjectStoreKey.Create("qualification/probe.txt"), CancellationToken.None).ConfigureAwait(false);

        return Assert.Single(captured);
    }

    private static (string DateStamp, string Region, string Service, string SignedHeaders, string Signature)
        ParseAuthorization(string authorization)
    {
        // "AWS4-HMAC-SHA256 Credential=<key>/<date>/<region>/<service>/aws4_request,
        //  SignedHeaders=a;b;c, Signature=<hex>". None of the three fields contains ", ".
        var fields = authorization.Split(", ");
        Assert.Equal(3, fields.Length);

        var algorithm = fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, algorithm.Length);
        Assert.Equal("AWS4-HMAC-SHA256", algorithm[0]);
        Assert.StartsWith("Credential=", algorithm[1], StringComparison.Ordinal);

        var credential = algorithm[1][(algorithm[1].IndexOf('=') + 1)..];
        var scopeParts = credential.Split('/');
        Assert.Equal(5, scopeParts.Length);
        Assert.Equal("aws4_request", scopeParts[4]);

        var signedHeaders = fields[1][(fields[1].IndexOf("SignedHeaders=", StringComparison.Ordinal) + "SignedHeaders=".Length)..];
        var signature = fields[2][(fields[2].IndexOf("Signature=", StringComparison.Ordinal) + "Signature=".Length)..];
        return (scopeParts[1], scopeParts[2], scopeParts[3], signedHeaders, signature);
    }

    private static byte[] Hmac(byte[] key, string value) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));

    private static string ExpectedSignature(CapturedRequest request, (string DateStamp, string Region, string Service, string SignedHeaders, string Signature) parts, string? canonicalPathOverride = null)
    {
        var payloadHash = request.Headers["x-amz-content-sha256"];
        var canonicalHeaders = string.Concat(parts.SignedHeaders
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(name => $"{name}:{request.Headers[name].Trim()}\n"));
        var canonicalRequest = string.Join(
            '\n',
            request.Method.Method,
            canonicalPathOverride ?? request.Uri.AbsolutePath,
            string.Empty,
            canonicalHeaders,
            parts.SignedHeaders,
            payloadHash);
        var scope = $"{parts.DateStamp}/{parts.Region}/{parts.Service}/aws4_request";
        var stringToSign =
            $"AWS4-HMAC-SHA256\n{request.Headers["x-amz-date"]}\n{scope}\n{Sha256Hex(Encoding.UTF8.GetBytes(canonicalRequest))}";

        var key = Hmac(Encoding.UTF8.GetBytes("AWS4" + SecretAccessKey), parts.DateStamp);
        key = Hmac(key, parts.Region);
        key = Hmac(key, parts.Service);
        key = Hmac(key, "aws4_request");
        return Convert.ToHexString(Hmac(key, stringToSign)).ToLowerInvariant();
    }

    [Fact]
    public async Task SignatureMatchesAnIndependentRecomputationFromTheRequestOnTheWire()
    {
        var request = await CaptureAsync(Sha256Hex(Payload()));
        var parts = ParseAuthorization(request.Headers["Authorization"]);

        // A provider recomputes the signature from the request it actually received. If the
        // store's own value disagrees with that recomputation, every provider call is rejected
        // with SignatureDoesNotMatch however correct the credentials are.
        var expected = ExpectedSignature(request, parts);
        Assert.Equal(expected, parts.Signature);

    }

    [Fact]
    public async Task PayloadHashHeaderIsPresentAndWellFormed()
    {
        var request = await CaptureAsync(Sha256Hex(Payload()));

        // The signer must declare the payload hash it signed over, as lowercase hex, because
        // that is the value a provider recomputes the signature from.
        var payloadHash = request.Headers["x-amz-content-sha256"];
        Assert.Equal(64, payloadHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", payloadHash);
    }

    [Fact]
    public async Task CanonicalPathMatchesTheEscapedWirePath()
    {
        var request = await CaptureAsync(Sha256Hex(Payload()));

        // The request URI is what the transport sends. If the signed canonical path were built
        // from a different accessor, the signature would cover a path the provider never sees.
        Assert.StartsWith("/godetret/SDF_Test/qualification/probe.txt", request.Uri.AbsolutePath, StringComparison.Ordinal);
        Assert.DoesNotContain("%2F", request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }
}

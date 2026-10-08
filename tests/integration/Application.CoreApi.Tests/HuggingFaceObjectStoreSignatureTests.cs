using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.CoreApi.Storage;
using Application.ObjectStorage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.CoreApi.Tests;

// Controlled HTTP regressions verify emitted SigV4 requests, streamed-content lifetime and the
// provider-status/outcome mapping of the object-store adapter. They do not qualify a live provider
// or real credentials. Every case drives the adapter through an injected transport, so the claims
// are about the adapter's own behaviour rather than about Hugging Face.
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

    private sealed class RecordingHandler(
        HttpStatusCode status,
        List<CapturedRequest> captured,
        Func<CapturedRequest, HttpResponseMessage>? responder = null) :
        HttpMessageHandler
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

            var capturedRequest = new CapturedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Headers = headers,
                Body = body,
            };
            captured.Add(capturedRequest);

            return responder?.Invoke(capturedRequest)
                ?? new HttpResponseMessage(status) { Content = new StringContent(string.Empty) };
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

    // The remaining cases below exercise the provider-facing controls the signature tests do not
    // reach: immutable-object admission, the 412 verify-existing path, capacity/permission/error
    // mapping, byte verification of a returned stream, and the redirect allow-list. They live in
    // this file beside the transport harness rather than in a separate IObjectStore contract
    // class because HuggingFaceObjectStore is the only implemented adapter: a shared contract
    // runner would add a layer without a second implementation to falsify it.
    private const string ProbeKey = "qualification/provider-control.txt";

    private static HttpResponseMessage Status(HttpStatusCode status) =>
        new(status) { Content = new StringContent(string.Empty) };

    private static HttpResponseMessage Body(HttpStatusCode status, byte[] body, string contentType = "text/plain")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return new HttpResponseMessage(status) { Content = content };
    }

    private static HttpResponseMessage RedirectTo(Uri location)
    {
        var response = Status(HttpStatusCode.Found);
        response.Headers.Location = location;
        return response;
    }

    // The recording handler is the transport's primary handler, so the adapter's own Location
    // allow-list, not an automatic redirect follower, decides whether a redirect target is ever
    // contacted. A redirect case fails loudly if a transport ever starts following it.
    private static HttpClient ScriptedClient(
        List<CapturedRequest> captured, Func<CapturedRequest, HttpResponseMessage> responder) =>
        new(new RecordingHandler(HttpStatusCode.OK, captured, responder));

    private static byte[] SameLengthVariantOf(byte[] payload)
    {
        var variant = (byte[])payload.Clone();
        variant[^2] = variant[^2] == (byte)'e' ? (byte)'E' : (byte)'e';
        Assert.Equal(payload.Length, variant.Length);
        Assert.NotEqual(payload, variant);
        return variant;
    }

    private static async Task DrainAsync(Stream content)
    {
        var buffer = new byte[81920];
        while (await content.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false) > 0) { }
    }

    [Fact]
    public async Task PutDeclaresIfNoneMatchStarSoAProviderObjectIsNeverOverwritten()
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Put
            ? Status(HttpStatusCode.OK)
            : Body(HttpStatusCode.OK, payload));
        var store = new HuggingFaceObjectStore(http, Configuration());
        using var content = new MemoryStream(payload);

        var result = await store.PutAsync(new ObjectStorePutRequest(
            ObjectStoreKey.Create(ProbeKey), content, payload.Length, Sha256Hex(payload), "text/plain"),
            CancellationToken.None);

        Assert.Equal(ObjectStorePutOutcome.Created, result.Outcome);
        var put = Assert.Single(captured, request => request.Method == HttpMethod.Put);
        // Without the precondition the gateway would overwrite whatever already occupies the
        // content-addressed key, and the bytes a previous import published would silently change.
        Assert.Equal("*", put.Headers["If-None-Match"]);
        Assert.Equal(Sha256Hex(payload), put.Headers["x-amz-content-sha256"]);
        Assert.Equal(payload, put.Body);
        // A created object is only reported after its stored length and digest were read back.
        Assert.Equal(2, captured.Count(request => request.Method == HttpMethod.Head));
        Assert.Single(captured, request => request.Method == HttpMethod.Get);
        Assert.DoesNotContain(captured, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task PreconditionFailedPutHashVerifiesTheStoredObjectAndReportsAlreadyExists()
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Put
            ? Status(HttpStatusCode.PreconditionFailed)
            : Body(HttpStatusCode.OK, payload, "text/csv; charset=utf-8"));
        var store = new HuggingFaceObjectStore(http, Configuration());
        using var content = new MemoryStream(payload);

        var result = await store.PutAsync(new ObjectStorePutRequest(
            ObjectStoreKey.Create(ProbeKey), content, payload.Length, Sha256Hex(payload), "text/plain"),
            CancellationToken.None);

        Assert.Equal(ObjectStorePutOutcome.AlreadyExists, result.Outcome);
        Assert.Equal(payload.Length, result.Metadata!.Length);
        Assert.Equal(Sha256Hex(payload), result.Metadata.Sha256);
        Assert.Equal("text/csv; charset=utf-8", result.Metadata.ContentType);
        // The precondition failure alone is not accepted as proof that the stored bytes are the
        // expected bytes: AlreadyExists is only produced after the existing object was read back
        // and its length and digest verified, and an existing immutable object is never deleted.
        var read = Assert.Single(captured, request => request.Method == HttpMethod.Get);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=", read.Headers["Authorization"], StringComparison.Ordinal);
        Assert.Equal("s3.hf.co", read.Headers["host"]);
        Assert.DoesNotContain(captured, request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task PreconditionFailedPutReportsCorruptWhenTheStoredObjectIsNotTheExpectedLength()
    {
        var payload = Payload();
        var stored = Encoding.UTF8.GetBytes("object-store-provider-probe-extra\n");
        Assert.NotEqual(payload.Length, stored.Length);
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Put
            ? Status(HttpStatusCode.PreconditionFailed)
            : Body(HttpStatusCode.OK, stored));
        var store = new HuggingFaceObjectStore(http, Configuration());
        using var content = new MemoryStream(payload);

        var result = await store.PutAsync(new ObjectStorePutRequest(
            ObjectStoreKey.Create(ProbeKey), content, payload.Length, Sha256Hex(payload), "text/plain"),
            CancellationToken.None);

        // A retained key holding different bytes is a corrupt provider state, not a replay, and it
        // is reported through the adapter's own outcome vocabulary rather than by overwriting.
        Assert.Equal(ObjectStorePutOutcome.Corrupt, result.Outcome);
        Assert.Null(result.Metadata);
        Assert.Equal(payload, Assert.Single(captured, request => request.Method == HttpMethod.Put).Body);
        Assert.DoesNotContain(captured, request => request.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(413)]
    [InlineData(507)]
    public async Task PutMapsProviderCapacityRejectionsToCapacityRejected(int providerStatus)
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Put
            ? Status((HttpStatusCode)providerStatus)
            : Body(HttpStatusCode.OK, payload));
        var store = new HuggingFaceObjectStore(http, Configuration());
        using var content = new MemoryStream(payload);

        var result = await store.PutAsync(new ObjectStorePutRequest(
            ObjectStoreKey.Create(ProbeKey), content, payload.Length, Sha256Hex(payload), "text/plain"),
            CancellationToken.None);

        // A quota/payload/store-capacity refusal must stay distinguishable from a transport
        // failure, because the caller's response to it differs and it must never be retried blindly.
        Assert.Equal(ObjectStorePutOutcome.CapacityRejected, result.Outcome);
        Assert.Single(captured, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task ReadRefusesToPublishStoredBytesWhoseDigestDiffersFromTheRetainedSource()
    {
        var payload = Payload();
        var stored = SameLengthVariantOf(payload);
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, _ => Body(HttpStatusCode.OK, stored));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        Assert.Equal(ObjectStoreReadOutcome.Opened, read.Outcome);
        Assert.NotNull(read.Content);
        await Assert.ThrowsAsync<InvalidDataException>(() => DrainAsync(read.Content!));
    }

    [Fact]
    public async Task ReadRefusesToPublishStoredBytesWhoseLengthDiffersFromTheRetainedSource()
    {
        var payload = Payload();
        var stored = Encoding.UTF8.GetBytes("object-store-provider-probe-extra\n");
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Head
            ? Body(HttpStatusCode.OK, payload)
            : Body(HttpStatusCode.OK, stored));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        // The declared length is what admission accepted, so a body that ends early must fail
        // verification instead of being handed to the caller as a complete retained source.
        Assert.Equal(ObjectStoreReadOutcome.Opened, read.Outcome);
        await Assert.ThrowsAsync<InvalidDataException>(() => DrainAsync(read.Content!));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ObjectStoreReadOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ObjectStoreReadOutcome.PermissionDenied)]
    [InlineData(HttpStatusCode.Unauthorized, ObjectStoreReadOutcome.PermissionDenied)]
    [InlineData(HttpStatusCode.BadGateway, ObjectStoreReadOutcome.Unavailable)]
    public async Task ReadMapsAFailedMetadataProbeToStableOutcomesWithoutOpeningContent(
        HttpStatusCode providerStatus, ObjectStoreReadOutcome expected)
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, _ => Status(providerStatus));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        Assert.Equal(expected, read.Outcome);
        Assert.Null(read.Content);
        Assert.Null(read.Metadata);
        Assert.Single(captured, request => request.Method == HttpMethod.Head);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, ObjectStoreReadOutcome.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, ObjectStoreReadOutcome.PermissionDenied)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ObjectStoreReadOutcome.Unavailable)]
    public async Task ReadMapsAFailedBodyAfterASuccessfulMetadataProbeToStableOutcomes(
        HttpStatusCode providerStatus, ObjectStoreReadOutcome expected)
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Head
            ? Body(HttpStatusCode.OK, payload)
            : Status(providerStatus));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        Assert.Equal(expected, read.Outcome);
        Assert.Null(read.Content);
        Assert.Equal(2, captured.Count);
    }

    [Fact]
    public async Task ReadReportsALostProviderResponseAsTimedOut()
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Head
            ? Body(HttpStatusCode.OK, payload)
            : throw new TaskCanceledException("The provider did not answer in time."));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        // A timed-out body read is retryable; reporting it as a hard failure would discard a
        // retained source that is still perfectly readable.
        Assert.Equal(ObjectStoreReadOutcome.TimedOut, read.Outcome);
        Assert.Null(read.Content);
    }

    [Fact]
    public async Task ReadReportsABrokenProviderConnectionAsUnavailable()
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Head
            ? Body(HttpStatusCode.OK, payload)
            : throw new HttpRequestException("The provider connection was reset."));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        Assert.Equal(ObjectStoreReadOutcome.Unavailable, read.Outcome);
        Assert.Null(read.Content);
    }

    [Theory]
    [InlineData("https://s3.hf.co.attacker.example/stolen")]
    [InlineData("https://attacker.example/stolen")]
    [InlineData("https://huggingface.co.attacker.example/stolen")]
    [InlineData("http://cdn-lfs.hf.co/stolen")]
    public async Task ReadRefusesARedirectOutsideTheHuggingFaceHttpsAllowListWithoutContactingIt(string location)
    {
        var payload = Payload();
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Method == HttpMethod.Head
            ? Body(HttpStatusCode.OK, payload)
            : RedirectTo(new Uri(location)));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        // A provider-chosen Location is untrusted input. Following anything outside the bounded
        // HTTPS Hugging Face allow-list would let the bucket gateway redirect the adapter's
        // authenticated read to an arbitrary host.
        Assert.Equal(ObjectStoreReadOutcome.Unavailable, read.Outcome);
        Assert.Null(read.Content);
        Assert.Equal(2, captured.Count);
        Assert.DoesNotContain(captured, request => request.Uri.ToString() == location);
    }

    [Theory]
    [InlineData("https://cdn-lfs.hf.co/godetret/SDF_Test/qualification/provider-control.txt")]
    [InlineData("https://huggingface.co/godetret/SDF_Test/qualification/provider-control.txt")]
    public async Task ReadFollowsAnAllowedHuggingFaceRedirectWithoutForwardingTheSignedCredential(string location)
    {
        var payload = Payload();
        var target = new Uri(location);
        var captured = new List<CapturedRequest>();
        using var http = ScriptedClient(captured, request => request.Uri == target
            ? Body(HttpStatusCode.OK, payload)
            : request.Method == HttpMethod.Head
                ? Body(HttpStatusCode.OK, payload)
                : RedirectTo(target));
        var store = new HuggingFaceObjectStore(http, Configuration());

        await using var read = await store.OpenReadAsync(
            new(ObjectStoreKey.Create(ProbeKey), payload.Length, Sha256Hex(payload)), CancellationToken.None);

        Assert.Equal(ObjectStoreReadOutcome.Opened, read.Outcome);
        Assert.Equal(3, captured.Count);
        var followed = captured[2];
        Assert.Equal(target, followed.Uri);
        // The signed credential is bound to the gateway host, so the unsigned redirect fetch must
        // not replay the SigV4 Authorization or signing headers to the redirect target.
        Assert.DoesNotContain("Authorization", followed.Headers.Keys);
        Assert.DoesNotContain(followed.Headers.Keys, header => header.StartsWith("x-amz-", StringComparison.OrdinalIgnoreCase));
        await DrainAsync(read.Content!);
    }
}

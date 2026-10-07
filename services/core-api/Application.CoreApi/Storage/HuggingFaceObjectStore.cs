using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.ObjectStorage;

namespace Application.CoreApi.Storage;

internal sealed class HuggingFaceObjectStore(
    HttpClient client,
    HuggingFaceObjectStoreConfiguration configuration) : IObjectStore
{
    private const string Region = "us-east-1";

    public async Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ValidRequest(request)) return new(ObjectStorePutOutcome.InvalidRequest);

        using var hashed = new HashingReadStream(request.Content, request.ExpectedLength);
        using var message = CreateRequest(HttpMethod.Put, request.Key, request.ExpectedSha256, request.ContentType);
        message.Content = new StreamContent(hashed);
        message.Content.Headers.ContentType = new(request.ContentType);
        message.Content.Headers.ContentLength = request.ExpectedLength;
        message.Headers.TryAddWithoutValidation("If-None-Match", "*");

        try
        {
            using var response = await SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                return await VerifyExistingAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new(MapPutFailure(response.StatusCode));
            }

            var actual = hashed.CompleteHash();
            if (hashed.BytesRead != request.ExpectedLength || !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actual), Convert.FromHexString(request.ExpectedSha256)))
            {
                var deletion = await DeleteAsync(request.Key, cancellationToken).ConfigureAwait(false);
                return new(deletion.Outcome == ObjectStoreDeleteOutcome.OutcomeUnknown
                    ? ObjectStorePutOutcome.OutcomeUnknown
                    : ObjectStorePutOutcome.Corrupt);
            }

            return await VerifyRemoteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ObjectStorePutOutcome.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(ObjectStorePutOutcome.OutcomeUnknown);
        }
    }

    public async Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ValidExpected(request.ExpectedLength, request.ExpectedSha256))
            return new(ObjectStoreReadOutcome.Corrupt);

        var metadata = await GetMetadataAsync(request.Key, cancellationToken).ConfigureAwait(false);
        if (metadata.Outcome != ObjectStoreMetadataOutcome.Found || metadata.Metadata is null)
            return new(MapReadMetadataFailure(metadata.Outcome));
        if (metadata.Metadata.Length != request.ExpectedLength)
            return new(ObjectStoreReadOutcome.Corrupt);

        try
        {
            using var signed = CreateRequest(HttpMethod.Get, request.Key, EmptySha256, null);
            var response = await SendAsync(signed, cancellationToken, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            if (IsRedirect(response.StatusCode))
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (!IsAllowedRedirect(location)) return new(ObjectStoreReadOutcome.Unavailable);
                using var redirectTimeout = Timeout(cancellationToken);
                response = await client.GetAsync(location, HttpCompletionOption.ResponseHeadersRead, redirectTimeout.Token)
                    .ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                var outcome = MapReadFailure(response.StatusCode);
                response.Dispose();
                return new(outcome);
            }

            using var contentTimeout = Timeout(cancellationToken);
            var content = await response.Content.ReadAsStreamAsync(contentTimeout.Token).ConfigureAwait(false);
            var verified = new VerifyingReadStream(content, response, request.ExpectedLength, request.ExpectedSha256);
            return new(ObjectStoreReadOutcome.Opened, verified,
                new(request.Key, request.ExpectedLength, request.ExpectedSha256,
                    metadata.Metadata.ContentType));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ObjectStoreReadOutcome.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(ObjectStoreReadOutcome.Unavailable);
        }
    }

    public async Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Head, key, EmptySha256, null);
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(ObjectStoreMetadataOutcome.NotFound);
            if (!response.IsSuccessStatusCode) return new(MapMetadataFailure(response.StatusCode));
            if (response.Content.Headers.ContentLength is not { } length || length < 0)
                return new(ObjectStoreMetadataOutcome.Unavailable);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            return new(ObjectStoreMetadataOutcome.Found, new(key, length, "", contentType));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ObjectStoreMetadataOutcome.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(ObjectStoreMetadataOutcome.Unavailable);
        }
    }

    public async Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Delete, key, EmptySha256, null);
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(ObjectStoreDeleteOutcome.NotFound);
            return response.IsSuccessStatusCode
                ? new(ObjectStoreDeleteOutcome.Deleted)
                : new(MapDeleteFailure(response.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ObjectStoreDeleteOutcome.TimedOut);
        }
        catch (HttpRequestException)
        {
            return new(ObjectStoreDeleteOutcome.OutcomeUnknown);
        }
    }

    private async Task<ObjectStorePutResult> VerifyExistingAsync(ObjectStorePutRequest request, CancellationToken cancellationToken)
    {
        await using var existing = await OpenReadAsync(
            new(request.Key, request.ExpectedLength, request.ExpectedSha256), cancellationToken).ConfigureAwait(false);
        if (existing.Outcome != ObjectStoreReadOutcome.Opened || existing.Content is null)
        {
            return new(existing.Outcome switch
            {
                ObjectStoreReadOutcome.NotFound => ObjectStorePutOutcome.NotFound,
                ObjectStoreReadOutcome.PermissionDenied => ObjectStorePutOutcome.PermissionDenied,
                ObjectStoreReadOutcome.TimedOut => ObjectStorePutOutcome.TimedOut,
                ObjectStoreReadOutcome.Corrupt => ObjectStorePutOutcome.Corrupt,
                _ => ObjectStorePutOutcome.Unavailable,
            });
        }

        var buffer = new byte[81920];
        while (await existing.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0) { }
        return new(ObjectStorePutOutcome.AlreadyExists, existing.Metadata);
    }

    private async Task<ObjectStorePutResult> VerifyRemoteAsync(
        ObjectStorePutRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(request.Key, cancellationToken).ConfigureAwait(false);
        if (metadata.Outcome != ObjectStoreMetadataOutcome.Found || metadata.Metadata is null)
        {
            return new(metadata.Outcome switch
            {
                ObjectStoreMetadataOutcome.NotFound => ObjectStorePutOutcome.NotFound,
                ObjectStoreMetadataOutcome.PermissionDenied => ObjectStorePutOutcome.PermissionDenied,
                ObjectStoreMetadataOutcome.TimedOut => ObjectStorePutOutcome.TimedOut,
                _ => ObjectStorePutOutcome.OutcomeUnknown,
            });
        }

        if (metadata.Metadata.Length != request.ExpectedLength)
            return new(ObjectStorePutOutcome.Corrupt);

        try
        {
            await using var read = await OpenReadAsync(
                new(request.Key, request.ExpectedLength, request.ExpectedSha256), cancellationToken)
                .ConfigureAwait(false);
            if (read.Outcome != ObjectStoreReadOutcome.Opened || read.Content is null)
            {
                return new(read.Outcome switch
                {
                    ObjectStoreReadOutcome.NotFound => ObjectStorePutOutcome.NotFound,
                    ObjectStoreReadOutcome.PermissionDenied => ObjectStorePutOutcome.PermissionDenied,
                    ObjectStoreReadOutcome.TimedOut => ObjectStorePutOutcome.TimedOut,
                    ObjectStoreReadOutcome.Corrupt => ObjectStorePutOutcome.Corrupt,
                    _ => ObjectStorePutOutcome.OutcomeUnknown,
                });
            }

            var buffer = new byte[81920];
            while (await read.Content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0) { }
            return new(ObjectStorePutOutcome.Created,
                new(request.Key, request.ExpectedLength, request.ExpectedSha256, request.ContentType));
        }
        catch (InvalidDataException)
        {
            return new(ObjectStorePutOutcome.Corrupt);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, ObjectStoreKey key, string payloadHash,
        string? contentType)
    {
        var uri = BuildUri(key);
        var request = new HttpRequestMessage(method, uri);
        var timestamp = DateTimeOffset.UtcNow;
        var amzDate = timestamp.ToString("yyyyMMdd'T'HHmmss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var date = timestamp.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        request.Headers.Host = uri.Host;
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        if (contentType is not null) request.Headers.TryAddWithoutValidation("content-type", contentType);

        var signedHeaders = contentType is null
            ? "host;x-amz-content-sha256;x-amz-date"
            : "content-type;host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = BuildCanonicalRequest(method, uri, payloadHash, amzDate, contentType);
        var scope = $"{date}/{Region}/s3/aws4_request";
        var signature = Sign(date, "AWS4-HMAC-SHA256\n" + amzDate + "\n" + scope + "\n" + Hash(canonicalRequest));
        request.Headers.TryAddWithoutValidation("Authorization",
            $"AWS4-HMAC-SHA256 Credential={configuration.AccessKeyId}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        return request;
    }

    private Uri BuildUri(ObjectStoreKey key)
    {
        var prefix = configuration.Endpoint.AbsoluteUri.TrimEnd('/');
        return new Uri($"{prefix}/{Escape(configuration.Namespace)}/{Escape(configuration.Bucket)}/{string.Join('/', key.Value.Split('/').Select(Escape))}");
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string CanonicalPath(Uri uri)
    {
        var path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static string BuildCanonicalRequest(HttpMethod method, Uri uri, string payloadHash, string amzDate,
        string? contentType)
    {
        var signedHeaders = contentType is null
            ? "host;x-amz-content-sha256;x-amz-date"
            : "content-type;host;x-amz-content-sha256;x-amz-date";
        var canonicalHeaders = contentType is null
            ? $"host:{uri.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n"
            : $"content-type:{contentType}\nhost:{uri.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        return $"{method.Method}\n{CanonicalPath(uri)}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        using var timeout = Timeout(cancellationToken);
        return await client.SendAsync(request, completion, timeout.Token).ConfigureAwait(false);
    }

    private CancellationTokenSource Timeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.RequestTimeoutSeconds));
        return timeout;
    }

    private string Sign(string date, string value)
    {
        var key = Encoding.UTF8.GetBytes("AWS4" + configuration.SecretAccessKey);
        key = Hmac(key, date); key = Hmac(key, Region); key = Hmac(key, "s3"); key = Hmac(key, "aws4_request");
        return Convert.ToHexString(Hmac(key, value)).ToLowerInvariant();
    }

    private static byte[] Hmac(byte[] key, string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static readonly string EmptySha256 = Hash(string.Empty);

    private static bool ValidRequest(ObjectStorePutRequest request) => request.ExpectedLength >= 0
        && request.Content.CanRead && !string.IsNullOrWhiteSpace(request.ContentType) && request.ContentType.Length <= 128
        && ValidExpected(request.ExpectedLength, request.ExpectedSha256);
    private static bool ValidExpected(long length, string? hash) => length >= 0 && hash is not null && hash.Length == 64
        && hash.All(Uri.IsHexDigit) && string.Equals(hash, hash.ToLowerInvariant(), StringComparison.Ordinal);
    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and <= 399;
    private static bool IsAllowedRedirect(Uri? uri) => uri is not null && uri.Scheme == Uri.UriSchemeHttps
        && (uri.Host.EndsWith(".hf.co", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".huggingface.co", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("huggingface.co", StringComparison.OrdinalIgnoreCase));

    private static ObjectStorePutOutcome MapPutFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => ObjectStorePutOutcome.PermissionDenied,
        HttpStatusCode.NotFound => ObjectStorePutOutcome.NotFound,
        (HttpStatusCode)429 or HttpStatusCode.RequestEntityTooLarge or (HttpStatusCode)507
            => ObjectStorePutOutcome.CapacityRejected,
        _ when (int)status >= 500 => ObjectStorePutOutcome.Unavailable,
        _ => ObjectStorePutOutcome.InvalidRequest,
    };
    private static ObjectStoreReadOutcome MapReadFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => ObjectStoreReadOutcome.NotFound,
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => ObjectStoreReadOutcome.PermissionDenied,
        _ when (int)status >= 500 => ObjectStoreReadOutcome.Unavailable,
        _ => ObjectStoreReadOutcome.Unavailable,
    };
    private static ObjectStoreReadOutcome MapReadMetadataFailure(ObjectStoreMetadataOutcome outcome) => outcome switch
    {
        ObjectStoreMetadataOutcome.NotFound => ObjectStoreReadOutcome.NotFound,
        ObjectStoreMetadataOutcome.PermissionDenied => ObjectStoreReadOutcome.PermissionDenied,
        ObjectStoreMetadataOutcome.TimedOut => ObjectStoreReadOutcome.TimedOut,
        _ => ObjectStoreReadOutcome.Unavailable,
    };
    private static ObjectStoreMetadataOutcome MapMetadataFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => ObjectStoreMetadataOutcome.PermissionDenied,
        HttpStatusCode.RequestTimeout or (HttpStatusCode)504 => ObjectStoreMetadataOutcome.TimedOut,
        _ => ObjectStoreMetadataOutcome.Unavailable,
    };
    private static ObjectStoreDeleteOutcome MapDeleteFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => ObjectStoreDeleteOutcome.PermissionDenied,
        (HttpStatusCode)408 or (HttpStatusCode)504 => ObjectStoreDeleteOutcome.TimedOut,
        _ when (int)status >= 500 => ObjectStoreDeleteOutcome.Unavailable,
        _ => ObjectStoreDeleteOutcome.Unavailable,
    };

    private sealed class HashingReadStream(Stream inner, long expectedLength) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private string? _completedHash;
        internal long BytesRead { get; private set; }
        internal string CompleteHash() => _completedHash ??= Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => expectedLength;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(Span<byte> buffer) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                BytesRead += read;
                _hash.AppendData(buffer.Span[..read]);
            }
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // StreamContent can dispose this non-seekable source after serialization,
                // before PutAsync verifies its length and digest.
                _ = CompleteHash();
                _hash.Dispose();
            }
            base.Dispose(disposing);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class VerifyingReadStream(Stream content, HttpResponseMessage response, long expectedLength, string expectedHash)
        : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _read;
        private bool _verified;
        public override bool CanRead => content.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => expectedLength;
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(Span<byte> buffer) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0) { _read += read; _hash.AppendData(buffer.Span[..read]); }
            else Verify();
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        private void Verify()
        {
            if (_verified) return;
            _verified = true;
            var actual = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
            if (_read != expectedLength || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(expectedHash)))
                throw new InvalidDataException("The stored object failed integrity verification.");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { content.Dispose(); response.Dispose(); _hash.Dispose(); }
            base.Dispose(disposing);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

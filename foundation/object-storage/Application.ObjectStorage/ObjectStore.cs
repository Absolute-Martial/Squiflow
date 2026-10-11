namespace Application.ObjectStorage;

public sealed class ObjectStoreKey
{
    private ObjectStoreKey(string value) => Value = value;

    public string Value { get; }

    public static ObjectStoreKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 || value.StartsWith('/') || value.EndsWith('/')
            || value.StartsWith("./", StringComparison.Ordinal) || value.EndsWith("..", StringComparison.Ordinal)
            || value.Contains("//", StringComparison.Ordinal) || value.Contains("../", StringComparison.Ordinal)
            || value.Contains('\\') || value.Contains('\0') || value.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The object key is invalid.", nameof(value));
        }

        return new ObjectStoreKey(value);
    }

    public override string ToString() => Value;
}

public sealed record ObjectStorePutRequest(
    ObjectStoreKey Key,
    Stream Content,
    long ExpectedLength,
    string ExpectedSha256,
    string ContentType);

public enum ObjectStorePutOutcome
{
    Created = 1,
    AlreadyExists = 2,
    InvalidRequest = 3,
    CapacityRejected = 4,
    PermissionDenied = 5,
    NotFound = 6,
    Unavailable = 7,
    TimedOut = 8,
    OutcomeUnknown = 9,
    Corrupt = 10,
}

public sealed record ObjectStorePutResult(
    ObjectStorePutOutcome Outcome,
    ObjectStoreMetadata? Metadata = null);

public sealed record ObjectStoreReadRequest(
    ObjectStoreKey Key,
    long ExpectedLength,
    string ExpectedSha256);

public enum ObjectStoreReadOutcome
{
    Opened = 1,
    NotFound = 2,
    PermissionDenied = 3,
    Unavailable = 4,
    TimedOut = 5,
    Corrupt = 6,
}

public sealed record ObjectStoreReadResult(
    ObjectStoreReadOutcome Outcome,
    Stream? Content = null,
    ObjectStoreMetadata? Metadata = null) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content is IAsyncDisposable disposable
        ? disposable.DisposeAsync()
        : ValueTask.CompletedTask;
}

public enum ObjectStoreMetadataOutcome
{
    Found = 1,
    NotFound = 2,
    PermissionDenied = 3,
    Unavailable = 4,
    TimedOut = 5,
}

public sealed record ObjectStoreMetadataResult(
    ObjectStoreMetadataOutcome Outcome,
    ObjectStoreMetadata? Metadata = null);

public enum ObjectStoreDeleteOutcome
{
    Deleted = 1,
    NotFound = 2,
    PermissionDenied = 3,
    Unavailable = 4,
    TimedOut = 5,
    OutcomeUnknown = 6,
}

public sealed record ObjectStoreDeleteResult(ObjectStoreDeleteOutcome Outcome);

public sealed record ObjectStoreMetadata(
    ObjectStoreKey Key,
    long Length,
    string Sha256,
    string ContentType,
    string? Version = null);

public static class ObjectStoreRetirement
{
    public const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    public const string ContentType = "application/octet-stream";
}

public interface IObjectStore
{
    Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken);

    Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken);

    Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken);

    Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken);

    // Atomically replace observed bytes with a permanent zero-byte marker, or create it
    // conditionally when absent. A stale request must never delete or replace a newer marker.
    // Unsupported adapters fail closed; an ordinary delete is not retirement evidence.
    Task<ObjectStorePutResult> RetireAsync(ObjectStoreKey key, CancellationToken cancellationToken) =>
        Task.FromResult(new ObjectStorePutResult(ObjectStorePutOutcome.Unavailable));
}

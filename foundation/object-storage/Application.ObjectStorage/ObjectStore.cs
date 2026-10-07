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
            || value.Contains('\\') || value.Contains('\0'))
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
    string ContentType);

public interface IObjectStore
{
    Task<ObjectStorePutResult> PutAsync(ObjectStorePutRequest request, CancellationToken cancellationToken);

    Task<ObjectStoreReadResult> OpenReadAsync(ObjectStoreReadRequest request, CancellationToken cancellationToken);

    Task<ObjectStoreMetadataResult> GetMetadataAsync(ObjectStoreKey key, CancellationToken cancellationToken);

    Task<ObjectStoreDeleteResult> DeleteAsync(ObjectStoreKey key, CancellationToken cancellationToken);
}

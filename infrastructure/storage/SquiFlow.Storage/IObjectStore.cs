namespace SquiFlow.Storage;

public readonly record struct ObjectKey(string Value);

public sealed record StoredObject(ObjectKey Key, Stream Content, long? Length, string? ContentType);

public interface IObjectStore
{
    Task PutAsync(
        ObjectKey key,
        Stream content,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<StoredObject?> GetAsync(ObjectKey key, CancellationToken cancellationToken = default);

    Task DeleteAsync(ObjectKey key, CancellationToken cancellationToken = default);
}

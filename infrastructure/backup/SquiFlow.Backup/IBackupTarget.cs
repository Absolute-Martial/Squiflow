namespace SquiFlow.Backup;

public readonly record struct BackupArchiveId(string Value);

public interface IBackupTarget
{
    Task UploadAsync(
        BackupArchiveId archiveId,
        Stream encryptedArchive,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        BackupArchiveId archiveId,
        CancellationToken cancellationToken = default);
}

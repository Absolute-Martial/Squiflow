namespace Application.PlatformAdministration;

public sealed record PlatformAdminAuditPosition(DateTimeOffset OccurredAt, Guid Id);

public sealed record PlatformAdminAccessAuditQuery
{
    private PlatformAdminAccessAuditQuery(int limit, PlatformAdminAuditPosition? before,
        Guid? principalId, PlatformAdminAccessAuditOutcome? outcome)
    {
        Limit = limit;
        Before = before;
        PrincipalId = principalId;
        Outcome = outcome;
    }

    public int Limit { get; }
    public PlatformAdminAuditPosition? Before { get; }
    public Guid? PrincipalId { get; }
    public PlatformAdminAccessAuditOutcome? Outcome { get; }

    public static PlatformAdminAccessAuditQuery Create(int limit, PlatformAdminAuditPosition? before = null,
        Guid? principalId = null, PlatformAdminAccessAuditOutcome? outcome = null)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (principalId == Guid.Empty) throw new ArgumentException("Principal identity cannot be empty.", nameof(principalId));
        if (outcome is { } value && !Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(outcome));
        if (before is { Id: var id } && id == Guid.Empty) throw new ArgumentException("Audit position cannot be empty.", nameof(before));
        return new(limit, before, principalId, outcome);
    }
}

// Deliberately omits provider identities and certificate fingerprints from the retained access evidence.
public sealed record PlatformAdminAccessAuditSummary(Guid Id, Guid? PrincipalId, Guid? DeviceId,
    string Operation, PlatformAdminAccessAuditOutcome Outcome, string Reason, DateTimeOffset OccurredAt);

public sealed record PlatformAdminAccessAuditPage(IReadOnlyList<PlatformAdminAccessAuditSummary> Items,
    PlatformAdminAuditPosition? NextPosition);

public interface IPlatformAdminAuditReader
{
    Task<PlatformAdminAccessAuditPage> BrowseAccessAsync(PlatformAdminAccessAuditQuery query,
        CancellationToken cancellationToken);
}

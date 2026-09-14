namespace SquiFlow.ApplicationKernel.Presentation;

public readonly record struct WorkspaceId
{
    public WorkspaceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workspace id must be non-empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record WorkspaceContributionMetadata(
    WorkspaceId WorkspaceId,
    string Label,
    int Order,
    ModuleId OwnerModuleId,
    FeatureId? RequiredFeature = null,
    PermissionId? RequiredPermission = null);

public interface IWorkspaceContribution<out TView>
{
    WorkspaceContributionMetadata Metadata { get; }
    TView CreateView();
}

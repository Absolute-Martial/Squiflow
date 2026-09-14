namespace SquiFlow.ApplicationKernel.Tenancy;

public sealed record TenantContext(TenantId TenantId, SubjectId SubjectId);

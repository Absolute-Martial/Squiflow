namespace Application.Orders;

public sealed record CommittedOrderLineReference(
    Guid TenantId,
    Guid OrderId,
    long OrderRevision,
    int LinePosition);

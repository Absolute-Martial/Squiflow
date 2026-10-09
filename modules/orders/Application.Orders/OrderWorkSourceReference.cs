using Application.Catalog;

namespace Application.Orders;

public sealed record OrderWorkSourceReference(Guid TenantId, Guid OrderId, long OrderRevision, Guid ProgramId,
    bool IsCommitted, int LineCount, int LinePosition, decimal OrderedQuantity, CatalogLineFacts? Catalog);

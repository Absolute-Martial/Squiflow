using Application.Customers;

namespace Application.Orders;

public sealed record OrderFinancialSourceLine(
    int Position,
    string Description,
    decimal Quantity,
    string UnitCode,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderFinancialSourceFacts(
    Guid TenantId,
    Guid OrderId,
    long Revision,
    bool IsCommitted,
    string CurrencyCode,
    CustomerOrderContext? CustomerContext,
    IReadOnlyList<OrderFinancialSourceLine> Lines,
    decimal Total);

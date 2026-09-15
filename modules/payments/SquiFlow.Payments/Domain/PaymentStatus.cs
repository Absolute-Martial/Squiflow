namespace SquiFlow.Payments.Domain;

/// <summary>
/// Represents the documented SquiFlow payment-status vocabulary.
/// Transition rules, monetary semantics, settlement, persistence, and authority are deliberately
/// outside this type because the current slice does not introduce those responsibilities.
/// </summary>
public enum PaymentStatus
{
    NotStarted,
    Pending,
    Succeeded,
    Failed,
    OutcomeUnknown,
    PartiallyRefunded,
    Refunded,
    Reversed,
}

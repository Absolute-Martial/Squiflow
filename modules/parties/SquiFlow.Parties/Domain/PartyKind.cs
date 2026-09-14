namespace SquiFlow.Parties.Domain;

/// <summary>
/// Describes the structural kind of a Party in the documented SquiFlow domain model.
/// Customer, supplier, representative, account, and commercial-relationship meanings are
/// deliberately not encoded here because those semantics remain separate or discovery-sensitive.
/// </summary>
public enum PartyKind
{
    Person,
    Organization,
}

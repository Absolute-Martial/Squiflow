using SquiFlow.Parties.Domain;

namespace SquiFlow.Parties.Tests.Domain;

public sealed class PartyKindTests
{
    [Fact]
    public void Accepted_kinds_match_the_documented_party_semantics()
    {
        var kinds = Enum.GetValues<PartyKind>();

        Assert.Equal(2, kinds.Length);
        Assert.Contains(PartyKind.Person, kinds);
        Assert.Contains(PartyKind.Organization, kinds);
    }
}

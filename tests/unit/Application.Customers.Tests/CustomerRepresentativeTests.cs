using Application.Customers;
using Xunit;

namespace Application.Customers.Tests;

public sealed class CustomerRepresentativeTests
{
    [Fact]
    public void LinkIntentBindsOrganizationProgramAndIndividualWithoutGrantMeaning()
    {
        var organizationId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var individualId = Guid.NewGuid();

        var organizationLink = CustomerRepresentativeLinkIntent.Create(new(
            organizationId, null, individualId));
        var programLink = CustomerRepresentativeLinkIntent.Create(new(
            organizationId, programId, individualId));

        Assert.Equal(organizationId, organizationLink.OrganizationId);
        Assert.Null(organizationLink.ProgramId);
        Assert.Equal(individualId, organizationLink.IndividualId);
        Assert.NotEqual(organizationLink.Fingerprint, programLink.Fingerprint);
    }

    [Fact]
    public void RepresentativeIntentsRejectMissingIdentityAndStaleRevisionShape()
    {
        var organizationId = Guid.NewGuid();
        var individualId = Guid.NewGuid();
        var representativeId = Guid.NewGuid();

        Assert.Equal("organization_id_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerRepresentativeLinkIntent.Create(new(Guid.Empty, null, individualId))).Code);
        Assert.Equal("individual_id_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerRepresentativeLinkIntent.Create(new(organizationId, null, Guid.Empty))).Code);
        Assert.Equal("representative_id_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerRepresentativeUnlinkIntent.Create(new(organizationId, Guid.Empty, 1))).Code);
        Assert.Equal("revision_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerRepresentativeUnlinkIntent.Create(new(organizationId, representativeId, 0))).Code);
    }
}

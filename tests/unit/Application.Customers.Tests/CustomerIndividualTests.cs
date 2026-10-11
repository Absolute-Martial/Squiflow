using Application.Customers;
using Xunit;

namespace Application.Customers.Tests;

public sealed class CustomerIndividualTests
{
    [Fact]
    public void CreateIntentNormalizesContactsAndBindsEachToFingerprint()
    {
        var first = CustomerIndividualIntent.Create(new("  Cafe\u0301  ", " me@example.test ", " +1 555 "));
        var equivalent = CustomerIndividualIntent.Create(new("Café", "me@example.test", "+1 555"));
        var changed = CustomerIndividualIntent.Create(new("Café", "other@example.test", "+1 555"));
        Assert.Equal("Café", first.DisplayName);
        Assert.Equal("me@example.test", first.Email);
        Assert.Equal("+1 555", first.Phone);
        Assert.Equal(first.Fingerprint, equivalent.Fingerprint);
        Assert.NotEqual(first.Fingerprint, changed.Fingerprint);
    }

    [Theory]
    [InlineData("", null, "display_name_invalid")]
    [InlineData("Name", "bad-address", "email_invalid")]
    [InlineData("Name", "a@@b", "email_invalid")]
    [InlineData("Name", "a b@example.test", "email_invalid")]
    [InlineData("Name", "Name <a@example.test>", "email_invalid")]
    [InlineData("Name", "a@", "email_invalid")]
    public void CreateIntentRejectsInvalidNameAndEmail(string name, string? email, string code)
    {
        var error = Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualIntent.Create(new(name, email, null)));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void CreateIntentRejectsOversizedOrMalformedContact()
    {
        var longEmail = Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualIntent.Create(new("Name", new string('a', 255) + "@b", null)));
        var malformed = Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualIntent.Create(new("Name", null, "\ud800")));
        var badPhone = Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualIntent.Create(new("Name", null, "call me")));
        var noDigits = Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualIntent.Create(new("Name", null, "---")));
        Assert.Equal("email_invalid", longEmail.Code);
        Assert.Equal("phone_invalid", malformed.Code);
        Assert.Equal("phone_invalid", badPhone.Code);
        Assert.Equal("phone_invalid", noDigits.Code);
    }

    [Fact]
    public void AvailabilityIntentRejectsMissingIdRevisionAndUnknownState()
    {
        var id = Guid.NewGuid();
        Assert.Equal("individual_id_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualAvailabilityIntent.Create(new(Guid.Empty, 1,
                CustomerIndividualAvailability.Inactive))).Code);
        Assert.Equal("revision_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualAvailabilityIntent.Create(new(id, 0,
                CustomerIndividualAvailability.Inactive))).Code);
        Assert.Equal("availability_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualAvailabilityIntent.Create(new(id, 1,
                (CustomerIndividualAvailability)99))).Code);
    }

    [Fact]
    public void ContactEditIntentNormalizesFieldsAndBindsRevisionIntoFingerprint()
    {
        var id = Guid.NewGuid();
        var first = CustomerIndividualContactIntent.Create(new(
            id, 4, "  Cafe\u0301 Person  ", " person@example.test ", " +977 12345 "));
        var equivalent = CustomerIndividualContactIntent.Create(new(
            id, 4, "Café Person", "person@example.test", "+977 12345"));
        var staleRevision = CustomerIndividualContactIntent.Create(new(
            id, 3, "Café Person", "person@example.test", "+977 12345"));

        Assert.Equal("Café Person", first.DisplayName);
        Assert.Equal("person@example.test", first.Email);
        Assert.Equal("+977 12345", first.Phone);
        Assert.Equal(first.Fingerprint, equivalent.Fingerprint);
        Assert.NotEqual(first.Fingerprint, staleRevision.Fingerprint);
        Assert.Equal("revision_invalid", Assert.Throws<CustomerValidationException>(() =>
            CustomerIndividualContactIntent.Create(new(id, 0, "Person", null, null))).Code);
    }
}

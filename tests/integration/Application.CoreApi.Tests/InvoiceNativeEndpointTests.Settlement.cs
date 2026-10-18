using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace Application.CoreApi.Tests;

public sealed partial class InvoiceNativeEndpointTests
{
    private const string PrivatePaymentEvidence = "private-customer-payment-evidence";
    private static string PaymentPath(Guid tenant, Guid payment) => $"/api/v1/tenants/{tenant:D}/customer-payments/{payment:D}";
    private static object CashReceipt(Guid organization) => new
    {
        debtorKind = "Organization",
        debtorId = organization,
        currencyCode = "NPR",
        amount = 50m,
        channel = "Cash",
        evidence = PrivatePaymentEvidence,
    };

    private static string FinancialToken(WhiteLabelApiFactory identity, Guid actor) => identity.CreateToken(actor.ToString("D"), extraClaims:
    [
        new Claim("azp", "tenant-web-financial-test"), new Claim("acr", "urn:application:assurance:financial-test"),
        new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
    ]);

    private async Task<Guid> VerifySettlementAsync(HttpClient client, WhiteLabelApiFactory identity, HttpClient fga,
        string storeId, string modelId, Guid tenant, Guid other, Guid owner, Guid recorder, Guid viewer, Guid reconciler,
        Guid organization, Guid program, Guid individual, Guid orgInvoice, Guid programInvoice, Guid individualInvoice)
    {
        var recorderToken = identity.CreateToken(recorder.ToString("D"));
        var viewerToken = identity.CreateToken(viewer.ToString("D"));
        var reconcilerToken = identity.CreateToken(reconciler.ToString("D"));
        var ownerToken = identity.CreateToken(owner.ToString("D"));
        var invoicePath = $"/api/v1/tenants/{tenant:D}/receivables/{orgInvoice:D}";
        await SendAsync(client, HttpMethod.Get, invoicePath, viewerToken, null, null, HttpStatusCode.Forbidden);
        await SendAsync(client, HttpMethod.Get, invoicePath, ownerToken, null, null, HttpStatusCode.Forbidden);
        await GrantAsync(fga, storeId, modelId, tenant, recorder, ["customer_payment_recorder"]);
        await GrantAsync(fga, storeId, modelId, tenant, reconciler, ["customer_payment_reconciler"]);
        var financialViewerRole = await GrantRoleAsync(fga, storeId, modelId, tenant, viewer, "financial_viewer");
        var receivable = await SendAsync(client, HttpMethod.Get, invoicePath, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal(34.5m, receivable.GetProperty("outstandingAmount").GetDecimal());
        var zero = await SendAsync(client, HttpMethod.Get, $"/api/v1/tenants/{tenant:D}/receivables/{individualInvoice:D}", viewerToken, null, null, HttpStatusCode.OK);
        Assert.True(zero.GetProperty("isSettled").GetBoolean());
        Assert.Equal(individual, zero.GetProperty("debtorId").GetGuid());

        var paymentId = Guid.NewGuid();
        var path = PaymentPath(tenant, paymentId);
        await SendAsync(client, HttpMethod.Put, path, viewerToken, "viewer-cannot-record", CashReceipt(organization), HttpStatusCode.Forbidden);
        var recorded = await SendAsync(client, HttpMethod.Put, path, recorderToken, "customer-cash-receipt", CashReceipt(organization), HttpStatusCode.Created);
        Assert.Equal(3, recorded.EnumerateObject().Count());
        Assert.Equal(1, recorded.GetProperty("revision").GetInt64());
        Assert.DoesNotContain(PrivatePaymentEvidence, recorded.GetRawText(), StringComparison.Ordinal);
        await SendAsync(client, HttpMethod.Get, path, recorderToken, null, null, HttpStatusCode.Forbidden);
        var pending = await SendAsync(client, HttpMethod.Get, path, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal("Pending", pending.GetProperty("state").GetString());
        Assert.Equal(0m, pending.GetProperty("availableAmount").GetDecimal());

        await GrantAsync(fga, storeId, modelId, tenant, recorder, ["customer_payment_allocator", "customer_payment_reconciler"]);
        var self = await SendAsync(client, HttpMethod.Post, path + "/confirm", recorderToken, "self-confirm",
            new { expectedRevision = 1, evidence = "Receipt counted" }, HttpStatusCode.Conflict);
        Assert.Equal("payment_independent_reconciler_required", self.GetProperty("code").GetString());
        await SendAsync(client, HttpMethod.Post, path + "/allocations", recorderToken, "pending-allocation",
            new { expectedRevision = 1, receivableId = orgInvoice, amount = 20m }, HttpStatusCode.Conflict);
        await SendAsync(client, HttpMethod.Post, path + "/confirm", reconcilerToken, "confirm-cash",
            new { expectedRevision = 1, evidence = "Received cash independently counted" }, HttpStatusCode.OK);
        var mismatch = await SendAsync(client, HttpMethod.Post, path + "/allocations", recorderToken, "cross-debtor-allocation",
            new { expectedRevision = 2, receivableId = programInvoice, amount = 1m }, HttpStatusCode.Conflict);
        Assert.Equal("payment_debtor_mismatch", mismatch.GetProperty("code").GetString());
        var allocated = await SendAsync(client, HttpMethod.Post, path + "/allocations", recorderToken, "partial-allocation",
            new { expectedRevision = 2, receivableId = orgInvoice, amount = 20m }, HttpStatusCode.OK);
        var allocationId = allocated.GetProperty("eventId").GetGuid();
        Assert.Equal(14.5m, (await SendAsync(client, HttpMethod.Get, invoicePath, viewerToken, null, null, HttpStatusCode.OK)).GetProperty("outstandingAmount").GetDecimal());
        var balancePath = $"/api/v1/tenants/{tenant:D}/debtors/Organization/{organization:D}/balance";
        var balance = await SendAsync(client, HttpMethod.Get, balancePath, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal(balance.GetProperty("recognizedAmount").GetDecimal() - 20m, balance.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal(30m, balance.GetProperty("confirmedUnappliedAmount").GetDecimal());
        await SendAsync(client, HttpMethod.Post, path + "/dispute", reconcilerToken, "dispute-cash",
            new { expectedRevision = 3, evidence = "Receipt evidence disputed" }, HttpStatusCode.OK);
        var disputed = await SendAsync(client, HttpMethod.Get, path, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal("Disputed", disputed.GetProperty("state").GetString());
        Assert.Equal(20m, disputed.GetProperty("allocatedAmount").GetDecimal());
        Assert.Equal(0m, disputed.GetProperty("availableAmount").GetDecimal());
        await SendAsync(client, HttpMethod.Post, path + "/allocations", recorderToken, "disputed-allocation",
            new { expectedRevision = 4, receivableId = orgInvoice, amount = 1m }, HttpStatusCode.Conflict);

        await SendAsync(client, HttpMethod.Post, path + "/reverse", ownerToken, "owner-without-correction",
            new { expectedRevision = 4, reason = "Wrong receipt" }, HttpStatusCode.Forbidden);
        await GrantAsync(fga, storeId, modelId, tenant, owner, ["customer_payment_corrector"]);
        await GrantAsync(fga, storeId, modelId, tenant, reconciler, ["customer_payment_corrector"]);
        await SendAsync(client, HttpMethod.Post, path + "/reverse", ownerToken, "owner-without-step-up",
            new { expectedRevision = 4, reason = "Wrong receipt" }, HttpStatusCode.Forbidden);
        await SendAsync(client, HttpMethod.Post, path + "/reverse", FinancialToken(identity, reconciler), "non-owner-correction",
            new { expectedRevision = 4, reason = "Wrong receipt" }, HttpStatusCode.Forbidden);
        var trustedOwner = FinancialToken(identity, owner);
        var active = await SendAsync(client, HttpMethod.Post, path + "/reverse", trustedOwner, "cannot-reverse-active",
            new { expectedRevision = 4, reason = "Wrong receipt" }, HttpStatusCode.Conflict);
        Assert.Equal("payment_has_active_allocations", active.GetProperty("code").GetString());
        await SendAsync(client, HttpMethod.Post, path + $"/allocations/{allocationId:D}/release", trustedOwner, "release-full-allocation",
            new { expectedRevision = 4, reason = "Release the wrong invoice allocation" }, HttpStatusCode.OK);
        Assert.Equal(34.5m, (await SendAsync(client, HttpMethod.Get, invoicePath, viewerToken, null, null, HttpStatusCode.OK)).GetProperty("outstandingAmount").GetDecimal());
        await SendAsync(client, HttpMethod.Post, path + "/confirm", reconcilerToken, "confirm-new-evidence",
            new { expectedRevision = 5, evidence = "Fresh received-funds evidence" }, HttpStatusCode.OK);
        await SendAsync(client, HttpMethod.Post, path + "/reverse", trustedOwner, "reverse-receipt",
            new { expectedRevision = 6, reason = "Correct the original receipt evidence" }, HttpStatusCode.OK);
        var reversed = await SendAsync(client, HttpMethod.Get, path, viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal("Reversed", reversed.GetProperty("state").GetString());
        Assert.Equal(0m, reversed.GetProperty("availableAmount").GetDecimal());
        var history = await SendAsync(client, HttpMethod.Get, path + "/history", viewerToken, null, null, HttpStatusCode.OK);
        Assert.Equal(7, history.GetProperty("items").GetArrayLength());
        Assert.Equal(allocationId, history.GetProperty("items")[4].GetProperty("allocationId").GetGuid());
        Assert.Equal(20m, history.GetProperty("items")[4].GetProperty("amount").GetDecimal());

        var inactivePayment = Guid.NewGuid();
        await SendAsync(client, HttpMethod.Put, PaymentPath(tenant, inactivePayment), recorderToken, "inactive-historical-debtor",
            new { debtorKind = "Individual", debtorId = individual, currencyCode = "NPR", amount = 1m, channel = "Bank", evidence = "Manual bank evidence", externalReference = "  Bank-Case  " }, HttpStatusCode.Created);
        var duplicate = await SendAsync(client, HttpMethod.Put, PaymentPath(tenant, Guid.NewGuid()), recorderToken, "duplicate-bank-reference",
            new { debtorKind = "Program", debtorId = program, currencyCode = "NPR", amount = 1m, channel = "Bank", evidence = "Manual bank evidence", externalReference = "Bank-Case" }, HttpStatusCode.Conflict);
        Assert.Equal("payment_reference_conflict", duplicate.GetProperty("code").GetString());
        await SendAsync(client, HttpMethod.Put, PaymentPath(tenant, Guid.NewGuid()), recorderToken, "case-distinct-bank-reference",
            new { debtorKind = "Program", debtorId = program, currencyCode = "NPR", amount = 1m, channel = "Bank", evidence = "Manual bank evidence", externalReference = "bank-case" }, HttpStatusCode.Created);
        await GrantAsync(fga, storeId, modelId, other, recorder, ["financial_viewer"]);
        await SendAsync(client, HttpMethod.Get, PaymentPath(other, paymentId), recorderToken, null, null, HttpStatusCode.NotFound);
        var empty = await SendAsync(client, HttpMethod.Get, $"/api/v1/tenants/{other:D}/receivables", recorderToken, null, null, HttpStatusCode.OK);
        Assert.Equal(0, empty.GetProperty("items").GetArrayLength());
        await ChangeRoleAssignmentAsync(fga, storeId, modelId, tenant, viewer, financialViewerRole, false);
        await SendAsync(client, HttpMethod.Get, path, viewerToken, null, null, HttpStatusCode.Forbidden);
        await ChangeRoleAssignmentAsync(fga, storeId, modelId, tenant, viewer, financialViewerRole, true);
        return paymentId;
    }
}

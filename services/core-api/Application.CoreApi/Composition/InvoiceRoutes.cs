using System.Text.Json;
using System.Text.Json.Serialization;
using Application.CoreApi.Authorization;
using Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace Application.CoreApi.Composition;

internal static class InvoiceRoutes
{
    internal const int MaximumRequestBodyBytes = 8192;
    private const string OrderPath = "/api/v1/tenants/{tenantId:guid}/orders/{orderId:guid}/invoice";
    private const string InvoicePath = "/api/v1/tenants/{tenantId:guid}/invoices/{invoiceId:guid}";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 6 };

    internal static WebApplication MapInvoiceRoutes(this WebApplication app)
    {
        Protect(app.MapPost(OrderPath, (Guid tenantId, Guid orderId, HttpContext http, IssueInvoice issue) =>
            IssueAsync(tenantId, orderId, http, issue)), EndpointAccess.InvoiceIssue)
            .WithSummary("Issues one full invoice from the exact committed Order revision.")
            .WithDescription("The optional debtor kind is Organization, Program, or Individual. Individual requires individualId. " +
                "An omitted debtor uses the Order's organization. Prices, references and the business date are server-owned. " +
                "JSON is bounded to 8192 bytes and depth 6; duplicate and unknown members are rejected. " +
                "The reply contains only invoiceId; frozen invoice facts require the separate view permission.")
            .Accepts<IssuePayload>("application/json")
            .Produces<InvoiceIssueResponse>(StatusCodes.Status201Created)
            .Produces<InvoiceIssueResponse>(StatusCodes.Status200OK);
        Protect(app.MapGet(InvoicePath, (Guid tenantId, Guid invoiceId, HttpContext http, IInvoiceIssuedFactsReader reader) =>
            ReadAsync(tenantId, invoiceId, false, http, reader)), EndpointAccess.InvoiceView).Produces<InvoiceResponse>();
        Protect(app.MapGet(OrderPath, (Guid tenantId, Guid orderId, HttpContext http, IInvoiceIssuedFactsReader reader) =>
            ReadAsync(tenantId, orderId, true, http, reader)), EndpointAccess.InvoiceView).Produces<InvoiceResponse>();
        return app;
    }

    private static RouteHandlerBuilder Protect(RouteHandlerBuilder route, EndpointAccess access) => route
        .RequireAuthorization().WithCoreApiAccess(access).WithCoreApiApplicationAuthorization(access).WithTags("Invoices")
        .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestBodyBytes))
        .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
        .ProducesProblem(409).ProducesProblem(413).ProducesProblem(429).ProducesProblem(500).ProducesProblem(503).ProducesProblem(504);

    private static async Task<IResult> IssueAsync(Guid tenantId, Guid orderId, HttpContext http, IssueInvoice issue) =>
        await GuardAsync(async () =>
        {
            if (orderId == Guid.Empty || http.Request.Query.Count != 0 || !TryKey(http, out var key)) return Invalid();
            var payload = await ReadPayloadAsync(http).ConfigureAwait(false);
            if (payload.ExpectedOrderRevision < 1 ||
                !TryDebtor(payload.Debtor, out var debtor)) return Invalid();
            var result = await issue.ExecuteAsync(TenantRequestAccess.RequireResolvedContext(tenantId, http),
                new(orderId, payload.ExpectedOrderRevision, debtor, key), http.RequestAborted).ConfigureAwait(false);
            if (result.Status is not (InvoiceIssueStatus.Issued or InvoiceIssueStatus.Replayed)) return IssueFailure(result.Status);
            var invoice = result.Invoice ?? throw new InvalidOperationException("Invoice issue result is missing.");
            RequireInvoice(invoice, tenantId, orderId, true);
            if (invoice.SourceOrderRevision != payload.ExpectedOrderRevision)
                throw new InvalidOperationException("Invoice issue result changed its source revision.");
            var response = new InvoiceIssueResponse(invoice.InvoiceId.ToString("D"));
            if (result.Status == InvoiceIssueStatus.Replayed)
            {
                http.Response.Headers.Append("Idempotency-Replayed", "true");
                return Results.Ok(response);
            }
            return Results.Created($"/api/v1/tenants/{tenantId:D}/invoices/{invoice.InvoiceId:D}", response);
        }).ConfigureAwait(false);

    private static async Task<IResult> ReadAsync(Guid tenantId, Guid id, bool byOrder, HttpContext http,
        IInvoiceIssuedFactsReader reader) => await GuardAsync(async () =>
    {
        if (id == Guid.Empty || http.Request.Query.Count != 0) return Invalid();
        var context = TenantRequestAccess.RequireResolvedContext(tenantId, http);
        var result = byOrder
            ? await reader.FindByOrderAsync(context, id, http.RequestAborted).ConfigureAwait(false)
            : await reader.FindByIdAsync(context, id, http.RequestAborted).ConfigureAwait(false);
        if (result.Status != InvoiceReadStatus.Found)
            return result.Status switch
            {
                InvoiceReadStatus.Missing => Problem(404, "invoice_not_found"),
                InvoiceReadStatus.NotAuthorized => Problem(403, "invoice_denied"),
                InvoiceReadStatus.Unavailable => Problem(503, "invoice_dependency_unavailable"),
                InvoiceReadStatus.HistoricalInvalid => Problem(500, "invoice_history_invalid"),
                _ => throw new InvalidOperationException("Invoice query returned an unsupported status."),
            };
        var invoice = result.Invoice ?? throw new InvalidOperationException("Invoice read result is missing.");
        RequireInvoice(invoice, tenantId, id, byOrder);
        return Results.Ok(new InvoiceResponse(invoice.InvoiceId.ToString("D"), invoice.SourceOrderId.ToString("D"),
            invoice.SourceOrderRevision, invoice.SourceCustomerContext!.OrganizationId.ToString("D"),
            invoice.SourceCustomerContext.ProgramId?.ToString("D"), invoice.OrganizationOrdinal, invoice.CurrencyCode,
            invoice.Lines.Select(line => new InvoiceLineResponse(line.Position, line.Description, line.Quantity,
                line.UnitCode, line.UnitPrice, line.LineTotal)).ToArray(), invoice.Total, Debtor(invoice.Debtor),
            invoice.IssuedByAccountId.ToString("D"), invoice.IssuedAt, invoice.BusinessDate));
    }).ConfigureAwait(false);

    private static InvoiceDebtorResponse Debtor(InvoiceDebtorFacts value) => value switch
    {
        InvoiceDebtorFacts.Organization organization => new("Organization", organization.OrganizationId.ToString("D"),
            organization.OrganizationDisplayName, null, null, null, null, null),
        InvoiceDebtorFacts.Program program => new("Program", program.OrganizationId.ToString("D"), program.OrganizationDisplayName,
            program.ProgramId.ToString("D"), program.ProgramDisplayName, null, null, null),
        InvoiceDebtorFacts.Individual individual => new("Individual", null, null, null, null, individual.IndividualId.ToString("D"),
            individual.ObservedRevision, individual.DisplayName),
        _ => throw new InvalidOperationException("Invoice debtor has an unsupported kind."),
    };

    private static void RequireInvoice(InvoiceIssuedFacts invoice, Guid tenantId, Guid id, bool byOrder)
    {
        if (invoice.TenantId != tenantId || invoice.InvoiceId == Guid.Empty || invoice.SourceCustomerContext is null ||
            (byOrder ? invoice.SourceOrderId != id : invoice.InvoiceId != id))
            throw new InvalidOperationException("Invoice returned another resource's retained facts.");
    }

    private static bool TryDebtor(DebtorPayload? payload, out InvoiceDebtorSelection? selection)
    {
        selection = null;
        if (payload is null) return true;
        switch (payload.Kind)
        {
            case "Organization" when payload.IndividualId is null:
                selection = new InvoiceDebtorSelection.DefaultOrganization(); return true;
            case "Program" when payload.IndividualId is null:
                selection = new InvoiceDebtorSelection.AttributedProgram(); return true;
            case "Individual" when payload.IndividualId is { } id && id != Guid.Empty:
                selection = new InvoiceDebtorSelection.Individual(id); return true;
            default: return false;
        }
    }

    private static bool TryKey(HttpContext http, out string key)
    {
        key = string.Empty;
        if (!http.Request.Headers.TryGetValue("Idempotency-Key", out var values) || values.Count != 1) return false;
        key = values[0] ?? string.Empty;
        return key.Length is >= 1 and <= 128 && key.All(c => c is >= '!' and <= '~');
    }

    private static async Task<IssuePayload> ReadPayloadAsync(HttpContext http)
    {
        if (!http.Request.HasJsonContentType()) throw new JsonException();
        if (http.Request.ContentLength > MaximumRequestBodyBytes) throw new InvoicePayloadTooLargeException();
        var buffer = new byte[MaximumRequestBodyBytes + 1];
        var count = await http.Request.Body.ReadAtLeastAsync(buffer, buffer.Length, false, http.RequestAborted).ConfigureAwait(false);
        if (count > MaximumRequestBodyBytes) throw new InvoicePayloadTooLargeException();
        using var document = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 6 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        RequireUniqueNames(document.RootElement);
        return JsonSerializer.Deserialize<IssuePayload>(buffer.AsSpan(0, count), JsonOptions) ?? throw new JsonException();
    }

    private static void RequireUniqueNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                RequireUniqueNames(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RequireUniqueNames(item);
    }

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> operation)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (InvoicePayloadTooLargeException) { return Problem(413, "request_too_large"); }
        catch (BadHttpRequestException error) when (error.StatusCode == 413) { return Problem(413, "request_too_large"); }
        catch (InvoiceIssueValidationException) { return Invalid(); }
        catch (JsonException) { return Invalid(); }
        catch (ArgumentException) { return Invalid(); }
    }

    private static IResult IssueFailure(InvoiceIssueStatus status) => status switch
    {
        InvoiceIssueStatus.NotAuthorized => Problem(403, "invoice_denied"),
        InvoiceIssueStatus.SourceNotFound => Problem(404, "invoice_source_not_found"),
        InvoiceIssueStatus.OrderNotCommitted => Problem(409, "invoice_order_not_committed"),
        InvoiceIssueStatus.OrderRevisionConflict => Problem(409, "invoice_order_revision_conflict"),
        InvoiceIssueStatus.UnsupportedCurrency => Problem(409, "invoice_currency_unsupported"),
        InvoiceIssueStatus.InvalidDebtor => Problem(409, "invoice_debtor_ineligible"),
        InvoiceIssueStatus.InvalidCommittedOrderFacts => Problem(409, "invoice_source_invalid"),
        InvoiceIssueStatus.IdempotencyKeyConflict => Problem(409, "idempotency_key_conflict"),
        InvoiceIssueStatus.OrderAlreadyInvoiced => Problem(409, "invoice_order_already_invoiced"),
        InvoiceIssueStatus.ResourceLimit => Problem(409, "invoice_resource_limit"),
        InvoiceIssueStatus.DependencyUnavailable => Problem(503, "invoice_dependency_unavailable"),
        InvoiceIssueStatus.OutcomeUnknown => Problem(503, "invoice_outcome_unknown"),
        InvoiceIssueStatus.HistoricalReceiptInvalid => Problem(500, "invoice_history_invalid"),
        _ => throw new InvalidOperationException("Invoice issue returned an unsupported status."),
    };

    private static IResult Invalid() => Problem(400, "invoice_request_invalid");
    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "The invoice operation cannot continue.", extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed class InvoicePayloadTooLargeException : Exception;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record IssuePayload([property: JsonRequired] long ExpectedOrderRevision, DebtorPayload? Debtor);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record DebtorPayload([property: JsonRequired] string Kind, Guid? IndividualId);
    private sealed record InvoiceIssueResponse(string InvoiceId);
    private sealed record InvoiceLineResponse(int Position, string Description, decimal Quantity, string UnitCode, decimal UnitPrice, decimal LineTotal);
    private sealed record InvoiceDebtorResponse(string Kind, string? OrganizationId, string? OrganizationDisplayName,
        string? ProgramId, string? ProgramDisplayName, string? IndividualId, long? ObservedRevision, string? IndividualDisplayName);
    private sealed record InvoiceResponse(string InvoiceId, string SourceOrderId, long SourceOrderRevision,
        string OrganizationId, string? ProgramId, long OrganizationOrdinal, string CurrencyCode,
        IReadOnlyList<InvoiceLineResponse> Lines, decimal Total, InvoiceDebtorResponse Debtor,
        string IssuedByAccountId, DateTimeOffset IssuedAt, DateOnly BusinessDate);
}

using System.Text.Json;
using Application.AdminApi.Authentication;
using Application.AdminApi.IdentityProvisioning;
using Application.IdentityAccess;

namespace Application.AdminApi;

internal static class AccountOnboardingEndpoint
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const int MaximumRequestBodyBytes = 4 * 1024;

    internal static Task<IResult> PostAsync(
        HttpContext context,
        IExternalIdentityVerifier verifier,
        AdminOidcAuthenticationConfiguration authentication,
        AccountOnboarding onboarding) =>
        ExecuteAsync(context, verifier, authentication, onboarding, accountId: null);

    internal static Task<IResult> LinkAsync(
        Guid accountId,
        HttpContext context,
        IExternalIdentityVerifier verifier,
        AdminOidcAuthenticationConfiguration authentication,
        AccountOnboarding onboarding) =>
        ExecuteAsync(context, verifier, authentication, onboarding, accountId);

    private static async Task<IResult> ExecuteAsync(
        HttpContext context,
        IExternalIdentityVerifier verifier,
        AdminOidcAuthenticationConfiguration authentication,
        AccountOnboarding onboarding,
        Guid? accountId)
    {
        var isLink = accountId.HasValue;
        var access = AdminApiPlatformAuthorization.GetRequiredAccess(context);

        if (isLink && accountId == Guid.Empty)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid_account_id",
                "The account identity is invalid.");
        }

        if (!TryGetIdempotencyKey(context.Request, out var key, out var failure))
        {
            return failure!;
        }

        var payload = await ReadSubjectAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        if (payload.Failure is not null)
        {
            return payload.Failure;
        }

        ExternalIdentity identity;
        try
        {
            identity = ExternalIdentity.Create(authentication.Authority, payload.Subject!);
        }
        catch (ArgumentException)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid_external_identity",
                "The external identity is invalid.");
        }

        var actor = IdentityAdministrationActor.Create(
            access.PrincipalId,
            access.DeviceId);
        if (!isLink)
        {
            AccountOnboardingIntent intent;
            try
            {
                intent = AccountOnboardingIntent.Create(identity, key!);
            }
            catch (ArgumentException)
            {
                return Problem(
                    StatusCodes.Status400BadRequest,
                    "invalid_account_onboarding_request",
                    "The account onboarding request is invalid.");
            }

            var receipt = await onboarding
                .FindOnboardingReceiptAsync(actor, intent, context.RequestAborted)
                .ConfigureAwait(false);
            switch (receipt.Status)
            {
                case IdentityAdministrationReceiptStatus.Replayed when receipt.Account is not null:
                    return TypedResults.Ok(AccountResponse(receipt.Account));
                case IdentityAdministrationReceiptStatus.IdempotencyKeyConflict:
                    return Problem(
                        StatusCodes.Status409Conflict,
                        "idempotency_key_conflict",
                        "The Idempotency-Key was already used for a different account onboarding request.");
                case IdentityAdministrationReceiptStatus.Missing:
                    break;
                default:
                    throw new InvalidOperationException(
                        "Account onboarding receipt lookup returned an invalid result.");
            }

            var verificationFailure = await VerifyIdentityAsync(
                    verifier,
                    identity,
                    context.RequestAborted)
                .ConfigureAwait(false);
            if (verificationFailure is not null)
            {
                return verificationFailure;
            }

            var result = await onboarding
                .OnboardAsync(actor, intent, context.RequestAborted)
                .ConfigureAwait(false);
            return result.Status switch
            {
                OnboardAccountStatus.Created when result.Account is not null =>
                    Results.Json(AccountResponse(result.Account), statusCode: StatusCodes.Status201Created),
                OnboardAccountStatus.Replayed when result.Account is not null =>
                    TypedResults.Ok(AccountResponse(result.Account)),
                OnboardAccountStatus.IdempotencyKeyConflict =>
                    Problem(
                        StatusCodes.Status409Conflict,
                        "idempotency_key_conflict",
                        "The Idempotency-Key was already used for a different account onboarding request."),
                OnboardAccountStatus.IdentityAlreadyBound =>
                    Problem(
                        StatusCodes.Status409Conflict,
                        "external_identity_already_bound",
                        "The external identity is already bound to an account."),
                _ => throw new InvalidOperationException("Account onboarding returned an invalid result."),
            };
        }

        ExternalIdentityLinkIntent linkIntent;
        try
        {
            linkIntent = ExternalIdentityLinkIntent.Create(accountId!.Value, identity, key!);
        }
        catch (ArgumentException)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "invalid_identity_link_request",
                "The identity link request is invalid.");
        }

        var linkReceipt = await onboarding
            .FindLinkReceiptAsync(actor, linkIntent, context.RequestAborted)
            .ConfigureAwait(false);
        switch (linkReceipt.Status)
        {
            case IdentityAdministrationReceiptStatus.Replayed when linkReceipt.Link is not null:
                return TypedResults.Ok(LinkResponse(linkReceipt.Link));
            case IdentityAdministrationReceiptStatus.IdempotencyKeyConflict:
                return Problem(
                    StatusCodes.Status409Conflict,
                    "idempotency_key_conflict",
                    "The Idempotency-Key was already used for a different identity link request.");
            case IdentityAdministrationReceiptStatus.Missing:
                break;
            default:
                throw new InvalidOperationException(
                    "Identity link receipt lookup returned an invalid result.");
        }

        var linkVerificationFailure = await VerifyIdentityAsync(
                verifier,
                identity,
                context.RequestAborted)
            .ConfigureAwait(false);
        if (linkVerificationFailure is not null)
        {
            return linkVerificationFailure;
        }

        var link = await onboarding
            .LinkAsync(actor, linkIntent, context.RequestAborted)
            .ConfigureAwait(false);
        return link.Status switch
        {
            LinkExternalIdentityStatus.Linked when link.Link is not null =>
                Results.Json(LinkResponse(link.Link), statusCode: StatusCodes.Status201Created),
            LinkExternalIdentityStatus.Replayed when link.Link is not null =>
                TypedResults.Ok(LinkResponse(link.Link)),
            LinkExternalIdentityStatus.AccountNotFound =>
                Problem(StatusCodes.Status404NotFound, "account_not_found", "The account was not found."),
            LinkExternalIdentityStatus.AccountDisabled =>
                Problem(StatusCodes.Status409Conflict, "account_disabled", "The account is disabled."),
            LinkExternalIdentityStatus.IdentityAlreadyLinked =>
                Problem(
                    StatusCodes.Status409Conflict,
                    "external_identity_already_linked",
                    "The external identity is already linked to this account."),
            LinkExternalIdentityStatus.IdentityBoundElsewhere =>
                Problem(
                    StatusCodes.Status409Conflict,
                    "external_identity_already_bound",
                    "The external identity is already bound to another account."),
            LinkExternalIdentityStatus.IdempotencyKeyConflict =>
                Problem(
                    StatusCodes.Status409Conflict,
                    "idempotency_key_conflict",
                    "The Idempotency-Key was already used for a different identity link request."),
            _ => throw new InvalidOperationException("Identity linking returned an invalid result."),
        };
    }

    private static async Task<IResult?> VerifyIdentityAsync(
        IExternalIdentityVerifier verifier,
        ExternalIdentity identity,
        CancellationToken cancellationToken)
    {
        var verification = await verifier
            .VerifyAsync(identity, cancellationToken)
            .ConfigureAwait(false);
        return verification switch
        {
            ExternalIdentityVerification.Verified => null,
            ExternalIdentityVerification.NotFound => Problem(
                StatusCodes.Status404NotFound,
                "external_identity_not_found",
                "The external identity was not found."),
            ExternalIdentityVerification.NotInteractiveUser => Problem(
                StatusCodes.Status422UnprocessableEntity,
                "external_identity_not_interactive",
                "The external identity is not an interactive user."),
            ExternalIdentityVerification.IssuerMismatch => Problem(
                StatusCodes.Status400BadRequest,
                "external_identity_issuer_mismatch",
                "The external identity issuer is not accepted."),
            _ => throw new InvalidOperationException("Unknown external identity verification result."),
        };
    }

    private static AccountOnboardingResponse AccountResponse(AccountOnboardingSnapshot account) =>
        new(
            account.AccountId,
            account.Identity.Issuer,
            account.Identity.Subject,
            "active",
            account.CreatedAt);

    private static ExternalIdentityLinkResponse LinkResponse(ExternalIdentityLinkSnapshot link) =>
        new(link.AccountId, link.Identity.Issuer, link.Identity.Subject, link.LinkedAt);

    private static bool TryGetIdempotencyKey(
        HttpRequest request,
        out string? key,
        out IResult? failure)
    {
        var values = request.Headers[IdempotencyHeader];
        if (values.Count != 1)
        {
            key = null;
            failure = Problem(
                StatusCodes.Status400BadRequest,
                "idempotency_key_required",
                "Exactly one Idempotency-Key header is required.");
            return false;
        }

        key = values[0];
        failure = null;
        return true;
    }

    private static async Task<(string? Subject, IResult? Failure)> ReadSubjectAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is long length && length > MaximumRequestBodyBytes)
        {
            return (null, Problem(
                StatusCodes.Status413PayloadTooLarge,
                "request_too_large",
                "The request body is too large."));
        }

        if (!request.HasJsonContentType())
        {
            return (null, Problem(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported_media_type",
                "The request body must use application/json."));
        }

        try
        {
            var body = new byte[MaximumRequestBodyBytes + 1];
            var received = 0;
            while (received < body.Length)
            {
                var count = await request.Body
                    .ReadAsync(body.AsMemory(received), cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                received += count;
            }

            if (received > MaximumRequestBodyBytes)
            {
                return (null, Problem(
                    StatusCodes.Status413PayloadTooLarge,
                    "request_too_large",
                    "The request body is too large."));
            }

            using var document = JsonDocument.Parse(
                body.AsMemory(0, received),
                new JsonDocumentOptions { MaxDepth = 2 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidPayload();
            }

            using var properties = document.RootElement.EnumerateObject();
            if (!properties.MoveNext() ||
                !properties.Current.NameEquals("subject") ||
                properties.Current.Value.ValueKind != JsonValueKind.String ||
                properties.Current.Value.GetString() is not { } subject ||
                properties.MoveNext())
            {
                return InvalidPayload();
            }

            return (subject, null);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, Problem(
                StatusCodes.Status413PayloadTooLarge,
                "request_too_large",
                "The request body is too large."));
        }
        catch (Exception exception) when (exception is BadHttpRequestException or JsonException)
        {
            return InvalidPayload();
        }
    }

    private static (string? Subject, IResult? Failure) InvalidPayload() =>
        (null, Problem(
            StatusCodes.Status400BadRequest,
            "invalid_external_identity_request",
            "The external identity request is invalid."));

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

internal sealed record AccountOnboardingResponse(
    Guid AccountId,
    string Issuer,
    string Subject,
    string Availability,
    DateTimeOffset CreatedAt);

internal sealed record ExternalIdentityLinkResponse(
    Guid AccountId,
    string Issuer,
    string Subject,
    DateTimeOffset LinkedAt);

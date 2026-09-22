using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace Application.CoreApi.Authorization;

internal sealed class OpenFgaAuthorizationConfiguration
{
    internal const string SectionName = "Authorization:OpenFga";

    private OpenFgaAuthorizationConfiguration(
        Uri apiUri,
        string storeId,
        string authorizationModelId,
        TimeSpan requestTimeout,
        int maximumRetries,
        int minimumRetryDelayMilliseconds,
        Credentials credentials)
    {
        ApiUri = apiUri;
        StoreId = storeId;
        AuthorizationModelId = authorizationModelId;
        RequestTimeout = requestTimeout;
        MaximumRetries = maximumRetries;
        MinimumRetryDelayMilliseconds = minimumRetryDelayMilliseconds;
        Credentials = credentials;
    }

    internal Uri ApiUri { get; }

    internal string StoreId { get; }

    internal string AuthorizationModelId { get; }

    internal TimeSpan RequestTimeout { get; }

    internal int MaximumRetries { get; }

    internal int MinimumRetryDelayMilliseconds { get; }

    internal Credentials Credentials { get; }

    internal static OpenFgaAuthorizationConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetRequiredSection(SectionName);
        var apiUrl = Required(section, "ApiUrl");
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri) ||
            (apiUri.Scheme != Uri.UriSchemeHttps && apiUri.Scheme != Uri.UriSchemeHttp) ||
            (!apiUri.IsLoopback && apiUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(apiUri.UserInfo) ||
            !string.IsNullOrEmpty(apiUri.Query) ||
            !string.IsNullOrEmpty(apiUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{SectionName}:ApiUrl must be an absolute HTTPS URL without user information, a query, or a fragment; HTTP is allowed only for a loopback OpenFGA endpoint.");
        }

        var storeId = Required(section, "StoreId");
        var authorizationModelId = Required(section, "AuthorizationModelId");
        if (!ClientConfiguration.IsWellFormedUlidString(storeId))
        {
            throw new InvalidOperationException($"{SectionName}:StoreId must be a valid OpenFGA ULID.");
        }

        if (!ClientConfiguration.IsWellFormedUlidString(authorizationModelId))
        {
            throw new InvalidOperationException(
                $"{SectionName}:AuthorizationModelId must be a valid OpenFGA ULID and cannot target the implicit latest model.");
        }

        if (!int.TryParse(section["RequestTimeoutSeconds"], out var timeoutSeconds) ||
            timeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException(
                $"{SectionName}:RequestTimeoutSeconds must be an integer from 1 through 30.");
        }

        if (!int.TryParse(section["MaximumRetries"], out var maximumRetries) ||
            maximumRetries is < 0 or > 3)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MaximumRetries must be an integer from 0 through 3.");
        }

        if (!int.TryParse(section["MinimumRetryDelayMilliseconds"], out var minimumRetryDelayMilliseconds) ||
            minimumRetryDelayMilliseconds is < 1 or > 1000)
        {
            throw new InvalidOperationException(
                $"{SectionName}:MinimumRetryDelayMilliseconds must be an integer from 1 through 1000.");
        }

        var credentials = CreateCredentials(section);
        credentials.EnsureValid();

        return new OpenFgaAuthorizationConfiguration(
            apiUri,
            storeId,
            authorizationModelId,
            TimeSpan.FromSeconds(timeoutSeconds),
            maximumRetries,
            minimumRetryDelayMilliseconds,
            credentials);
    }

    internal ClientConfiguration ToClientConfiguration() => new()
    {
        ApiUrl = ApiUri.AbsoluteUri.TrimEnd('/'),
        StoreId = StoreId,
        AuthorizationModelId = AuthorizationModelId,
        Credentials = Credentials,
        MaxRetry = MaximumRetries,
        MinWaitInMs = MinimumRetryDelayMilliseconds,
    };

    private static Credentials CreateCredentials(IConfigurationSection section)
    {
        var rawMethod = Required(section, "CredentialMethod");
        if (!Enum.TryParse<CredentialsMethod>(rawMethod, ignoreCase: false, out var method) ||
            !Enum.IsDefined(method))
        {
            throw new InvalidOperationException(
                $"{SectionName}:CredentialMethod must be None, ApiToken, or ClientCredentials.");
        }

        var values = new CredentialsConfig
        {
            ApiToken = EmptyToNull(section["ApiToken"]),
            ClientId = EmptyToNull(section["ClientId"]),
            ClientSecret = EmptyToNull(section["ClientSecret"]),
            ApiTokenIssuer = EmptyToNull(section["ApiTokenIssuer"]),
            ApiAudience = EmptyToNull(section["ApiAudience"]),
            Scopes = EmptyToNull(section["Scopes"]),
        };

        if (method == CredentialsMethod.None && HasAnyCredentialValue(values))
        {
            throw new InvalidOperationException(
                $"{SectionName} contains credential material while CredentialMethod is None.");
        }

        if (method == CredentialsMethod.ApiToken &&
            values is { ClientId: not null } or
            { ClientSecret: not null } or
            { ApiTokenIssuer: not null } or
            { ApiAudience: not null } or
            { Scopes: not null })
        {
            throw new InvalidOperationException(
                $"{SectionName} contains client-credential material while CredentialMethod is ApiToken.");
        }

        if (method == CredentialsMethod.ClientCredentials && values.ApiToken is not null)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ApiToken cannot be set while CredentialMethod is ClientCredentials.");
        }

        if (method == CredentialsMethod.ClientCredentials && values.ApiTokenIssuer is not null)
        {
            var issuerText = values.ApiTokenIssuer.Contains("://", StringComparison.Ordinal)
                ? values.ApiTokenIssuer
                : $"https://{values.ApiTokenIssuer}";
            if (!Uri.TryCreate(issuerText, UriKind.Absolute, out var issuerUri) ||
                (issuerUri.Scheme != Uri.UriSchemeHttps && issuerUri.Scheme != Uri.UriSchemeHttp) ||
                (!issuerUri.IsLoopback && issuerUri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(issuerUri.UserInfo) ||
                !string.IsNullOrEmpty(issuerUri.Fragment))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:ApiTokenIssuer must use HTTPS; HTTP is allowed only for loopback.");
            }
        }

        return new Credentials
        {
            Method = method,
            Config = method == CredentialsMethod.None ? null : values,
        };
    }

    private static string Required(IConfigurationSection section, string name)
    {
        var value = section[name];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{SectionName}:{name} is required.");
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasAnyCredentialValue(CredentialsConfig values) =>
        values is { ApiToken: not null } or
        { ClientId: not null } or
        { ClientSecret: not null } or
        { ApiTokenIssuer: not null } or
        { ApiAudience: not null } or
        { Scopes: not null };
}

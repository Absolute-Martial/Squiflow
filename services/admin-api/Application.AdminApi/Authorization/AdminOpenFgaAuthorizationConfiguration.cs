using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace Application.AdminApi.Authorization;

internal sealed class AdminOpenFgaAuthorizationConfiguration
{
    internal const string SectionName = "Authorization:PlatformOpenFga";

    private AdminOpenFgaAuthorizationConfiguration(
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

    internal static AdminOpenFgaAuthorizationConfiguration From(IConfiguration configuration)
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
                $"{SectionName}:ApiUrl must be HTTPS without user information, query or fragment; HTTP is loopback-only.");
        }

        var storeId = Required(section, "StoreId");
        var modelId = Required(section, "AuthorizationModelId");
        if (!ClientConfiguration.IsWellFormedUlidString(storeId))
        {
            throw new InvalidOperationException($"{SectionName}:StoreId must be a valid OpenFGA ULID.");
        }
        if (!ClientConfiguration.IsWellFormedUlidString(modelId))
        {
            throw new InvalidOperationException(
                $"{SectionName}:AuthorizationModelId must be a valid pinned OpenFGA ULID.");
        }

        var timeout = BoundedInt(section, "RequestTimeoutSeconds", 1, 30);
        var retries = BoundedInt(section, "MaximumRetries", 0, 3);
        var retryDelay = BoundedInt(section, "MinimumRetryDelayMilliseconds", 1, 1000);
        var credentials = CreateCredentials(section);
        credentials.EnsureValid();
        return new AdminOpenFgaAuthorizationConfiguration(
            apiUri,
            storeId,
            modelId,
            TimeSpan.FromSeconds(timeout),
            retries,
            retryDelay,
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

        var config = new CredentialsConfig
        {
            ApiToken = EmptyToNull(section["ApiToken"]),
            ClientId = EmptyToNull(section["ClientId"]),
            ClientSecret = EmptyToNull(section["ClientSecret"]),
            ApiTokenIssuer = EmptyToNull(section["ApiTokenIssuer"]),
            ApiAudience = EmptyToNull(section["ApiAudience"]),
            Scopes = EmptyToNull(section["Scopes"]),
        };
        if (method == CredentialsMethod.None && HasAnyCredentialValue(config))
        {
            throw new InvalidOperationException(
                $"{SectionName} contains credential material while CredentialMethod is None.");
        }
        if (method == CredentialsMethod.ApiToken &&
            config is { ClientId: not null } or { ClientSecret: not null } or
            { ApiTokenIssuer: not null } or { ApiAudience: not null } or { Scopes: not null })
        {
            throw new InvalidOperationException(
                $"{SectionName} contains client-credential material while CredentialMethod is ApiToken.");
        }
        if (method == CredentialsMethod.ClientCredentials && config.ApiToken is not null)
        {
            throw new InvalidOperationException(
                $"{SectionName}:ApiToken cannot be set while CredentialMethod is ClientCredentials.");
        }

        return new Credentials
        {
            Method = method,
            Config = method == CredentialsMethod.None ? null : config,
        };
    }

    private static string Required(IConfigurationSection section, string name) =>
        section[name] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{SectionName}:{name} is required.");

    private static int BoundedInt(
        IConfigurationSection section,
        string name,
        int minimum,
        int maximum) =>
        int.TryParse(section[name], out var value) && value >= minimum && value <= maximum
            ? value
            : throw new InvalidOperationException(
                $"{SectionName}:{name} must be an integer from {minimum} through {maximum}.");

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasAnyCredentialValue(CredentialsConfig config) =>
        config is { ApiToken: not null } or { ClientId: not null } or
        { ClientSecret: not null } or { ApiTokenIssuer: not null } or
        { ApiAudience: not null } or { Scopes: not null };
}

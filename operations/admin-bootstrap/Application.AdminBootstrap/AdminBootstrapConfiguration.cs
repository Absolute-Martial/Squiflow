using OpenFga.Sdk.Client;
using OpenFga.Sdk.Configuration;

namespace Application.AdminBootstrap;

internal sealed record AdminBootstrapConfiguration(
    string DatabaseConnectionString,
    PlatformOpenFgaConfiguration OpenFga)
{
    internal static AdminBootstrapConfiguration FromEnvironment()
    {
        var database = Required("ConnectionStrings__PlatformAdministrationBootstrap");
        return new AdminBootstrapConfiguration(database, PlatformOpenFgaConfiguration.FromEnvironment());
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{name} is required.");
}

internal sealed record PlatformOpenFgaConfiguration(
    Uri ApiUri,
    string StoreId,
    string AuthorizationModelId,
    TimeSpan RequestTimeout,
    int MaximumRetries,
    int MinimumRetryDelayMilliseconds,
    Credentials Credentials)
{
    private const string Prefix = "Authorization__PlatformOpenFga__";

    internal static PlatformOpenFgaConfiguration FromEnvironment()
    {
        var apiUrl = Required("ApiUrl");
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri) ||
            (apiUri.Scheme != Uri.UriSchemeHttps && apiUri.Scheme != Uri.UriSchemeHttp) ||
            (!apiUri.IsLoopback && apiUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(apiUri.UserInfo) ||
            !string.IsNullOrEmpty(apiUri.Query) ||
            !string.IsNullOrEmpty(apiUri.Fragment))
        {
            throw new InvalidOperationException(
                $"{Prefix}ApiUrl must be HTTPS without user information, query or fragment; HTTP is loopback-only.");
        }

        var storeId = Required("StoreId");
        var modelId = Required("AuthorizationModelId");
        if (!ClientConfiguration.IsWellFormedUlidString(storeId))
        {
            throw new InvalidOperationException($"{Prefix}StoreId must be a valid OpenFGA ULID.");
        }
        if (!ClientConfiguration.IsWellFormedUlidString(modelId))
        {
            throw new InvalidOperationException(
                $"{Prefix}AuthorizationModelId must be a valid pinned OpenFGA ULID.");
        }

        var timeout = ParseBoundedInt("RequestTimeoutSeconds", defaultValue: 5, minimum: 1, maximum: 30);
        var maximumRetries = ParseBoundedInt("MaximumRetries", defaultValue: 1, minimum: 0, maximum: 3);
        var minimumRetryDelay = ParseBoundedInt(
            "MinimumRetryDelayMilliseconds", defaultValue: 100, minimum: 1, maximum: 1000);
        var credentials = CreateCredentials();
        credentials.EnsureValid();

        return new PlatformOpenFgaConfiguration(
            apiUri,
            storeId,
            modelId,
            TimeSpan.FromSeconds(timeout),
            maximumRetries,
            minimumRetryDelay,
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

    private static Credentials CreateCredentials()
    {
        var rawMethod = Value("CredentialMethod") ?? "None";
        if (!Enum.TryParse<CredentialsMethod>(rawMethod, ignoreCase: false, out var method) ||
            !Enum.IsDefined(method))
        {
            throw new InvalidOperationException(
                $"{Prefix}CredentialMethod must be None, ApiToken, or ClientCredentials.");
        }

        var config = new CredentialsConfig
        {
            ApiToken = EmptyToNull(Value("ApiToken")),
            ClientId = EmptyToNull(Value("ClientId")),
            ClientSecret = EmptyToNull(Value("ClientSecret")),
            ApiTokenIssuer = EmptyToNull(Value("ApiTokenIssuer")),
            ApiAudience = EmptyToNull(Value("ApiAudience")),
            Scopes = EmptyToNull(Value("Scopes")),
        };
        if (method == CredentialsMethod.None && HasAnyCredentialValue(config))
        {
            throw new InvalidOperationException(
                $"{Prefix} contains credential material while CredentialMethod is None.");
        }
        if (method == CredentialsMethod.ApiToken &&
            config is { ClientId: not null } or { ClientSecret: not null } or
            { ApiTokenIssuer: not null } or { ApiAudience: not null } or { Scopes: not null })
        {
            throw new InvalidOperationException(
                $"{Prefix} contains client-credential material while CredentialMethod is ApiToken.");
        }
        if (method == CredentialsMethod.ClientCredentials && config.ApiToken is not null)
        {
            throw new InvalidOperationException(
                $"{Prefix}ApiToken cannot be set while CredentialMethod is ClientCredentials.");
        }

        return new Credentials
        {
            Method = method,
            Config = method == CredentialsMethod.None ? null : config,
        };
    }

    private static int ParseBoundedInt(string name, int defaultValue, int minimum, int maximum)
    {
        var raw = Value(name);
        if (raw is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, out var value) || value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{Prefix}{name} must be an integer from {minimum} through {maximum}.");
        }

        return value;
    }

    private static string Required(string name) =>
        Value(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{Prefix}{name} is required.");

    private static string? Value(string name) => Environment.GetEnvironmentVariable(Prefix + name);

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasAnyCredentialValue(CredentialsConfig config) =>
        config is { ApiToken: not null } or { ClientId: not null } or
        { ClientSecret: not null } or { ApiTokenIssuer: not null } or
        { ApiAudience: not null } or { Scopes: not null };
}

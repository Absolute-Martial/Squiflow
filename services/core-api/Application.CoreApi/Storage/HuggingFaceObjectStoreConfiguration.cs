namespace Application.CoreApi.Storage;

internal sealed class HuggingFaceObjectStoreConfiguration
{
    internal const string SectionName = "ObjectStorage";

    private HuggingFaceObjectStoreConfiguration(
        bool enabled,
        Uri endpoint,
        string @namespace,
        string bucket,
        string accessKeyId,
        string secretAccessKey,
        string providerScope,
        long maximumRetainedBytes,
        int requestTimeoutSeconds)
    {
        Enabled = enabled;
        Endpoint = endpoint;
        Namespace = @namespace;
        Bucket = bucket;
        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        ProviderScope = providerScope;
        MaximumRetainedBytes = maximumRetainedBytes;
        RequestTimeoutSeconds = requestTimeoutSeconds;
    }

    internal bool Enabled { get; }
    internal Uri Endpoint { get; }
    internal string Namespace { get; }
    internal string Bucket { get; }
    internal string AccessKeyId { get; }
    internal string SecretAccessKey { get; }
    internal string ProviderScope { get; }
    internal long MaximumRetainedBytes { get; }
    internal int RequestTimeoutSeconds { get; }

    internal static HuggingFaceObjectStoreConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        var enabled = bool.TryParse(section["Enabled"], out var parsed) && parsed;
        if (!enabled)
        {
            return new(false, new Uri("https://s3.hf.co", UriKind.Absolute), "", "", "", "", "", 0, 30);
        }

        var endpoint = RequiredUri(section, "Endpoint");
        var @namespace = Required(section, "Namespace", 96);
        var bucket = Required(section, "Bucket", 96);
        var accessKeyId = Required(section, "AccessKeyId", 256);
        var secretAccessKey = Required(section, "SecretAccessKey", 512);
        var providerScope = Required(section, "ProviderScope", 256);
        var maximumRetainedBytes = ReadPositiveLong(section, "MaximumRetainedBytes");
        var timeout = ReadInt(section, "RequestTimeoutSeconds", 1, 120);
        if (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(endpoint.Host, "s3.hf.co", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{SectionName}:Endpoint must be the HTTPS Hugging Face Storage Bucket gateway.");
        }

        return new(true, endpoint, @namespace, bucket, accessKeyId, secretAccessKey,
            providerScope, maximumRetainedBytes, timeout);
    }

    private static Uri RequiredUri(IConfigurationSection section, string key)
    {
        var raw = Required(section, key, 2048);
        return Uri.TryCreate(raw, UriKind.Absolute, out var value) && value is not null
            ? value
            : throw new InvalidOperationException($"{SectionName}:{key} must be an absolute URI.");
    }

    private static string Required(IConfigurationSection section, string key, int maximumLength)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidOperationException($"{SectionName}:{key} is required and bounded.");
        }

        return value.Trim();
    }

    private static long ReadPositiveLong(IConfigurationSection section, string key)
    {
        if (!long.TryParse(section[key], out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:{key} must be a positive integer.");
        }

        return value;
    }

    private static int ReadInt(IConfigurationSection section, string key, int minimum, int maximum)
    {
        if (!int.TryParse(section[key], out var value) || value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{SectionName}:{key} must be between {minimum} and {maximum}.");
        }

        return value;
    }
}

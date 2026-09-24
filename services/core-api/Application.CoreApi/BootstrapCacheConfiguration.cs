namespace Application.CoreApi;

internal sealed class BootstrapCacheConfiguration
{
    public int? CacheMaxAgeSeconds { get; init; }

    public int GetCacheMaxAgeSeconds()
    {
        if (CacheMaxAgeSeconds is < 0 or > 86_400 || CacheMaxAgeSeconds is null)
        {
            throw new InvalidOperationException(
                $"Branding:CacheMaxAgeSeconds must be an integer between 0 and 86400 seconds.");
        }

        return CacheMaxAgeSeconds.Value;
    }
}

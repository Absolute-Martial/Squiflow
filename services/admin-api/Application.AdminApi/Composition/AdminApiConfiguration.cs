namespace Application.AdminApi.Composition;

internal sealed record AdminApiConfiguration(
    string DatabaseConnectionString,
    int MaximumConcurrentRequests,
    int ProtectedRequestTimeoutSeconds)
{
    internal static AdminApiConfiguration From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString("PlatformAdministration");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:PlatformAdministration is required.");
        }

        if (!int.TryParse(
                configuration["AdminApi:MaximumConcurrentRequests"],
                out var maximumConcurrentRequests) ||
            maximumConcurrentRequests is < 1 or > 64)
        {
            throw new InvalidOperationException(
                "AdminApi:MaximumConcurrentRequests must be an integer from 1 through 64.");
        }

        if (!int.TryParse(
                configuration["AdminApi:ProtectedRequestTimeoutSeconds"],
                out var protectedRequestTimeoutSeconds) ||
            protectedRequestTimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "AdminApi:ProtectedRequestTimeoutSeconds must be an integer from 1 through 120.");
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) ||
            allowedHosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(host => host is "*" or "+"))
        {
            throw new InvalidOperationException(
                "AllowedHosts must explicitly name the private Admin API host(s); wildcards are not allowed.");
        }

        return new AdminApiConfiguration(
            connectionString,
            maximumConcurrentRequests,
            protectedRequestTimeoutSeconds);
    }
}

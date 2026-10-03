using System.Security.Cryptography.X509Certificates;

namespace Application.AdminApi;

internal interface IAdminClientCertificateProvider
{
    Task<X509Certificate2?> GetAsync(HttpContext context, CancellationToken cancellationToken);
}

internal sealed class ConnectionAdminClientCertificateProvider : IAdminClientCertificateProvider
{
    public Task<X509Certificate2?> GetAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Connection.GetClientCertificateAsync(cancellationToken);
    }
}

using OpenFga.Sdk.Client;

namespace Application.CoreApi.Authorization;

internal sealed class OpenFgaTenantAuthorizationAdministrationClient : OpenFgaClient, IDisposable
{
    private readonly HttpClient transport;

    internal OpenFgaTenantAuthorizationAdministrationClient(OpenFgaAuthorizationConfiguration configuration)
        : this(configuration, new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        { Timeout = configuration.RequestTimeout })
    { }

    // The reconciliation client owns its transport as well as the SDK client lifetime.
    internal OpenFgaTenantAuthorizationAdministrationClient(OpenFgaAuthorizationConfiguration configuration, HttpClient transport)
        : base(WithoutRetries(configuration), transport) => this.transport = transport;

    private static ClientConfiguration WithoutRetries(OpenFgaAuthorizationConfiguration configuration)
    {
        var client = configuration.ToClientConfiguration();
        // A retried write cannot prove that an earlier timed-out HTTP mutation has drained.
        client.MaxRetry = 0;
        return client;
    }

    public new void Dispose()
    {
        base.Dispose();
        transport.Dispose();
    }
}

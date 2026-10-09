using Application.IdentityAccess;
using Application.PlatformAdministration;
using Application.PlatformAdministration.Postgres;
using OpenFga.Sdk.Client;

namespace Application.AdminBootstrap;

internal static class AdminBootstrapProgram
{
    internal static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = BootstrapArguments.Parse(args);
            var configuration = AdminBootstrapConfiguration.FromEnvironment();
            var intent = PlatformAdminBootstrapIntent.Create(
                ExternalIdentity.Create(arguments.Issuer, arguments.Subject),
                AdminDeviceCertificateFingerprint.Create(arguments.DeviceCertificateSha256),
                arguments.DeviceName,
                arguments.IdempotencyKey);

            await using var database = PlatformAdministrationPostgresMigrations.CreateContext(
                configuration.DatabaseConnectionString);
            var store = new PostgresPlatformAdminBootstrapStore(database);
            using var client = new OpenFgaClient(configuration.OpenFga.ToClientConfiguration());
            var provisioner = new OpenFgaInitialPlatformAdministratorProvisioner(
                client,
                configuration.OpenFga);
            var coordinator = new PlatformAdminBootstrapCoordinator(
                store,
                provisioner,
                TimeProvider.System);

            var outcome = await coordinator.ExecuteAsync(intent, cancellationToken).ConfigureAwait(false);
            Console.WriteLine(
                $"[admin-bootstrap] {outcome.ExecutionKind}: bootstrap={outcome.Snapshot.BootstrapId:D} " +
                $"principal={outcome.Snapshot.PrincipalId:D} device={outcome.Snapshot.DeviceId:D} " +
                $"status={outcome.Snapshot.Status}");
            return 0;
        }
        catch (BootstrapUsageException exception)
        {
            Console.Error.WriteLine($"[admin-bootstrap] Invalid input: {exception.Message}");
            return 2;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"[admin-bootstrap] Invalid input: {exception.Message}");
            return 2;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"[admin-bootstrap] Invalid configuration/state: {exception.Message}");
            return 2;
        }
        catch (PlatformAdminBootstrapConflictException exception)
        {
            Console.Error.WriteLine($"[admin-bootstrap] Bootstrap conflict: {exception.Message}");
            return 3;
        }
        catch (PlatformAuthorizationProviderUnavailableException exception)
        {
            Console.Error.WriteLine($"[admin-bootstrap] Authorization unavailable: {exception.Message}");
            return 4;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("[admin-bootstrap] Cancelled.");
            return 5;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("[admin-bootstrap] Bootstrap failed.");
            return 6;
        }
    }
}

internal sealed record BootstrapArguments(
    string Issuer,
    string Subject,
    string DeviceName,
    string DeviceCertificateSha256,
    string IdempotencyKey)
{
    internal static BootstrapArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            throw new BootstrapUsageException(Usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new BootstrapUsageException(Usage);
            }

            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new BootstrapUsageException($"Duplicate option {args[index]}. {Usage}");
            }
        }

        var allowed = new HashSet<string>(
            ["--issuer", "--subject", "--device-name", "--device-certificate-sha256", "--idempotency-key"],
            StringComparer.Ordinal);
        var unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));
        if (unknown is not null)
        {
            throw new BootstrapUsageException($"Unknown option {unknown}. {Usage}");
        }

        return new BootstrapArguments(
            Required(values, "--issuer"),
            Required(values, "--subject"),
            Required(values, "--device-name"),
            Required(values, "--device-certificate-sha256"),
            Required(values, "--idempotency-key"));
    }

    private static string Required(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new BootstrapUsageException($"Missing {key}. {Usage}");

    private const string Usage =
        "Required options: --issuer <https-url> --subject <oidc-subject> --device-name <name> " +
        "--device-certificate-sha256 <64-hex> --idempotency-key <key>.";
}

internal sealed class BootstrapUsageException(string message) : Exception(message);

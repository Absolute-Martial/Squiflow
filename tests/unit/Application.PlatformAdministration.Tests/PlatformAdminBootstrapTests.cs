using Application.IdentityAccess;
using Application.PlatformAdministration;
using Xunit;

namespace Application.PlatformAdministration.Tests;

public sealed class PlatformAdminBootstrapTests
{
    [Fact]
    public void CertificateFingerprintRequiresExactSha256HexAndNormalizesCase()
    {
        var fingerprint = AdminDeviceCertificateFingerprint.Create(new string('a', 64));

        Assert.Equal(new string('A', 64), fingerprint.Value);
        Assert.Throws<ArgumentException>(() => AdminDeviceCertificateFingerprint.Create("AA:BB"));
        Assert.Throws<ArgumentException>(() => AdminDeviceCertificateFingerprint.Create(new string('G', 64)));
    }

    [Fact]
    public void IntentFingerprintIsDeterministicAndCoversMaterialBootstrapIdentity()
    {
        var first = Intent("device-a", "bootstrap-1", new string('A', 64));
        var same = Intent("device-a", "bootstrap-1", new string('A', 64));
        var otherDevice = Intent("device-b", "bootstrap-1", new string('A', 64));

        Assert.Equal(first.IntentFingerprint, same.IntentFingerprint);
        Assert.NotEqual(first.IntentFingerprint, otherDevice.IntentFingerprint);
        Assert.Equal("device-a", first.DeviceName);
    }

    [Fact]
    public async Task CompletedBootstrapReplaysWithoutCallingProviderOrCompletingAgain()
    {
        var snapshot = Snapshot(PlatformAdminBootstrapStatus.Completed);
        var store = new TestStore(new PlatformAdminBootstrapPreparation(snapshot, Existing: true));
        var provider = new TestProvisioner();
        var coordinator = new PlatformAdminBootstrapCoordinator(store, provider, TimeProvider.System);

        var outcome = await coordinator.ExecuteAsync(Intent(), CancellationToken.None);

        Assert.Equal(PlatformAdminBootstrapExecutionKind.Replayed, outcome.ExecutionKind);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(0, store.CompleteCalls);
    }

    [Fact]
    public async Task PendingExistingBootstrapReconcilesProviderAndCompletes()
    {
        var snapshot = Snapshot(PlatformAdminBootstrapStatus.PendingAuthorization);
        var store = new TestStore(new PlatformAdminBootstrapPreparation(snapshot, Existing: true));
        var provider = new TestProvisioner();
        var coordinator = new PlatformAdminBootstrapCoordinator(store, provider, TimeProvider.System);

        var outcome = await coordinator.ExecuteAsync(Intent(), CancellationToken.None);

        Assert.Equal(PlatformAdminBootstrapExecutionKind.Resumed, outcome.ExecutionKind);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(1, store.CompleteCalls);
        Assert.Equal(PlatformAdminBootstrapStatus.Completed, outcome.Snapshot.Status);
    }

    private static PlatformAdminBootstrapIntent Intent(
        string deviceName = "admin-laptop",
        string idempotencyKey = "bootstrap-1",
        string? fingerprint = null) =>
        PlatformAdminBootstrapIntent.Create(
            ExternalIdentity.Create("https://identity.example.test", "admin-subject"),
            AdminDeviceCertificateFingerprint.Create(fingerprint ?? new string('A', 64)),
            deviceName,
            idempotencyKey);

    private static PlatformAdminBootstrapSnapshot Snapshot(PlatformAdminBootstrapStatus status) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            status == PlatformAdminBootstrapStatus.Completed ? DateTimeOffset.UtcNow : null);

    private sealed class TestStore(PlatformAdminBootstrapPreparation preparation)
        : IPlatformAdminBootstrapStore
    {
        public int CompleteCalls { get; private set; }

        public Task<PlatformAdminBootstrapPreparation> PrepareAsync(
            PlatformAdminBootstrapIntent intent,
            DateTimeOffset now,
            CancellationToken cancellationToken) => Task.FromResult(preparation);

        public Task<PlatformAdminBootstrapSnapshot> CompleteAsync(
            Guid bootstrapId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            CompleteCalls++;
            return Task.FromResult(preparation.Snapshot with
            {
                Status = PlatformAdminBootstrapStatus.Completed,
                CompletedAt = now,
            });
        }
    }

    private sealed class TestProvisioner : IInitialPlatformAdministratorProvisioner
    {
        public int Calls { get; private set; }

        public Task EnsureAdministratorAsync(
            Guid platformPrincipalId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}

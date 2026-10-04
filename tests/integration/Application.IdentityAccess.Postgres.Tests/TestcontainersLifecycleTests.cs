using Docker.DotNet;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Testcontainers.PostgreSql;
using Xunit;

namespace Application.IdentityAccess.Postgres.Tests;

public sealed class TestcontainersLifecycleTests
{
    public TestcontainersLifecycleTests()
    {
        var evidence = Environment.GetEnvironmentVariable("APPLICATION_STARTUP_PROBE_EVIDENCE");
        if (evidence is not null)
        {
            // Persist the session before starting Docker resources so the external
            // guard can observe every owned Reaper even after an early host exit.
            File.WriteAllText(Path.Combine(evidence, $"{ResourceReaper.DefaultSessionId:D}.session"), string.Empty);
        }
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task DisposalDeletesContainerAfterCompletionFailureOrCancellation(string outcome)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var interruption = new InterruptedStartup(outcome, cancellation);
        var builder = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithLabel("application.startup-probe", Environment.GetEnvironmentVariable("APPLICATION_STARTUP_PROBE_RUN") ?? "repository-gate");
        if (outcome != "complete")
        {
            builder = builder.WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(interruption));
        }

        var container = builder.Build();
        string id;
        try
        {
            if (outcome == "complete")
            {
                await container.StartAsync(cancellation.Token);
                Assert.Equal(TestcontainersStates.Running, container.State);
            }
            else if (outcome == "failure")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => container.StartAsync(cancellation.Token));
            }
            else
            {
                var exception = await Record.ExceptionAsync(() => container.StartAsync(cancellation.Token));
                // WaitUntilAsync races its canceled delay against the canceled
                // readiness task and documents TimeoutException for that path.
                Assert.True(exception is OperationCanceledException or TimeoutException);
                Assert.True(interruption.Invoked);
                Assert.True(cancellation.IsCancellationRequested);
            }

            id = container.Id;
            Assert.False(ResourceReaper.IsUnavailable);
            using var docker = new DockerClientBuilder().WithEndpoint(TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint).Build();
            var inspection = await docker.Containers.InspectContainerAsync(id);
            Assert.NotNull(inspection.Config);
            Assert.Equal(ResourceReaper.DefaultSessionId.ToString("D"), inspection.Config.Labels[ResourceReaper.ResourceReaperSessionLabel]);
        }
        finally
        {
            await container.DisposeAsync();
        }

        using var verification = new DockerClientBuilder().WithEndpoint(TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint).Build();
        await Assert.ThrowsAsync<DockerContainerNotFoundException>(() => verification.Containers.InspectContainerAsync(id));
    }

    [Fact]
    public async Task ResourceReaperOwnsUndisposedContainerUntilProcessExit()
    {
        // The external fresh-process guard proves Ryuk deletes this resource after
        // the test host exits, independently of explicit fixture disposal.
        var container = new PostgreSqlBuilder(new DockerImage(repository: "postgres", tag: "17-alpine"))
            .WithLabel("application.startup-probe", Environment.GetEnvironmentVariable("APPLICATION_STARTUP_PROBE_RUN") ?? "repository-gate")
            .Build();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await container.StartAsync(deadline.Token);
        Assert.False(ResourceReaper.IsUnavailable);
        Assert.Equal(TestcontainersStates.Running, container.State);
        using var docker = new DockerClientBuilder().WithEndpoint(TestcontainersSettings.OS.DockerEndpointAuthConfig.Endpoint).Build();
        var inspection = await docker.Containers.InspectContainerAsync(container.Id);
        Assert.NotNull(inspection.Config);
        Assert.Equal(ResourceReaper.DefaultSessionId.ToString("D"), inspection.Config.Labels[ResourceReaper.ResourceReaperSessionLabel]);
    }

    private sealed class InterruptedStartup(string outcome, CancellationTokenSource cancellation) : IWaitUntil
    {
        public bool Invoked { get; private set; }

        public Task<bool> UntilAsync(IContainer container)
        {
            Assert.Equal(TestcontainersStates.Running, container.State);
            Invoked = true;
            if (outcome == "failure")
            {
                throw new InvalidOperationException("Synthetic startup failure after Docker created the container.");
            }

            cancellation.Cancel();
            return Task.FromCanceled<bool>(cancellation.Token);
        }
    }
}

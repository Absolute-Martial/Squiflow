using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Npgsql;

namespace Application.Invoices.Postgres.Tests;

// This test-only TCP boundary withholds PostgreSQL's final COMMIT ReadyForQuery.
// The server has committed, while Npgsql cannot confirm the transaction outcome.
internal sealed class CommitResponseLossProxy : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly TaskCompletionSource commitCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource dropResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task forwarding;
    public Task CommitCompleted => commitCompleted.Task;
    public string ConnectionString { get; }

    internal CommitResponseLossProxy(string connectionString)
    {
        var upstream = new NpgsqlConnectionStringBuilder(connectionString);
        listener.Start();
        ConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Host = IPAddress.Loopback.ToString(),
            Port = ((IPEndPoint)listener.LocalEndpoint).Port,
            SslMode = SslMode.Disable,
        }.ConnectionString;
        forwarding = ForwardAsync(upstream.Host!, upstream.Port, stop.Token);
    }

    public void DropResponse() => dropResponse.TrySetResult();

    private async Task ForwardAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var server = new TcpClient();
            await server.ConnectAsync(host, port, ct);
            await using var clientStream = client.GetStream();
            await using var serverStream = server.GetStream();
            var requests = clientStream.CopyToAsync(serverStream, ct);
            var header = new byte[5];
            var commitAcknowledged = false;
            while (true)
            {
                await serverStream.ReadExactlyAsync(header, ct);
                var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
                if (length is < 4 or > 1_048_576) throw new InvalidDataException("Synthetic PostgreSQL response frame exceeded its bound.");
                var body = new byte[length - 4];
                await serverStream.ReadExactlyAsync(body, ct);
                if (header[0] == (byte)'C' && Encoding.ASCII.GetString(body) == "COMMIT\0") commitAcknowledged = true;
                if (commitAcknowledged && header[0] == (byte)'Z')
                {
                    commitCompleted.TrySetResult();
                    await dropResponse.Task.WaitAsync(ct);
                    client.Close();
                    server.Close();
                    await ObserveShutdownAsync(requests);
                    return;
                }
                await clientStream.WriteAsync(header, ct);
                await clientStream.WriteAsync(body, ct);
                await clientStream.FlushAsync(ct);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
            if (!stop.IsCancellationRequested) commitCompleted.TrySetException(exception);
        }
    }

    private static async Task ObserveShutdownAsync(Task task)
    {
        try { await task; }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        await forwarding;
        stop.Dispose();
    }
}

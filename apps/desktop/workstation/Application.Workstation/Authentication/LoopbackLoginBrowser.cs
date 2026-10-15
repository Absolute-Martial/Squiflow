using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Duende.IdentityModel.OidcClient.Browser;

namespace Application.Workstation.Authentication;

public sealed class LoopbackLoginBrowser : IBrowser, IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly Func<Uri, Task> openBrowser;
    public Uri CallbackUri { get; }

    public LoopbackLoginBrowser(Func<Uri, Task>? openBrowser = null)
    {
        this.openBrowser = openBrowser ?? (uri =>
        {
            using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return Task.CompletedTask;
        });
        listener.Start(4);
        CallbackUri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/callback");
    }

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(options.StartUrl, UriKind.Absolute, out var authorization) || authorization.Scheme != "https" ||
            !string.Equals(options.EndUrl, CallbackUri.AbsoluteUri, StringComparison.Ordinal))
            return new() { ResultType = BrowserResultType.UnknownError, Error = "invalid_browser_transaction" };
        await openBrowser(authorization).ConfigureAwait(false);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            using var socket = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            using var requestBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestBudget.CancelAfter(TimeSpan.FromSeconds(5));
            await using var stream = socket.GetStream();
            var bytes = new byte[16 * 1024];
            var length = 0;
            string? request = null;
            try
            {
                while (length < bytes.Length)
                {
                    var count = await stream.ReadAsync(bytes.AsMemory(length), requestBudget.Token).ConfigureAwait(false);
                    if (count == 0) break;
                    length += count;
                    request = Encoding.ASCII.GetString(bytes, 0, length);
                    if (request.Contains("\r\n\r\n", StringComparison.Ordinal)) break;
                }
                var lines = request?.Split("\r\n") ?? [];
                var first = lines.FirstOrDefault()?.Split(' ') ?? [];
                var hosts = lines.Where(line => line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)).ToArray();
                var valid = first.Length == 3 && first[0] == "GET" && first[2] == "HTTP/1.1" &&
                    first[1].StartsWith("/callback?", StringComparison.Ordinal) && first[1].Length <= 8192 &&
                    !first[1].Any(char.IsControl) && request!.Contains("\r\n\r\n", StringComparison.Ordinal) &&
                    hosts.Length == 1 && string.Equals(hosts[0][5..].Trim(), CallbackUri.Authority, StringComparison.OrdinalIgnoreCase);
                var body = valid ? "Sign-in received. You can return to the application." : "This request cannot complete sign-in.";
                var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: default-src 'none'; frame-ancestors 'none'\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}");
                await stream.WriteAsync(response, requestBudget.Token).ConfigureAwait(false);
                if (valid) return new() { ResultType = BrowserResultType.Success, Response = new Uri(CallbackUri, first[1]).AbsoluteUri };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
        }
        return new() { ResultType = BrowserResultType.UnknownError, Error = "invalid_browser_transaction" };
    }

    public void Dispose() { listener.Stop(); GC.SuppressFinalize(this); }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Application.Web.Authentication;

public sealed class WebSessionTickets(TimeProvider time) : ITicketStore
{
    public const string SessionClaim = "application.session";
    public const int MaximumSessions = 128;
    private readonly Dictionary<string, byte[]> tickets = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = ticket.Principal.FindFirst(SessionClaim)?.Value;
        var bytes = TicketSerializer.Default.Serialize(ticket);
        if (key is null || key.Length != 43 || bytes.Length > 64 * 1024 ||
            ticket.Properties.ExpiresUtc is not { } expires || expires <= time.GetUtcNow())
            throw new InvalidOperationException("Invalid application session.");
        lock (gate)
        {
            foreach (var expired in tickets.Keys.Where(value => Read(value) is null).ToArray()) tickets.Remove(expired);
            if (tickets.Count >= MaximumSessions) throw new InvalidOperationException("Application session capacity reached.");
            tickets.Add(key, bytes);
        }
        return Task.FromResult(key);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        lock (gate) return Task.FromResult(Read(key));
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        lock (gate)
        {
            var previous = Read(key);
            if (previous is null) return Task.CompletedTask;
            var bytes = TicketSerializer.Default.Serialize(ticket);
            if (ticket.Principal.FindFirst(SessionClaim)?.Value != key || bytes.Length > 64 * 1024 ||
                ticket.Properties.ExpiresUtc is not { } expires || expires > previous.Properties.ExpiresUtc)
                throw new InvalidOperationException("Invalid application session renewal.");
            tickets[key] = bytes;
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        lock (gate) tickets.Remove(key);
        return Task.CompletedTask;
    }

    public string? AccessToken(string? key)
    {
        if (key is null) return null;
        lock (gate) return Read(key)?.Properties.GetTokenValue("access_token");
    }

    private AuthenticationTicket? Read(string key)
    {
        if (!tickets.TryGetValue(key, out var bytes)) return null;
        var ticket = TicketSerializer.Default.Deserialize(bytes);
        if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= time.GetUtcNow()) return null;
        return ticket;
    }
}

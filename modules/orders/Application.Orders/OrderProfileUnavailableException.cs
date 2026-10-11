namespace Application.Orders;

public sealed class OrderProfileUnavailableException() : InvalidOperationException("A compatible authoritative Order profile is unavailable.");

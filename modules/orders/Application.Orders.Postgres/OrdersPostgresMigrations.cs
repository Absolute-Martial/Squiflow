using Microsoft.EntityFrameworkCore;

namespace Application.Orders.Postgres;

public static class OrdersPostgresMigrations
{
    public static OrderDbContext CreateContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<OrderDbContext>();
        PostgresOrderOptions.Configure(builder, connectionString);
        return new OrderDbContext(builder.Options);
    }
}

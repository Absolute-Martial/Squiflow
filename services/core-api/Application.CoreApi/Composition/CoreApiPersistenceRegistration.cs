using Application.IdentityAccess.Postgres;
using Application.Catalog.Postgres;
using Application.Customers.Postgres;
using Application.Orders.Postgres;
using Application.Pricing;
using Application.Pricing.Postgres;
using Application.Tenancy.Postgres;
using Npgsql;

namespace Application.CoreApi.Composition;

internal static class CoreApiPersistenceRegistration
{
    internal static IServiceCollection AddCoreApiPersistence(
        this IServiceCollection services,
        RuntimeDatabaseConfiguration configuration)
    {
        services.AddSingleton(configuration);
        services.AddSingleton<NpgsqlDataSource>(_ => configuration.CreateDataSource());

        services.AddIdentityAccessPostgres();
        services.AddTenancyPostgres();
        services.AddCustomersPostgres();
        services.AddCatalogPostgres();
        services.AddPricingPostgres();
        services.AddScoped<IPricingReferenceReader, PricingReferenceReader>();
        services.AddSingleton(TimeProvider.System);
        Application.Profiles.Postgres.ProfilesPostgresRegistration.AddProfilesPostgres(services);
        services.AddScoped<Application.Orders.Postgres.IOrderProfilePolicySource, OrderProfilePolicySource>();
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Orders.IOrderApprovalAuthority, Application.CoreApi.Authorization.OrderApprovalAuthority>();
        services.AddOrdersPostgres();
        services.AddScoped<Application.CoreApi.Authorization.FulfillmentAuthority>();
        services.AddScoped<Application.Fulfillment.IFulfillmentAuthorityReader>(provider =>
            provider.GetRequiredService<Application.CoreApi.Authorization.FulfillmentAuthority>());
        services.AddScoped<Application.Fulfillment.Postgres.IFulfillmentTransactionalAuthorityReader>(provider =>
            provider.GetRequiredService<Application.CoreApi.Authorization.FulfillmentAuthority>());
        services.AddScoped<Application.Fulfillment.Postgres.IFulfillmentStartSource, FulfillmentSourceReader>();
        Application.Fulfillment.Postgres.FulfillmentPostgresRegistration.AddFulfillmentPostgres(services);
        services.AddScoped<Application.CoreApi.Authorization.PurchasingAuthority>();
        services.AddScoped<Application.Purchasing.IPurchasingAuthorityReader>(provider =>
            provider.GetRequiredService<Application.CoreApi.Authorization.PurchasingAuthority>());
        services.AddScoped<Application.Purchasing.Postgres.IPurchasingTransactionalAuthorityReader>(provider =>
            provider.GetRequiredService<Application.CoreApi.Authorization.PurchasingAuthority>());
        services.AddScoped<Application.Purchasing.Postgres.IPurchasingSourceReader, PurchasingSourceReader>();
        Application.Purchasing.Postgres.PurchasingPostgresRegistration.AddPurchasingPostgres(services);
        services.AddCatalogOrderIntegration();
        Application.Quotations.Postgres.QuotationsPostgresRegistration.AddQuotationsPostgres(services);
        services.AddScoped<Application.Quotations.Postgres.IQuotationOrderWriter, QuotationOrderWriter>();
        services.AddScoped<Application.CoreApi.Authorization.InvoiceAuthority>();
        services.AddScoped<Application.Invoices.IInvoiceIssueAuthority>(provider => provider.GetRequiredService<Application.CoreApi.Authorization.InvoiceAuthority>());
        services.AddScoped<Application.Invoices.IInvoiceViewAuthority>(provider => provider.GetRequiredService<Application.CoreApi.Authorization.InvoiceAuthority>());
        services.AddScoped<Application.Invoices.Postgres.IInvoiceIssueTransactionalAuthority>(provider => provider.GetRequiredService<Application.CoreApi.Authorization.InvoiceAuthority>());
        services.AddScoped<InvoiceSourceReader>();
        services.AddScoped<Application.Invoices.IInvoiceSourceOrderReader>(provider => provider.GetRequiredService<InvoiceSourceReader>());
        services.AddScoped<Application.Invoices.Postgres.IInvoiceIssueTransactionalSourceReader>(provider => provider.GetRequiredService<InvoiceSourceReader>());
        Application.Invoices.Postgres.InvoicesPostgresRegistration.AddInvoicesPostgres(services);
        services.AddScoped<Application.Invoices.IssueInvoice>();
        services.AddScoped<Application.CoreApi.Authorization.FinancialAuthority>();
        services.AddScoped<Application.Invoices.IFinancialAuthorityReader>(provider => provider.GetRequiredService<Application.CoreApi.Authorization.FinancialAuthority>());
        services.AddScoped<Application.Invoices.Postgres.IFinancialTransactionalAuthorityReader>(provider => provider.GetRequiredService<Application.CoreApi.Authorization.FinancialAuthority>());
        services.AddScoped<Application.Invoices.Postgres.IFinancialDebtorReader, FinancialDebtorReader>();

        return services;
    }
}

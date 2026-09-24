namespace Application.CoreApi;

internal static class CoreApiNoStoreHeaders
{
    public static IApplicationBuilder UseCoreApiNoStoreHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<EndpointAccessMetadata>() is { } metadata
                && metadata.Access is not EndpointAccess.PublicApplicationBootstrap
                and not EndpointAccess.PublicApiDescription
                and not EndpointAccess.PublicLiveness
                and not EndpointAccess.PublicReadiness)
            {
                context.Response.OnStarting(static state =>
                {
                    ((HttpResponse)state).Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                }, context.Response);
            }

            await next();
        });
    }
}

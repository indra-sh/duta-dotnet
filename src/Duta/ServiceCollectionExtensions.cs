using Duta;

// In the DI namespace, so AddDuta shows up wherever services are configured.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Duta with dependency injection.</summary>
public static class DutaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DutaClient"/> as a typed HttpClient, so it can be injected anywhere.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddDuta(o => o.ApiKey = builder.Configuration["Duta:ApiKey"]);
    /// </code>
    /// </example>
    public static IHttpClientBuilder AddDuta(this IServiceCollection services, Action<DutaClientOptions>? configure = null)
    {
        var options = services.AddOptions<DutaClientOptions>();
        if (configure is not null) options.Configure(configure);
        return services.AddHttpClient<DutaClient>();
    }
}

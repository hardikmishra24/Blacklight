using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TipShield.Gemma;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGemmaServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GemmaOptions>()
            .Bind(configuration.GetSection(GemmaOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient<GemmaClient>(client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton<IGemmaClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GemmaOptions>>();
            if (options.Value.Mock)
            {
                return new MockGemmaClient();
            }
            return sp.GetRequiredService<GemmaClient>();
        });

        return services;
    }
}

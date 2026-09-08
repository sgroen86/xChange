using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using InvoicePlatform.Infrastructure.Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InvoicePlatform.Infrastructure;

/// <summary>
/// Composition for the invoice extraction flow. The API and Worker call this;
/// neither references the provider implementation directly.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInvoiceExtraction(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .Configure(options =>
            {
                // ANTHROPIC_API_KEY is the conventional name and what the SDK and
                // CLI already use, so accept it as a fallback to Anthropic:ApiKey.
                options.ApiKey ??= Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            });

        // Deterministic, dependency-free domain services.
        services.AddSingleton<InvoiceCalculationValidator>();
        services.AddSingleton<CanonicalInvoiceXmlSerializer>();

        services.AddSingleton<IAnthropicApiKeyProvider, AnthropicApiKeyProvider>();
        services.AddSingleton<IInvoiceInterpreter, AnthropicInvoiceInterpreter>();
        services.AddScoped<ExtractInvoiceService>();

        return services;
    }
}

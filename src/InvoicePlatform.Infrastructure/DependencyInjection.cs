using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Amazon.DynamoDBv2;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Infrastructure.Anthropic;
using InvoicePlatform.Infrastructure.Identity;
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

    /// <summary>
    /// Accounts, sessions and login. xChange holds its own users: it is a
    /// separate application that shares a domain with Bookkeeping, not a part
    /// of it. The behaviour deliberately matches Bookkeeping's login.
    /// </summary>
    public static IServiceCollection AddIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<IdentityOptions>()
            .Bind(configuration.GetSection(IdentityOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());

        // One class backs all three stores: they share a table, and splitting
        // the registration would not split the storage.
        services.AddSingleton<DynamoDbIdentityStore>();
        services.AddSingleton<IUserStore>(sp => sp.GetRequiredService<DynamoDbIdentityStore>());
        services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<DynamoDbIdentityStore>());
        services.AddSingleton<ILoginAttemptStore>(sp => sp.GetRequiredService<DynamoDbIdentityStore>());

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuthService>();

        return services;
    }
}

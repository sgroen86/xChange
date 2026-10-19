using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Application.Imports;
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Learning;
using InvoicePlatform.Application.Networks;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Ubl;
using InvoicePlatform.Domain.Serialization;
using InvoicePlatform.Domain.Validation;
using Amazon.DynamoDBv2;
using Amazon.Lambda;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SimpleEmailV2;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Infrastructure.Usage;
using InvoicePlatform.Application.Identity;
using InvoicePlatform.Infrastructure.Imports;
using InvoicePlatform.Infrastructure.Invoices;
using InvoicePlatform.Infrastructure.Learning;
using InvoicePlatform.Infrastructure.Networks;
using InvoicePlatform.Infrastructure.Validator;
using InvoicePlatform.Infrastructure.Relations;
using InvoicePlatform.Infrastructure.Anthropic;
using InvoicePlatform.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using InvoicePlatform.Application.Quality;
using InvoicePlatform.Infrastructure.Quality;

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
        services.AddSingleton<InvoiceRulesetRegistry>();
        services.AddSingleton<UblInvoiceWriter>();

        services.AddSingleton<IAnthropicApiKeyProvider, AnthropicApiKeyProvider>();
        services.AddSingleton<IInvoiceInterpreter, AnthropicInvoiceInterpreter>();
        services.AddScoped<InvoiceEnrichment>();
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
        services.AddSingleton<IOrganizationSettingsStore>(sp => sp.GetRequiredService<DynamoDbIdentityStore>());
        services.AddSingleton<IPlatformStore, DynamoDbPlatformStore>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuthService>();

        // Self sign-up: pending sign-ups expire by themselves; the confirmation goes out through SES.
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton(sp => new SignupSettings(
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value.AppUrl.TrimEnd('/')));
        services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ => new AmazonSimpleEmailServiceV2Client());
        services.AddSingleton<ISignupStore, DynamoDbSignupStore>();
        services.AddSingleton<IEmailSender, SesEmailSender>();
        services.AddScoped<SignupService>();
        services.AddScoped<OrganizationSettingsService>();
        services.AddScoped<PlatformService>();

        return services;
    }

    /// <summary>Invoice persistence: records and relations in DynamoDB, documents in S3.</summary>
    public static IServiceCollection AddInvoiceStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<InvoiceStorageOptions>()
            .Bind(configuration.GetSection(InvoiceStorageOptions.SectionName));

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client());
        services.AddSingleton<IInvoiceRepository, DynamoDbInvoiceRepository>();
        services.AddSingleton<IDocumentStore, S3DocumentStore>();
        services.AddSingleton<IRelationRepository, DynamoDbRelationRepository>();
        services.AddScoped<RelationService>();
        services.AddSingleton<ILearningStore, DynamoDbLearningStore>();
        services.AddScoped<LearningService>();

        // Conversions per organisation: a trial limit for self-signed-up ones.
        services.AddSingleton<IUsageMeter, DynamoDbUsageMeter>();

        // What every model call cost, for the platform administrator.
        services.AddSingleton<IUsageLedger, DynamoDbUsageLedger>();
        services.AddScoped<CostReportService>();

        // How well the model read what reviewers booked, for the platform administrator.
        services.AddSingleton<IQualityLedger, DynamoDbQualityLedger>();
        services.AddScoped<ReadingQualityService>();
        services.AddScoped<QualityReportService>();

        // Batch and email import: records in DynamoDB, the queue on SQS, read by the worker.
        services
            .AddOptions<ImportQueueOptions>()
            .Bind(configuration.GetSection(ImportQueueOptions.SectionName));
        services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());
        services.AddSingleton<IImportRepository, DynamoDbImportRepository>();
        services.AddSingleton<IImportQueue, SqsImportQueue>();
        services.AddScoped<ImportService>();

        // Email import: an address per organisation, mail received by SES into S3.
        services
            .AddOptions<InboxOptions>()
            .Bind(configuration.GetSection(InboxOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var inbox = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InboxOptions>>().Value;
            return new InboxSettings(inbox.Domain, inbox.PreviousDomains);
        });
        services.AddSingleton<IInboxDirectory, DynamoDbInboxDirectory>();
        services.AddSingleton<IReceivedMailSource, S3ReceivedMailSource>();
        services.AddScoped<InboxService>();
        services.AddScoped<BookInvoiceService>();
        services.AddScoped<ExportInvoiceService>();

        // The complete official rules run in a separate Lambda (validator/).
        services
            .AddOptions<OfficialValidationOptions>()
            .Bind(configuration.GetSection(OfficialValidationOptions.SectionName));
        services.AddSingleton<IAmazonLambda>(_ => new AmazonLambdaClient());
        services.AddSingleton<IOfficialValidator, LambdaOfficialValidator>();

        // Where a buyer can receive e-invoices: PEPPOL's public SML and SMPs, read-only.
        services
            .AddOptions<PeppolOptions>()
            .Bind(configuration.GetSection(PeppolOptions.SectionName));
        services.AddSingleton<IReceiverNetwork, PeppolNetwork>();
        services.AddScoped<ReceiverLookupService>();

        return services;
    }
}

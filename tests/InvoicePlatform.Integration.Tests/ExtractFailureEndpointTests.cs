using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Domain.Learning;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoicePlatform.Integration.Tests;

/// <summary>A failed reading reaches the user as a message in their language, never as a bare 500.</summary>
public class ExtractFailureEndpointTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public ExtractFailureEndpointTests(SignedInFactory factory) => _factory = factory;

    [Theory]
    [InlineData(InterpretationFailure.TimedOut, HttpStatusCode.GatewayTimeout, "nl", "Het lezen duurde te lang")]
    [InlineData(InterpretationFailure.TimedOut, HttpStatusCode.GatewayTimeout, "en", "Reading took too long")]
    [InlineData(InterpretationFailure.TooLarge, HttpStatusCode.BadGateway, "nl", "De factuur is te groot")]
    [InlineData(InterpretationFailure.ProviderUnavailable, HttpStatusCode.BadGateway, "en", "The reading service cannot be reached")]
    public async Task A_failed_reading_says_why(InterpretationFailure reason, HttpStatusCode status, string language, string message)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInvoiceInterpreter>();
            services.AddSingleton<IInvoiceInterpreter>(new FailingInterpreter(reason));
        }));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer user-a");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);

        var response = await client.PostAsync("/api/v1/invoices/extract", Pdf());

        Assert.Equal(status, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.StartsWith(message, problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private static MultipartFormDataContent Pdf()
    {
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { file, "file", "factuur.pdf" } };
    }

    private sealed class FailingInterpreter(InterpretationFailure reason) : IInvoiceInterpreter
    {
        public Task<InvoiceInterpretationResult> InterpretAsync(
            ReadOnlyMemory<byte> pdf, IReadOnlyList<ReadingHint>? hints = null, string? model = null, CancellationToken cancellationToken = default)
            => throw new InterpretationFailedException("scripted", reason: reason);
    }
}

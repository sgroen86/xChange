using InvoicePlatform.Api.Invoices;
using InvoicePlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Hard ceiling on the multipart body, independent of the per-file check in the
// endpoint: this one rejects an oversized upload before it is buffered.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ExtractInvoiceEndpoint.MaxUploadBytes;
});

builder.Services.AddInvoiceExtraction(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapGroup("/api/v1").MapInvoiceEndpoints();

app.Run();

// Exposed so the integration test host can boot this application.
public partial class Program;

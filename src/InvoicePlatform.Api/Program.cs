using InvoicePlatform.Api.Invoices;
using InvoicePlatform.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "xchange-web";

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();

// Hard ceiling on the multipart body, independent of the per-file check in the
// endpoint: this one rejects an oversized upload before it is buffered.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ExtractInvoiceEndpoint.MaxUploadBytes;
});

// The frontend is served from a different origin (greenitsolutions.net) than
// the API, so the browser needs an explicit allow. Origins come from
// configuration - never a wildcard, which would let any site post invoices here.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

builder.Services.AddInvoiceExtraction(builder.Configuration);

var app = builder.Build();

// Behind App Runner (or any TLS-terminating proxy) the original scheme and host
// arrive in headers; without this the app sees plain HTTP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Only locally. In front of a proxy that already terminates TLS, an internal
    // HTTPS redirect sends the client into a loop.
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);
app.UseStatusCodePages();

app.MapHealthChecks("/health");
app.MapGroup("/api/v1").MapInvoiceEndpoints().RequireCors(CorsPolicy);

app.Run();

// Exposed so the integration test host can boot this application.
public partial class Program;

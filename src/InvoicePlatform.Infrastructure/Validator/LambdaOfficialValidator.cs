using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using InvoicePlatform.Application.Invoices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoicePlatform.Infrastructure.Validator;

/// <summary>
/// Asks the xchange-validator Lambda (validator/, Java and Saxon-HE) for the
/// official verdict on a document. Any trouble - not configured, unreachable,
/// an error from the function - is reported as unavailable, never as "valid".
/// </summary>
internal sealed class LambdaOfficialValidator(
    IAmazonLambda lambda,
    IOptions<OfficialValidationOptions> options,
    ILogger<LambdaOfficialValidator> logger)
    : IOfficialValidator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OfficialValidation> ValidateAsync(string rulesetId, string xml, CancellationToken cancellationToken = default)
    {
        var function = options.Value.FunctionName;
        if (string.IsNullOrWhiteSpace(function))
        {
            return OfficialValidation.Unavailable("No official validator is configured (OfficialValidation:FunctionName).");
        }

        try
        {
            var response = await lambda.InvokeAsync(
                new InvokeRequest
                {
                    FunctionName = function,
                    InvocationType = InvocationType.RequestResponse,
                    Payload = JsonSerializer.Serialize(new { rulesetId, xml }, Json),
                },
                cancellationToken).ConfigureAwait(false);

            var body = Encoding.UTF8.GetString(response.Payload.ToArray());

            if (!string.IsNullOrEmpty(response.FunctionError))
            {
                logger.LogError("The official validator failed: {Error} {Body}", response.FunctionError, body);
                return OfficialValidation.Unavailable($"The official validator failed: {response.FunctionError}.");
            }

            var verdict = JsonSerializer.Deserialize<ValidatorResponse>(body, Json);
            if (verdict is null || verdict.Error is not null || verdict.Failures is null)
            {
                logger.LogError("The official validator refused the request: {Body}", body);
                return OfficialValidation.Unavailable(verdict?.Error ?? "The official validator gave no verdict.");
            }

            return new OfficialValidation(
                true,
                verdict.Release,
                [.. verdict.Failures.Select(f => new OfficialFailure(f.Flag ?? "fatal", f.RuleId ?? "", f.Message ?? "", f.Location ?? ""))]);
        }
        catch (Exception exception) when (exception is AmazonLambdaException or HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(exception, "The official validator could not be reached");
            return OfficialValidation.Unavailable("The official validator could not be reached.");
        }
    }

    private sealed record ValidatorResponse(
        [property: JsonPropertyName("release")] string? Release,
        [property: JsonPropertyName("failures")] IReadOnlyList<ValidatorFailure>? Failures,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record ValidatorFailure(
        [property: JsonPropertyName("flag")] string? Flag,
        [property: JsonPropertyName("ruleId")] string? RuleId,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("location")] string? Location);
}

public sealed class OfficialValidationOptions
{
    public const string SectionName = "OfficialValidation";

    /// <summary>Name or ARN (with alias) of the validator function. Empty: exports are refused.</summary>
    public string? FunctionName { get; set; }
}

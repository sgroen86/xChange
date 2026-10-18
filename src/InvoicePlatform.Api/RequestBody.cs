using System.Text.Json;

namespace InvoicePlatform.Api;

/// <summary>
/// Reads a JSON body inside a handler. Minimal APIs bind parameters before
/// endpoint filters run, so a bound body would let an anonymous caller get a
/// 400 before the login check answered 401.
/// </summary>
internal static class RequestBody
{
    /// <returns>The body, or null when it is missing or not valid JSON for the type.</returns>
    public static async Task<T?> ReadAsync<T>(HttpContext context, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // Not a JSON content type.
            return null;
        }
    }

    public static IResult Invalid()
        => Results.Problem(
            detail: "De aanvraag is geen geldige JSON of mist verplichte velden.",
            title: "Ongeldige aanvraag",
            statusCode: StatusCodes.Status400BadRequest);
}

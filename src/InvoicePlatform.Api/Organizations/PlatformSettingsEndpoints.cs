using System.Text.Json.Serialization;
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Organizations;

namespace InvoicePlatform.Api.Organizations;

/// <summary>Platform-wide settings: which model reads the PDFs. Platform administrator only.</summary>
public static class PlatformSettingsEndpoints
{
    public sealed record ModelOption([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name);

    public sealed record PlatformSettingsResponse(
        [property: JsonPropertyName("extractionModel")] string? ExtractionModel,
        [property: JsonPropertyName("models")] IReadOnlyList<ModelOption> Models,
        [property: JsonPropertyName("signupOpen")] bool SignupOpen);

    /// <param name="SignupOpen">Null leaves it as it is.</param>
    public sealed record UpdatePlatformSettingsRequest(
        [property: JsonPropertyName("extractionModel")] string? ExtractionModel,
        [property: JsonPropertyName("signupOpen")] bool? SignupOpen = null);

    public static RouteGroupBuilder MapPlatformSettingsEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/platform/settings", GetAsync)
            .WithName("GetPlatformSettings")
            .RequirePlatformAdmin();

        group.MapPut("/platform/settings", UpdateAsync)
            .WithName("UpdatePlatformSettings")
            .RequirePlatformAdmin();

        return group;
    }

    private static async Task<IResult> GetAsync(PlatformService platform, CancellationToken cancellationToken)
        => Results.Ok(ToResponse(await platform.GetSettingsAsync(cancellationToken).ConfigureAwait(false)));

    private static async Task<IResult> UpdateAsync(HttpContext context, PlatformService platform, CancellationToken cancellationToken)
    {
        var request = await RequestBody.ReadAsync<UpdatePlatformSettingsRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var error = await platform.UpdateSettingsAsync(request.ExtractionModel, request.SignupOpen, cancellationToken).ConfigureAwait(false);
        return error is null
            ? Results.Ok(ToResponse(await platform.GetSettingsAsync(cancellationToken).ConfigureAwait(false)))
            : Results.Problem(detail: error, title: OrganizationApiText.Current.SettingNotSaved, statusCode: StatusCodes.Status400BadRequest);
    }

    private static PlatformSettingsResponse ToResponse(PlatformSettings settings)
        => new(settings.ExtractionModel, [.. ExtractionModels.All.Select(m => new ModelOption(m.Id, m.Name))], settings.SignupOpen);
}

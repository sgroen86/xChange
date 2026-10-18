using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Invoices;

public sealed record RulesetInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("rulesRelease")]
    public required string RulesRelease { get; init; }
}

/// <summary>Wire shape of GET and PUT /api/v1/organization/settings.</summary>
public sealed record OrganizationSettingsResponse
{
    [JsonPropertyName("defaultRulesetId")]
    public string? DefaultRulesetId { get; init; }

    [JsonPropertyName("endpointId")]
    public string? EndpointId { get; init; }

    [JsonPropertyName("endpointScheme")]
    public string? EndpointScheme { get; init; }

    /// <summary>Every ruleset the organisation can choose from, the system default first.</summary>
    [JsonPropertyName("rulesets")]
    public required IReadOnlyList<RulesetInfo> Rulesets { get; init; }
}

public sealed record UpdateOrganizationSettingsRequest
{
    [JsonPropertyName("defaultRulesetId")]
    public string? DefaultRulesetId { get; init; }

    [JsonPropertyName("endpointId")]
    public string? EndpointId { get; init; }

    [JsonPropertyName("endpointScheme")]
    public string? EndpointScheme { get; init; }
}

public sealed record RuleViolationResponse
{
    [JsonPropertyName("ruleId")]
    public required string RuleId { get; init; }

    [JsonPropertyName("businessTerm")]
    public string? BusinessTerm { get; init; }

    /// <summary>Dotted field path, equal to the input id on the review screen.</summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>Body of a check, and of an export refused with 422.</summary>
public sealed record RuleViolationsResponse
{
    [JsonPropertyName("violations")]
    public required IReadOnlyList<RuleViolationResponse> Violations { get; init; }
}

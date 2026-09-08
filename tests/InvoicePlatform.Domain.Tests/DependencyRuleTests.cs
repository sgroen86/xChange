using System.Reflection;
using InvoicePlatform.Domain;

namespace InvoicePlatform.Domain.Tests;

/// <summary>
/// Guards the dependency rule in CLAUDE.md: Domain must not depend on AWS, OCR,
/// LLM or web frameworks.
/// </summary>
public class DependencyRuleTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Amazon",
        "AWSSDK"
    ];

    [Fact]
    public void Domain_does_not_reference_infrastructure_concerns()
    {
        var referenced = typeof(AssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var violations = referenced
            .Where(name => ForbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(violations);
    }
}

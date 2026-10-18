namespace InvoicePlatform.Domain.Localization;

/// <summary>The languages messages are written in. Dutch is the default.</summary>
public enum Language
{
    Nl,
    En,
}

/// <summary>
/// The language of the messages produced while handling one request: rule
/// messages, API errors, mails. Set once at the edge (the API reads
/// Accept-Language) and flowing with the async call, like a culture - but our
/// own, so neither the server's culture nor the test machine's changes a
/// message. Without a scope it is Dutch.
///
/// Only text depends on it. Rule ids, outcomes, amounts and the XML never do.
///
/// Messages live in catalogues per area: an interface with one member per
/// message and one class per language implementing it, so a message missing
/// in one language is a compile error. <see cref="Pick{T}"/> chooses the
/// catalogue for the current language.
/// </summary>
public static class Texts
{
    private static readonly AsyncLocal<Language?> Scoped = new();

    public static Language Current => Scoped.Value ?? Language.Nl;

    /// <summary>Uses <paramref name="language"/> until the returned scope is disposed.</summary>
    public static IDisposable Use(Language language)
    {
        var previous = Scoped.Value;
        Scoped.Value = language;
        return new Restore(previous);
    }

    /// <summary>The catalogue for the current language.</summary>
    public static T Pick<T>(T nl, T en) => Current == Language.En ? en : nl;

    /// <summary>
    /// The first supported language in an Accept-Language header ("en-GB,en;q=0.9,nl;q=0.8"),
    /// honouring q-values; Dutch when none is supported or the header is absent.
    /// </summary>
    public static Language FromAcceptLanguage(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return Language.Nl;
        }

        var ranked = header
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((part, index) =>
            {
                var pieces = part.Split(';', StringSplitOptions.TrimEntries);
                var quality = 1.0m;
                foreach (var parameter in pieces.Skip(1))
                {
                    if (parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                        && decimal.TryParse(parameter[2..], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var q))
                    {
                        quality = q;
                    }
                }

                return (Tag: pieces[0].ToLowerInvariant(), Quality: quality, Index: index);
            })
            .Where(entry => entry.Quality > 0)
            .OrderByDescending(entry => entry.Quality)
            .ThenBy(entry => entry.Index);

        foreach (var entry in ranked)
        {
            if (entry.Tag == "en" || entry.Tag.StartsWith("en-", StringComparison.Ordinal))
            {
                return Language.En;
            }

            if (entry.Tag == "nl" || entry.Tag.StartsWith("nl-", StringComparison.Ordinal))
            {
                return Language.Nl;
            }
        }

        return Language.Nl;
    }

    private sealed class Restore(Language? previous) : IDisposable
    {
        public void Dispose() => Scoped.Value = previous;
    }
}

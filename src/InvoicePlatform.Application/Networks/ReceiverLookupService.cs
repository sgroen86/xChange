using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Networks;

namespace InvoicePlatform.Application.Networks;

/// <summary>
/// Finds out whether a buyer can receive e-invoices, under which address, and
/// in which of xChange's formats. The buyer is looked up by the numbers the
/// invoice gives (electronic address, VAT, registration), never by name. The
/// candidates are asked at once; the most trustworthy one that is registered
/// wins. Nothing is stored and nothing is blocked: an unreachable network is
/// an answer, not an error.
/// </summary>
public sealed class ReceiverLookupService(IEnumerable<IReceiverNetwork> networks, InvoiceRulesetRegistry rulesets)
{
    /// <summary>More than this many candidates means something odd was entered; the rest are not asked.</summary>
    private const int MaxCandidates = 5;

    public async Task<ReceiverLookupResult> LookupAsync(CanonicalParty? party, CancellationToken cancellationToken = default)
    {
        var candidates = PeppolIdentifiers.CandidatesFor(party).Take(MaxCandidates).ToList();
        if (candidates.Count == 0)
        {
            return new ReceiverLookupResult(ReceiverLookupStatus.NothingToLookUp, Tried: []);
        }

        var unavailable = false;
        foreach (var network in networks)
        {
            var answers = await Task.WhenAll(candidates.Select(c => network.FindAsync(c, cancellationToken))).ConfigureAwait(false);

            for (var i = 0; i < candidates.Count; i++)
            {
                if (answers[i].Kind == NetworkAnswerKind.Registered)
                {
                    return new ReceiverLookupResult(
                        ReceiverLookupStatus.Found,
                        candidates,
                        network.Name,
                        candidates[i],
                        PeppolDocumentTypes.AcceptedRulesets(answers[i].DocumentTypes, rulesets.All),
                        answers[i].DocumentTypes.Count);
                }
            }

            unavailable |= answers.Any(answer => answer.Kind == NetworkAnswerKind.Unavailable);
        }

        return new ReceiverLookupResult(unavailable ? ReceiverLookupStatus.Unavailable : ReceiverLookupStatus.NotFound, candidates);
    }
}

public enum ReceiverLookupStatus
{
    Found,

    /// <summary>Every candidate was asked and none is registered.</summary>
    NotFound,

    /// <summary>Not found, and at least one question went unanswered.</summary>
    Unavailable,

    /// <summary>The party has no number to look up.</summary>
    NothingToLookUp,
}

/// <param name="Tried">The addresses asked, in order of confidence.</param>
/// <param name="AcceptedRulesetIds">xChange formats the receiver takes invoices in.</param>
/// <param name="DocumentTypeCount">Everything it registered, including document types xChange does not produce.</param>
public sealed record ReceiverLookupResult(
    ReceiverLookupStatus Status,
    IReadOnlyList<ParticipantIdentifier> Tried,
    string? Network = null,
    ParticipantIdentifier? Participant = null,
    IReadOnlyList<string>? AcceptedRulesetIds = null,
    int DocumentTypeCount = 0);

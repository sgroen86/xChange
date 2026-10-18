using InvoicePlatform.Application.Networks;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Networks;

namespace InvoicePlatform.Application.Tests;

public class ReceiverLookupServiceTests
{
    private const string Bis = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1";

    private static readonly CanonicalParty VanDijk = new()
    {
        VatIdentifier = "NL814912345B01",
        LegalRegistrationId = "87654321",
        Address = new CanonicalAddress { CountryCode = "NL" },
    };

    private static ReceiverLookupService Service(FakeNetwork network) => new([network], new InvoiceRulesetRegistry());

    [Fact]
    public async Task The_most_trustworthy_registered_address_wins()
    {
        var network = new FakeNetwork
        {
            ["9944:NL814912345B01"] = NetworkAnswer.Registered([new DocumentTypeId("busdox-docid-qns", Bis)]),
            ["0106:87654321"] = NetworkAnswer.Registered([]),
        };

        var result = await Service(network).LookupAsync(VanDijk);

        Assert.Equal(ReceiverLookupStatus.Found, result.Status);
        Assert.Equal("9944:NL814912345B01", result.Participant!.ToString());
        Assert.Equal(["peppol-bis-billing-3.0"], result.AcceptedRulesetIds);
        Assert.Equal("peppol", result.Network);
    }

    [Fact]
    public async Task Nobody_registered_is_not_found()
    {
        var result = await Service(new FakeNetwork()).LookupAsync(VanDijk);

        Assert.Equal(ReceiverLookupStatus.NotFound, result.Status);
        Assert.Equal(["9944:NL814912345B01", "0106:87654321"], result.Tried.Select(t => t.ToString()));
    }

    [Fact]
    public async Task An_unanswered_question_is_unavailable_not_not_found()
    {
        var network = new FakeNetwork { ["0106:87654321"] = NetworkAnswer.Unavailable };

        var result = await Service(network).LookupAsync(VanDijk);

        Assert.Equal(ReceiverLookupStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task A_buyer_known_only_by_name_is_not_looked_up()
    {
        var network = new FakeNetwork();

        var result = await Service(network).LookupAsync(new CanonicalParty { Name = "Van Dijk" });

        Assert.Equal(ReceiverLookupStatus.NothingToLookUp, result.Status);
        Assert.Empty(network.Asked);
    }

    private sealed class FakeNetwork : Dictionary<string, NetworkAnswer>, IReceiverNetwork
    {
        public List<string> Asked { get; } = [];

        public string Name => "peppol";

        public Task<NetworkAnswer> FindAsync(ParticipantIdentifier participant, CancellationToken cancellationToken = default)
        {
            lock (Asked)
            {
                Asked.Add(participant.ToString());
            }

            return Task.FromResult(TryGetValue(participant.ToString(), out var answer) ? answer : NetworkAnswer.NotRegistered);
        }
    }
}

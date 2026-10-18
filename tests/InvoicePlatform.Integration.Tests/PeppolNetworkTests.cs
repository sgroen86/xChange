using System.Net;
using System.Xml.Linq;
using InvoicePlatform.Domain.Networks;
using InvoicePlatform.Infrastructure.Networks;

namespace InvoicePlatform.Integration.Tests;

/// <summary>The offline parts of the PEPPOL lookup: naming, parsing and the address check.</summary>
public class PeppolNetworkTests
{
    [Fact]
    public void The_sml_name_is_sha256_base32_of_the_lower_case_identifier()
        // Verified against the live SML: this participant resolves under this name.
        => Assert.Equal("ecflotdofcz3zswh534ufkl754xdezsf4hznms5e4ffa6qzhrhra",
            PeppolNetwork.Hash(new ParticipantIdentifier("0106", "82924082")));

    [Theory]
    [InlineData("!.*!https://smp.tradeinterop.com/!", "https://smp.tradeinterop.com/")]
    [InlineData("!.*!http://smp.example.org/path/!", "http://smp.example.org/path/")]
    [InlineData("!.*!ftp://smp.example.org/!", null)]
    [InlineData("", null)]
    public void The_smp_address_is_read_from_the_naptr_expression(string expression, string? expected)
        => Assert.Equal(expected, PeppolNetwork.SmpUri(expression)?.ToString());

    [Fact]
    public void Document_types_are_read_from_the_service_group()
    {
        var serviceGroup = XDocument.Parse(
            "<smp:ServiceGroup xmlns:smp=\"http://busdox.org/serviceMetadata/publishing/1.0/\">"
            + "<smp:ServiceMetadataReferenceCollection>"
            + "<smp:ServiceMetadataReference href=\"https://smp.example.org/iso6523-actorid-upis%3A%3A0106%3A1/services/busdox-docid-qns%3A%3Aurn%3Aoasis%3Anames%3Aspecification%3Aubl%3Aschema%3Axsd%3AInvoice-2%3A%3AInvoice%23%23urn%3Acen.eu%3Aen16931%3A2017%23compliant%23urn%3Afdc%3Apeppol.eu%3A2017%3Apoacc%3Abilling%3A3.0%3A%3A2.1\"/>"
            + "</smp:ServiceMetadataReferenceCollection>"
            + "</smp:ServiceGroup>");

        var type = Assert.Single(PeppolNetwork.DocumentTypes(serviceGroup));

        Assert.Equal("busdox-docid-qns", type.Scheme);
        Assert.Equal(
            "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2::Invoice##urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0::2.1",
            type.Value);
    }

    [Theory]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.20.1.1", true)]
    [InlineData("192.168.4.1", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("52.212.186.3", false)]
    [InlineData("2a01:238:20a:202:1148::", false)]
    public void Only_public_addresses_may_be_requested(string address, bool isPrivate)
        => Assert.Equal(isPrivate, PeppolNetwork.IsPrivate(IPAddress.Parse(address)));
}

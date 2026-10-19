using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InvoicePlatform.Infrastructure.Imports;

namespace InvoicePlatform.Integration.Tests;

public class InboxTests : IClassFixture<SignedInFactory>
{
    private readonly SignedInFactory _factory;

    public InboxTests(SignedInFactory factory) => _factory = factory;

    [Fact]
    public async Task Pdf_attachments_sender_and_subject_are_read_from_a_raw_mail()
    {
        var pdf = Convert.ToBase64String("%PDF-1.4 hello"u8.ToArray());
        var raw = string.Join("\r\n",
            "From: Boekhouding <boekhouding@klant.nl>",
            "To: abc@inbox.greenitsolutions.net",
            "Subject: Facturen oktober",
            "MIME-Version: 1.0",
            "Content-Type: multipart/mixed; boundary=\"b1\"",
            "",
            "--b1",
            "Content-Type: text/plain; charset=utf-8",
            "",
            "Zie bijlage.",
            "--b1",
            "Content-Type: application/pdf; name=\"F-2026-041.pdf\"",
            "Content-Disposition: attachment; filename=\"F-2026-041.pdf\"",
            "Content-Transfer-Encoding: base64",
            "",
            pdf,
            "--b1",
            "Content-Type: image/png; name=\"logo.png\"",
            "Content-Disposition: attachment; filename=\"logo.png\"",
            "Content-Transfer-Encoding: base64",
            "",
            "iVBORw0KGgo=",
            "--b1--",
            "");

        var mail = await S3ReceivedMailSource.ParseAsync(new MemoryStream(Encoding.ASCII.GetBytes(raw)));

        Assert.Equal("boekhouding@klant.nl", mail.Sender);
        Assert.Equal("Facturen oktober", mail.Subject);
        var attachment = Assert.Single(mail.Attachments);
        Assert.Equal("F-2026-041.pdf", attachment.FileName);
        Assert.Equal("%PDF-1.4 hello", Encoding.ASCII.GetString(attachment.Content));
    }

    [Fact]
    public async Task Only_an_admin_creates_the_address_and_every_user_can_see_it()
    {
        var byUser = await _factory.As("user-a").PostAsync("/api/v1/organization/inbox", null);
        var created = await _factory.As("admin-a").PostAsync("/api/v1/organization/inbox", null);
        var seen = await _factory.As("readonly-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/inbox");
        var otherOrg = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/organization/inbox");

        Assert.Equal(HttpStatusCode.Forbidden, byUser.StatusCode);
        var address = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("address").GetString();
        Assert.EndsWith("@inbox.xchangeinvoice.com", address, StringComparison.Ordinal);
        Assert.Equal(address, seen.GetProperty("address").GetString());
        Assert.NotEqual(address, otherOrg.GetProperty("address").GetString());
    }
}

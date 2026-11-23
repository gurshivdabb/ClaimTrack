using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Dtos.Documents;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ClaimTrack.IntegrationTests;

// POST/GET /api/claims/{id}/documents and GET /api/claims/{id}/documents/{documentId}.
[Collection(ApiCollection.Name)]
public class DocumentTests(ClaimTrackApiFactory factory)
{
    private static readonly byte[] Pdf = "%PDF-1.4\n% ClaimTrack test document\n%%EOF"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Program = "MZ\u0090\u0000 this is pretending to be a PDF"u8.ToArray();

    private readonly HttpClient _owner = factory.CreatePolicyholderClient();
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, Guid claimId, byte[]? bytes, string fileName = "receipt.pdf", string contentType = "application/pdf")
    {
        using var form = new MultipartFormDataContent();
        if (bytes is not null)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, "file", fileName);
        }
        else
        {
            form.Add(new StringContent("no file here"), "note");
        }
        return await client.PostAsync($"/api/claims/{claimId}/documents", form);
    }

    private static async Task ShouldBeFileErrorAsync(HttpResponseMessage response, string messagePart)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Should().ContainKey("file");
        problem.Errors["file"].Should().Contain(m => m.Contains(messagePart));
    }

    private WebApplicationFactory<Program> ApiWith(string setting, string value) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [$"Documents:{setting}"] = value })));

    [Fact]
    public async Task Upload_PdfByOwner_Returns201ThenListsAndDownloadsTheSameBytes()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await UploadAsync(_owner, claim.Id, Pdf);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await response.ReadAsync<DocumentResponse>();
        (document.FileName, document.ContentType, document.SizeBytes, document.UploadedByName)
            .Should().Be(("receipt.pdf", "application/pdf", Pdf.Length, "Test Policyholder"));
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/claims/{claim.Id}/documents/{document.Id}");

        var list = await (await _owner.GetAsync($"/api/claims/{claim.Id}/documents")).ReadAsync<List<DocumentResponse>>();
        list.Should().ContainSingle().Which.Should().Be(document);

        var download = await _owner.GetAsync(response.Headers.Location);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(Pdf);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment"); // save, don't open
        download.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    public async Task Upload_ContentTypeComesFromTheBytesNotTheUpload()
    {
        var claim = await factory.SubmitClaimAsync();

        // A real PNG sent with a misleading name and Content-Type.
        var response = await UploadAsync(_owner, claim.Id, Png, fileName: "scan.pdf", contentType: "application/pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<DocumentResponse>()).ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task Upload_ProgramRenamedToPdf_Returns400()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await UploadAsync(_owner, claim.Id, Program, fileName: "photo.pdf", contentType: "application/pdf");

        await ShouldBeFileErrorAsync(response, "Only PDF, PNG, or JPEG");
    }

    [Fact]
    public async Task Upload_EmptyOrMissingFile_Returns400()
    {
        var claim = await factory.SubmitClaimAsync();

        await ShouldBeFileErrorAsync(await UploadAsync(_owner, claim.Id, []), "empty");
        await ShouldBeFileErrorAsync(await UploadAsync(_owner, claim.Id, null), "Attach a file");
    }

    [Fact]
    public async Task Upload_OverTheSizeLimit_Returns400()
    {
        var claim = await factory.SubmitClaimAsync();
        using var api = ApiWith("MaxSizeBytes", "1024");
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(ClaimTrackApiFactory.PolicyholderId, Role.Policyholder));
        var bigPdf = Pdf.Concat(new byte[2000]).ToArray();

        var response = await UploadAsync(client, claim.Id, bigPdf);

        await ShouldBeFileErrorAsync(response, "larger than");
    }

    [Fact]
    public async Task Upload_OverTheLimitPerClaim_Returns409()
    {
        var claim = await factory.SubmitClaimAsync();
        using var api = ApiWith("MaxPerClaim", "2");
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateToken(ClaimTrackApiFactory.PolicyholderId, Role.Policyholder));

        (await UploadAsync(client, claim.Id, Pdf)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await UploadAsync(client, claim.Id, Pdf)).StatusCode.Should().Be(HttpStatusCode.Created);
        var third = await UploadAsync(client, claim.Id, Pdf);

        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await third.ReadAsync<ProblemDetails>()).Detail.Should().Be("A claim can have at most 2 documents.");
    }

    [Fact]
    public async Task Upload_FinalClaim_Returns409()
    {
        var claim = await factory.SubmitClaimAsync();
        await _owner.WithdrawAsync(claim.Id);

        var response = await UploadAsync(_owner, claim.Id, Pdf);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Be("Documents can't be added to a Withdrawn claim.");
    }

    [Fact]
    public async Task Upload_AssignedAdjusterAndSupervisor_CanUpload()
    {
        var (claim, _, adjuster) = await factory.SubmitAndAssignAsync();

        (await UploadAsync(adjuster, claim.Id, Pdf, "inspection-report.pdf")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await UploadAsync(_supervisor, claim.Id, Png, "photo.png", "image/png")).StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await (await _owner.GetAsync($"/api/claims/{claim.Id}/documents")).ReadAsync<List<DocumentResponse>>();
        list.Select(d => (d.FileName, d.UploadedByName)).Should().Equal(
            ("inspection-report.pdf", "Test Adjuster"), ("photo.png", "Test Supervisor"));
    }

    [Fact]
    public async Task Documents_PeopleWhoCannotSeeTheClaim_Get404()
    {
        var (claim, _, _) = await factory.SubmitAndAssignAsync();
        var document = await (await UploadAsync(_owner, claim.Id, Pdf)).ReadAsync<DocumentResponse>();
        var otherHolder = await factory.CreateClientForNewUserAsync(Role.Policyholder);
        var otherAdjuster = await factory.CreateClientForNewUserAsync(Role.Adjuster);

        foreach (var outsider in new[] { otherHolder, otherAdjuster })
        {
            (await UploadAsync(outsider, claim.Id, Pdf)).StatusCode.Should().Be(HttpStatusCode.NotFound); // BR-09
            (await outsider.GetAsync($"/api/claims/{claim.Id}/documents")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await outsider.GetAsync($"/api/claims/{claim.Id}/documents/{document.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Download_DocumentFromAnotherClaim_Returns404()
    {
        var claimA = await factory.SubmitClaimAsync();
        var claimB = await factory.SubmitClaimAsync();
        var documentOnA = await (await UploadAsync(_owner, claimA.Id, Pdf)).ReadAsync<DocumentResponse>();

        // The owner can see both claims, but the document belongs to A, not B.
        var response = await _owner.GetAsync($"/api/claims/{claimB.Id}/documents/{documentOnA.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_PathTraversalName_IsCleanedAndStoredUnderARandomName()
    {
        var claim = await factory.SubmitClaimAsync();
        var outsideFolder = Path.GetDirectoryName(factory.DocumentStoragePath)!;

        var response = await UploadAsync(_owner, claim.Id, Pdf, fileName: "../../evil-overwrite.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<DocumentResponse>()).FileName.Should().Be("evil-overwrite.pdf");
        Directory.GetFiles(factory.DocumentStoragePath).Select(Path.GetFileName)
            .Should().OnlyContain(name => name!.Length == 32 && name.All(Uri.IsHexDigit), "files are named by random keys only");
        File.Exists(Path.Combine(outsideFolder, "evil-overwrite.pdf")).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_IsRecordedInTheClaimHistory()
    {
        var claim = await factory.SubmitClaimAsync();

        await UploadAsync(_owner, claim.Id, Pdf, "receipt.pdf");

        var history = await (await _owner.GetAsync($"/api/claims/{claim.Id}/history")).ReadAsync<List<AuditLogEntryResponse>>();
        history.Last().Should().Match<AuditLogEntryResponse>(e =>
            e.Action == AuditAction.DocumentUploaded && e.Details == "Uploaded receipt.pdf" && e.ActorRole == Role.Policyholder);
    }

    // The file is saved before the database row. If the database save fails, the file must be
    // removed again. A supervisor token for a user who isn't in the database makes the save fail
    // (the document's UploadedById foreign key).
    [Fact]
    public async Task Upload_DatabaseSaveFails_LeavesNoOrphanFile()
    {
        var claim = await factory.SubmitClaimAsync();
        var ghost = factory.CreateClientAs(Guid.NewGuid(), Role.Supervisor);
        Directory.CreateDirectory(factory.DocumentStoragePath);
        var filesBefore = Directory.GetFiles(factory.DocumentStoragePath).ToHashSet();

        var response = await UploadAsync(ghost, claim.Id, Pdf);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        Directory.GetFiles(factory.DocumentStoragePath).Should().BeEquivalentTo(filesBefore, "the stored file was cleaned up");
    }

    [Fact]
    public async Task Documents_NoToken_Returns401()
    {
        var claim = await factory.SubmitClaimAsync();

        var response = await UploadAsync(factory.CreateClient(), claim.Id, Pdf);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

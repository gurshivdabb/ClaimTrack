using ClaimTrack.Api.Dtos.Documents;
using ClaimTrack.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.Api.Controllers;

// Documents attached to a claim. No role attribute: whoever can see the claim may use these
// (BR-09 in DocumentService); everyone else gets 404. A token is still required (Program.cs).
[ApiController]
[Route("api/claims/{claimId:guid}/documents")]
public class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DocumentResponse>> Upload(
        Guid claimId, [FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        // The validator has already checked the file is present, within the size limit, and an allowed type.
        var file = request.File!;
        await using var content = file.OpenReadStream();

        var result = await documentService.UploadAsync(claimId, content, file.FileName, file.Length, cancellationToken);
        return result.ToActionResult(this, document =>
            CreatedAtAction(nameof(Download), new { claimId, documentId = document.Id }, document));
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentResponse>>> List(Guid claimId, CancellationToken cancellationToken)
    {
        var result = await documentService.ListAsync(claimId, cancellationToken);
        return result.ToActionResult(this);
    }

    // Sent as an attachment ("save, don't open"), with nosniff so the browser never second-guesses
    // the content type. Together with only allowing PDF/PNG/JPEG, a download can't run scripts.
    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> Download(Guid claimId, Guid documentId, CancellationToken cancellationToken)
    {
        var result = await documentService.DownloadAsync(claimId, documentId, cancellationToken);
        return result.ToActionResult(this, download =>
        {
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(download.Content, download.ContentType, download.FileName);
        });
    }
}

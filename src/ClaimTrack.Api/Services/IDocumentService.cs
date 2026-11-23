using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Documents;

namespace ClaimTrack.Api.Services;

public interface IDocumentService
{
    // Content arrives already validated (size and file type) by UploadDocumentRequestValidator.
    Task<Result<DocumentResponse>> UploadAsync(
        Guid claimId, Stream content, string? fileName, long sizeBytes, CancellationToken cancellationToken);

    Task<Result<List<DocumentResponse>>> ListAsync(Guid claimId, CancellationToken cancellationToken);

    Task<Result<DocumentDownload>> DownloadAsync(Guid claimId, Guid documentId, CancellationToken cancellationToken);
}

using System.Linq.Expressions;
using ClaimTrack.Api.Entities;

namespace ClaimTrack.Api.Dtos.Documents;

public static class DocumentMappings
{
    // An expression so EF selects only these columns (one join for the uploader's name), like AuditLogMappings.
    public static readonly Expression<Func<Document, DocumentResponse>> ToResponse =
        document => new DocumentResponse(
            document.Id,
            document.ClaimId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.UploadedAt,
            document.UploadedBy.FullName);
}

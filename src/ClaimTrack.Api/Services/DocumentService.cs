using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Documents;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Services;

// Documents on claims. Whoever can see a claim (BR-09) can list, download, and, while the claim
// isn't final, upload to it; anyone else gets 404. Documents are never changed or deleted.
public class DocumentService(
    AppDbContext db,
    ICurrentUser currentUser,
    IDocumentStorage storage,
    IAuditService audit,
    IOptions<DocumentOptions> options,
    TimeProvider timeProvider) : IDocumentService
{
    public async Task<Result<DocumentResponse>> UploadAsync(
        Guid claimId, Stream content, string? fileName, long sizeBytes, CancellationToken cancellationToken)
    {
        var claim = await db.Claims.VisibleTo(currentUser) // BR-09
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);
        if (claim is null)
        {
            return Result<DocumentResponse>.NotFound();
        }

        // A decided or withdrawn claim's evidence is closed, like its status (§4).
        if (ClaimLifecycle.IsFinal(claim.Status))
        {
            return Result<DocumentResponse>.Conflict($"Documents can't be added to a {claim.Status} claim.");
        }

        var maxPerClaim = options.Value.MaxPerClaim;
        if (await db.Documents.CountAsync(d => d.ClaimId == claimId, cancellationToken) >= maxPerClaim)
        {
            return Result<DocumentResponse>.Conflict($"A claim can have at most {maxPerClaim} documents.");
        }

        // The type comes from the file's own bytes (already checked by the validator), never the upload's claims.
        var contentType = await FileSignatures.DetectAsync(content, cancellationToken)
            ?? throw new InvalidOperationException("Unsupported file type reached DocumentService; the validator should have rejected it.");

        var document = new Document
        {
            ClaimId = claimId,
            FileName = FileNames.Sanitize(fileName, contentType),
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedById = currentUser.Id,
            UploadedAt = timeProvider.GetUtcNow()
        };

        // The file is written first; if the database save then fails, it's removed again,
        // so there are never stored files that no record points to.
        document.StoragePath = await storage.SaveAsync(content, cancellationToken);
        try
        {
            db.Documents.Add(document);
            audit.Record(claim, AuditAction.DocumentUploaded, details: $"Uploaded {document.FileName}"); // BR-07
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(document.StoragePath, CancellationToken.None);
            throw;
        }

        var uploaderName = await db.Users
            .Where(u => u.Id == document.UploadedById)
            .Select(u => u.FullName)
            .SingleAsync(cancellationToken);

        return Result<DocumentResponse>.Ok(new DocumentResponse(
            document.Id, claimId, document.FileName, document.ContentType, document.SizeBytes,
            document.UploadedAt, uploaderName));
    }

    public async Task<Result<List<DocumentResponse>>> ListAsync(Guid claimId, CancellationToken cancellationToken)
    {
        if (!await CanSeeClaimAsync(claimId, cancellationToken))
        {
            return Result<List<DocumentResponse>>.NotFound();
        }

        var documents = await db.Documents
            .Where(d => d.ClaimId == claimId)
            .OrderBy(d => d.UploadedAt)
            .ThenBy(d => d.Id)
            .Select(DocumentMappings.ToResponse)
            .ToListAsync(cancellationToken);

        return Result<List<DocumentResponse>>.Ok(documents);
    }

    public async Task<Result<DocumentDownload>> DownloadAsync(
        Guid claimId, Guid documentId, CancellationToken cancellationToken)
    {
        if (!await CanSeeClaimAsync(claimId, cancellationToken))
        {
            return Result<DocumentDownload>.NotFound();
        }

        // The document must belong to this claim: guessing another claim's document ID gets 404.
        var document = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.ClaimId == claimId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentDownload>.NotFound();
        }

        var content = await storage.OpenReadAsync(document.StoragePath, cancellationToken);
        return Result<DocumentDownload>.Ok(new DocumentDownload(content, document.ContentType, document.FileName));
    }

    private Task<bool> CanSeeClaimAsync(Guid claimId, CancellationToken cancellationToken) =>
        db.Claims.VisibleTo(currentUser).AnyAsync(c => c.Id == claimId, cancellationToken); // BR-09
}

namespace ClaimTrack.Api.Dtos.Documents;

// A document's details. Never the storage key: that's internal, and downloads go through the API.
public record DocumentResponse(
    Guid Id,
    Guid ClaimId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string UploadedByName);

namespace ClaimTrack.Api.Dtos.Documents;

// What the download endpoint sends back: the file's bytes plus how to label them.
public record DocumentDownload(Stream Content, string ContentType, string FileName);

namespace ClaimTrack.Api.Services;

// Where document bytes live. Only a storage key is kept in the database. Today it's a local
// folder (LocalFileStorage); moving to Azure Blob Storage would be one new class implementing this.
public interface IDocumentStorage
{
    // Saves the content under a new random key and returns that key.
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);

    // Only used to clean up a file whose database record couldn't be saved. Documents themselves
    // are never deleted (evidence, like claims and the audit log).
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

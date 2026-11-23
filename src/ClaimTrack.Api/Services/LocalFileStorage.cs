using System.Text.RegularExpressions;
using ClaimTrack.Api.Common;
using Microsoft.Extensions.Options;

namespace ClaimTrack.Api.Services;

// Stores documents as files in one folder, each named by a random GUID the server chooses.
// The uploader's file name never reaches the disk, so path tricks like "../../appsettings.json"
// can't write or read anywhere else.
public partial class LocalFileStorage(IOptions<DocumentOptions> options, IHostEnvironment environment) : IDocumentStorage
{
    // Combine returns StoragePath unchanged if it's already absolute (as in the container).
    private readonly string _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.StoragePath));

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var key = Guid.NewGuid().ToString("N");

        // CreateNew: never overwrite an existing file, even in the (practically impossible) case of a clash.
        await using var file = new FileStream(PathFor(key), FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new FileStream(PathFor(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    // Defence in depth: keys always come from our own database, but a key that isn't exactly
    // 32 hex characters is refused rather than turned into a path.
    private string PathFor(string key) =>
        KeyFormat().IsMatch(key)
            ? Path.Combine(_root, key)
            : throw new ArgumentException("Invalid storage key.", nameof(key));

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex KeyFormat();
}

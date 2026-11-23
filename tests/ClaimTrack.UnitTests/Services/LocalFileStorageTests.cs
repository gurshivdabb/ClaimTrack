using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ClaimTrack.UnitTests.Services;

// The real storage class against a throwaway folder (a real disk, no mocks), deleted afterwards.
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"claimtrack-unit-storage-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storage = new LocalFileStorage(
            Options.Create(new DocumentOptions { StoragePath = _folder }),
            new TestEnvironment());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task SaveThenOpen_ReturnsTheSameBytesUnderARandomKey()
    {
        var bytes = "%PDF-1.4 hello"u8.ToArray();

        var key = await _storage.SaveAsync(new MemoryStream(bytes), CancellationToken.None);
        await using var stored = await _storage.OpenReadAsync(key, CancellationToken.None);
        using var copy = new MemoryStream();
        await stored.CopyToAsync(copy);

        copy.ToArray().Should().Equal(bytes);
        key.Should().MatchRegex("^[0-9a-f]{32}$");
        File.Exists(Path.Combine(_folder, key)).Should().BeTrue("the file is named by its key and nothing else");
    }

    [Fact]
    public async Task Delete_RemovesTheFile()
    {
        var key = await _storage.SaveAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        await _storage.DeleteAsync(key, CancellationToken.None);

        File.Exists(Path.Combine(_folder, key)).Should().BeFalse();
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData(@"..\..\secrets")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")] // upper case isn't a key we generate
    [InlineData("")]
    public async Task AnyKeyThatIsNotARandomKey_IsRefused(string key)
    {
        var open = () => _storage.OpenReadAsync(key, CancellationToken.None);
        var delete = () => _storage.DeleteAsync(key, CancellationToken.None);

        await open.Should().ThrowAsync<ArgumentException>();
        await delete.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ClaimTrack.Api";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

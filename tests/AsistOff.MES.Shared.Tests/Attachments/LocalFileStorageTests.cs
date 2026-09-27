using AsistOff.MES.Attachments.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace AsistOff.MES.Shared.Tests.Attachments;

/// <summary>
/// Root-resolution and traversal-guard tests for <c>LocalFileStorage</c>
/// (issue #373): a relative RootPath resolves under the host content root
/// regardless of the process working directory, absolute paths pass through,
/// and crafted storage keys containing <c>..</c> are still rejected.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    [Fact]
    public void ResolveRootPath_RelativePath_ResolvesUnderContentRoot()
    {
        // Arrange
        var contentRoot = NewTempDir();

        // Act
        var resolved = LocalFileStorageOptions.ResolveRootPath("App_Data/attachments", contentRoot);

        // Assert
        resolved.Should().Be(Path.GetFullPath(Path.Combine(contentRoot, "App_Data/attachments")));
    }

    [Fact]
    public void ResolveRootPath_AbsolutePath_PassesThroughUnchanged()
    {
        // Arrange
        var absolute = NewTempDir();
        var contentRoot = NewTempDir();

        // Act
        var resolved = LocalFileStorageOptions.ResolveRootPath(absolute, contentRoot);

        // Assert
        resolved.Should().Be(Path.GetFullPath(absolute));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveRootPath_BlankPath_FallsBackToDefaultUnderContentRoot(string? rootPath)
    {
        // Arrange
        var contentRoot = NewTempDir();

        // Act
        var resolved = LocalFileStorageOptions.ResolveRootPath(rootPath, contentRoot);

        // Assert
        resolved.Should().Be(Path.GetFullPath(Path.Combine(contentRoot, "App_Data/attachments")));
    }

    [Fact]
    public async Task Save_RelativeRoot_StoresBlobUnderContentRootIndependentOfCwd()
    {
        // Arrange - content root is a temp dir that is never the process CWD,
        // so a blob landing there proves content-root (not CWD) resolution.
        var contentRoot = NewTempDir();
        var storage = CreateStorage("App_Data/attachments", contentRoot);
        var payload = "%PDF-1.7 durable"u8.ToArray();

        // Act
        string key;
        await using (var content = new MemoryStream(payload))
            key = await storage.SaveAsync(content, "application/pdf", "control-chart.pdf");

        // Assert - the key is relative to the resolved blob root
        // (<content-root>/App_Data/attachments), not to the CWD.
        var resolvedRoot = LocalFileStorageOptions.ResolveRootPath("App_Data/attachments", contentRoot);
        var underContentRoot = Path.Combine(resolvedRoot, key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(underContentRoot).Should().BeTrue();

        var underCwd = Path.Combine(
            Directory.GetCurrentDirectory(), "App_Data", "attachments",
            key.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(underCwd).Should().BeFalse();
    }

    [Fact]
    public async Task Save_AbsoluteRoot_StoresBlobAtAbsolutePath()
    {
        // Arrange
        var absolute = NewTempDir();
        var storage = CreateStorage(absolute, NewTempDir());
        var payload = "%PDF-1.7 durable"u8.ToArray();

        // Act
        string key;
        await using (var content = new MemoryStream(payload))
            key = await storage.SaveAsync(content, "application/pdf", "certificate.pdf");

        // Assert
        File.Exists(Path.Combine(absolute, key.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
    }

    [Fact]
    public async Task SaveOpenRead_RoundTrip_ReturnsByteIdenticalContent()
    {
        // Arrange
        var storage = CreateStorage("App_Data/attachments", NewTempDir());
        var payload = "%PDF-1.7 durable"u8.ToArray();

        // Act
        string key;
        await using (var content = new MemoryStream(payload))
            key = await storage.SaveAsync(content, "application/pdf", "evidence.pdf");
        byte[] roundTripped;
        await using (var read = await storage.OpenReadAsync(key))
        await using (var buffer = new MemoryStream())
        {
            await read.CopyToAsync(buffer);
            roundTripped = buffer.ToArray();
        }

        // Assert
        roundTripped.Should().Equal(payload);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("ab/../../evil.txt")]
    public async Task OpenRead_TraversalKeys_AreRejected(string storageKey)
    {
        // Arrange
        var storage = CreateStorage("App_Data/attachments", NewTempDir());

        // Act
        Func<Task<Stream>> act = () => storage.OpenReadAsync(storageKey);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("ab/../../evil.txt")]
    public async Task Delete_TraversalKeys_AreRejected(string storageKey)
    {
        // Arrange
        var storage = CreateStorage("App_Data/attachments", NewTempDir());

        // Act
        Func<Task> act = () => storage.DeleteAsync(storageKey);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: temp cleanup must never fail the test run.
            }
            catch (UnauthorizedAccessException)
            {
                // Best effort: temp cleanup must never fail the test run.
            }
        }
    }

    private static LocalFileStorage CreateStorage(string rootPath, string contentRoot)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);

        return new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = rootPath }),
            environment.Object);
    }

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mes-att-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }
}

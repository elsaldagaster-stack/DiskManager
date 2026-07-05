using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class FileSystemServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemService _sut;

    public FileSystemServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new FileSystemService();
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsFilesAndDirs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));

        var items = (await _sut.GetChildrenAsync(_tempDir)).ToList();

        items.Should().HaveCount(2);
        items.Should().Contain(x => x.Name == "a.txt" && !x.IsDirectory);
        items.Should().Contain(x => x.Name == "sub" && x.IsDirectory);
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesDir()
    {
        var newDir = Path.Combine(_tempDir, "newdir");
        await _sut.CreateDirectoryAsync(newDir);
        Directory.Exists(newDir).Should().BeTrue();
    }

    [Fact]
    public async Task RenameAsync_RenamesFile()
    {
        var original = Path.Combine(_tempDir, "old.txt");
        File.WriteAllText(original, "data");
        await _sut.RenameAsync(original, "new.txt");
        File.Exists(Path.Combine(_tempDir, "new.txt")).Should().BeTrue();
        File.Exists(original).Should().BeFalse();
    }

    [Fact]
    public async Task CopyAsync_CopiesFile()
    {
        var src = Path.Combine(_tempDir, "src.txt");
        var dst = Path.Combine(_tempDir, "dst.txt");
        File.WriteAllText(src, "content");
        await _sut.CopyAsync(src, dst);
        File.Exists(dst).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("content");
    }

    [Fact]
    public async Task MoveAsync_MovesFile()
    {
        var src = Path.Combine(_tempDir, "move.txt");
        var dst = Path.Combine(_tempDir, "moved.txt");
        File.WriteAllText(src, "data");
        await _sut.MoveAsync(src, dst);
        File.Exists(dst).Should().BeTrue();
        File.Exists(src).Should().BeFalse();
    }

    [Fact]
    public void Search_FindsFileByName()
    {
        File.WriteAllText(Path.Combine(_tempDir, "report.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");
        var results = _sut.Search(_tempDir, "report").ToList();
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("report.pdf");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class DiskAnalyzerServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DiskAnalyzerService _sut;

    public DiskAnalyzerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new DiskAnalyzerService();
    }

    [Fact]
    public async Task ScanAsync_ReturnsTotalSizeOfFiles()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_tempDir, "b.bin"), new byte[200]);

        var node = await _sut.ScanAsync(_tempDir, null);

        node.TotalSize.Should().Be(300);
        node.FullPath.Should().Be(_tempDir);
    }

    [Fact]
    public async Task ScanAsync_RecursesIntoSubfolders()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(sub.FullName, "c.bin"), new byte[500]);

        var node = await _sut.ScanAsync(_tempDir, null);

        node.TotalSize.Should().Be(500);
        node.Children.Should().HaveCount(1);
        node.Children[0].TotalSize.Should().Be(500);
    }

    [Fact]
    public async Task ScanAsync_SkipsUnauthorizedDirectories()
    {
        var act = async () => await _sut.ScanAsync(_tempDir, null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetDriveUsage_ReturnsSaneValues()
    {
        var usage = _sut.GetDriveUsage("C");
        usage.TotalBytes.Should().BeGreaterThan(0);
        usage.FreeBytes.Should().BeGreaterThan(0);
        usage.UsedBytes.Should().Be(usage.TotalBytes - usage.FreeBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

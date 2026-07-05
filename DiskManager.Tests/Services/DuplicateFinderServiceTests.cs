using DiskManager.Models;
using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class DuplicateFinderServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DuplicateFinderService _sut;

    public DuplicateFinderServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new DuplicateFinderService();
    }

    [Fact]
    public async Task FindAsync_MD5_FindsIdenticalFiles()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "a.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_tempDir, "b.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_tempDir, "c.bin"), new byte[] { 9, 9, 9 });

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups.Should().HaveCount(1);
        groups[0].Paths.Should().HaveCount(2);
        groups[0].FileSize.Should().Be(3);
    }

    [Fact]
    public async Task FindAsync_NameAndSize_GroupsByNameAndSize()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "same.txt"), new byte[10]);
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(sub.FullName, "same.txt"), new byte[10]);

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.NameAndSize, null)).ToList();

        groups.Should().HaveCount(1);
        groups[0].Paths.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_NoDuplicates_ReturnsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "unique1.bin"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_tempDir, "unique2.bin"), new byte[] { 2 });

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAsync_WastedBytes_CalculatedCorrectly()
    {
        var content = new byte[50];
        File.WriteAllBytes(Path.Combine(_tempDir, "x.bin"), content);
        File.WriteAllBytes(Path.Combine(_tempDir, "y.bin"), content);
        File.WriteAllBytes(Path.Combine(_tempDir, "z.bin"), content);

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups[0].WastedBytes.Should().Be(100); // 50 × (3 - 1)
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}

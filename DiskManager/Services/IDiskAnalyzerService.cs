using DiskManager.Models;

namespace DiskManager.Services;

public record DriveUsage(string Letter, long TotalBytes, long UsedBytes, long FreeBytes);

public interface IDiskAnalyzerService
{
    Task<FolderNode> ScanAsync(string rootPath, IProgress<string>? progress, CancellationToken ct = default);
    DriveUsage GetDriveUsage(string driveLetter);
}

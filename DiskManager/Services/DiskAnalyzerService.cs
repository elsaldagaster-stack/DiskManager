using DiskManager.Models;
using System.IO;

namespace DiskManager.Services;

public class DiskAnalyzerService : IDiskAnalyzerService
{
    // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS — set by OneDrive, Google Drive, etc. on cloud-only files
    private const int RecallOnDataAccess = 0x00400000;
    // Option B (future): expose IncludeCloudFiles toggle on ScanAsync to count cloud sizes too
    private static bool IsCloudOnly(FileAttributes attrs) => ((int)attrs & RecallOnDataAccess) != 0;

    public async Task<FolderNode> ScanAsync(string rootPath, IProgress<string>? progress, CancellationToken ct = default)
    {
        return await Task.Run(() => ScanDirectory(rootPath, progress, ct), ct);
    }

    private static FolderNode ScanDirectory(string path, IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(path);

        var node = new FolderNode
        {
            Name = Path.GetFileName(path),
            FullPath = path
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    if (IsCloudOnly(fi.Attributes)) { node.CloudOnlyFiles++; continue; }
                    node.TotalSize += fi.Length;
                }
                catch (IOException) { }
            }

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var child = ScanDirectory(dir, progress, ct);
                    node.TotalSize += child.TotalSize;
                    node.SkippedFolders += child.SkippedFolders;
                    node.CloudOnlyFiles += child.CloudOnlyFiles;
                    node.Children.Add(child);
                }
                catch (UnauthorizedAccessException) { node.SkippedFolders++; }
            }
        }
        catch (UnauthorizedAccessException) { node.SkippedFolders++; }

        return node;
    }

    public DriveUsage GetDriveUsage(string driveLetter)
    {
        var drive = new DriveInfo(driveLetter);
        return new DriveUsage(
            driveLetter,
            drive.TotalSize,
            drive.TotalSize - drive.TotalFreeSpace,
            drive.TotalFreeSpace);
    }
}

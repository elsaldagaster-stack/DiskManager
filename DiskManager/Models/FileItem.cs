namespace DiskManager.Models;

public record FileItem(
    string Name,
    string FullPath,
    long Size,
    DateTime Modified,
    bool IsDirectory
);

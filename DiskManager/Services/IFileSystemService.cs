using DiskManager.Models;

namespace DiskManager.Services;

public interface IFileSystemService
{
    Task<IEnumerable<FileItem>> GetChildrenAsync(string path, CancellationToken ct = default);
    Task CopyAsync(string source, string destination, CancellationToken ct = default);
    Task MoveAsync(string source, string destination, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task CreateDirectoryAsync(string path, CancellationToken ct = default);
    Task RenameAsync(string path, string newName, CancellationToken ct = default);
    IEnumerable<FileItem> Search(string folder, string query);
}

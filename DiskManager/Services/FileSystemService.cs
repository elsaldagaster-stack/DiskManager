using DiskManager.Models;
using System.IO;

namespace DiskManager.Services;

public class FileSystemService : IFileSystemService
{
    public Task<IEnumerable<FileItem>> GetChildrenAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var items = new List<FileItem>();
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var info = new DirectoryInfo(dir);
                    items.Add(new FileItem(info.Name, info.FullName, 0, info.LastWriteTime, true));
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    items.Add(new FileItem(info.Name, info.FullName, info.Length, info.LastWriteTime, false));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return (IEnumerable<FileItem>)items;
        }, ct);
    }

    public Task CopyAsync(string source, string destination, CancellationToken ct = default)
        => Task.Run(() => File.Copy(source, destination, overwrite: false), ct);

    public Task MoveAsync(string source, string destination, CancellationToken ct = default)
        => Task.Run(() => File.Move(source, destination), ct);

    public Task DeleteAsync(string path, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }, ct);

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default)
        => Task.Run(() => Directory.CreateDirectory(path), ct);

    public Task RenameAsync(string path, string newName, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var parent = Path.GetDirectoryName(path)!;
            var dest = Path.Combine(parent, newName);
            if (File.Exists(path)) File.Move(path, dest);
            else Directory.Move(path, dest);
        }, ct);

    public IEnumerable<FileItem> Search(string folder, string query)
    {
        try
        {
            return Directory.EnumerateFiles(folder, $"*{query}*", SearchOption.TopDirectoryOnly)
                .Select(f =>
                {
                    var info = new FileInfo(f);
                    return new FileItem(info.Name, info.FullName, info.Length, info.LastWriteTime, false);
                })
                .ToList();
        }
        catch (UnauthorizedAccessException) { return Enumerable.Empty<FileItem>(); }
        catch (IOException) { return Enumerable.Empty<FileItem>(); }
    }
}

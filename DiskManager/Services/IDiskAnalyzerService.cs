using DiskManager.Models;

namespace DiskManager.Services;

public interface IDiskAnalyzerService
{
    Task<FolderNode> AnalyzeAsync(string path, CancellationToken ct = default);
}

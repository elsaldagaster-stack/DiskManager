using DiskManager.Models;

namespace DiskManager.Services;

public class DiskAnalyzerService : IDiskAnalyzerService
{
    public Task<FolderNode> AnalyzeAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException("Stub — implemented in Task 5");
}

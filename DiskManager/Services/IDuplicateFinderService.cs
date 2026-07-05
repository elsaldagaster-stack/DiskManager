using DiskManager.Models;

namespace DiskManager.Services;

public interface IDuplicateFinderService
{
    // Option B (future): add bool IncludeCloudFiles to FindAsync to optionally scan cloud-only files
    int LastCloudOnlySkipped { get; }

    Task<IEnumerable<DuplicateGroup>> FindAsync(
        string rootPath,
        DuplicateMethod method,
        IProgress<int>? progress,
        CancellationToken ct = default);
}

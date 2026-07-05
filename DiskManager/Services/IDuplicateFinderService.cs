using DiskManager.Models;

namespace DiskManager.Services;

public interface IDuplicateFinderService
{
    Task<IEnumerable<DuplicateGroup>> FindAsync(
        string rootPath,
        DuplicateMethod method,
        IProgress<int>? progress,
        CancellationToken ct = default);
}

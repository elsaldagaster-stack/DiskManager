using DiskManager.Models;

namespace DiskManager.Services;

public interface IDuplicateFinderService
{
    IAsyncEnumerable<DuplicateGroup> FindDuplicatesAsync(string path, CancellationToken ct = default);
}

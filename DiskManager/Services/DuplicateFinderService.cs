using DiskManager.Models;

namespace DiskManager.Services;

public class DuplicateFinderService : IDuplicateFinderService
{
    public IAsyncEnumerable<DuplicateGroup> FindDuplicatesAsync(string path, CancellationToken ct = default)
        => throw new NotImplementedException("Stub — implemented in Task 6");
}

namespace DiskManager.Models;

public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public List<string> Paths { get; set; } = new();
    public long WastedBytes => FileSize * (Paths.Count - 1);
}

namespace DiskManager.Models;

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public List<FolderNode> Children { get; set; } = new();
    public int SkippedFolders { get; set; }
    public int CloudOnlyFiles { get; set; }
}

using DiskManager.Models;
using System.IO;
using System.Security.Cryptography;

namespace DiskManager.Services;

public class DuplicateFinderService : IDuplicateFinderService
{
    private const int RecallOnDataAccess = 0x00400000;
    private static bool IsCloudOnly(FileAttributes attrs) => ((int)attrs & RecallOnDataAccess) != 0;

    public int LastCloudOnlySkipped { get; private set; }

    public async Task<IEnumerable<DuplicateGroup>> FindAsync(
        string rootPath,
        DuplicateMethod method,
        IProgress<int>? progress,
        CancellationToken ct = default)
    {
        var (groups, cloudSkipped) = await Task.Run(() => Find(rootPath, method, progress, ct), ct);
        LastCloudOnlySkipped = cloudSkipped;
        return groups;
    }

    private static (IEnumerable<DuplicateGroup> Groups, int CloudSkipped) Find(
        string rootPath, DuplicateMethod method,
        IProgress<int>? progress, CancellationToken ct)
    {
        int cloudSkipped = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true
        };
        var files = Directory.EnumerateFiles(rootPath, "*", options)
            .Where(f =>
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (IsCloudOnly(fi.Attributes)) { cloudSkipped++; return false; }
                    return fi.Length > 0;
                }
                catch { return false; }
            })
            .ToList();

        var grouped = method switch
        {
            DuplicateMethod.HashMD5     => GroupByHash(files, progress, ct),
            DuplicateMethod.NameAndSize => GroupByNameAndSize(files),
            DuplicateMethod.SizeOnly    => GroupBySize(files),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        return (grouped.Where(g => g.Paths.Count > 1), cloudSkipped);
    }

    private static IEnumerable<DuplicateGroup> GroupByHash(
        List<string> files, IProgress<int>? progress, CancellationToken ct)
    {
        var dict = new Dictionary<string, DuplicateGroup>();
        int count = 0;
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var hash = ComputeMd5(file);
                var size = new FileInfo(file).Length;
                if (!dict.TryGetValue(hash, out var group))
                {
                    group = new DuplicateGroup { Hash = hash, FileSize = size };
                    dict[hash] = group;
                }
                group.Paths.Add(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            progress?.Report(++count);
        }
        return dict.Values;
    }

    private static IEnumerable<DuplicateGroup> GroupByNameAndSize(List<string> files)
        => files
            .GroupBy(f => { var i = new FileInfo(f); return $"{i.Name}|{i.Length}"; })
            .Select(g => new DuplicateGroup
            {
                Hash = g.Key,
                FileSize = new FileInfo(g.First()).Length,
                Paths = g.ToList()
            });

    private static IEnumerable<DuplicateGroup> GroupBySize(List<string> files)
        => files
            .GroupBy(f => new FileInfo(f).Length)
            .Select(g => new DuplicateGroup
            {
                Hash = g.Key.ToString(),
                FileSize = g.Key,
                Paths = g.ToList()
            });

    private static string ComputeMd5(string path)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(stream));
    }
}

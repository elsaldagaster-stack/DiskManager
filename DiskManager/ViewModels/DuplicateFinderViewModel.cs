using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using Microsoft.VisualBasic.FileIO;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DiskManager.ViewModels;

public partial class DuplicateFinderViewModel : ObservableObject, IDisposable
{
    private readonly IDuplicateFinderService _finder;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    [ObservableProperty] private DuplicateMethod _selectedMethod = DuplicateMethod.HashMD5;
    [ObservableProperty] private ObservableCollection<DuplicateGroupViewModel> _groups = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSearching))]
    private bool _isSearching;
    public bool IsNotSearching => !IsSearching;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _statusText = string.Empty;

    public DuplicateMethod[] Methods => Enum.GetValues<DuplicateMethod>();

    public DuplicateFinderViewModel(IDuplicateFinderService finder)
    {
        _finder = finder;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    [RelayCommand]
    private void ChooseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = RootPath
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            RootPath = dialog.SelectedPath;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsSearching = true;
        ProgressValue = 0;
        Groups.Clear();
        StatusText = "Buscando...";

        var progress = new Progress<int>(v => ProgressValue = v);
        try
        {
            var results = await _finder.FindAsync(RootPath, SelectedMethod, progress, _cts.Token);
            foreach (var group in results.OrderByDescending(g => g.WastedBytes))
                Groups.Add(new DuplicateGroupViewModel(group));
            var cloudNote = _finder.LastCloudOnlySkipped > 0
                ? $" · {_finder.LastCloudOnlySkipped} archivo(s) solo en nube omitidos"
                : string.Empty;
            StatusText = Groups.Count == 0
                ? $"No se encontraron duplicados.{cloudNote}"
                : $"{Groups.Count} grupos · {FormatSize(Groups.Sum(g => g.Group.WastedBytes))} recuperables{cloudNote}";
        }
        catch (OperationCanceledException) { StatusText = "Búsqueda cancelada."; }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private void StopSearch() => _cts?.Cancel();

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var toDelete = Groups
            .SelectMany(g => g.SelectedPaths)
            .ToList();

        if (toDelete.Count == 0) { StatusText = "Ningún archivo seleccionado."; return; }

        var totalSize = toDelete.Sum(p => { try { return new FileInfo(p).Length; } catch { return 0L; } });
        var msg = $"¿Enviar {toDelete.Count} archivo(s) ({FormatSize(totalSize)}) a la Papelera?";
        if (MessageBox.Show(msg, "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        int deleted = 0;
        var errors = new List<string>();
        foreach (var path in toDelete)
        {
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                deleted++;
            }
            catch (Exception ex) { errors.Add($"{System.IO.Path.GetFileName(path)}: {ex.Message}"); }
        }

        StatusText = errors.Count == 0
            ? $"{deleted} archivo(s) enviados a la Papelera."
            : $"{deleted} eliminados, {errors.Count} errores: {errors[0]}";

        await SearchAsync();
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public partial class DuplicateGroupViewModel : ObservableObject
{
    public DuplicateGroup Group { get; }
    public ObservableCollection<DuplicatePathItem> PathItems { get; }

    public IEnumerable<string> SelectedPaths => PathItems.Where(p => p.IsSelected).Select(p => p.Path);

    public DuplicateGroupViewModel(DuplicateGroup group)
    {
        Group = group;
        var ordered = group.Paths
            .Select(p => new { Path = p, Modified = TryGetDate(p) })
            .OrderBy(x => x.Modified)
            .Select((x, i) => new DuplicatePathItem(x.Path, i > 0))
            .ToList();
        PathItems = new ObservableCollection<DuplicatePathItem>(ordered);
    }

    private static DateTime TryGetDate(string path)
    {
        try { return File.GetCreationTime(path); }
        catch { return DateTime.MaxValue; }
    }
}

public partial class DuplicatePathItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public string Path { get; }

    public DuplicatePathItem(string path, bool isSelected)
    {
        Path = path;
        _isSelected = isSelected;
    }
}

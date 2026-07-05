using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace DiskManager.ViewModels;

public partial class DiskAnalyzerViewModel : ObservableObject, IDisposable
{
    private readonly IDiskAnalyzerService _analyzer;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _selectedDrive = "C";
    [ObservableProperty] private FolderNode? _rootNode;
    [ObservableProperty] private ObservableCollection<FolderNode> _topFolders = new();
    [ObservableProperty] private DriveUsage? _driveUsage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotScanning))]
    private bool _isScanning;
    public bool IsNotScanning => !IsScanning;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _availableDrives = new();

    public DiskAnalyzerViewModel(IDiskAnalyzerService analyzer)
    {
        _analyzer = analyzer;
        LoadDrives();
    }

    private void LoadDrives()
    {
        foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
            AvailableDrives.Add(d.Name.TrimEnd('\\', '/'));
        if (AvailableDrives.Any()) SelectedDrive = AvailableDrives[0];
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsScanning = true;
        ProgressText = "Escaneando...";

        var progress = new Progress<string>(p =>
            ProgressText = $"Escaneando: {Path.GetFileName(p)}");

        try
        {
            var driveLetter = SelectedDrive.TrimEnd('\\', '/', ':');
            var rootPath = driveLetter + ":\\";
            DriveUsage = _analyzer.GetDriveUsage(driveLetter);
            RootNode = await _analyzer.ScanAsync(rootPath, progress, _cts.Token);
            TopFolders = new ObservableCollection<FolderNode>(
                RootNode.Children.OrderByDescending(c => c.TotalSize).Take(20));
            var parts = new System.Collections.Generic.List<string>();
            if (RootNode.SkippedFolders > 0)
                parts.Add($"{RootNode.SkippedFolders} carpeta(s) inaccesible(s)");
            if (RootNode.CloudOnlyFiles > 0)
                parts.Add($"{RootNode.CloudOnlyFiles} archivo(s) solo en nube");
            ProgressText = parts.Count > 0
                ? $"Listo. Omitidos: {string.Join(", ", parts)}."
                : "Listo.";
        }
        catch (OperationCanceledException) { ProgressText = "Cancelado."; }
        catch (Exception ex) { ProgressText = $"Error: {ex.Message}"; }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    private void StopScan() => _cts?.Cancel();

    [RelayCommand]
    private void OpenInExplorer(FolderNode? node)
    {
        if (node is null) return;
        try { System.Diagnostics.Process.Start("explorer.exe", node.FullPath); }
        catch (Exception ex) { ProgressText = $"Error: {ex.Message}"; }
    }
}

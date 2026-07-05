using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DiskManager.ViewModels;

public partial class ExplorerViewModel : ObservableObject
{
    private readonly IFileSystemService _fs;
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    [ObservableProperty] private string _currentPath = string.Empty;
    [ObservableProperty] private ObservableCollection<FileItem> _items = new();
    [ObservableProperty] private FileItem? _selectedItem;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ExplorerViewModel(IFileSystemService fs)
    {
        _fs = fs;
        _ = NavigateToAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    [RelayCommand]
    private async Task NavigateToAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        if (!string.IsNullOrEmpty(CurrentPath)) _backStack.Push(CurrentPath);
        _forwardStack.Clear();
        await LoadPathAsync(path);
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task GoBackAsync()
    {
        if (_backStack.Count == 0) return;
        _forwardStack.Push(CurrentPath);
        await LoadPathAsync(_backStack.Pop());
    }

    [RelayCommand(CanExecute = nameof(CanGoForward))]
    private async Task GoForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        _backStack.Push(CurrentPath);
        await LoadPathAsync(_forwardStack.Pop());
    }

    [RelayCommand]
    private async Task GoUpAsync()
    {
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        if (parent is not null) await NavigateToAsync(parent);
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileItem? item)
    {
        if (item is null) return;
        if (item.IsDirectory) await NavigateToAsync(item.FullPath);
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (SelectedItem is null) return;
        var result = MessageBox.Show(
            $"¿Eliminar '{SelectedItem.Name}'?",
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try
        {
            await _fs.DeleteAsync(SelectedItem.FullPath);
            Items.Remove(SelectedItem);
            StatusText = "Elemento eliminado.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        var name = $"Nueva carpeta {DateTime.Now:HHmmss}";
        var path = Path.Combine(CurrentPath, name);
        try
        {
            await _fs.CreateDirectoryAsync(path);
            await RefreshAsync();
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private void Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) { _ = RefreshAsync(); return; }
        var results = _fs.Search(CurrentPath, SearchQuery).ToList();
        Items = new ObservableCollection<FileItem>(results);
        StatusText = $"{results.Count} resultado(s) para '{SearchQuery}'";
    }

    private bool CanGoBack() => _backStack.Count > 0;
    private bool CanGoForward() => _forwardStack.Count > 0;

    private async Task LoadPathAsync(string path)
    {
        IsBusy = true;
        try
        {
            CurrentPath = path;
            var children = await _fs.GetChildrenAsync(path);
            Items = new ObservableCollection<FileItem>(
                children.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name));
            StatusText = $"{Items.Count} elementos";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private Task RefreshAsync() => LoadPathAsync(CurrentPath);
}

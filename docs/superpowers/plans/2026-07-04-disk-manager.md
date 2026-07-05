# DiskManager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF desktop suite with file explorer, disk space analyzer, and duplicate file finder, all in one tabbed window.

**Architecture:** MVVM with service layer injected via `Microsoft.Extensions.Hosting`. Each module (Explorer, DiskAnalyzer, DuplicateFinder) has its own View/ViewModel/Service trio. All I/O is async with `CancellationToken`. Tests target the service layer with xUnit.

**Tech Stack:** C# 12 · .NET 8 · WPF · CommunityToolkit.Mvvm · Microsoft.Extensions.Hosting · xUnit · NSubstitute

---

## File Map

| File | Responsibility |
|---|---|
| `DiskManager/App.xaml.cs` | DI host bootstrap, theme init |
| `DiskManager/MainWindow.xaml` | TabControl shell |
| `DiskManager/Models/FileItem.cs` | Record for file/dir in explorer |
| `DiskManager/Models/FolderNode.cs` | Tree node for disk analyzer |
| `DiskManager/Models/DuplicateGroup.cs` | Group of identical files |
| `DiskManager/Models/DuplicateMethod.cs` | Enum: HashMD5, NameAndSize, SizeOnly |
| `DiskManager/Services/IFileSystemService.cs` | Interface: browse, copy, move, delete, search |
| `DiskManager/Services/FileSystemService.cs` | Implementation via System.IO |
| `DiskManager/Services/IDiskAnalyzerService.cs` | Interface: scan, drive usage |
| `DiskManager/Services/DiskAnalyzerService.cs` | Recursive size scan |
| `DiskManager/Services/IDuplicateFinderService.cs` | Interface: find duplicates |
| `DiskManager/Services/DuplicateFinderService.cs` | MD5/name+size/size scan |
| `DiskManager/Services/IThemeService.cs` | Interface: current theme, event |
| `DiskManager/Services/ThemeService.cs` | Reads registry, fires on change |
| `DiskManager/ViewModels/ExplorerViewModel.cs` | Navigation state, file commands |
| `DiskManager/ViewModels/DiskAnalyzerViewModel.cs` | Scan state, progress, tree |
| `DiskManager/ViewModels/DuplicateFinderViewModel.cs` | Search state, selection, delete |
| `DiskManager/Views/ExplorerView.xaml` | TreeView + ListView layout |
| `DiskManager/Views/DiskAnalyzerView.xaml` | TreeMap + folder list |
| `DiskManager/Views/DuplicateFinderView.xaml` | Groups list + action bar |
| `DiskManager/Controls/TreeMapPanel.cs` | Custom WPF Panel: squarified treemap |
| `DiskManager/Converters/FileSizeConverter.cs` | long bytes → "4.2 MB" |
| `DiskManager/Converters/BoolToVisibilityConverter.cs` | bool → Visibility |
| `DiskManager/Themes/Dark.xaml` | Dark ResourceDictionary |
| `DiskManager/Themes/Light.xaml` | Light ResourceDictionary |
| `DiskManager.Tests/Services/FileSystemServiceTests.cs` | xUnit tests for FileSystemService |
| `DiskManager.Tests/Services/DiskAnalyzerServiceTests.cs` | xUnit tests for DiskAnalyzerService |
| `DiskManager.Tests/Services/DuplicateFinderServiceTests.cs` | xUnit tests for DuplicateFinderService |

---

## Task 1: Project scaffold + NuGet + DI bootstrap

**Files:**
- Create: `DiskManager/DiskManager.csproj`
- Create: `DiskManager/App.xaml`
- Create: `DiskManager/App.xaml.cs`
- Create: `DiskManager/MainWindow.xaml`
- Create: `DiskManager/MainWindow.xaml.cs`
- Create: `DiskManager.Tests/DiskManager.Tests.csproj`
- Create: `DiskManager.sln`

- [ ] **Step 1: Create solution and projects**

```bash
dotnet new sln -n DiskManager
dotnet new wpf -n DiskManager -f net8.0-windows
dotnet new xunit -n DiskManager.Tests -f net8.0
dotnet sln add DiskManager/DiskManager.csproj
dotnet sln add DiskManager.Tests/DiskManager.Tests.csproj
dotnet add DiskManager.Tests/DiskManager.Tests.csproj reference DiskManager/DiskManager.csproj
```

- [ ] **Step 2: Add NuGet packages**

```bash
dotnet add DiskManager/DiskManager.csproj package CommunityToolkit.Mvvm
dotnet add DiskManager/DiskManager.csproj package Microsoft.Extensions.Hosting
dotnet add DiskManager.Tests/DiskManager.Tests.csproj package NSubstitute
dotnet add DiskManager.Tests/DiskManager.Tests.csproj package FluentAssertions
```

- [ ] **Step 3: Edit `DiskManager/DiskManager.csproj` — enable Windows APIs and nullable**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Replace `App.xaml` — remove StartupUri (DI will open MainWindow)**

```xml
<Application x:Class="DiskManager.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Themes/Light.xaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

- [ ] **Step 5: Write `App.xaml.cs` — DI host bootstrap**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using DiskManager.Services;
using DiskManager.ViewModels;

namespace DiskManager;

public partial class App : Application
{
    private IHost _host = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IFileSystemService, FileSystemService>();
                services.AddSingleton<IDiskAnalyzerService, DiskAnalyzerService>();
                services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();

                services.AddTransient<ExplorerViewModel>();
                services.AddTransient<DiskAnalyzerViewModel>();
                services.AddTransient<DuplicateFinderViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var themeService = _host.Services.GetRequiredService<IThemeService>();
        themeService.Apply(Resources);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 6: Write `MainWindow.xaml` — TabControl shell**

```xml
<Window x:Class="DiskManager.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:DiskManager.Views"
        Title="DiskManager" Height="650" Width="1100"
        Background="{DynamicResource AppBackground}">
  <TabControl>
    <TabItem Header="📁 Explorador">
      <views:ExplorerView/>
    </TabItem>
    <TabItem Header="📊 Analizador de disco">
      <views:DiskAnalyzerView/>
    </TabItem>
    <TabItem Header="🔍 Duplicados">
      <views:DuplicateFinderView/>
    </TabItem>
  </TabControl>
</Window>
```

- [ ] **Step 7: Write `MainWindow.xaml.cs`**

```csharp
namespace DiskManager;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow() => InitializeComponent();
}
```

- [ ] **Step 8: Verify solution builds (stubs for missing types will cause errors — expected)**

```bash
dotnet build DiskManager.sln
```

Expected: errors about missing types (Views, Services, ViewModels not yet created). That is fine — scaffold is done.

- [ ] **Step 9: Commit**

```bash
git init
git add .
git commit -m "feat: scaffold WPF solution with DI host bootstrap"
```

---

## Task 2: Models and enums

**Files:**
- Create: `DiskManager/Models/FileItem.cs`
- Create: `DiskManager/Models/FolderNode.cs`
- Create: `DiskManager/Models/DuplicateGroup.cs`
- Create: `DiskManager/Models/DuplicateMethod.cs`

- [ ] **Step 1: Create `Models/FileItem.cs`**

```csharp
namespace DiskManager.Models;

public record FileItem(
    string Name,
    string FullPath,
    long Size,
    DateTime Modified,
    bool IsDirectory
);
```

- [ ] **Step 2: Create `Models/FolderNode.cs`**

```csharp
namespace DiskManager.Models;

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public List<FolderNode> Children { get; set; } = new();
}
```

- [ ] **Step 3: Create `Models/DuplicateGroup.cs`**

```csharp
namespace DiskManager.Models;

public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public List<string> Paths { get; set; } = new();
    public long WastedBytes => FileSize * (Paths.Count - 1);
}
```

- [ ] **Step 4: Create `Models/DuplicateMethod.cs`**

```csharp
namespace DiskManager.Models;

public enum DuplicateMethod
{
    HashMD5,
    NameAndSize,
    SizeOnly
}
```

- [ ] **Step 5: Commit**

```bash
git add DiskManager/Models/
git commit -m "feat: add FileItem, FolderNode, DuplicateGroup, DuplicateMethod models"
```

---

## Task 3: Theme service + XAML resource dictionaries

**Files:**
- Create: `DiskManager/Services/IThemeService.cs`
- Create: `DiskManager/Services/ThemeService.cs`
- Create: `DiskManager/Themes/Dark.xaml`
- Create: `DiskManager/Themes/Light.xaml`

- [ ] **Step 1: Create `Services/IThemeService.cs`**

```csharp
using System.Windows;

namespace DiskManager.Services;

public enum Theme { Light, Dark }

public interface IThemeService
{
    Theme CurrentTheme { get; }
    event EventHandler<Theme> ThemeChanged;
    void Apply(ResourceDictionary resources);
}
```

- [ ] **Step 2: Create `Services/ThemeService.cs`**

```csharp
using Microsoft.Win32;
using System.Windows;

namespace DiskManager.Services;

public class ThemeService : IThemeService, IDisposable
{
    public Theme CurrentTheme { get; private set; }
    public event EventHandler<Theme>? ThemeChanged;

    public ThemeService()
    {
        CurrentTheme = ReadWindowsTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(ResourceDictionary resources)
    {
        var uri = CurrentTheme == Theme.Dark
            ? new Uri("Themes/Dark.xaml", UriKind.Relative)
            : new Uri("Themes/Light.xaml", UriKind.Relative);

        var dict = resources.MergedDictionaries.FirstOrDefault();
        if (dict is not null)
            resources.MergedDictionaries.Remove(dict);

        resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = uri });
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        var newTheme = ReadWindowsTheme();
        if (newTheme == CurrentTheme) return;
        CurrentTheme = newTheme;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Apply(Application.Current.Resources);
            ThemeChanged?.Invoke(this, CurrentTheme);
        });
    }

    private static Theme ReadWindowsTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var value = key?.GetValue("AppsUseLightTheme");
        return value is int i && i == 0 ? Theme.Dark : Theme.Light;
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
}
```

- [ ] **Step 3: Create `Themes/Light.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="AppBackground" Color="#F3F3F3"/>
  <SolidColorBrush x:Key="PanelBackground" Color="#FFFFFF"/>
  <SolidColorBrush x:Key="SidebarBackground" Color="#E8E8E8"/>
  <SolidColorBrush x:Key="AccentBrush" Color="#0078D4"/>
  <SolidColorBrush x:Key="TextPrimary" Color="#1E1E1E"/>
  <SolidColorBrush x:Key="TextSecondary" Color="#5F6368"/>
  <SolidColorBrush x:Key="BorderBrush" Color="#CCCCCC"/>
  <SolidColorBrush x:Key="RowHover" Color="#E3F0FB"/>
  <SolidColorBrush x:Key="RowSelected" Color="#CCE4F7"/>
  <SolidColorBrush x:Key="DangerBrush" Color="#D32F2F"/>
  <SolidColorBrush x:Key="SuccessBrush" Color="#388E3C"/>
</ResourceDictionary>
```

- [ ] **Step 4: Create `Themes/Dark.xaml`**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="AppBackground" Color="#1E1E2E"/>
  <SolidColorBrush x:Key="PanelBackground" Color="#181825"/>
  <SolidColorBrush x:Key="SidebarBackground" Color="#313244"/>
  <SolidColorBrush x:Key="AccentBrush" Color="#89B4FA"/>
  <SolidColorBrush x:Key="TextPrimary" Color="#CDD6F4"/>
  <SolidColorBrush x:Key="TextSecondary" Color="#A6ADC8"/>
  <SolidColorBrush x:Key="BorderBrush" Color="#45475A"/>
  <SolidColorBrush x:Key="RowHover" Color="#313244"/>
  <SolidColorBrush x:Key="RowSelected" Color="#45475A"/>
  <SolidColorBrush x:Key="DangerBrush" Color="#F38BA8"/>
  <SolidColorBrush x:Key="SuccessBrush" Color="#A6E3A1"/>
</ResourceDictionary>
```

- [ ] **Step 5: Commit**

```bash
git add DiskManager/Services/IThemeService.cs DiskManager/Services/ThemeService.cs DiskManager/Themes/
git commit -m "feat: add ThemeService with auto Windows theme detection"
```

---

## Task 4: FileSystemService + tests

**Files:**
- Create: `DiskManager/Services/IFileSystemService.cs`
- Create: `DiskManager/Services/FileSystemService.cs`
- Create: `DiskManager.Tests/Services/FileSystemServiceTests.cs`

- [ ] **Step 1: Create `Services/IFileSystemService.cs`**

```csharp
using DiskManager.Models;

namespace DiskManager.Services;

public interface IFileSystemService
{
    Task<IEnumerable<FileItem>> GetChildrenAsync(string path, CancellationToken ct = default);
    Task CopyAsync(string source, string destination, CancellationToken ct = default);
    Task MoveAsync(string source, string destination, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task CreateDirectoryAsync(string path, CancellationToken ct = default);
    Task RenameAsync(string path, string newName, CancellationToken ct = default);
    IEnumerable<FileItem> Search(string folder, string query);
}
```

- [ ] **Step 2: Write failing tests in `DiskManager.Tests/Services/FileSystemServiceTests.cs`**

```csharp
using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class FileSystemServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemService _sut;

    public FileSystemServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new FileSystemService();
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsFilesAndDirs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));

        var items = (await _sut.GetChildrenAsync(_tempDir)).ToList();

        items.Should().HaveCount(2);
        items.Should().Contain(x => x.Name == "a.txt" && !x.IsDirectory);
        items.Should().Contain(x => x.Name == "sub" && x.IsDirectory);
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesDir()
    {
        var newDir = Path.Combine(_tempDir, "newdir");
        await _sut.CreateDirectoryAsync(newDir);
        Directory.Exists(newDir).Should().BeTrue();
    }

    [Fact]
    public async Task RenameAsync_RenamesFile()
    {
        var original = Path.Combine(_tempDir, "old.txt");
        File.WriteAllText(original, "data");
        await _sut.RenameAsync(original, "new.txt");
        File.Exists(Path.Combine(_tempDir, "new.txt")).Should().BeTrue();
        File.Exists(original).Should().BeFalse();
    }

    [Fact]
    public async Task CopyAsync_CopiesFile()
    {
        var src = Path.Combine(_tempDir, "src.txt");
        var dst = Path.Combine(_tempDir, "dst.txt");
        File.WriteAllText(src, "content");
        await _sut.CopyAsync(src, dst);
        File.Exists(dst).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("content");
    }

    [Fact]
    public async Task MoveAsync_MovesFile()
    {
        var src = Path.Combine(_tempDir, "move.txt");
        var dst = Path.Combine(_tempDir, "moved.txt");
        File.WriteAllText(src, "data");
        await _sut.MoveAsync(src, dst);
        File.Exists(dst).Should().BeTrue();
        File.Exists(src).Should().BeFalse();
    }

    [Fact]
    public void Search_FindsFileByName()
    {
        File.WriteAllText(Path.Combine(_tempDir, "report.pdf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "photo.jpg"), "");
        var results = _sut.Search(_tempDir, "report").ToList();
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("report.pdf");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 3: Run tests — verify all fail**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "FileSystemServiceTests" -v minimal
```

Expected: compile error — `FileSystemService` not defined yet.

- [ ] **Step 4: Create `Services/FileSystemService.cs`**

```csharp
using DiskManager.Models;

namespace DiskManager.Services;

public class FileSystemService : IFileSystemService
{
    public Task<IEnumerable<FileItem>> GetChildrenAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var items = new List<FileItem>();
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var info = new DirectoryInfo(dir);
                    items.Add(new FileItem(info.Name, info.FullName, 0, info.LastWriteTime, true));
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    ct.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    items.Add(new FileItem(info.Name, info.FullName, info.Length, info.LastWriteTime, false));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            return (IEnumerable<FileItem>)items;
        }, ct);
    }

    public Task CopyAsync(string source, string destination, CancellationToken ct = default)
        => Task.Run(() => File.Copy(source, destination, overwrite: false), ct);

    public Task MoveAsync(string source, string destination, CancellationToken ct = default)
        => Task.Run(() => File.Move(source, destination), ct);

    public Task DeleteAsync(string path, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }, ct);

    public Task CreateDirectoryAsync(string path, CancellationToken ct = default)
        => Task.Run(() => Directory.CreateDirectory(path), ct);

    public Task RenameAsync(string path, string newName, CancellationToken ct = default)
        => Task.Run(() =>
        {
            var parent = Path.GetDirectoryName(path)!;
            var dest = Path.Combine(parent, newName);
            if (File.Exists(path)) File.Move(path, dest);
            else Directory.Move(path, dest);
        }, ct);

    public IEnumerable<FileItem> Search(string folder, string query)
    {
        try
        {
            return Directory.EnumerateFiles(folder, $"*{query}*", SearchOption.TopDirectoryOnly)
                .Select(f =>
                {
                    var info = new FileInfo(f);
                    return new FileItem(info.Name, info.FullName, info.Length, info.LastWriteTime, false);
                });
        }
        catch (UnauthorizedAccessException) { return Enumerable.Empty<FileItem>(); }
    }
}
```

- [ ] **Step 5: Run tests — verify all pass**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "FileSystemServiceTests" -v minimal
```

Expected: `6 passed`.

- [ ] **Step 6: Commit**

```bash
git add DiskManager/Services/IFileSystemService.cs DiskManager/Services/FileSystemService.cs DiskManager.Tests/Services/FileSystemServiceTests.cs
git commit -m "feat: add FileSystemService with browse, copy, move, rename, delete, search"
```

---

## Task 5: DiskAnalyzerService + tests

**Files:**
- Create: `DiskManager/Services/IDiskAnalyzerService.cs`
- Create: `DiskManager/Services/DiskAnalyzerService.cs`
- Create: `DiskManager.Tests/Services/DiskAnalyzerServiceTests.cs`

- [ ] **Step 1: Create `Services/IDiskAnalyzerService.cs`**

```csharp
using DiskManager.Models;

namespace DiskManager.Services;

public record DriveUsage(string Letter, long TotalBytes, long UsedBytes, long FreeBytes);

public interface IDiskAnalyzerService
{
    Task<FolderNode> ScanAsync(string rootPath, IProgress<string>? progress, CancellationToken ct = default);
    DriveUsage GetDriveUsage(string driveLetter);
}
```

- [ ] **Step 2: Write failing tests**

```csharp
using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class DiskAnalyzerServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DiskAnalyzerService _sut;

    public DiskAnalyzerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new DiskAnalyzerService();
    }

    [Fact]
    public async Task ScanAsync_ReturnsTotalSizeOfFiles()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(_tempDir, "b.bin"), new byte[200]);

        var node = await _sut.ScanAsync(_tempDir, null);

        node.TotalSize.Should().Be(300);
        node.FullPath.Should().Be(_tempDir);
    }

    [Fact]
    public async Task ScanAsync_RecursesIntoSubfolders()
    {
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(sub.FullName, "c.bin"), new byte[500]);

        var node = await _sut.ScanAsync(_tempDir, null);

        node.TotalSize.Should().Be(500);
        node.Children.Should().HaveCount(1);
        node.Children[0].TotalSize.Should().Be(500);
    }

    [Fact]
    public async Task ScanAsync_SkipsUnauthorizedDirectories()
    {
        // Can't reliably lock dirs in tests — verify no exception thrown on valid path
        var act = async () => await _sut.ScanAsync(_tempDir, null);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetDriveUsage_ReturnsSaneValues()
    {
        var usage = _sut.GetDriveUsage("C");
        usage.TotalBytes.Should().BeGreaterThan(0);
        usage.FreeBytes.Should().BeGreaterThan(0);
        usage.UsedBytes.Should().Be(usage.TotalBytes - usage.FreeBytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 3: Run — verify compile error**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "DiskAnalyzerServiceTests" -v minimal
```

- [ ] **Step 4: Create `Services/DiskAnalyzerService.cs`**

```csharp
using DiskManager.Models;

namespace DiskManager.Services;

public class DiskAnalyzerService : IDiskAnalyzerService
{
    public async Task<FolderNode> ScanAsync(string rootPath, IProgress<string>? progress, CancellationToken ct = default)
    {
        return await Task.Run(() => ScanDirectory(rootPath, progress, ct), ct);
    }

    private static FolderNode ScanDirectory(string path, IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(path);

        var node = new FolderNode
        {
            Name = Path.GetFileName(path),
            FullPath = path
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try { node.TotalSize += new FileInfo(file).Length; }
                catch (IOException) { }
            }

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var child = ScanDirectory(dir, progress, ct);
                    node.TotalSize += child.TotalSize;
                    node.Children.Add(child);
                }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (UnauthorizedAccessException) { }

        return node;
    }

    public DriveUsage GetDriveUsage(string driveLetter)
    {
        var drive = new DriveInfo(driveLetter);
        return new DriveUsage(
            driveLetter,
            drive.TotalSize,
            drive.TotalSize - drive.TotalFreeSpace,
            drive.TotalFreeSpace);
    }
}
```

- [ ] **Step 5: Run — verify 4 passed**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "DiskAnalyzerServiceTests" -v minimal
```

Expected: `4 passed`.

- [ ] **Step 6: Commit**

```bash
git add DiskManager/Services/IDiskAnalyzerService.cs DiskManager/Services/DiskAnalyzerService.cs DiskManager.Tests/Services/DiskAnalyzerServiceTests.cs
git commit -m "feat: add DiskAnalyzerService with recursive scan and drive usage"
```

---

## Task 6: DuplicateFinderService + tests

**Files:**
- Create: `DiskManager/Services/IDuplicateFinderService.cs`
- Create: `DiskManager/Services/DuplicateFinderService.cs`
- Create: `DiskManager.Tests/Services/DuplicateFinderServiceTests.cs`

- [ ] **Step 1: Create `Services/IDuplicateFinderService.cs`**

```csharp
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
```

- [ ] **Step 2: Write failing tests**

```csharp
using DiskManager.Models;
using DiskManager.Services;
using FluentAssertions;
using System.IO;

namespace DiskManager.Tests.Services;

public class DuplicateFinderServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DuplicateFinderService _sut;

    public DuplicateFinderServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _sut = new DuplicateFinderService();
    }

    [Fact]
    public async Task FindAsync_MD5_FindsIdenticalFiles()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "a.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_tempDir, "b.bin"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_tempDir, "c.bin"), new byte[] { 9, 9, 9 });

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups.Should().HaveCount(1);
        groups[0].Paths.Should().HaveCount(2);
        groups[0].FileSize.Should().Be(3);
    }

    [Fact]
    public async Task FindAsync_NameAndSize_GroupsByNameAndSize()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "same.txt"), new byte[10]);
        var sub = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(sub.FullName, "same.txt"), new byte[10]);

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.NameAndSize, null)).ToList();

        groups.Should().HaveCount(1);
        groups[0].Paths.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_NoDuplicates_ReturnsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "unique1.bin"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(_tempDir, "unique2.bin"), new byte[] { 2 });

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task FindAsync_WastedBytes_CalculatedCorrectly()
    {
        var content = new byte[50];
        File.WriteAllBytes(Path.Combine(_tempDir, "x.bin"), content);
        File.WriteAllBytes(Path.Combine(_tempDir, "y.bin"), content);
        File.WriteAllBytes(Path.Combine(_tempDir, "z.bin"), content);

        var groups = (await _sut.FindAsync(_tempDir, DuplicateMethod.HashMD5, null)).ToList();

        groups[0].WastedBytes.Should().Be(100); // 50 × (3 - 1)
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 3: Run — verify compile error**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "DuplicateFinderServiceTests" -v minimal
```

- [ ] **Step 4: Create `Services/DuplicateFinderService.cs`**

```csharp
using DiskManager.Models;
using System.Security.Cryptography;

namespace DiskManager.Services;

public class DuplicateFinderService : IDuplicateFinderService
{
    public async Task<IEnumerable<DuplicateGroup>> FindAsync(
        string rootPath,
        DuplicateMethod method,
        IProgress<int>? progress,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Find(rootPath, method, progress, ct), ct);
    }

    private static IEnumerable<DuplicateGroup> Find(
        string rootPath, DuplicateMethod method,
        IProgress<int>? progress, CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .Where(f => { try { return new FileInfo(f).Length > 0; } catch { return false; } })
            .ToList();

        var grouped = method switch
        {
            DuplicateMethod.HashMD5     => GroupByHash(files, progress, ct),
            DuplicateMethod.NameAndSize => GroupByNameAndSize(files),
            DuplicateMethod.SizeOnly    => GroupBySize(files),
            _ => throw new ArgumentOutOfRangeException(nameof(method))
        };

        return grouped.Where(g => g.Paths.Count > 1);
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
```

- [ ] **Step 5: Run — verify 4 passed**

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj --filter "DuplicateFinderServiceTests" -v minimal
```

Expected: `4 passed`.

- [ ] **Step 6: Commit**

```bash
git add DiskManager/Services/IDuplicateFinderService.cs DiskManager/Services/DuplicateFinderService.cs DiskManager.Tests/Services/DuplicateFinderServiceTests.cs
git commit -m "feat: add DuplicateFinderService with MD5, name+size, size-only strategies"
```

---

## Task 7: Converters + TreeMapPanel

**Files:**
- Create: `DiskManager/Converters/FileSizeConverter.cs`
- Create: `DiskManager/Converters/BoolToVisibilityConverter.cs`
- Create: `DiskManager/Controls/TreeMapPanel.cs`

- [ ] **Step 1: Create `Converters/FileSizeConverter.cs`**

```csharp
using System.Globalization;
using System.Windows.Data;

namespace DiskManager.Converters;

[ValueConversion(typeof(long), typeof(string))]
public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes) return "—";
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Create `Converters/BoolToVisibilityConverter.cs`**

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DiskManager.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
```

- [ ] **Step 3: Create `Controls/TreeMapPanel.cs` — squarified treemap layout**

```csharp
using DiskManager.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DiskManager.Controls;

public class TreeMapPanel : Panel
{
    public static readonly DependencyProperty RootNodeProperty =
        DependencyProperty.Register(nameof(RootNode), typeof(FolderNode), typeof(TreeMapPanel),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty NodeClickCommandProperty =
        DependencyProperty.Register(nameof(NodeClickCommand), typeof(ICommand), typeof(TreeMapPanel));

    public FolderNode? RootNode
    {
        get => (FolderNode?)GetValue(RootNodeProperty);
        set => SetValue(RootNodeProperty, value);
    }

    public ICommand? NodeClickCommand
    {
        get => (ICommand?)GetValue(NodeClickCommandProperty);
        set => SetValue(NodeClickCommandProperty, value);
    }

    private static readonly Brush[] _palette =
    {
        new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
        new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
        new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
        new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
        new SolidColorBrush(Color.FromRgb(0xF9, 0xE2, 0xAF)),
        new SolidColorBrush(Color.FromRgb(0x89, 0xDC, 0xEB)),
    };

    private readonly List<(FolderNode Node, Rect Rect, Brush Fill)> _rects = new();

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rects.Clear();
        InternalChildren.Clear();

        if (RootNode is null || RootNode.TotalSize == 0) return finalSize;

        var nodes = RootNode.Children
            .Where(c => c.TotalSize > 0)
            .OrderByDescending(c => c.TotalSize)
            .ToList();

        Squarify(nodes, new Rect(0, 0, finalSize.Width, finalSize.Height), RootNode.TotalSize);
        return finalSize;
    }

    private void Squarify(List<FolderNode> nodes, Rect area, long totalSize)
    {
        if (nodes.Count == 0 || area.Width < 2 || area.Height < 2) return;

        // Simple slice-and-dice layout (horizontal splits)
        double remaining = area.Height;
        double y = area.Y;
        int colorIdx = 0;

        foreach (var node in nodes)
        {
            double ratio = totalSize > 0 ? (double)node.TotalSize / totalSize : 0;
            double height = remaining * ratio;
            if (height < 1) height = 1;

            var rect = new Rect(area.X, y, area.Width, height);
            var fill = _palette[colorIdx % _palette.Length];
            _rects.Add((node, rect, fill));
            colorIdx++;
            y += height;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var textBrush = new SolidColorBrush(Colors.Black);
        foreach (var (node, rect, fill) in _rects)
        {
            dc.DrawRectangle(fill, new Pen(Brushes.White, 1), rect);
            if (rect.Width > 40 && rect.Height > 20)
            {
                var ft = new FormattedText(
                    $"{node.Name}\n{FormatSize(node.TotalSize)}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10, textBrush, 96);
                dc.DrawText(ft, new Point(rect.X + 4, rect.Y + 4));
            }
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);
        var hit = _rects.FirstOrDefault(r => r.Rect.Contains(pos));
        if (hit.Node is not null && NodeClickCommand?.CanExecute(hit.Node) == true)
            NodeClickCommand.Execute(hit.Node);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
```

- [ ] **Step 4: Build — verify no errors**

```bash
dotnet build DiskManager/DiskManager.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add DiskManager/Converters/ DiskManager/Controls/TreeMapPanel.cs
git commit -m "feat: add FileSizeConverter, BoolToVisibilityConverter, TreeMapPanel"
```

---

## Task 8: ExplorerViewModel + ExplorerView

**Files:**
- Create: `DiskManager/ViewModels/ExplorerViewModel.cs`
- Create: `DiskManager/Views/ExplorerView.xaml`
- Create: `DiskManager/Views/ExplorerView.xaml.cs`

- [ ] **Step 1: Create `ViewModels/ExplorerViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using System.Collections.ObjectModel;
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
```

- [ ] **Step 2: Create `Views/ExplorerView.xaml`**

```xml
<UserControl x:Class="DiskManager.Views.ExplorerView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:DiskManager.Converters"
             xmlns:vm="clr-namespace:DiskManager.ViewModels">
  <UserControl.Resources>
    <conv:FileSizeConverter x:Key="SizeConv"/>
    <conv:BoolToVisibilityConverter x:Key="BoolVis"/>
  </UserControl.Resources>

  <DockPanel Background="{DynamicResource AppBackground}">
    <!-- Toolbar -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="4"
                Background="{DynamicResource SidebarBackground}">
      <Button Content="←" Command="{Binding GoBackCommand}" Width="28" Margin="2"/>
      <Button Content="→" Command="{Binding GoForwardCommand}" Width="28" Margin="2"/>
      <Button Content="↑" Command="{Binding GoUpCommand}" Width="28" Margin="2"/>
      <TextBox Text="{Binding CurrentPath, UpdateSourceTrigger=LostFocus}"
               Width="400" Margin="4,2"
               Background="{DynamicResource PanelBackground}"
               Foreground="{DynamicResource TextPrimary}"/>
      <TextBox Text="{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged}"
               Width="140" Margin="4,2"
               Background="{DynamicResource PanelBackground}"
               Foreground="{DynamicResource TextPrimary}"/>
      <Button Content="🔍" Command="{Binding SearchCommand}" Width="30" Margin="2"/>
      <Button Content="+ Carpeta" Command="{Binding NewFolderCommand}" Margin="4,2"/>
    </StackPanel>

    <!-- Status bar -->
    <TextBlock DockPanel.Dock="Bottom" Text="{Binding StatusText}"
               Foreground="{DynamicResource TextSecondary}" Margin="8,3"/>

    <!-- Main panels -->
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="220"/>
        <ColumnDefinition Width="4"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>

      <!-- Tree placeholder (drives) -->
      <TreeView Grid.Column="0" Background="{DynamicResource SidebarBackground}"
                Foreground="{DynamicResource TextPrimary}">
        <TreeViewItem Header="💻 Este equipo" IsExpanded="True">
          <TreeViewItem Header="💾 C:\" Tag="C:\"
                        MouseDoubleClick="DriveItem_DoubleClick"/>
          <TreeViewItem Header="💽 D:\" Tag="D:\"
                        MouseDoubleClick="DriveItem_DoubleClick"/>
        </TreeViewItem>
      </TreeView>

      <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Stretch"/>

      <!-- File list -->
      <ListView Grid.Column="2"
                ItemsSource="{Binding Items}"
                SelectedItem="{Binding SelectedItem}"
                Background="{DynamicResource PanelBackground}"
                Foreground="{DynamicResource TextPrimary}">
        <ListView.InputBindings>
          <KeyBinding Key="Delete" Command="{Binding DeleteItemCommand}"/>
          <KeyBinding Key="F2" Command="{Binding NewFolderCommand}"/>
        </ListView.InputBindings>
        <ListView.View>
          <GridView>
            <GridViewColumn Header="Nombre" Width="280"
                            DisplayMemberBinding="{Binding Name}"/>
            <GridViewColumn Header="Tamaño" Width="90"
                            DisplayMemberBinding="{Binding Size, Converter={StaticResource SizeConv}}"/>
            <GridViewColumn Header="Modificado" Width="130"
                            DisplayMemberBinding="{Binding Modified, StringFormat=yyyy-MM-dd HH:mm}"/>
          </GridView>
        </ListView.View>
        <ListView.ItemContainerStyle>
          <Style TargetType="ListViewItem">
            <EventSetter Event="MouseDoubleClick" Handler="Item_DoubleClick"/>
          </Style>
        </ListView.ItemContainerStyle>
      </ListView>
    </Grid>
  </DockPanel>
</UserControl>
```

- [ ] **Step 3: Create `Views/ExplorerView.xaml.cs`**

```csharp
using DiskManager.Models;
using DiskManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiskManager.Views;

public partial class ExplorerView : UserControl
{
    public ExplorerView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<ExplorerViewModel>()
                     ?? new ExplorerViewModel(App.Current.GetService<Services.IFileSystemService>()!);
    }

    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExplorerViewModel vm && sender is ListViewItem { DataContext: FileItem item })
            vm.OpenItemCommand.Execute(item);
    }

    private void DriveItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExplorerViewModel vm && sender is TreeViewItem { Tag: string path })
            vm.NavigateToCommand.Execute(path);
    }
}
```

- [ ] **Step 4: Add `GetService` extension to `App.xaml.cs`**

Add this after the class opening brace in `App.xaml.cs`:

```csharp
public T? GetService<T>() where T : class
    => _host.Services.GetService<T>();

public new static App Current => (App)Application.Current;
```

- [ ] **Step 5: Build**

```bash
dotnet build DiskManager/DiskManager.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add DiskManager/ViewModels/ExplorerViewModel.cs DiskManager/Views/ExplorerView.xaml DiskManager/Views/ExplorerView.xaml.cs
git commit -m "feat: add ExplorerViewModel and ExplorerView with navigation, delete, search"
```

---

## Task 9: DiskAnalyzerViewModel + DiskAnalyzerView

**Files:**
- Create: `DiskManager/ViewModels/DiskAnalyzerViewModel.cs`
- Create: `DiskManager/Views/DiskAnalyzerView.xaml`
- Create: `DiskManager/Views/DiskAnalyzerView.xaml.cs`

- [ ] **Step 1: Create `ViewModels/DiskAnalyzerViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using System.Collections.ObjectModel;

namespace DiskManager.ViewModels;

public partial class DiskAnalyzerViewModel : ObservableObject
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

    [RelayCommand]
    private async Task ScanAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        IsScanning = true;
        ProgressText = "Escaneando...";

        var progress = new Progress<string>(p =>
            ProgressText = $"Escaneando: {Path.GetFileName(p)}");

        try
        {
            DriveUsage = _analyzer.GetDriveUsage(SelectedDrive.TrimEnd('\\', ':'));
            RootNode = await _analyzer.ScanAsync(SelectedDrive, progress, _cts.Token);
            TopFolders = new ObservableCollection<FolderNode>(
                RootNode.Children.OrderByDescending(c => c.TotalSize).Take(20));
            ProgressText = "Listo.";
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
        catch { }
    }
}
```

- [ ] **Step 2: Create `Views/DiskAnalyzerView.xaml`**

```xml
<UserControl x:Class="DiskManager.Views.DiskAnalyzerView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:DiskManager.Converters"
             xmlns:ctrl="clr-namespace:DiskManager.Controls">
  <UserControl.Resources>
    <conv:FileSizeConverter x:Key="SizeConv"/>
  </UserControl.Resources>

  <DockPanel Background="{DynamicResource AppBackground}">
    <!-- Toolbar -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="6"
                Background="{DynamicResource SidebarBackground}">
      <TextBlock Text="Unidad:" VerticalAlignment="Center" Margin="4,0"
                 Foreground="{DynamicResource TextPrimary}"/>
      <ComboBox ItemsSource="{Binding AvailableDrives}"
                SelectedItem="{Binding SelectedDrive}"
                Width="80" Margin="4,2"/>
      <Button Content="▶ Escanear" Command="{Binding ScanCommand}"
              IsEnabled="{Binding IsNotScanning}"
              Margin="4,2" Background="{DynamicResource SuccessBrush}"/>
      <Button Content="■ Detener" Command="{Binding StopScanCommand}"
              IsEnabled="{Binding IsScanning}" Margin="4,2"/>
      <TextBlock Text="{Binding ProgressText}" VerticalAlignment="Center" Margin="8,0"
                 Foreground="{DynamicResource TextSecondary}"/>
    </StackPanel>

    <!-- Drive usage bar -->
    <StackPanel DockPanel.Dock="Top" Margin="8,4">
      <TextBlock Foreground="{DynamicResource TextSecondary}" FontSize="11">
        <TextBlock.Text>
          <MultiBinding StringFormat="{}{0} — {1} usados de {2}">
            <Binding Path="SelectedDrive"/>
            <Binding Path="DriveUsage.UsedBytes" Converter="{StaticResource SizeConv}"/>
            <Binding Path="DriveUsage.TotalBytes" Converter="{StaticResource SizeConv}"/>
          </MultiBinding>
        </TextBlock.Text>
      </TextBlock>
      <ProgressBar Height="8" Margin="0,3"
                   Maximum="{Binding DriveUsage.TotalBytes}"
                   Value="{Binding DriveUsage.UsedBytes}"
                   Background="{DynamicResource BorderBrush}"
                   Foreground="{DynamicResource AccentBrush}"/>
    </StackPanel>

    <!-- Main panels -->
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="4"/>
        <ColumnDefinition Width="220"/>
      </Grid.ColumnDefinitions>

      <!-- TreeMap -->
      <ctrl:TreeMapPanel Grid.Column="0"
                         RootNode="{Binding RootNode}"
                         NodeClickCommand="{Binding OpenInExplorerCommand}"
                         Background="{DynamicResource PanelBackground}"/>

      <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Stretch"/>

      <!-- Top folders list -->
      <ListView Grid.Column="2"
                ItemsSource="{Binding TopFolders}"
                Background="{DynamicResource PanelBackground}"
                Foreground="{DynamicResource TextPrimary}">
        <ListView.View>
          <GridView>
            <GridViewColumn Header="Carpeta" Width="120" DisplayMemberBinding="{Binding Name}"/>
            <GridViewColumn Header="Tamaño" Width="80"
                            DisplayMemberBinding="{Binding TotalSize, Converter={StaticResource SizeConv}}"/>
          </GridView>
        </ListView.View>
      </ListView>
    </Grid>
  </DockPanel>
</UserControl>
```

- [ ] **Step 3: Create `Views/DiskAnalyzerView.xaml.cs`**

```csharp
using DiskManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace DiskManager.Views;

public partial class DiskAnalyzerView : UserControl
{
    public DiskAnalyzerView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<DiskAnalyzerViewModel>()!;
    }
}
```

- [ ] **Step 4: No additional fix needed** — `BoolToVisibilityConverter` is used as `StaticResource` in XAML (no `Instance` needed).

- [ ] **Step 5: Build**

```bash
dotnet build DiskManager/DiskManager.csproj
```

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add DiskManager/ViewModels/DiskAnalyzerViewModel.cs DiskManager/Views/DiskAnalyzerView.xaml DiskManager/Views/DiskAnalyzerView.xaml.cs
git commit -m "feat: add DiskAnalyzerViewModel, DiskAnalyzerView with TreeMap and folder list"
```

---

## Task 10: DuplicateFinderViewModel + DuplicateFinderView

**Files:**
- Create: `DiskManager/ViewModels/DuplicateFinderViewModel.cs`
- Create: `DiskManager/Views/DuplicateFinderView.xaml`
- Create: `DiskManager/Views/DuplicateFinderView.xaml.cs`

- [ ] **Step 1: Create `ViewModels/DuplicateFinderViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskManager.Models;
using DiskManager.Services;
using Microsoft.VisualBasic.FileIO;
using System.Collections.ObjectModel;
using System.Windows;

namespace DiskManager.ViewModels;

public partial class DuplicateFinderViewModel : ObservableObject
{
    private readonly IDuplicateFinderService _finder;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    [ObservableProperty] private DuplicateMethod _selectedMethod = DuplicateMethod.HashMD5;
    [ObservableProperty] private ObservableCollection<DuplicateGroupViewModel> _groups = new();
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _statusText = string.Empty;

    public DuplicateMethod[] Methods => Enum.GetValues<DuplicateMethod>();

    public DuplicateFinderViewModel(IDuplicateFinderService finder)
    {
        _finder = finder;
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
            StatusText = Groups.Count == 0
                ? "No se encontraron duplicados."
                : $"{Groups.Count} grupos · {FormatSize(Groups.Sum(g => g.Group.WastedBytes))} recuperables";
        }
        catch (OperationCanceledException) { StatusText = "Búsqueda cancelada."; }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsSearching = false; }
    }

    [RelayCommand]
    private void DeleteSelected()
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
        foreach (var path in toDelete)
        {
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                deleted++;
            }
            catch (Exception ex) { StatusText = $"Error eliminando {Path.GetFileName(path)}: {ex.Message}"; }
        }
        StatusText = $"{deleted} archivo(s) enviados a la Papelera.";
        _ = SearchAsync();
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
        try { return File.GetLastWriteTime(path); }
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
```

- [ ] **Step 2: Add `System.Windows.Forms` reference to `DiskManager.csproj`**

Add inside `<PropertyGroup>`:

```xml
<UseWindowsForms>true</UseWindowsForms>
```

- [ ] **Step 3: Create `Views/DuplicateFinderView.xaml`**

```xml
<UserControl x:Class="DiskManager.Views.DuplicateFinderView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:conv="clr-namespace:DiskManager.Converters">
  <UserControl.Resources>
    <conv:FileSizeConverter x:Key="SizeConv"/>
  </UserControl.Resources>

  <DockPanel Background="{DynamicResource AppBackground}">
    <!-- Toolbar -->
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="6"
                Background="{DynamicResource SidebarBackground}">
      <TextBlock Text="Carpeta:" VerticalAlignment="Center" Margin="4,0"
                 Foreground="{DynamicResource TextPrimary}"/>
      <TextBox Text="{Binding RootPath}" Width="320" Margin="4,2"
               Background="{DynamicResource PanelBackground}"
               Foreground="{DynamicResource TextPrimary}"/>
      <Button Content="📂" Command="{Binding ChooseFolderCommand}" Width="30" Margin="2"/>
      <TextBlock Text="Método:" VerticalAlignment="Center" Margin="8,0,4,0"
                 Foreground="{DynamicResource TextPrimary}"/>
      <ComboBox ItemsSource="{Binding Methods}" SelectedItem="{Binding SelectedMethod}"
                Width="120" Margin="2"/>
      <Button Content="▶ Buscar" Command="{Binding SearchCommand}" Margin="6,2"
              Background="{DynamicResource AccentBrush}"/>
    </StackPanel>

  <UserControl.Resources>
    <conv:BoolToVisibilityConverter x:Key="BoolVis"/>
  </UserControl.Resources>

    <!-- Progress -->
    <ProgressBar DockPanel.Dock="Top" Height="4" IsIndeterminate="{Binding IsSearching}"
                 Visibility="{Binding IsSearching, Converter={StaticResource BoolVis}}"
                 Foreground="{DynamicResource AccentBrush}"/>

    <!-- Status + action bar -->
    <DockPanel DockPanel.Dock="Bottom" Margin="6,4"
               Background="{DynamicResource SidebarBackground}">
      <Button DockPanel.Dock="Right" Content="🗑️ Eliminar seleccionados"
              Command="{Binding DeleteSelectedCommand}" Margin="4"
              Background="{DynamicResource DangerBrush}" Foreground="White"/>
      <TextBlock Text="{Binding StatusText}" VerticalAlignment="Center"
                 Foreground="{DynamicResource TextSecondary}" Margin="8,0"/>
    </DockPanel>

    <!-- Groups list -->
    <ScrollViewer VerticalScrollBarVisibility="Auto">
      <ItemsControl ItemsSource="{Binding Groups}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Border Margin="6,3" Padding="8" CornerRadius="4"
                    Background="{DynamicResource PanelBackground}"
                    BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1">
              <StackPanel>
                <TextBlock Foreground="{DynamicResource TextSecondary}" FontSize="11" Margin="0,0,0,4">
                  <Run Text="{Binding Group.Paths.Count, StringFormat={}{0} archivos · }" />
                  <Run Text="{Binding Group.FileSize, Converter={StaticResource SizeConv}}" />
                  <Run Text=" c/u · Desperdiciado: " />
                  <Run Text="{Binding Group.WastedBytes, Converter={StaticResource SizeConv}}"
                       Foreground="{DynamicResource DangerBrush}"/>
                </TextBlock>
                <ItemsControl ItemsSource="{Binding PathItems}">
                  <ItemsControl.ItemTemplate>
                    <DataTemplate>
                      <StackPanel Orientation="Horizontal" Margin="0,2">
                        <CheckBox IsChecked="{Binding IsSelected}" VerticalAlignment="Center" Margin="0,0,6,0"/>
                        <TextBlock Text="{Binding Path}"
                                   Foreground="{DynamicResource TextPrimary}" FontSize="11"
                                   VerticalAlignment="Center"/>
                      </StackPanel>
                    </DataTemplate>
                  </ItemsControl.ItemTemplate>
                </ItemsControl>
              </StackPanel>
            </Border>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </ScrollViewer>
  </DockPanel>
</UserControl>
```

- [ ] **Step 4: Create `Views/DuplicateFinderView.xaml.cs`**

```csharp
using DiskManager.ViewModels;
using System.Windows.Controls;

namespace DiskManager.Views;

public partial class DuplicateFinderView : UserControl
{
    public DuplicateFinderView()
    {
        InitializeComponent();
        DataContext = App.Current.GetService<DuplicateFinderViewModel>()!;
    }
}
```

- [ ] **Step 5: Final build + all tests**

```bash
dotnet build DiskManager.sln
dotnet test DiskManager.Tests/DiskManager.Tests.csproj -v minimal
```

Expected: `Build succeeded` · all tests pass.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: add DuplicateFinderViewModel, DuplicateFinderView — suite complete"
```

---

## Task 11: Smoke test — run the app

- [ ] **Step 1: Launch the app**

```bash
dotnet run --project DiskManager/DiskManager.csproj
```

Expected: window opens with 3 tabs. Explorador shows user home folder. No crash on startup.

- [ ] **Step 2: Test Explorer tab**
  - Navigate using TreeView drives
  - Double-click a folder → contents load
  - Back/Forward buttons work
  - Create new folder (toolbar button)
  - Search a filename
  - Delete a test file (confirm dialog appears, file removed)

- [ ] **Step 3: Test Disk Analyzer tab**
  - Select C: drive, click Escanear
  - Progress text updates
  - TreeMap renders colored rectangles after scan
  - Top folders list populated, sorted by size
  - Stop button cancels cleanly

- [ ] **Step 4: Test Duplicate Finder tab**
  - Choose a folder with known duplicates (or create two identical files manually)
  - Click Buscar → groups appear
  - Checkboxes are pre-selected (all except oldest copy)
  - Click Eliminar → confirmation dialog → files go to Recycle Bin

- [ ] **Step 5: Test theme switching**
  - Change Windows theme (Settings → Personalization → Colors → Dark/Light)
  - App theme updates without restart

- [ ] **Step 6: Commit final state**

```bash
git add .
git commit -m "chore: smoke test passed — DiskManager v1 complete"
```

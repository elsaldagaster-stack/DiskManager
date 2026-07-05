# DiskManager

A WPF desktop application for Windows that lets you explore, analyze, and manage your hard drive content.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![WPF](https://img.shields.io/badge/UI-WPF-blue) ![License](https://img.shields.io/badge/license-MIT-green)

## Features

### File Explorer
- Navigate drives and folders with back / forward / up history
- File list with Name, Size, and Modified date columns
- Create folders, rename, copy, move, delete (with confirmation)
- Search files in the current directory
- Keyboard shortcuts: `Delete` to delete, `F2` for new folder

### Disk Analyzer
- Scan any drive and visualize space usage as a **TreeMap**
- Drive usage bar showing used / free / total space
- Top-20 largest folders list
- Click a block to open the folder in Windows Explorer
- Cancel scan at any time

### Duplicate Finder
- Find duplicate files by **MD5 hash** (exact), **name + size** (fast), or **size only** (approximate)
- Groups duplicates by wasted space, largest first
- Auto-selects all copies except the oldest (by creation date)
- Sends files to the **Recycle Bin** — no permanent deletion
- Confirmation dialog before any deletion

### Theme
- Automatically detects Windows dark / light mode
- Switches in real-time without restarting the app

## Requirements

- Windows 10 / 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop Runtime)

## Build from source

```bash
git clone https://github.com/elsaldagaster-stack/DiskManager.git
cd DiskManager
dotnet build DiskManager.slnx
dotnet run --project DiskManager/DiskManager.csproj
```

Run tests:

```bash
dotnet test DiskManager.Tests/DiskManager.Tests.csproj -v minimal
```

## Tech stack

| Layer | Technology |
|---|---|
| UI | WPF (.NET 8, C# 12) |
| MVVM | CommunityToolkit.Mvvm |
| DI / Host | Microsoft.Extensions.Hosting |
| Tests | xUnit · FluentAssertions · NSubstitute |

## Architecture

```
Views (XAML)
    ↕ Data Binding / ICommand
ViewModels (ObservableObject + RelayCommand)
    ↕ Constructor injection
Services (interfaces + implementations)
    ↕ System.IO / Win32 API / Registry
File System (NTFS)
```

## License

MIT

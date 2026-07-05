# DiskManager — Contexto de continuación

## Estado
Diseño aprobado. Plan de implementación completo. Listo para ejecutar.

## Archivos clave
- **Spec de diseño:** `docs/superpowers/specs/2026-07-04-disk-manager-design.md`
- **Plan de implementación:** `docs/superpowers/plans/2026-07-04-disk-manager.md`

## Cómo continuar con Claude Code
```
cd D:\Claude\Proyectos\DiskManager
claude
```
Luego di: "Ejecuta el plan en `docs/superpowers/plans/2026-07-04-disk-manager.md` empezando por la Tarea 1"

## Stack
- C# 12 · .NET 8 · WPF · MVVM
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting (DI)
- xUnit + NSubstitute + FluentAssertions (tests)

## Resumen del plan (11 tareas)
| Tarea | Descripción |
|---|---|
| 1 | Scaffold solución + DI bootstrap |
| 2 | Models (FileItem, FolderNode, DuplicateGroup) |
| 3 | ThemeService + Dark/Light XAML |
| 4 | FileSystemService + tests |
| 5 | DiskAnalyzerService + tests |
| 6 | DuplicateFinderService + tests |
| 7 | Converters + TreeMapPanel custom control |
| 8 | ExplorerViewModel + ExplorerView |
| 9 | DiskAnalyzerViewModel + DiskAnalyzerView |
| 10 | DuplicateFinderViewModel + DuplicateFinderView |
| 11 | Smoke test completo |

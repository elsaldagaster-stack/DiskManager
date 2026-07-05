# DiskManager — Spec de Diseño

**Fecha:** 2026-07-04  
**Stack:** C# · WPF · .NET 8 · MVVM · DI  
**Estado:** Aprobado por usuario

---

## 1. Objetivo

App de escritorio Windows para explorar y gestionar el contenido del disco duro. Suite de tres módulos integrados en una sola ventana con pestañas: explorador de archivos, analizador de disco y buscador de duplicados.

---

## 2. Arquitectura

**Patrón:** MVVM con capa de servicios inyectados vía DI.

**Capas:**

```
Views (XAML)
    ↕ Data Binding / ICommand
ViewModels (CommunityToolkit.Mvvm)
    ↕ Constructor injection (Microsoft.Extensions.DI)
Services (interfaces + implementaciones)
    ↕ System.IO / Win32 API / Registry
Sistema de archivos NTFS
```

**Bootstrap:** `App.xaml.cs` construye el `IHost` con `Microsoft.Extensions.Hosting`, registra servicios y resuelve `MainWindow` desde el contenedor.

**NuGet requeridos:**
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Hosting`

---

## 3. Estructura de proyecto

```
DiskManager/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
│
├── Models/
│   ├── FileItem.cs
│   ├── FolderNode.cs
│   └── DuplicateGroup.cs
│
├── ViewModels/
│   ├── ExplorerViewModel.cs
│   ├── DiskAnalyzerViewModel.cs
│   └── DuplicateFinderViewModel.cs
│
├── Views/
│   ├── ExplorerView.xaml
│   ├── DiskAnalyzerView.xaml
│   └── DuplicateFinderView.xaml
│
├── Services/
│   ├── IFileSystemService.cs
│   ├── FileSystemService.cs
│   ├── IDiskAnalyzerService.cs
│   ├── DiskAnalyzerService.cs
│   ├── IDuplicateFinderService.cs
│   ├── DuplicateFinderService.cs
│   ├── IThemeService.cs
│   └── ThemeService.cs
│
├── Converters/
│   ├── FileSizeConverter.cs
│   └── BoolToVisibilityConverter.cs
│
├── Themes/
│   ├── Dark.xaml
│   └── Light.xaml
│
└── Helpers/
    └── AsyncRelayCommandHelper.cs
```

---

## 4. Layout y UX

- **Ventana principal:** `TabControl` con 3 `TabItem` — Explorador, Analizador, Duplicados.
- **Tema:** automático — `ThemeService` detecta `AppsUseLightTheme` en el registro de Windows al arrancar y aplica `Dark.xaml` o `Light.xaml`. Se suscribe a `SystemEvents.UserPreferenceChanged` para cambio en tiempo real sin reiniciar.

---

## 5. Módulo — Explorador de archivos

**View:** `ExplorerView.xaml`  
**ViewModel:** `ExplorerViewModel`  
**Servicio:** `IFileSystemService`

**Componentes UI:**
- Toolbar: botones atrás / adelante / subir, barra de dirección editable, botón buscar, botón nueva carpeta.
- Panel izquierdo: `TreeView` con árbol de directorios (carga lazy por nodo).
- Panel derecho: `ListView` con columnas Nombre, Tamaño, Tipo, Fecha modificación. Selección múltiple habilitada.
- Status bar: count de elementos, tamaño seleccionado, acciones rápidas (Copiar, Mover, Renombrar, Eliminar).

**Operaciones:**
- Navegar (TreeView y doble clic en carpeta)
- Copiar / Mover (drag & drop + teclado Ctrl+C / Ctrl+X / Ctrl+V)
- Renombrar (F2)
- Eliminar (Delete → confirmación obligatoria)
- Nueva carpeta (Ctrl+Shift+N)
- Búsqueda en carpeta actual (Ctrl+F)

**Modelo:**
```csharp
record FileItem(
    string Name,
    string FullPath,
    long Size,
    DateTime Modified,
    bool IsDirectory
);
```

---

## 6. Módulo — Analizador de disco

**View:** `DiskAnalyzerView.xaml`  
**ViewModel:** `DiskAnalyzerViewModel`  
**Servicio:** `IDiskAnalyzerService`

**Componentes UI:**
- Selector de unidad (botones por cada drive disponible).
- Botón "Escanear" — arranca scan async con barra de progreso indeterminada.
- Barra de uso total del disco (used/total GB + % visual).
- Panel izquierdo: TreeMap de rectángulos coloreados por carpeta (tamaño = área). Clic navega; doble clic abre en Explorador. **Implementación:** `Canvas` WPF custom con algoritmo squarified treemap — sin dependencia de terceros. Clase `TreeMapPanel : Panel` calcula layout en `MeasureOverride`/`ArrangeOverride`.
- Panel derecho: lista de carpetas ordenada por tamaño descendente.

**Modelo:**
```csharp
class FolderNode {
    public string Name { get; set; }
    public string FullPath { get; set; }
    public long TotalSize { get; set; }
    public List<FolderNode> Children { get; set; }
}
```

**Comportamiento:**
- Scan recorre árbol de directorios recursivamente en background thread.
- Carpetas inaccesibles (`UnauthorizedAccessException`) se omiten silenciosamente.
- Progreso reportado vía `IProgress<string>` (ruta actual escaneándose).
- Cancelación via `CancellationToken` al cerrar tab o presionar Stop.

---

## 7. Módulo — Buscador de duplicados

**View:** `DuplicateFinderView.xaml`  
**ViewModel:** `DuplicateFinderViewModel`  
**Servicio:** `IDuplicateFinderService`

**Componentes UI:**
- Selector de carpeta raíz + botón "Elegir".
- Selector de método: Hash MD5 (exacto) / Nombre+Tamaño (rápido) / Solo tamaño (aproximado).
- Botón "Buscar" — scan async con barra de progreso.
- Lista de grupos: cada grupo muestra hash, tamaño × copias = espacio desperdiciado, y checkboxes por archivo.
- Action bar inferior: count seleccionados, GB recuperables, botón "Eliminar seleccionados".

**Modelo:**
```csharp
class DuplicateGroup {
    public string Hash { get; set; }
    public long FileSize { get; set; }
    public List<string> Paths { get; set; }
}
```

**Comportamiento:**
- Por defecto marca para eliminar todos excepto el primero de cada grupo (el más antiguo por fecha).
- Eliminación requiere confirmación explícita con resumen (N archivos, X GB).
- Archivos eliminados van a la Papelera de reciclaje (`FileSystem.DeleteFile` con `RecycleOption.SendToRecycleBin`) — no borrado permanente.

---

## 8. Servicios

### IFileSystemService
```csharp
Task<IEnumerable<FileItem>> GetChildrenAsync(string path, CancellationToken ct);
Task CopyAsync(string source, string destination, CancellationToken ct);
Task MoveAsync(string source, string destination, CancellationToken ct);
Task DeleteAsync(string path, CancellationToken ct);
Task CreateDirectoryAsync(string path, CancellationToken ct);
Task RenameAsync(string path, string newName, CancellationToken ct);
IEnumerable<FileItem> Search(string folder, string query);
```

### IDiskAnalyzerService
```csharp
Task<FolderNode> ScanAsync(string rootPath, IProgress<string> progress, CancellationToken ct);
DriveUsage GetDriveUsage(string driveLetter);

record DriveUsage(string Letter, long TotalBytes, long UsedBytes, long FreeBytes);
```

### IDuplicateFinderService
```csharp
Task<IEnumerable<DuplicateGroup>> FindAsync(
    string rootPath,
    DuplicateMethod method,
    IProgress<int> progress,
    CancellationToken ct);
```

`DuplicateMethod` enum vive en `Models/DuplicateMethod.cs`:
```csharp
enum DuplicateMethod { HashMD5, NameAndSize, SizeOnly }
```

### IThemeService
```csharp
Theme CurrentTheme { get; }
event EventHandler<Theme> ThemeChanged;
void Apply(ResourceDictionary resources);
```

---

## 9. Manejo de errores

| Escenario | Comportamiento |
|---|---|
| `UnauthorizedAccessException` en scan | Skip silencioso, continúa iteración |
| `IOException` en operación de archivo | Mensaje en status bar, no crash |
| `DirectoryNotFoundException` | Mensaje en status bar |
| Eliminación de archivos | Confirmación obligatoria antes de ejecutar |
| Operación cancelada | Estado limpio, UI regresa a idle |

No hay `catch(Exception)` genérico que trague errores sin mostrarlos.

---

## 10. Criterios de éxito

- [ ] Navegar árbol de directorios sin lag perceptible (carga lazy).
- [ ] Scan de disco completo en C:\ en menos de 60 segundos en SSD típico.
- [ ] Búsqueda de duplicados no bloquea UI thread.
- [ ] Cambio de tema Windows refleja en app sin reiniciar.
- [ ] Eliminación nunca ocurre sin confirmación explícita del usuario.
- [ ] Carpetas inaccesibles no crashean el scan.

// ViewModels/MainWindowViewModel.cs

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UrbanChaosEditor.Shared.ViewModels;
using UrbanChaosPrimEditor.Models;
using UrbanChaosPrimEditor.Services;

namespace UrbanChaosPrimEditor.ViewModels
{
    public sealed class MainWindowViewModel : BaseViewModel
    {
        private readonly PrmParserService _parser = new();

        public MainWindowViewModel()
        {
            // Wire up directory service
            PrimDirectoryService.Instance.DirectoryChanged       += (_, __) => RefreshFileList();
            PrimDirectoryService.Instance.TextureDirectoryChanged += (_, __) => RebuildMeshBuilder();

            // 3D scene
            RebuildMeshBuilder();

            // Commands
            OpenFileCommand        = new RelayCommand(_ => OpenFile());
            OpenDirectoryCommand   = new RelayCommand(_ => OpenDirectory());
            SetTextureDirCommand   = new RelayCommand(_ => SetTextureDirectory());
            SaveAsCommand          = new RelayCommand(_ => SaveAs(),    _ => IsFileLoaded);
            SaveAsPrmCommand       = new RelayCommand(_ => SaveAsPrm(), _ => IsFileLoaded);
            SaveAsObjCommand       = new RelayCommand(_ => SaveAsObj(), _ => IsFileLoaded);

            // Load initial file list from persisted directory
            RefreshFileList();
            UpdateStatus();
        }

        // ── Prim information panel ────────────────────────────────────────────

        public PrimInfoViewModel PrimInfo { get; } = new();

        // ── 3D viewport ───────────────────────────────────────────────────────

        public ThreeDViewModel ThreeD { get; private set; } = new(new PrmMeshBuilderService(null));

        private void RebuildMeshBuilder()
        {
            string? texDir = PrimDirectoryService.Instance.TextureDirectory;
            PrimTextureResolverService? resolver = texDir is null ? null : new PrimTextureResolverService(texDir);
            ThreeD = new ThreeDViewModel(new PrmMeshBuilderService(resolver));
            RaisePropertyChanged(nameof(ThreeD));
        }

        // ── Window title ──────────────────────────────────────────────────────

        private string _windowTitle = "Urban Chaos Prim Editor";
        public string WindowTitle
        {
            get => _windowTitle;
            private set { _windowTitle = value; RaisePropertyChanged(); }
        }

        // ── Status bar ────────────────────────────────────────────────────────

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; RaisePropertyChanged(); }
        }

        // ── Loaded file ───────────────────────────────────────────────────────

        private PrimFile? _loadedFile;
        public PrimFile? LoadedFile
        {
            get => _loadedFile;
            private set
            {
                _loadedFile = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsFileLoaded));
                RaisePropertyChanged(nameof(LoadedFileName));
                CommandManager.InvalidateRequerySuggested();
                UpdateWindowTitle();
            }
        }

        public bool IsFileLoaded => _loadedFile is not null;

        public string LoadedFileName => _loadedFile?.FileName ?? "—";

        // ── Panel visibility ──────────────────────────────────────────────────

        private bool _isFileListPanelOpen = true;
        public bool IsFileListPanelOpen
        {
            get => _isFileListPanelOpen;
            set { _isFileListPanelOpen = value; RaisePropertyChanged(); }
        }

        private bool _isPrimInfoPanelOpen = true;
        public bool IsPrimInfoPanelOpen
        {
            get => _isPrimInfoPanelOpen;
            set { _isPrimInfoPanelOpen = value; RaisePropertyChanged(); }
        }

        // ── File list (left panel) ────────────────────────────────────────────

        public ObservableCollection<PrimFileListItem> PrmFiles { get; } = [];

        private PrimFileListItem? _selectedPrmFile;
        public PrimFileListItem? SelectedPrmFile
        {
            get => _selectedPrmFile;
            set
            {
                _selectedPrmFile = value;
                RaisePropertyChanged();
                if (value is not null)
                    LoadPrimFromPath(value.FilePath);
            }
        }

        private string _directoryLabel = "No directory selected";
        public string DirectoryLabel
        {
            get => _directoryLabel;
            private set { _directoryLabel = value; RaisePropertyChanged(); }
        }

        private void RefreshFileList()
        {
            PrmFiles.Clear();

            var dir = PrimDirectoryService.Instance.PrmDirectory;
            if (dir is null)
            {
                DirectoryLabel = "No directory selected";
                return;
            }

            DirectoryLabel = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar,
                                                           Path.AltDirectorySeparatorChar))
                             ?? dir;

            foreach (var path in PrimDirectoryService.Instance.ScanForPrmFiles())
                PrmFiles.Add(new PrimFileListItem(path));

            StatusMessage = PrmFiles.Count == 0
                ? "No .prm files found in directory."
                : $"{PrmFiles.Count} .prm file(s) found.";
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand OpenFileCommand      { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand SetTextureDirCommand { get; }
        public ICommand SaveAsCommand        { get; }
        public ICommand SaveAsPrmCommand     { get; }
        public ICommand SaveAsObjCommand     { get; }

        private void OpenFile()
        {
            var ofd = new OpenFileDialog
            {
                Title     = "Open Prim File",
                Filter    = "Prim Files (*.prm)|*.prm|All Files (*.*)|*.*",
                DefaultExt = ".prm"
            };

            if (ofd.ShowDialog() != true) return;

            LoadPrimFromPath(ofd.FileName);
        }

        private void OpenDirectory()
        {
            // WPF has no built-in folder browser — use the WinForms one via UseWindowsForms,
            // or use the newer OpenFolderDialog available in .NET 8 WPF.
            var ofd = new OpenFolderDialog
            {
                Title = "Select PRM Directory"
            };

            if (ofd.ShowDialog() != true) return;

            PrimDirectoryService.Instance.PrmDirectory = ofd.FolderName;
        }

        private void SetTextureDirectory()
        {
            var ofd = new OpenFolderDialog
            {
                Title = "Select Prim Texture Directory (folder containing Tex###hi.tga files)",
                FolderName = PrimDirectoryService.Instance.TextureDirectory ?? string.Empty
            };
            if (ofd.ShowDialog() != true) return;

            PrimDirectoryService.Instance.TextureDirectory = ofd.FolderName;
            RebuildMeshBuilder();
            StatusMessage = $"Texture directory set: {ofd.FolderName}";

            // If a file is already loaded, reload it so textures apply immediately.
            if (_loadedFile is not null)
                LoadPrimFromPath(_loadedFile.FilePath);
        }

        private bool PromptForTextureDirIfNeeded()
        {
            if (PrimDirectoryService.Instance.TextureDirectory is not null) return true;

            var result = MessageBox.Show(
                "No texture directory is set.\n\n" +
                "Would you like to select the folder containing prim texture files (Tex###hi.tga) now?\n\n" +
                "If you skip this, models will render without textures.",
                "Texture Directory Not Set",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return false;

            SetTextureDirectory();
            return true; // proceed regardless — user may have set it or cancelled
        }

        private void LoadPrimFromPath(string path)
        {
            PromptForTextureDirIfNeeded();

            try
            {
                StatusMessage = $"Loading {Path.GetFileName(path)}…";
                var prim = PrimFile.FromPath(path);
                LoadedFile = prim;

                // Parse the binary mesh and update the 3D viewport.
                try
                {
                    var prmModel = _parser.Decode(prim.FileName, prim.RawBytes);
                    ThreeD.LoadModel(prmModel);
                    PrimInfo.Update(prmModel);
                    prim.VertexCount = prmModel.Points.Count;
                    prim.FaceCount   = prmModel.Triangles.Count + prmModel.Quadrangles.Count;
                    RaisePropertyChanged(nameof(LoadedFile));
                }
                catch (Exception parseEx)
                {
                    ThreeD.Clear();
                    PrimInfo.Clear();
                    StatusMessage = $"Loaded raw bytes; PRM parse failed: {parseEx.Message}";
                    return;
                }

                StatusMessage = $"Loaded: {prim.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load prim file:\n\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Load failed.";
            }
        }

        private void SaveAs()
        {
            // Delegate to PRM save by default
            SaveAsPrm();
        }

        private void SaveAsPrm()
        {
            if (_loadedFile is null) return;

            var sfd = new SaveFileDialog
            {
                Title      = "Save As PRM",
                Filter     = "Prim Files (*.prm)|*.prm|All Files (*.*)|*.*",
                DefaultExt = ".prm",
                FileName   = _loadedFile.FileName
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                // TODO: serialise parsed model back to binary.
                // For now, round-trip the raw bytes.
                File.WriteAllBytes(sfd.FileName, _loadedFile.RawBytes);
                _loadedFile.FilePath = sfd.FileName;
                _loadedFile.IsDirty  = false;
                StatusMessage = $"Saved: {Path.GetFileName(sfd.FileName)}";
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAsObj()
        {
            if (_loadedFile is null) return;

            var sfd = new SaveFileDialog
            {
                Title      = "Export As OBJ",
                Filter     = "Wavefront OBJ (*.obj)|*.obj|All Files (*.*)|*.*",
                DefaultExt = ".obj",
                FileName   = Path.GetFileNameWithoutExtension(_loadedFile.FileName)
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                // TODO: convert PrimFile geometry to OBJ format and write.
                var objContent = BuildObjStub(_loadedFile);
                File.WriteAllText(sfd.FileName, objContent);
                StatusMessage = $"Exported: {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export OBJ:\n\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string BuildObjStub(PrimFile prim)
        {
            // Minimal OBJ stub — replace with real geometry export once parsing is implemented.
            return $"# Exported from Urban Chaos Prim Editor\n" +
                   $"# Source: {prim.FileName}\n" +
                   $"# Vertices: {prim.VertexCount}\n" +
                   $"# Faces:    {prim.FaceCount}\n\n" +
                   $"# TODO: geometry data\n";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void UpdateWindowTitle()
        {
            WindowTitle = _loadedFile is null
                ? "Urban Chaos Prim Editor"
                : $"Urban Chaos Prim Editor — {_loadedFile.FileName}{(_loadedFile.IsDirty ? " *" : "")}";
        }

        private void UpdateStatus()
        {
            if (!IsFileLoaded)
                StatusMessage = "Ready. Open a .prm file or select a directory.";
        }
    }

    /// <summary>Lightweight list-item model for the file browser panel.</summary>
    public sealed class PrimFileListItem
    {
        public PrimFileListItem(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            FileSize = new FileInfo(filePath).Length;
        }

        public string FilePath { get; }
        public string FileName { get; }
        public long   FileSize { get; }
        public string FileSizeDisplay => FileSize < 1024
            ? $"{FileSize} B"
            : $"{FileSize / 1024.0:F1} KB";
    }
}

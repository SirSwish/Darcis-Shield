// ViewModels/MainWindowViewModel.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly PrmWriterService _writer = new();

        public MainWindowViewModel()
        {
            // Wire up PrimInfo property changes → dirty flag
            PrimInfo.ModelChanged += (_, _) =>
            {
                if (_loadedFile is not null) _loadedFile.IsDirty = true;
                UpdateWindowTitle();
            };

            // Wire up directory service
            PrimDirectoryService.Instance.DirectoryChanged       += (_, __) => RefreshFileList();
            PrimDirectoryService.Instance.TextureDirectoryChanged += (_, __) =>
            {
                RebuildMeshBuilder();
                RefreshTextureLibrary();
                RebuildScene();
            };

            // 3D scene
            RebuildMeshBuilder();

            // Commands
            NewFileCommand         = new RelayCommand(_ => NewFile());
            OpenFileCommand        = new RelayCommand(_ => OpenFile());
            ImportObjCommand       = new RelayCommand(_ => ImportObj());
            OpenDirectoryCommand   = new RelayCommand(_ => OpenDirectory());
            SetTextureDirCommand   = new RelayCommand(_ => SetTextureDirectory());
            ImportTextureCommand   = new RelayCommand(_ => ImportTexture());
            SaveCommand            = new RelayCommand(_ => Save(),      _ => IsFileLoaded);
            SaveAsCommand          = new RelayCommand(_ => SaveAs(),    _ => IsFileLoaded);
            SaveAsPrmCommand       = new RelayCommand(_ => SaveAsPrm(), _ => IsFileLoaded);
            SaveAsObjCommand       = new RelayCommand(_ => SaveAsObj(), _ => IsFileLoaded);
            ApplyTextureToFaceCommand    = new RelayCommand(_ => ApplyTextureToSelectedFace(), _ => SelectedFace is not null && SelectedTexture is not null);
            RotateTexture90Command       = new RelayCommand(_ => RotateSelectedFaceTexture(),  _ => SelectedFace is not null);
            ToggleTexturePaletteCommand  = new RelayCommand(_ => IsTexturePaletteOpen = !IsTexturePaletteOpen);

            SetToolSelectCommand      = new RelayCommand(_ => ActiveTool = PrimEditTool.Select);
            SetToolMoveCommand        = new RelayCommand(_ => ActiveTool = PrimEditTool.MovePoint,    _ => IsFileLoaded);
            SetToolAddPointCommand    = new RelayCommand(_ => SelectAddPointTool());
            SetToolNewTriangleCommand = new RelayCommand(_ => ActiveTool = PrimEditTool.NewTriangle,  _ => IsFileLoaded);
            SetToolNewQuadCommand     = new RelayCommand(_ => ActiveTool = PrimEditTool.NewQuad,      _ => IsFileLoaded);
            SetToolDeletePointCommand = new RelayCommand(_ => ActiveTool = PrimEditTool.DeletePoint,  _ => IsFileLoaded);

            // Load initial file list from persisted directory
            RefreshFileList();
            RefreshTextureLibrary();
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
        public ObservableCollection<PrmFaceListItem> Faces { get; } = [];
        public ObservableCollection<PrmTextureListItem> TextureLibrary { get; } = [];

        private PrmFaceListItem? _selectedFace;
        public PrmFaceListItem? SelectedFace
        {
            get => _selectedFace;
            set
            {
                if (_selectedFace == value) return;
                _selectedFace = value;
                RaisePropertyChanged();
                CommandManager.InvalidateRequerySuggested();
                RebuildScene();
            }
        }

        private PrmTextureListItem? _selectedTexture;
        public PrmTextureListItem? SelectedTexture
        {
            get => _selectedTexture;
            set
            {
                if (_selectedTexture == value) return;
                _selectedTexture = value;
                RaisePropertyChanged();
                TextureSelection = new Rect(0, 0, 1, 1);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // UV sub-region selection within the selected texture, in [0,1] normalised coords.
        private Rect _textureSelection = new(0, 0, 1, 1);
        public Rect TextureSelection
        {
            get => _textureSelection;
            set
            {
                _textureSelection = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(TextureSelectionDisplay));
            }
        }

        public string TextureSelectionDisplay
        {
            get
            {
                Rect r = _textureSelection;
                bool full = r.X < 0.001 && r.Y < 0.001 && r.Width > 0.998 && r.Height > 0.998;
                if (full) return "Full texture";
                return $"({r.X:P0}, {r.Y:P0}) → ({r.Right:P0}, {r.Bottom:P0})";
            }
        }

        // ── Edit-time model + state ──────────────────────────────────────────
        // CurrentModel is the parsed, in-memory PRM. Edits mutate it directly;
        // the 3D scene is rebuilt from it on every change. PRM serialisation
        // is not yet implemented — edits do not persist when saving.


        private bool _isTexturePaletteOpen = true;
        public bool IsTexturePaletteOpen
        {
            get => _isTexturePaletteOpen;
            set
            {
                if (_isTexturePaletteOpen == value) return;
                _isTexturePaletteOpen = value;
                RaisePropertyChanged();
            }
        }

        private PrmModel? _currentModel;
        public PrmModel? CurrentModel
        {
            get => _currentModel;
            private set { _currentModel = value; RaisePropertyChanged(); }
        }

        private PrimEditTool _activeTool = PrimEditTool.Select;
        public PrimEditTool ActiveTool
        {
            get => _activeTool;
            set
            {
                if (_activeTool == value) return;
                _activeTool = value;
                _faceBuildIds.Clear();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(ActiveToolDisplay));
                RaisePropertyChanged(nameof(IsToolSelect));
                RaisePropertyChanged(nameof(IsToolMove));
                RaisePropertyChanged(nameof(IsToolAddPoint));
                RaisePropertyChanged(nameof(IsToolNewTriangle));
                RaisePropertyChanged(nameof(IsToolNewQuad));
                RaisePropertyChanged(nameof(IsToolDeletePoint));
                RebuildScene();
                StatusMessage = $"Tool: {ActiveToolDisplay}";
            }
        }

        public string ActiveToolDisplay => _activeTool switch
        {
            PrimEditTool.Select       => "Select",
            PrimEditTool.MovePoint    => "Move Point",
            PrimEditTool.AddPoint     => "Add Point",
            PrimEditTool.NewTriangle  => "New Triangle (pick 3 points)",
            PrimEditTool.NewQuad      => "New Quad (pick 4 points)",
            PrimEditTool.DeletePoint  => "Delete Point",
            _                         => _activeTool.ToString(),
        };

        public bool IsToolSelect       => _activeTool == PrimEditTool.Select;
        public bool IsToolMove         => _activeTool == PrimEditTool.MovePoint;
        public bool IsToolAddPoint     => _activeTool == PrimEditTool.AddPoint;
        public bool IsToolNewTriangle  => _activeTool == PrimEditTool.NewTriangle;
        public bool IsToolNewQuad      => _activeTool == PrimEditTool.NewQuad;
        public bool IsToolDeletePoint  => _activeTool == PrimEditTool.DeletePoint;

        public void ReportAddPointProjectionFailed()
        {
            StatusMessage = "Could not place point from this view. Try rotating the camera slightly and click again.";
        }

        private int? _selectedPointId;
        public int? SelectedPointId
        {
            get => _selectedPointId;
            private set
            {
                if (_selectedPointId == value) return;
                _selectedPointId = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedPointDisplay));
            }
        }

        public string SelectedPointDisplay
        {
            get
            {
                if (_currentModel is null || _selectedPointId is null) return "—";
                int idx = _currentModel.Points.FindIndex(p => p.GlobalId == _selectedPointId.Value);
                if (idx < 0) return "—";
                PrmPoint p = _currentModel.Points[idx];
                return $"#{p.GlobalId}  ({p.X}, {p.Y}, {p.Z})";
            }
        }

        // Points being collected for an in-progress face build.
        private readonly List<int> _faceBuildIds = new();
        public IReadOnlyList<int> FaceBuildIds => _faceBuildIds;

        // ── Edit operations ──────────────────────────────────────────────────

        /// <summary>Update the screen state for a clicked-on point given the active tool.</summary>
        public void OnPointClicked(int globalId)
        {
            if (_currentModel is null) return;

            switch (_activeTool)
            {
                case PrimEditTool.Select:
                case PrimEditTool.MovePoint:
                    SelectedPointId = globalId;
                    StatusMessage = $"Selected point {SelectedPointDisplay}";
                    RebuildScene();
                    break;

                case PrimEditTool.NewTriangle:
                    AddFaceBuildPoint(globalId, target: 3);
                    break;

                case PrimEditTool.NewQuad:
                    AddFaceBuildPoint(globalId, target: 4);
                    break;

                case PrimEditTool.DeletePoint:
                    DeletePoint(globalId);
                    break;

                case PrimEditTool.AddPoint:
                    // AddPoint clicks are handled by the view (it computes 3D from the ray).
                    break;
            }
        }

        /// <summary>Move a point's coordinates and rebuild the scene. Called from drag logic.</summary>
        public void MovePointTo(int globalId, short x, short y, short z)
        {
            if (_currentModel is null) return;
            int idx = _currentModel.Points.FindIndex(p => p.GlobalId == globalId);
            if (idx < 0) return;
            _currentModel.Points[idx] = _currentModel.Points[idx] with { X = x, Y = y, Z = z };
            if (_loadedFile is not null) _loadedFile.IsDirty = true;
            UpdateWindowTitle();
            RaisePropertyChanged(nameof(SelectedPointDisplay));
            RebuildScene();
        }

        /// <summary>Add a new point at the given coordinates and select it.</summary>
        public int AddPoint(short x, short y, short z)
        {
            if (_currentModel is null) return -1;
            int newId = NextPointId();
            _currentModel.Points.Add(new PrmPoint(newId, x, y, z));
            if (_loadedFile is not null)
            {
                _loadedFile.IsDirty = true;
                _loadedFile.VertexCount = _currentModel.Points.Count;
            }
            UpdateWindowTitle();
            SelectedPointId = newId;
            StatusMessage = $"Added point #{newId} at ({x}, {y}, {z})";
            RebuildScene();
            return newId;
        }

        private void AddFaceBuildPoint(int globalId, int target)
        {
            if (_currentModel is null) return;
            if (_faceBuildIds.Contains(globalId))
            {
                StatusMessage = $"Point #{globalId} is already in the face — pick a different one.";
                return;
            }

            _faceBuildIds.Add(globalId);
            StatusMessage = $"Face build: {_faceBuildIds.Count}/{target} points selected.";

            if (_faceBuildIds.Count >= target)
            {
                if (target == 3)
                {
                    _currentModel.Triangles.Add(new PrmTriangle(
                        TexturePage: 0, Properties: 0,
                        PointAId: (short)_faceBuildIds[0],
                        PointBId: (short)_faceBuildIds[1],
                        PointCId: (short)_faceBuildIds[2],
                        UA: 0, VA: 0, UB: 0, VB: 0, UC: 0, VC: 0,
                        BrightA: 200, BrightB: 200, BrightC: 200));
                    StatusMessage = $"Triangle created from points {_faceBuildIds[0]}, {_faceBuildIds[1]}, {_faceBuildIds[2]}.";
                }
                else // 4 → quad
                {
                    _currentModel.Quadrangles.Add(new PrmQuadrangle(
                        TexturePage: 0, Properties: 0,
                        PointAId: (short)_faceBuildIds[0],
                        PointBId: (short)_faceBuildIds[1],
                        PointCId: (short)_faceBuildIds[2],
                        PointDId: (short)_faceBuildIds[3],
                        UA: 0, VA: 0, UB: 0, VB: 0, UC: 0, VC: 0, UD: 0, VD: 0,
                        BrightA: 200, BrightB: 200, BrightC: 200, BrightD: 200));
                    StatusMessage = $"Quad created from points {_faceBuildIds[0]}, {_faceBuildIds[1]}, {_faceBuildIds[2]}, {_faceBuildIds[3]}.";
                }
                _faceBuildIds.Clear();
                if (_loadedFile is not null)
                {
                    _loadedFile.IsDirty   = true;
                    _loadedFile.FaceCount = _currentModel.Triangles.Count + _currentModel.Quadrangles.Count;
                }
                RefreshFaceList();
                UpdateWindowTitle();
            }

            RebuildScene();
        }

        private void DeletePoint(int globalId)
        {
            if (_currentModel is null) return;

            // Refuse if any face still references this point.
            bool refTri = _currentModel.Triangles.Any(t =>
                t.PointAId == globalId || t.PointBId == globalId || t.PointCId == globalId);
            bool refQuad = _currentModel.Quadrangles.Any(q =>
                q.PointAId == globalId || q.PointBId == globalId || q.PointCId == globalId || q.PointDId == globalId);

            if (refTri || refQuad)
            {
                StatusMessage = $"Cannot delete point #{globalId} — it is still used by a face.";
                return;
            }

            int removed = _currentModel.Points.RemoveAll(p => p.GlobalId == globalId);
            if (removed == 0)
            {
                StatusMessage = $"Point #{globalId} not found.";
                return;
            }

            if (_selectedPointId == globalId) SelectedPointId = null;
            if (_loadedFile is not null)
            {
                _loadedFile.IsDirty     = true;
                _loadedFile.VertexCount = _currentModel.Points.Count;
            }
            UpdateWindowTitle();
            StatusMessage = $"Deleted point #{globalId}.";
            RebuildScene();
        }

        private int NextPointId()
        {
            if (_currentModel is null || _currentModel.Points.Count == 0)
                return 0;
            return _currentModel.Points.Max(p => p.GlobalId) + 1;
        }

        private void RebuildScene()
        {
            if (_currentModel is null) { ThreeD.Clear(); return; }
            SelectedFaceHint? faceHint = _selectedFace is null ? null
                : new SelectedFaceHint(_selectedFace.FaceType == PrmFaceType.Triangle, _selectedFace.Index);
            ThreeD.Rebuild(_currentModel, _selectedPointId, _faceBuildIds, faceHint);
        }

        // ── Panel visibility ──────────────────────────────────────────────────

        private void RefreshFaceList()
        {
            var prev = SelectedFace;   // capture before Clear() nulls it via the two-way ListBox binding

            Faces.Clear();
            if (_currentModel is null)
            {
                SelectedFace = null;
                return;
            }

            for (int i = 0; i < _currentModel.Triangles.Count; i++)
                Faces.Add(PrmFaceListItem.FromTriangle(i, _currentModel.Triangles[i]));

            for (int i = 0; i < _currentModel.Quadrangles.Count; i++)
                Faces.Add(PrmFaceListItem.FromQuadrangle(i, _currentModel.Quadrangles[i]));

            if (_loadedFile is not null)
            {
                _loadedFile.FaceCount = Faces.Count;
                _loadedFile.MaterialCount = Faces.Select(f => f.TextureId).Distinct().Count();
                RaisePropertyChanged(nameof(LoadedFile));
            }

            // Re-assign from the new collection so the ListBox SelectedItem reference stays valid.
            SelectedFace = prev is null
                ? Faces.FirstOrDefault()
                : Faces.FirstOrDefault(f => f.FaceType == prev.FaceType && f.Index == prev.Index)
                  ?? Faces.FirstOrDefault();
        }

        private void RefreshTextureLibrary()
        {
            TextureLibrary.Clear();

            string? textureDir = PrimDirectoryService.Instance.TextureDirectory;
            if (textureDir is null || !Directory.Exists(textureDir))
            {
                SelectedTexture = null;
                return;
            }

            foreach (string path in Directory.EnumerateFiles(textureDir, "Tex*hi.tga")
                         .Concat(Directory.EnumerateFiles(textureDir, "tex*hi.tga"))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                if (TryParseTextureId(Path.GetFileName(path), out int textureId))
                    TextureLibrary.Add(new PrmTextureListItem(textureId, path));
            }

            if (SelectedTexture is null || !TextureLibrary.Any(t => t.TextureId == SelectedTexture.TextureId))
                SelectedTexture = TextureLibrary.FirstOrDefault();
        }

        private void ImportTexture()
        {
            string? textureDir = PrimDirectoryService.Instance.TextureDirectory;
            if (textureDir is null)
            {
                MessageBox.Show(
                    "Set a texture directory first (File › Set Texture Directory…).",
                    "No Texture Directory", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title  = "Import Texture",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.tga;*.tif;*.tiff|All files|*.*",
            };
            if (ofd.ShowDialog() != true) return;

            System.Windows.Media.Imaging.BitmapSource? bitmap = LoadImageForImport(ofd.FileName);
            if (bitmap is null)
            {
                MessageBox.Show("Could not load the selected image file.", "Import Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Next free slot = highest existing ID + 1, or 1 if the directory is empty.
            int nextId   = TextureLibrary.Count > 0 ? TextureLibrary.Max(t => t.TextureId) + 1 : 1;
            string fileName = $"Tex{nextId:D3}hi.tga";
            string destPath = Path.Combine(textureDir, fileName);

            try
            {
                UrbanChaosPrimEditor.Services.TgaWriter.Write(destPath, bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to write texture:\n{ex.Message}", "Import Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RefreshTextureLibrary();
            SelectedTexture = TextureLibrary.FirstOrDefault(t => t.TextureId == nextId);
            StatusMessage   = $"Imported as {fileName}  (Tex ID {nextId}).";
        }

        private static System.Windows.Media.Imaging.BitmapSource? LoadImageForImport(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".tga")
                return UrbanChaosPrimEditor.Services.TgaLoader.Load(path);
            try
            {
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.UriSource   = new Uri(path, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        private void ApplyTextureToSelectedFace()
        {
            if (_currentModel is null || SelectedFace is null || SelectedTexture is null) return;

            TextureTile tile = TextureTileFromSelection(SelectedTexture.TextureId, _textureSelection);

            if (SelectedFace.FaceType == PrmFaceType.Triangle)
            {
                PrmTriangle tri = _currentModel.Triangles[SelectedFace.Index];
                _currentModel.Triangles[SelectedFace.Index] = tri with
                {
                    TexturePage = tile.TexturePage,
                    UA = tile.U0,
                    VA = tile.V0,
                    UB = tile.U1,
                    VB = tile.V1,
                    UC = tile.U3,
                    VC = tile.V3
                };
            }
            else
            {
                PrmQuadrangle quad = _currentModel.Quadrangles[SelectedFace.Index];
                _currentModel.Quadrangles[SelectedFace.Index] = quad with
                {
                    TexturePage = tile.TexturePage,
                    UA = tile.U0,
                    VA = tile.V0,
                    UB = tile.U1,
                    VB = tile.V1,
                    UC = tile.U2,
                    VC = tile.V2,
                    UD = tile.U3,
                    VD = tile.V3
                };
            }

            if (_loadedFile is not null)
                _loadedFile.IsDirty = true;

            string faceName = SelectedFace.DisplayName;
            RefreshFaceList();
            RebuildScene();
            UpdateWindowTitle();
            StatusMessage = $"Applied Tex{SelectedTexture.TextureId:D3}hi.tga to {faceName}.";
        }

        private void RotateSelectedFaceTexture()
        {
            if (_currentModel is null || SelectedFace is null) return;

            if (SelectedFace.FaceType == PrmFaceType.Triangle)
            {
                PrmTriangle t = _currentModel.Triangles[SelectedFace.Index];

                // Bounding-box centre in UV space.
                double minU = Math.Min(t.UA, Math.Min(t.UB, t.UC));
                double maxU = Math.Max(t.UA, Math.Max(t.UB, t.UC));
                double minV = Math.Min(t.VA, Math.Min(t.VB, t.VC));
                double maxV = Math.Max(t.VA, Math.Max(t.VB, t.VC));
                double cx = (minU + maxU) / 2.0;
                double cy = (minV + maxV) / 2.0;

                // 90° CW rotation: u' = cx + (v – cy),  v' = cy – (u – cx)
                _currentModel.Triangles[SelectedFace.Index] = t with
                {
                    UA = UvByte(cx + (t.VA - cy)), VA = UvByte(cy - (t.UA - cx)),
                    UB = UvByte(cx + (t.VB - cy)), VB = UvByte(cy - (t.UB - cx)),
                    UC = UvByte(cx + (t.VC - cy)), VC = UvByte(cy - (t.UC - cx)),
                };
            }
            else
            {
                PrmQuadrangle q = _currentModel.Quadrangles[SelectedFace.Index];

                double minU = Math.Min(Math.Min(q.UA, q.UB), Math.Min(q.UC, q.UD));
                double maxU = Math.Max(Math.Max(q.UA, q.UB), Math.Max(q.UC, q.UD));
                double minV = Math.Min(Math.Min(q.VA, q.VB), Math.Min(q.VC, q.VD));
                double maxV = Math.Max(Math.Max(q.VA, q.VB), Math.Max(q.VC, q.VD));
                double cx = (minU + maxU) / 2.0;
                double cy = (minV + maxV) / 2.0;

                _currentModel.Quadrangles[SelectedFace.Index] = q with
                {
                    UA = UvByte(cx + (q.VA - cy)), VA = UvByte(cy - (q.UA - cx)),
                    UB = UvByte(cx + (q.VB - cy)), VB = UvByte(cy - (q.UB - cx)),
                    UC = UvByte(cx + (q.VC - cy)), VC = UvByte(cy - (q.UC - cx)),
                    UD = UvByte(cx + (q.VD - cy)), VD = UvByte(cy - (q.UD - cx)),
                };
            }

            if (_loadedFile is not null) _loadedFile.IsDirty = true;
            RefreshFaceList();
            RebuildScene();
            UpdateWindowTitle();
            StatusMessage = $"Rotated texture UV 90° CW on {SelectedFace.DisplayName}.";
        }

        private static byte UvByte(double v) => (byte)Math.Clamp((int)Math.Round(v), 0, 255);

        /// <summary>
        /// Build a TextureTile using a sub-region of the texture in [0,1] normalised UV space.
        /// A full-texture selection is (0,0,1,1), matching the legacy FromTextureId output.
        /// </summary>
        private static TextureTile TextureTileFromSelection(int textureId, Rect selection)
        {
            const int FacePageOffset = 64 * 11;
            int page       = textureId + FacePageOffset;
            int texturePage = page / 64;
            int tileIdx    = page % 64;
            int tileU      = tileIdx % 8;
            int tileV      = tileIdx / 8;
            int baseU      = tileU * 32;
            int baseV      = tileV * 32;

            byte u0 = (byte)Math.Clamp((int)Math.Round(baseU + selection.Left   * 31), 0, 255);
            byte v0 = (byte)Math.Clamp((int)Math.Round(baseV + selection.Top    * 31), 0, 255);
            byte u1 = (byte)Math.Clamp((int)Math.Round(baseU + selection.Right  * 31), 0, 255);
            byte v1 = (byte)Math.Clamp((int)Math.Round(baseV + selection.Bottom * 31), 0, 255);

            return new TextureTile(
                (byte)texturePage,
                u0, v0,   // top-left     → vertex A
                u1, v0,   // top-right    → vertex B
                u1, v1,   // bottom-right → vertex C
                u0, v1);  // bottom-left  → vertex D
        }

        private static bool TryParseTextureId(string fileName, out int textureId)
        {
            textureId = 0;
            if (!fileName.StartsWith("Tex", StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith("hi.tga", StringComparison.OrdinalIgnoreCase) ||
                fileName.Length < 9)
            {
                return false;
            }

            string idText = fileName.Substring(3, fileName.Length - 9);
            return int.TryParse(idText, out textureId);
        }
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
                if (_selectedPrmFile == value) return;
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

        public ICommand NewFileCommand       { get; }
        public ICommand OpenFileCommand      { get; }
        public ICommand ImportObjCommand     { get; }
        public ICommand OpenDirectoryCommand { get; }
        public ICommand SetTextureDirCommand { get; }
        public ICommand ImportTextureCommand  { get; }
        public ICommand SaveCommand          { get; }
        public ICommand SaveAsCommand        { get; }
        public ICommand SaveAsPrmCommand     { get; }
        public ICommand SaveAsObjCommand     { get; }
        public ICommand ApplyTextureToFaceCommand { get; }
        public ICommand RotateTexture90Command    { get; }
        public ICommand ToggleTexturePaletteCommand { get; }

        public ICommand SetToolSelectCommand      { get; }
        public ICommand SetToolMoveCommand        { get; }
        public ICommand SetToolAddPointCommand    { get; }
        public ICommand SetToolNewTriangleCommand { get; }
        public ICommand SetToolNewQuadCommand     { get; }
        public ICommand SetToolDeletePointCommand { get; }

        private void NewFile()
        {
            var model = PrmTemplateService.CreateEmptyModel("untitled.prm", "untitled");
            LoadNewModelDocument(
                model,
                PrmTemplateService.CreateNprimHeader(model.Name),
                "untitled.prm",
                "New PRM file created. Add points/faces, then Save As PRM to write it.");
        }

        private void SelectAddPointTool()
        {
            if (_currentModel is null)
                NewFile();

            ActiveTool = PrimEditTool.AddPoint;
            StatusMessage = "Tool: Add Point. Click in the viewport to place a new point.";
        }

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

        private void ImportObj()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Import OBJ",
                Filter = "Wavefront OBJ (*.obj)|*.obj|All Files (*.*)|*.*",
                DefaultExt = ".obj"
            };

            if (ofd.ShowDialog() != true) return;

            try
            {
                var importer = new PrmObjImporterService();
                PrmObjImportResult result = importer.Import(ofd.FileName);
                string suggestedPrmName = Path.ChangeExtension(Path.GetFileName(ofd.FileName), ".prm");
                LoadNewModelDocument(
                    result.Model,
                    result.TemplatePrmBytes,
                    suggestedPrmName,
                    $"Imported OBJ: {Path.GetFileName(ofd.FileName)}. Save As PRM to write a .prm file.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import OBJ:\n\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "OBJ import failed.";
            }
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
            RefreshTextureLibrary();
            StatusMessage = $"Texture directory set: {ofd.FolderName}";

            // If a file is already loaded, reload it so textures apply immediately.
            if (_loadedFile is not null &&
                Path.IsPathFullyQualified(_loadedFile.FilePath) &&
                File.Exists(_loadedFile.FilePath))
            {
                LoadPrimFromPath(_loadedFile.FilePath);
            }
            else
            {
                RebuildScene();
            }
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
                    CurrentModel    = prmModel;
                    SelectedPointId = null;
                    _faceBuildIds.Clear();
                    ThreeD.LoadModel(prmModel, null, null);
                    PrimInfo.Update(prmModel);
                    prim.VertexCount = prmModel.Points.Count;
                    prim.FaceCount   = prmModel.Triangles.Count + prmModel.Quadrangles.Count;
                    RefreshFaceList();
                    RaisePropertyChanged(nameof(LoadedFile));
                    RaisePropertyChanged(nameof(SelectedPointDisplay));
                }
                catch (Exception parseEx)
                {
                    CurrentModel = null;
                    RefreshFaceList();
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

        private void Save()
        {
            if (_loadedFile is null) return;
            if (string.IsNullOrWhiteSpace(_loadedFile.FilePath) ||
                !Path.IsPathFullyQualified(_loadedFile.FilePath))
            {
                SaveAsPrm();
                return;
            }

            try
            {
                WritePrmToPath(_loadedFile.FilePath);
                StatusMessage = $"Saved: {_loadedFile.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                WritePrmToPath(sfd.FileName);
                StatusMessage = $"Saved: {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WritePrmToPath(string path)
        {
            if (_loadedFile is null) return;

            byte[] bytes = _currentModel is null
                ? _loadedFile.RawBytes
                : _writer.Encode(_currentModel, _loadedFile.RawBytes);

            File.WriteAllBytes(path, bytes);
            _loadedFile.FilePath = path;
            _loadedFile.RawBytes = bytes;
            _loadedFile.IsDirty = false;

            if (_currentModel is not null)
            {
                _currentModel.FileName = _loadedFile.FileName;
                _loadedFile.VertexCount = _currentModel.Points.Count;
                _loadedFile.FaceCount = _currentModel.Triangles.Count + _currentModel.Quadrangles.Count;
                PrimInfo.Update(_currentModel);
                RebuildScene();
            }

            RaisePropertyChanged(nameof(LoadedFile));
            RaisePropertyChanged(nameof(LoadedFileName));
            RaisePropertyChanged(nameof(SelectedPointDisplay));
            CommandManager.InvalidateRequerySuggested();
            UpdateWindowTitle();
        }

        private void LoadNewModelDocument(PrmModel model, byte[] templateBytes, string suggestedFileName, string statusMessage)
        {
            var prim = new PrimFile
            {
                FilePath = string.Empty,
                SuggestedFileName = suggestedFileName,
                RawBytes = templateBytes,
                VertexCount = model.Points.Count,
                FaceCount = model.Triangles.Count + model.Quadrangles.Count,
                IsDirty = true
            };

            SelectedPrmFile = null;
            LoadedFile = prim;
            CurrentModel = model;
            SelectedPointId = null;
            _faceBuildIds.Clear();
            ThreeD.LoadModel(model, null, null);
            PrimInfo.Update(model);
            RefreshFaceList();
            RaisePropertyChanged(nameof(LoadedFile));
            RaisePropertyChanged(nameof(LoadedFileName));
            RaisePropertyChanged(nameof(SelectedPointDisplay));
            CommandManager.InvalidateRequerySuggested();
            UpdateWindowTitle();
            StatusMessage = statusMessage;
        }

        private void SaveAsObj()
        {
            if (_loadedFile is null) return;
            if (_currentModel is null)
            {
                MessageBox.Show("This file was loaded as raw bytes and could not be parsed, so it cannot be exported as OBJ.",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                string? texDir = PrimDirectoryService.Instance.TextureDirectory;
                PrimTextureResolverService? resolver = texDir is null ? null : new PrimTextureResolverService(texDir);
                var exporter = new PrmObjExporterService(resolver);
                exporter.Export(_currentModel, sfd.FileName);
                StatusMessage = $"Exported: {Path.GetFileName(sfd.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export OBJ:\n\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

    public enum PrmFaceType
    {
        Triangle,
        Quadrangle
    }

    public sealed class PrmFaceListItem
    {
        private PrmFaceListItem(PrmFaceType faceType, int index, int textureId)
        {
            FaceType = faceType;
            Index = index;
            TextureId = textureId;
        }

        public PrmFaceType FaceType { get; }
        public int Index { get; }
        public int TextureId { get; }
        public string DisplayName => $"{(FaceType == PrmFaceType.Triangle ? "Tri" : "Quad")} #{Index}";
        public string DisplayText => $"{DisplayName}  Tex{TextureId:D3}";

        public static PrmFaceListItem FromTriangle(int index, PrmTriangle triangle)
        {
            var mapping = PrmUvService.CalculateTriangle(
                triangle.UA, triangle.VA,
                triangle.UB, triangle.VB,
                triangle.UC, triangle.VC,
                triangle.TexturePage);
            return new PrmFaceListItem(PrmFaceType.Triangle, index, mapping.TextureId);
        }

        public static PrmFaceListItem FromQuadrangle(int index, PrmQuadrangle quadrangle)
        {
            var mapping = PrmUvService.CalculateQuad(
                quadrangle.UA, quadrangle.VA,
                quadrangle.UB, quadrangle.VB,
                quadrangle.UC, quadrangle.VC,
                quadrangle.UD, quadrangle.VD,
                quadrangle.TexturePage);
            return new PrmFaceListItem(PrmFaceType.Quadrangle, index, mapping.TextureId);
        }
    }

    public sealed class PrmTextureListItem
    {
        public PrmTextureListItem(int textureId, string filePath)
        {
            TextureId = textureId;
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Thumbnail = UrbanChaosPrimEditor.Services.TgaLoader.Load(filePath);
        }

        public int TextureId { get; }
        public string FilePath { get; }
        public string FileName { get; }
        public string DisplayText => $"Tex{TextureId:D3}  {FileName}";
        public System.Windows.Media.Imaging.BitmapSource? Thumbnail { get; }
    }

    public readonly record struct TextureTile(byte TexturePage, byte U0, byte V0, byte U1, byte V1, byte U2, byte V2, byte U3, byte V3)
    {
        private const int FacePageOffset = 64 * 11;

        public static TextureTile FromTextureId(int textureId)
        {
            int page = textureId + FacePageOffset;
            int texturePage = page / 64;
            int tile = page % 64;
            int tileU = tile % 8;
            int tileV = tile / 8;
            int baseU = tileU * 32;
            int baseV = tileV * 32;

            if (texturePage is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException($"Texture id {textureId} maps to invalid texture page {texturePage}.");

            return new TextureTile(
                (byte)texturePage,
                (byte)baseU, (byte)baseV,
                (byte)(baseU + 31), (byte)baseV,
                (byte)(baseU + 31), (byte)(baseV + 31),
                (byte)baseU, (byte)(baseV + 31));
        }
    }
}

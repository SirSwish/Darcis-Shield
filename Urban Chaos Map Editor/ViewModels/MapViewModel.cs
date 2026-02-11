// /ViewModels/MapViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Resources;
using System.Windows.Media.Imaging;
using UrbanChaosMapEditor.Models;
using UrbanChaosMapEditor.Services;
using UrbanChaosMapEditor.Services.DataServices;
using UrbanChaosEditor.Shared.Services.Textures; // brings in TextureGroup
using UrbanChaosMapEditor.Views.Dialogs.Buildings;
using static UrbanChaosMapEditor.Models.PrimCatalog;
using static UrbanChaosMapEditor.Services.TexturesAccessor;

namespace UrbanChaosMapEditor.ViewModels
{
    // Thumbnail model used by the Textures tab
    public sealed class TextureThumb
    {
        public string RelativeKey { get; init; } = "";
        public ImageSource? Image { get; init; }
        public TextureGroup Group { get; init; }  // World / Shared / Prims
        public int Number { get; init; }          // parsed ### from key
    }

    // Add this simple model (public so XAML can see it)
    public sealed class PrimButton
    {
        public int Number { get; init; }                 // e.g. 228
        public string Title { get; init; } = "";         // e.g. "Fire Hydrant"
        public ImageSource? Icon { get; init; }          // button image
        public UrbanChaosMapEditor.Models.PrimButtonCategory Category { get; init; } // <- needed for XAML grouping
    }

    public sealed class PrimListItem : INotifyPropertyChanged
    {
        public int Index { get; set; }          // index in array
        public int MapWhoIndex { get; set; }    // 0..1023
        public int MapWhoRow { get; set; }
        public int MapWhoCol { get; set; }
        public byte PrimNumber { get; set; }
        public string Name { get; set; } = "";
        public short Y { get; set; }
        public byte X { get; set; }
        public byte Z { get; set; }
        public byte Yaw { get; set; }
        public byte Flags { get; set; }
        public byte InsideIndex { get; set; }

        private int _pixelX;
        public int PixelX { get => _pixelX; set { if (_pixelX != value) { _pixelX = value; OnPropertyChanged(nameof(PixelX)); } } }

        private int _pixelZ;
        public int PixelZ { get => _pixelZ; set { if (_pixelZ != value) { _pixelZ = value; OnPropertyChanged(nameof(PixelZ)); } } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string prop)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public sealed partial class MapViewModel : INotifyPropertyChanged
    {

        // === Map Modification Commands ===

        // Heights
        public ICommand RaiseHeightCommand { get; }
        public ICommand LowerHeightCommand { get; }
        public ICommand LevelHeightCommand { get; }
        public ICommand FlattenHeightCommand { get; }
        public ICommand DitchTemplateCommand { get; }
        public ICommand ClearToolCommand { get; }
        // Altitude
        public ICommand SetAltitudeCommand { get; }
        public ICommand SampleAltitudeCommand { get; }
        public ICommand ResetAltitudeCommand { get; }
        public ICommand DetectRoofCommand { get; }

        private IFacetMultiDrawWindow? _facetMultiDrawWindow;



        private bool _isMapLoaded;
        public bool IsMapLoaded
        {
            get => _isMapLoaded;
            private set
            {
                if (_isMapLoaded != value)
                {
                    _isMapLoaded = value;
                    OnPropertyChanged(nameof(IsMapLoaded));
                    CommandManager.InvalidateRequerySuggested(); // refresh CanExecute
                }
            }
        }


        // ===== View / overlays =====
        private double _zoom = 1.0;
        public double Zoom { get => _zoom; set { if (_zoom != value) { _zoom = value; OnPropertyChanged(); } } }

        private bool _showTextures = true;
        public bool ShowTextures { get => _showTextures; set { if (_showTextures != value) { _showTextures = value; OnPropertyChanged(); } } }

        private bool _showHeights = false;
        public bool ShowHeights { get => _showHeights; set { if (_showHeights != value) { _showHeights = value; OnPropertyChanged(); } } }

        private bool _showBuildings = true;
        public bool ShowBuildings { get => _showBuildings; set { if (_showBuildings != value) { _showBuildings = value; OnPropertyChanged(); } } }

        private bool _showObjects = true;
        public bool ShowWalkables { get => _showWalkables; set { if (_showWalkables != value) { _showWalkables = value; OnPropertyChanged(); } } }

        private bool _showWalkables = true;
        public bool ShowObjects { get => _showObjects; set { if (_showObjects != value) { _showObjects = value; OnPropertyChanged(); } } }

        private bool _showPrimGraphics = true;
        public bool ShowPrimGraphics { get => _showPrimGraphics; set { if (_showPrimGraphics != value) { _showPrimGraphics = value; OnPropertyChanged(); } } }

        private bool _showGridLines = true;
        public bool ShowGridLines { get => _showGridLines; set { if (_showGridLines != value) { _showGridLines = value; OnPropertyChanged(); } } }

        private bool _showMapWho = false;
        public bool ShowMapWho { get => _showMapWho; set { if (_showMapWho != value) { _showMapWho = value; OnPropertyChanged(); } } }

        // Facet redraw mode state
        private bool _isRedrawingFacet;
        private FacetPreviewWindow? _facetRedrawWindow;
        private int _facetRedrawId1;
        private (byte x, byte z)? _facetRedrawFirstPoint;
        private (int uiX0, int uiZ0, int uiX1, int uiZ1)? _facetRedrawPreviewLine;

        // Facet multi-draw mode state
        private bool _isMultiDrawingFacets;
        private AddWallWindow? _addFacetWindow;
        private FacetTemplate? _facetTemplate;
        private int _facetsAddedCount;
        private (byte x, byte z)? _multiDrawFirstPoint;
        private (int uiX0, int uiZ0, int uiX1, int uiZ1)? _multiDrawPreviewLine;

        // Door placement mode state
        private bool _isPlacingDoor;
        private AddDoorWindow? _addDoorWindow;
        private DoorTemplate? _doorTemplate;
        private (byte x, byte z)? _doorFirstPoint;
        private (int uiX0, int uiZ0, int uiX1, int uiZ1)? _doorPreviewLine;

        /// <summary>True when user is in door placement mode.</summary>
        public bool IsPlacingDoor
        {
            get => _isPlacingDoor;
            set { if (_isPlacingDoor != value) { _isPlacingDoor = value; OnPropertyChanged(); } }
        }

        /// <summary>Preview line for door placement.</summary>
        public (int uiX0, int uiZ0, int uiX1, int uiZ1)? DoorPreviewLine
        {
            get => _doorPreviewLine;
            set { if (_doorPreviewLine != value) { _doorPreviewLine = value; OnPropertyChanged(); } }
        }

        // Cable placement mode state
        private bool _isPlacingCable;
        private AddCableWindow? _addCableWindow;
        private (byte x, byte z)? _cableFirstPoint;
        private (int uiX0, int uiZ0, int uiX1, int uiZ1)? _cablePreviewLine;

        /// <summary>True when user is in cable placement mode.</summary>
        public bool IsPlacingCable
        {
            get => _isPlacingCable;
            set { if (_isPlacingCable != value) { _isPlacingCable = value; OnPropertyChanged(); } }
        }

        /// <summary>Preview line for cable placement.</summary>
        public (int uiX0, int uiZ0, int uiX1, int uiZ1)? CablePreviewLine
        {
            get => _cablePreviewLine;
            set { if (_cablePreviewLine != value) { _cablePreviewLine = value; OnPropertyChanged(); } }
        }

        // Ladder placement mode state
        private bool _isPlacingLadder;
        private AddLadderWindow? _addLadderWindow;
        private LadderTemplate? _ladderTemplate;
        private (byte x, byte z)? _ladderFirstPoint;
        private (int uiX0, int uiZ0, int uiX1, int uiZ1)? _ladderPreviewLine;

        /// <summary>True when user is in ladder placement mode.</summary>
        /// <summary>True when user is in ladder placement mode.</summary>
        public bool IsPlacingLadder
        {
            get => _isPlacingLadder;
            set { if (_isPlacingLadder != value) { _isPlacingLadder = value; OnPropertyChanged(); } }
        }

        /// <summary>Preview line for ladder placement (same as facet redraw).</summary>
        public (int uiX0, int uiZ0, int uiX1, int uiZ1)? LadderPreviewLine
        {
            get => _ladderPreviewLine;
            set { if (_ladderPreviewLine != value) { _ladderPreviewLine = value; OnPropertyChanged(); } }
        }


        /// <summary>True when user is in facet redraw mode.</summary>
        public bool IsRedrawingFacet
        {
            get => _isRedrawingFacet;
            set { if (_isRedrawingFacet != value) { _isRedrawingFacet = value; OnPropertyChanged(); } }
        }

        /// <summary>True when user is in facet multi-draw mode (adding new facets).</summary>
        public bool IsMultiDrawingFacets
        {
            get => _isMultiDrawingFacets;
            set { if (_isMultiDrawingFacets != value) { _isMultiDrawingFacets = value; OnPropertyChanged(); } }
        }

        /// <summary>Preview line for multi-draw mode (same rendering as single redraw).</summary>
        public (int uiX0, int uiZ0, int uiX1, int uiZ1)? MultiDrawPreviewLine
        {
            get => _multiDrawPreviewLine;
            set { if (_multiDrawPreviewLine != value) { _multiDrawPreviewLine = value; OnPropertyChanged(); } }
        }


        public (int uiX0, int uiZ0, int uiX1, int uiZ1)? FacetRedrawPreviewLine
        {
            get => _facetRedrawPreviewLine;
            set { if (_facetRedrawPreviewLine != value) { _facetRedrawPreviewLine = value; OnPropertyChanged(); } }
        }

        // ===== Cursor (pixels + tiles) =====
        private int _cursorX;
        public int CursorX { get => _cursorX; set { if (_cursorX != value) { _cursorX = value; OnPropertyChanged(); } } }

        private int _cursorZ;
        public int CursorZ { get => _cursorZ; set { if (_cursorZ != value) { _cursorZ = value; OnPropertyChanged(); } } }

        private int _cursorTileX;
        public int CursorTileX { get => _cursorTileX; set { if (_cursorTileX != value) { _cursorTileX = value; OnPropertyChanged(); } } }

        private int _cursorTileY;
        public int CursorTileZ { get => _cursorTileY; set { if (_cursorTileY != value) { _cursorTileY = value; OnPropertyChanged(); } } }


        // ===== Unified tool selection =====
        private EditorTool _selectedTool = EditorTool.None;
        public EditorTool SelectedTool
        {
            get => _selectedTool;
            set { if (_selectedTool != value) { _selectedTool = value; OnPropertyChanged(); } }
        }

        // ===== Heights editing =====
        private int _heightStep = 1; // positive, non-zero
        public int HeightStep
        {
            get => _heightStep;
            set
            {
                var v = value <= 0 ? 1 : value;
                if (_heightStep != v) { _heightStep = v; OnPropertyChanged(); }
            }
        }

        private int _brushSize = 1; // 1..10
        public int BrushSize
        {
            get => _brushSize;
            set
            {
                var v = value < 1 ? 1 : (value > 10 ? 10 : value);
                if (_brushSize != v) { _brushSize = v; OnPropertyChanged(); }
            }
        }

        // ===== Cell Altitude editing =====
        private int _targetAltitude = 0; // world altitude value
        public int TargetAltitude
        {
            get => _targetAltitude;
            set
            {
                if (_targetAltitude != value)
                {
                    _targetAltitude = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TargetAltitudeRaw));
                }
            }
        }

        /// <summary>
        /// Raw altitude value as stored in file (TargetAltitude >> 3).
        /// Read-only display property.
        /// </summary>
        public int TargetAltitudeRaw => TargetAltitude >> 3; // PAP_ALT_SHIFT = 3

        // ===== Altitude painting state (for rectangle selection and overlay) =====
        private bool _isSettingAltitude;
        public bool IsSettingAltitude
        {
            get => _isSettingAltitude;
            set
            {
                if (_isSettingAltitude != value)
                {
                    _isSettingAltitude = value;
                    OnPropertyChanged();
                }
            }
        }

        // Rectangle selection: start corner (set on mouse down)
        private int _altitudeSelectionStartX = -1;
        private int _altitudeSelectionStartY = -1;
        public int AltitudeSelectionStartX
        {
            get => _altitudeSelectionStartX;
            set { _altitudeSelectionStartX = value; OnPropertyChanged(); }
        }
        public int AltitudeSelectionStartY
        {
            get => _altitudeSelectionStartY;
            set { _altitudeSelectionStartY = value; OnPropertyChanged(); }
        }

        // Rectangle selection: current corner (updated on mouse move)
        private int _altitudeSelectionEndX = -1;
        private int _altitudeSelectionEndY = -1;
        public int AltitudeSelectionEndX
        {
            get => _altitudeSelectionEndX;
            set { _altitudeSelectionEndX = value; OnPropertyChanged(); }
        }
        public int AltitudeSelectionEndY
        {
            get => _altitudeSelectionEndY;
            set { _altitudeSelectionEndY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the normalized selection rectangle (min/max corners).
        /// Returns null if no valid selection.
        /// </summary>
        public (int MinX, int MinY, int MaxX, int MaxY)? GetAltitudeSelectionRect()
        {
            if (!IsSettingAltitude ||
                AltitudeSelectionStartX < 0 || AltitudeSelectionStartY < 0 ||
                AltitudeSelectionEndX < 0 || AltitudeSelectionEndY < 0)
                return null;

            int minX = Math.Min(AltitudeSelectionStartX, AltitudeSelectionEndX);
            int maxX = Math.Max(AltitudeSelectionStartX, AltitudeSelectionEndX);
            int minY = Math.Min(AltitudeSelectionStartY, AltitudeSelectionEndY);
            int maxY = Math.Max(AltitudeSelectionStartY, AltitudeSelectionEndY);

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Clears the altitude selection state.
        /// </summary>
        public void ClearAltitudeSelection()
        {
            IsSettingAltitude = false;
            AltitudeSelectionStartX = -1;
            AltitudeSelectionStartY = -1;
            AltitudeSelectionEndX = -1;
            AltitudeSelectionEndY = -1;
        }

        private HashSet<(int, int)>? _altitudePaintedTiles;
        public HashSet<(int, int)>? AltitudePaintedTiles
        {
            get => _altitudePaintedTiles;
            set
            {
                _altitudePaintedTiles = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Call this after adding tiles to AltitudePaintedTiles to trigger overlay repaint.
        /// </summary>
        public void NotifyAltitudePaintedTilesChanged()
        {
            OnPropertyChanged(nameof(AltitudePaintedTiles));
        }


        private AltitudeAccessor? _altitudeAccessor;
        public AltitudeAccessor? AltitudeAccessor => _altitudeAccessor;

        #region Walkable Drawing Selection

        private bool _isDrawingWalkable;
        public bool IsDrawingWalkable
        {
            get => _isDrawingWalkable;
            set { _isDrawingWalkable = value; OnPropertyChanged(); }
        }

        private int _walkableSelectionStartX = -1;
        public int WalkableSelectionStartX
        {
            get => _walkableSelectionStartX;
            set { _walkableSelectionStartX = value; OnPropertyChanged(); }
        }

        private int _walkableSelectionStartY = -1;
        public int WalkableSelectionStartY
        {
            get => _walkableSelectionStartY;
            set { _walkableSelectionStartY = value; OnPropertyChanged(); }
        }

        private int _walkableSelectionEndX = -1;
        public int WalkableSelectionEndX
        {
            get => _walkableSelectionEndX;
            set { _walkableSelectionEndX = value; OnPropertyChanged(); }
        }

        private int _walkableSelectionEndY = -1;
        public int WalkableSelectionEndY
        {
            get => _walkableSelectionEndY;
            set { _walkableSelectionEndY = value; OnPropertyChanged(); }
        }

        public (int MinX, int MinY, int MaxX, int MaxY)? GetWalkableSelectionRect()
        {
            if (_walkableSelectionStartX < 0 || _walkableSelectionStartY < 0) return null;

            int minX = Math.Min(_walkableSelectionStartX, _walkableSelectionEndX);
            int minY = Math.Min(_walkableSelectionStartY, _walkableSelectionEndY);
            int maxX = Math.Max(_walkableSelectionStartX, _walkableSelectionEndX);
            int maxY = Math.Max(_walkableSelectionStartY, _walkableSelectionEndY);

            return (minX, minY, maxX, maxY);
        }

        public void ClearWalkableSelection()
        {
            IsDrawingWalkable = false;
            WalkableSelectionStartX = -1;
            WalkableSelectionStartY = -1;
            WalkableSelectionEndX = -1;
            WalkableSelectionEndY = -1;
        }

        #endregion

        // ===== Textures: world / set (beta) / selection =====
        private int _textureWorld;
        public int TextureWorld
        {
            get => _textureWorld;
            set
            {
                if (_textureWorld == value) return;
                _textureWorld = value;
                OnPropertyChanged();

                // write-through to bytes if a map is loaded, then refresh lists
                try
                {
                    if (MapDataService.Instance.IsLoaded)
                    {
                        var acc = new TexturesAccessor(MapDataService.Instance);
                        acc.WriteTextureWorld(_textureWorld);
                    }
                }
                catch { /* non-fatal */ }

                RefreshTextureLists();
                TexturesChangeBus.Instance.NotifyChanged();
                LoadStylesForCurrentWorldAsync();
            }
        }

        private bool _useBetaTextures; // false = release, true = beta
        public bool UseBetaTextures
        {
            get => _useBetaTextures;
            set
            {
                if (_useBetaTextures == value) return;
                _useBetaTextures = value;
                OnPropertyChanged();

                TextureCacheService.Instance.ActiveSet = _useBetaTextures ? "beta" : "release";
                RefreshTextureLists();
                TexturesChangeBus.Instance.NotifyChanged();
                LoadStylesForCurrentWorldAsync();
            }
        }

        // Selection used by PaintTexture tool
        public TextureGroup SelectedTextureGroup { get; set; } = TextureGroup.World;
        public int SelectedTextureNumber { get; set; } = 0;

        private int _selectedRotationIndex = 2; // 0:180, 1:90, 2:0, 3:270 (v1 scheme)
        public int SelectedRotationIndex
        {
            get => _selectedRotationIndex;
            set
            {
                var v = ((value % 4) + 4) % 4;
                if (_selectedRotationIndex != v) { _selectedRotationIndex = v; OnPropertyChanged(); }
            }
        }

        // ===== Texture painting state (for rectangle selection) =====
        private bool _isPaintingTexture;
        public bool IsPaintingTexture
        {
            get => _isPaintingTexture;
            set
            {
                if (_isPaintingTexture != value)
                {
                    _isPaintingTexture = value;
                    OnPropertyChanged();
                }
            }
        }

        // Rectangle selection for texture painting: start corner (set on mouse down)
        private int _textureSelectionStartX = -1;
        private int _textureSelectionStartY = -1;
        public int TextureSelectionStartX
        {
            get => _textureSelectionStartX;
            set { _textureSelectionStartX = value; OnPropertyChanged(); }
        }
        public int TextureSelectionStartY
        {
            get => _textureSelectionStartY;
            set { _textureSelectionStartY = value; OnPropertyChanged(); }
        }

        // Rectangle selection for texture painting: current corner (updated on mouse move)
        private int _textureSelectionEndX = -1;
        private int _textureSelectionEndY = -1;
        public int TextureSelectionEndX
        {
            get => _textureSelectionEndX;
            set { _textureSelectionEndX = value; OnPropertyChanged(); }
        }
        public int TextureSelectionEndY
        {
            get => _textureSelectionEndY;
            set { _textureSelectionEndY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets the normalized texture selection rectangle (min/max corners).
        /// Returns null if no valid selection.
        /// </summary>
        public (int MinX, int MinY, int MaxX, int MaxY)? GetTextureSelectionRect()
        {
            if (!IsPaintingTexture ||
                TextureSelectionStartX < 0 || TextureSelectionStartY < 0 ||
                TextureSelectionEndX < 0 || TextureSelectionEndY < 0)
                return null;

            int minX = Math.Min(TextureSelectionStartX, TextureSelectionEndX);
            int maxX = Math.Max(TextureSelectionStartX, TextureSelectionEndX);
            int minY = Math.Min(TextureSelectionStartY, TextureSelectionEndY);
            int maxY = Math.Max(TextureSelectionStartY, TextureSelectionEndY);

            return (minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Clears the texture selection state.
        /// </summary>
        public void ClearTextureSelection()
        {
            IsPaintingTexture = false;
            TextureSelectionStartX = -1;
            TextureSelectionStartY = -1;
            TextureSelectionEndX = -1;
            TextureSelectionEndY = -1;
        }

        private PrimListItem? _selectedPrim;
        public PrimListItem? SelectedPrim
        {
            get => _selectedPrim;
            set
            {
                if (!ReferenceEquals(_selectedPrim, value))
                {
                    _selectedPrim = value;
                    OnPropertyChanged();
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested(); // <-- add this
                }
            }
        }

        private PrimListItem? _dragPreviewPrim;
        public PrimListItem? DragPreviewPrim
        {
            get => _dragPreviewPrim;
            set { _dragPreviewPrim = value; OnPropertyChanged(); }
        }
        // Selected prim number when placing (−1 = none)
        private int _newPrimNumber = -1;
        public int NewPrimNumber
        {
            get => _newPrimNumber;
            set { if (_newPrimNumber != value) { _newPrimNumber = value; OnPropertyChanged(); } }
        }

        private int _selectedBuildingId;
        public int SelectedBuildingId
        {
            get => _selectedBuildingId;
            set { if (_selectedBuildingId != value) { _selectedBuildingId = value; OnPropertyChanged(); } }
        }

        private int _selectedWalkableId1;
        public int SelectedWalkableId1
        {
            get => _selectedWalkableId1;
            set
            {
                if (_selectedWalkableId1 == value) return;
                _selectedWalkableId1 = value;
                OnPropertyChanged();
            }
        }


        private int? _selectedStoreyId;
        public int? SelectedStoreyId
        {
            get => _selectedStoreyId;
            set { if (_selectedStoreyId != value) { _selectedStoreyId = value; OnPropertyChanged(); } }
        }
        private int? _selectedFacetId;  // null = don’t facet-highlight
        public int? SelectedFacetId
        {
            get => _selectedFacetId;
            set { if (_selectedFacetId != value) { _selectedFacetId = value; OnPropertyChanged(); } }
        }

        // ===== Texture palettes shown in the Textures tab =====
        public ObservableCollection<TextureThumb> WorldTextures { get; } = new();
        public ObservableCollection<TextureThumb> SharedTextures { get; } = new();
        public ObservableCollection<TextureThumb> PrimTextures { get; } = new();
        public ObservableCollection<PrimListItem> Prims { get; } = new();

        // Palette buckets
        public ObservableCollection<PrimButton> CityAssets { get; } = new();
        public ObservableCollection<PrimButton> Nature { get; } = new();
        public ObservableCollection<PrimButton> Vehicles { get; } = new();
        public ObservableCollection<PrimButton> Weapons { get; } = new();
        public ObservableCollection<PrimButton> Structures { get; } = new();
        public ObservableCollection<PrimButton> Misc { get; } = new();

        public ObservableCollection<PrimButton> PrimButtons { get; } = new();

        // NEW: constructor — put the two lines here
        public MapViewModel()
        {
            System.Diagnostics.Debug.WriteLine("[MapVM] ctor: starting up…");

            var mapSvc = MapDataService.Instance;
            IsMapLoaded = mapSvc.IsLoaded;

            mapSvc.MapLoaded += (_, __) => IsMapLoaded = true;
            mapSvc.MapCleared += (_, __) => IsMapLoaded = false;
            // if bytes reset implies “still loaded”, reflect it:
            mapSvc.MapBytesReset += (_, __) => IsMapLoaded = mapSvc.IsLoaded;

            RaiseHeightCommand = new RelayCommand(_ => SelectedTool = EditorTool.RaiseHeight, _ => IsMapLoaded);
            LowerHeightCommand = new RelayCommand(_ => SelectedTool = EditorTool.LowerHeight, _ => IsMapLoaded);
            LevelHeightCommand = new RelayCommand(_ => SelectedTool = EditorTool.LevelHeight, _ => IsMapLoaded);
            FlattenHeightCommand = new RelayCommand(_ => SelectedTool = EditorTool.FlattenHeight, _ => IsMapLoaded);
            DitchTemplateCommand = new RelayCommand(_ => SelectedTool = EditorTool.DitchTemplate, _ => IsMapLoaded);
            ClearToolCommand = new RelayCommand(_ => SelectedTool = EditorTool.None, _ => IsMapLoaded);
            SetAltitudeCommand = new RelayCommand(_ => SelectedTool = EditorTool.SetAltitude, _ => IsMapLoaded);
            SampleAltitudeCommand = new RelayCommand(_ => SelectedTool = EditorTool.SampleAltitude, _ => IsMapLoaded);
            ResetAltitudeCommand = new RelayCommand(_ => SelectedTool = EditorTool.ResetAltitude, _ => IsMapLoaded);
            DetectRoofCommand = new RelayCommand(_ => SelectedTool = EditorTool.DetectRoof, _ => IsMapLoaded);

            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IsMapLoaded))
                    CommandManager.InvalidateRequerySuggested(); // refresh CanExecute
            };
        }

        // Placement state
        private bool _isPlacingPrim;
        public bool IsPlacingPrim
        {
            get => _isPlacingPrim;
            set { if (_isPlacingPrim != value) { _isPlacingPrim = value; OnPropertyChanged(); } }
        }

        private int _primNumberToPlace;
        public int PrimNumberToPlace
        {
            get => _primNumberToPlace;
            set
            {
                if (_primNumberToPlace == value) return;
                _primNumberToPlace = value;
                OnPropertyChanged();
            }
        }


        // Called when you click a palette button
        public void BeginPlacePrim(int primNumber)
        {
            PrimNumberToPlace = primNumber;
            IsPlacingPrim = true;
            DragPreviewPrim = null;   // ghost will be driven by MapView mouse move
        }
        // Cancel placement (Esc / right-click)
        public void CancelPlacePrim()
        {
            PrimNumberToPlace = -1;
            IsPlacingPrim = false;
            DragPreviewPrim = null;
        }

 
        /// <summary>Rebuilds the three texture lists from the cache for the current world/set.</summary>
        public void RefreshTextureLists()
        {
            WorldTextures.Clear();
            SharedTextures.Clear();
            PrimTextures.Clear();

            var cache = TextureCacheService.Instance;

            // WORLD depends on current world number
            var worldFolder = $"world{_textureWorld}";
            foreach (var key in cache.EnumerateRelativeKeys(worldFolder))
            {
                if (!cache.TryGetRelative(key, out var img) || img is null) continue;
                var num = ParseNumber(key);
                WorldTextures.Add(new TextureThumb { RelativeKey = key, Image = img, Group = TextureGroup.World, Number = num });
            }

            // SHARED
            foreach (var key in cache.EnumerateRelativeKeys("shared"))
            {
                if (!cache.TryGetRelative(key, out var img) || img is null) continue;
                var num = ParseNumber(key);
                SharedTextures.Add(new TextureThumb { RelativeKey = key, Image = img, Group = TextureGroup.Shared, Number = num });
            }

            // PRIMS
            foreach (var key in cache.EnumerateRelativeKeys("shared_prims"))
            {
                if (!cache.TryGetRelative(key, out var img) || img is null) continue;
                var num = ParseNumber(key);
                PrimTextures.Add(new TextureThumb { RelativeKey = key, Image = img, Group = TextureGroup.Prims, Number = num });
            }
        }

        /// <summary>
        /// Begins ladder placement mode. Called by AddLadderWindow when user clicks "Place on Map".
        /// </summary>
        public void BeginLadderPlacement(AddLadderWindow window, LadderTemplate template)
        {
            _addLadderWindow = window;
            _ladderTemplate = template;
            _ladderFirstPoint = null;
            LadderPreviewLine = null;
            IsPlacingLadder = true;
        }


        /// <summary>
        /// Called by MapView when user clicks during ladder placement mode.
        /// Two clicks required: first = start, second = end (must be within 1 cell).
        /// Returns true if the click was handled.
        /// </summary>
        public bool HandleLadderPlacementClick(int uiX, int uiZ)
        {
            if (!IsPlacingLadder || _ladderTemplate == null)
                return false;

            // Snap to nearest vertex (64px grid)
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Convert to tile coords (game uses bottom-right origin)
            byte tileX = (byte)Math.Clamp(128 - (snappedUiX / 64), 0, 127);
            byte tileZ = (byte)Math.Clamp(128 - (snappedUiZ / 64), 0, 127);

            if (_ladderFirstPoint == null)
            {
                // First click - store start point
                _ladderFirstPoint = (tileX, tileZ);
                LadderPreviewLine = (snappedUiX, snappedUiZ, snappedUiX, snappedUiZ);

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Ladder start: ({tileX},{tileZ}). Click end point (must be within 1 cell). Right-click to cancel.";
                }
                return true;
            }
            else
            {
                // Second click - validate distance and complete
                byte x0 = _ladderFirstPoint.Value.x;
                byte z0 = _ladderFirstPoint.Value.z;
                byte x1 = tileX;
                byte z1 = tileZ;

                // Calculate distance in cells
                int deltaX = Math.Abs((int)x1 - (int)x0);
                int deltaZ = Math.Abs((int)z1 - (int)z0);

                // Ladders must be exactly 1 cell in ONE direction and 0 in the other
                // Valid: (1,0) or (0,1) - i.e., horizontal or vertical, 1 cell long
                bool isValid = (deltaX == 1 && deltaZ == 0) || (deltaX == 0 && deltaZ == 1);

                if (!isValid)
                {
                    // Invalid - show error but don't cancel, let them try again
                    string errorMsg;
                    if (deltaX == 0 && deltaZ == 0)
                    {
                        errorMsg = "Start and end points are the same. Ladder must be 1 cell long.";
                    }
                    else if (deltaX > 1 || deltaZ > 1)
                    {
                        errorMsg = $"Ladder is {Math.Max(deltaX, deltaZ)} cells long. Maximum is 1 cell.";
                    }
                    else
                    {
                        errorMsg = $"Ladder must be horizontal OR vertical, not diagonal. Distance: ({deltaX},{deltaZ})";
                    }

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"⚠️ {errorMsg} Try again.";
                    }

                    // Reset first point so they can start over
                    _ladderFirstPoint = null;
                    LadderPreviewLine = null;
                    return true;
                }

                // Valid ladder - create it
                var facetTemplate = new FacetTemplate
                {
                    Type = FacetType.Ladder,
                    Height = _ladderTemplate.Height,
                    FHeight = _ladderTemplate.FHeight,
                    BlockHeight = _ladderTemplate.BlockHeight,
                    Y0 = _ladderTemplate.Y0,
                    Y1 = _ladderTemplate.Y1,
                    Flags = 0, // Ladders typically have no flags
                    BuildingId1 = _ladderTemplate.BuildingId1,
                    Storey = _ladderTemplate.Storey
                };

                // Commit the ladder
                var adder = new BuildingAdder(MapDataService.Instance);
                var coords = new List<(byte, byte, byte, byte)> { (x0, z0, x1, z1) };
                var result = adder.TryAddFacets(_ladderTemplate.BuildingId1, coords, facetTemplate);

                if (result.IsSuccess)
                {
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Ladder added at ({x0},{z0})->({x1},{z1}) to Building #{_ladderTemplate.BuildingId1}.";
                    }

                    MessageBox.Show($"Ladder added successfully to Building #{_ladderTemplate.BuildingId1}.\n\n" +
                        $"Position: ({x0},{z0}) → ({x1},{z1})\n\n" +
                        $"Note: The game will transform these coordinates (~67% scaling + perpendicular offset).",
                        "Ladder Added", MessageBoxButton.OK, MessageBoxImage.Information);

                    EndLadderPlacement(true);
                }
                else
                {
                    MessageBox.Show($"Failed to add ladder:\n\n{result.ErrorMessage}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Reset to let them try again
                    _ladderFirstPoint = null;
                    LadderPreviewLine = null;
                }

                return true;
            }
        }


        /// <summary>
        /// Updates the preview line endpoint as mouse moves during ladder placement.
        /// </summary>
        public void UpdateLadderPlacementPreview(int uiX, int uiZ)
        {
            if (!IsPlacingLadder || _ladderFirstPoint == null)
                return;

            // Snap to nearest vertex
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Get the first point in UI coords
            int firstUiX = (128 - _ladderFirstPoint.Value.x) * 64;
            int firstUiZ = (128 - _ladderFirstPoint.Value.z) * 64;

            LadderPreviewLine = (firstUiX, firstUiZ, snappedUiX, snappedUiZ);
        }

        /// <summary>
        /// Cancels ladder placement mode (right-click or escape).
        /// </summary>
        public void CancelLadderPlacement()
        {
            if (!IsPlacingLadder)
                return;

            EndLadderPlacement(false);

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.StatusMessage = "Ladder placement cancelled.";
            }
        }

        private void EndLadderPlacement(bool success)
        {
            IsPlacingLadder = false;
            LadderPreviewLine = null;
            _ladderFirstPoint = null;

            if (_addLadderWindow != null)
            {
                if (success)
                    _addLadderWindow.OnPlacementCompleted();
                else
                    _addLadderWindow.OnPlacementCancelled();
            }

            _addLadderWindow = null;
            _ladderTemplate = null;
        }

        public void RefreshPrimsList()
        {
            Prims.Clear();
            var acc = new ObjectsAccessor(MapDataService.Instance);
            var snap = acc.ReadSnapshot();
            if (snap.Prims.Length == 0 || snap.MapWho.Length != 1024) return;

            for (int i = 0; i < snap.Prims.Length; i++)
            {
                var p = snap.Prims[i];
                int cell = p.MapWhoIndex;
                if (cell < 0 || cell > 1023) continue;

                ObjectSpace.GameIndexToUiRowCol(cell, out int uiRow, out int uiCol);
                ObjectSpace.GamePrimToUiPixels(cell, p.X, p.Z, out int pixelX, out int pixelZ);

                Prims.Add(new PrimListItem
                {
                    Index = i,
                    MapWhoIndex = cell,
                    MapWhoRow = uiRow,        // store UI row/col for display (optional)
                    MapWhoCol = uiCol,
                    PrimNumber = p.PrimNumber,
                    Name = PrimCatalog.GetName(p.PrimNumber),
                    Y = p.Y,
                    X = p.X,
                    Z = p.Z,
                    Yaw = p.Yaw,
                    Flags = p.Flags,
                    InsideIndex = p.InsideIndex,
                    PixelX = pixelX,
                    PixelZ = pixelZ
                });
            }

            System.Diagnostics.Debug.WriteLine($"[MapVM] RefreshPrimsList: added {Prims.Count} items");
        }

        // Build the palette (load /Assets/Images/Buttons/###.png if present)
        public void BuildPrimPalette()
        {
            PrimButtons.Clear();

            for (int n = 1; n <= 255; n++)
            {
                var title = PrimCatalog.GetName(n);
                var cat = PrimCatalog.TryGetCategory(n, out var c)
                            ? c
                            : PrimButtonCategory.Misc;

                PrimButtons.Add(new PrimButton
                {
                    Number = n,
                    Title = title,                 // <-- was Name
                    Icon = TryLoadPrimButtonImage(n),
                    Category = cat
                });
            }
        }
        private void BuildPrimButtons()
        {
            PrimButtons.Clear();

            for (int num = 1; num <= 255; num++)
            {
                // Get category; fall back to Misc if not mapped
                var cat = PrimCatalog.TryGetCategory(num, out var c)
                          ? c
                          : PrimButtonCategory.Misc;

                PrimButtons.Add(new PrimButton
                {
                    Number = num,
                    Title = PrimCatalog.GetName(num),
                    Icon = TryLoadPrimButtonImage(num),
                    Category = cat                           // <- IMPORTANT
                });
            }
        }
        /// <summary>
        /// Begins facet redraw mode. Called by FacetPreviewWindow when user clicks "Redraw".
        /// </summary>
        public void BeginFacetRedraw(FacetPreviewWindow window, int facetId1)
        {
            _facetRedrawWindow = window;
            _facetRedrawId1 = facetId1;
            _facetRedrawFirstPoint = null;
            FacetRedrawPreviewLine = null;
            IsRedrawingFacet = true;
        }

        /// <summary>
        /// Called by MapView when user clicks during facet redraw mode.
        /// Returns true if the click was handled.
        /// </summary>
        public bool HandleFacetRedrawClick(int uiX, int uiZ)
        {
            if (!IsRedrawingFacet || _facetRedrawWindow == null)
                return false;

            // Snap to nearest vertex (64px grid)
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Convert to tile coords (game uses bottom-right origin)
            // UI: top-left = (0,0), bottom-right = (8192,8192)
            // Tile: 0..127, where tile 0 is at UI x=8192, tile 127 is at UI x=64
            byte tileX = (byte)Math.Clamp(128 - (snappedUiX / 64), 0, 127);
            byte tileZ = (byte)Math.Clamp(128 - (snappedUiZ / 64), 0, 127);

            if (_facetRedrawFirstPoint == null)
            {
                // First click - store start point (X0, Z0)
                _facetRedrawFirstPoint = (tileX, tileZ);
                FacetRedrawPreviewLine = (snappedUiX, snappedUiZ, snappedUiX, snappedUiZ);
                return true;
            }
            else
            {
                // Second click - complete the facet
                byte x0 = _facetRedrawFirstPoint.Value.x;
                byte z0 = _facetRedrawFirstPoint.Value.z;
                byte x1 = tileX;
                byte z1 = tileZ;

                // Apply the new coordinates
                _facetRedrawWindow.ApplyRedrawCoords(x0, z0, x1, z1);

                // End redraw mode and show window
                EndFacetRedraw(completed: true);
                return true;
            }
        }

        /// <summary>
        /// Updates the preview line endpoint as mouse moves.
        /// Called by MapView during mouse move when in redraw mode.
        /// </summary>
        public void UpdateFacetRedrawPreview(int uiX, int uiZ)
        {
            if (!IsRedrawingFacet || _facetRedrawFirstPoint == null)
                return;

            // Snap to nearest vertex
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Get the first point in UI coords
            int firstUiX = (128 - _facetRedrawFirstPoint.Value.x) * 64;
            int firstUiZ = (128 - _facetRedrawFirstPoint.Value.z) * 64;

            FacetRedrawPreviewLine = (firstUiX, firstUiZ, snappedUiX, snappedUiZ);
        }

        /// <summary>
        /// Cancels facet redraw mode (right-click or escape).
        /// </summary>
        public void CancelFacetRedraw()
        {
            EndFacetRedraw(completed: false);
        }

        private void EndFacetRedraw(bool completed)
        {
            IsRedrawingFacet = false;
            FacetRedrawPreviewLine = null;
            _facetRedrawFirstPoint = null;

            if (_facetRedrawWindow != null)
            {
                if (completed)
                    _facetRedrawWindow.OnRedrawCompleted();
                else
                    _facetRedrawWindow.OnRedrawCancelled();
            }

            _facetRedrawWindow = null;
            _facetRedrawId1 = 0;
        }

        /// <summary>
        /// Begins facet multi-draw mode. Called by AddFacetWindow when user clicks "Draw on Map".
        /// </summary>
        public void BeginFacetMultiDraw(IFacetMultiDrawWindow window, FacetTemplate template)
        {
            _facetMultiDrawWindow = window;
            _facetTemplate = template;
            IsMultiDrawingFacets = true;
        }

        /// <summary>
        /// Called by MapView when user clicks during facet multi-draw mode.
        /// Returns true if the click was handled.
        /// </summary>
        public bool HandleFacetMultiDrawClick(int uiX, int uiZ)
        {
            if (!IsMultiDrawingFacets || _facetTemplate == null)
                return false;

            // Snap to nearest vertex (64px grid)
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Convert to tile coords (game uses bottom-right origin)
            byte tileX = (byte)Math.Clamp(128 - (snappedUiX / 64), 0, 127);
            byte tileZ = (byte)Math.Clamp(128 - (snappedUiZ / 64), 0, 127);

            if (_multiDrawFirstPoint == null)
            {
                // First click - store start point (X0, Z0)
                _multiDrawFirstPoint = (tileX, tileZ);
                MultiDrawPreviewLine = (snappedUiX, snappedUiZ, snappedUiX, snappedUiZ);
                return true;
            }
            else
            {
                // Second click - complete and immediately commit this facet
                byte x0 = _multiDrawFirstPoint.Value.x;
                byte z0 = _multiDrawFirstPoint.Value.z;
                byte x1 = tileX;
                byte z1 = tileZ;

                // Commit this facet immediately
                var adder = new BuildingAdder(MapDataService.Instance);
                var coords = new List<(byte, byte, byte, byte)> { (x0, z0, x1, z1) };
                var result = adder.TryAddFacets(_facetTemplate.BuildingId1, coords, _facetTemplate);

                if (result.IsSuccess)
                {
                    _facetsAddedCount++;

                    // Update status
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Facet #{_facetsAddedCount} added at ({x0},{z0})->({x1},{z1}). Click to draw more, right-click to finish.";
                    }
                }
                else
                {
                    // Show error but continue drawing
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Failed to add facet: {result.ErrorMessage}";
                    }
                }

                // Reset for next facet
                _multiDrawFirstPoint = null;
                MultiDrawPreviewLine = null;

                return true;
            }
        }

        /// <summary>
        /// Updates the preview line endpoint as mouse moves during multi-draw.
        /// </summary>
        public void UpdateFacetMultiDrawPreview(int uiX, int uiZ)
        {
            if (!IsMultiDrawingFacets || _multiDrawFirstPoint == null)
                return;

            // Snap to nearest vertex
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Get the first point in UI coords
            int firstUiX = (128 - _multiDrawFirstPoint.Value.x) * 64;
            int firstUiZ = (128 - _multiDrawFirstPoint.Value.z) * 64;

            MultiDrawPreviewLine = (firstUiX, firstUiZ, snappedUiX, snappedUiZ);
        }

        /// <summary>
        /// Finishes facet multi-draw mode (right-click).
        /// Commits all drawn facets to the building.
        /// </summary>
        public void FinishFacetMultiDraw()
        {
            if (!IsMultiDrawingFacets)
                return;

            int totalAdded = _facetsAddedCount;
            EndFacetMultiDraw(totalAdded);

            if (totalAdded > 0)
            {
                MessageBox.Show($"Successfully added {totalAdded} facet(s) to Building #{_facetTemplate?.BuildingId1}.",
                    "Facets Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Cancels facet multi-draw mode (right-click with no facets, or escape).
        /// </summary>
        public void CancelFacetMultiDraw()
        {
            if (!IsMultiDrawingFacets)
                return;

            int totalAdded = _facetsAddedCount;
            EndFacetMultiDraw(totalAdded);

            // Facets already added are kept (they were committed immediately)
            if (totalAdded > 0)
            {
                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Drawing finished. {totalAdded} facet(s) were added.";
                }
            }
        }

        private void EndFacetMultiDraw(int facetsAdded)
        {
            IsMultiDrawingFacets = false;
            MultiDrawPreviewLine = null;
            _multiDrawFirstPoint = null;

            if (_addFacetWindow != null)
            {
                if (facetsAdded > 0)
                    _addFacetWindow.OnDrawCompleted(facetsAdded);
                else
                    _addFacetWindow.OnDrawCancelled();
            }

            _addFacetWindow = null;
            _facetTemplate = null;
            _facetsAddedCount = 0;
        }

        private static Uri ResolveStyleTmaPath(int world, bool useBeta)
        {
            var set = useBeta ? "Beta" : "Release";
            // e.g. pack://application:,,,/Assets/Textures/Release/world3/style.tma
            return new Uri(
                $"pack://application:,,,/Assets/Textures/{set}/world{world}/style.tma",
                UriKind.Absolute);
        }

        private async void LoadStylesForCurrentWorldAsync()
        {
            try
            {
                var uri = ResolveStyleTmaPath(TextureWorld, UseBetaTextures);
                System.Diagnostics.Debug.WriteLine($"STYLE URI = {uri}");

                // Try to get the embedded resource stream
                StreamResourceInfo? sri = Application.GetResourceStream(uri);
                if (sri != null && sri.Stream != null)
                {
                    using (var stream = sri.Stream)
                    {
                        // Load via the resource-stream API we just implemented
                        await StyleDataService.Instance.LoadFromResourceStreamAsync(
                            stream,
                            uri.ToString());
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[StyleDataService] style.tma resource not found for current world");
                    StyleDataService.Instance.Clear();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StyleDataService] Failed to load styles: {ex}");
                StyleDataService.Instance.Clear();
            }
        }

        // helper used above
        private static ImageSource? TryLoadPrimButtonImage(int number)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/Images/Buttons/{number:000}.png", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>
        /// Begins cable placement mode. Called by AddCableWindow when user clicks "Draw on Map".
        /// </summary>
        /// <summary>
        /// Begins cable placement mode. Called by AddCableWindow when user clicks "Draw on Map".
        /// </summary>
        public void BeginCablePlacement(AddCableWindow window)
        {
            _addCableWindow = window;
            _cableFirstPoint = null;
            CablePreviewLine = null;
            IsPlacingCable = true;
        }

        /// <summary>
        /// Called by MapView when user clicks during cable placement mode.
        /// Two clicks required: first = start, second = end.
        /// Returns true if the click was handled.
        /// </summary>
        public bool HandleCablePlacementClick(int uiX, int uiZ)
        {
            if (!IsPlacingCable || _addCableWindow == null)
                return false;

            // Snap to nearest vertex (64px grid)
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Convert to tile coords (game uses bottom-right origin)
            byte tileX = (byte)Math.Clamp(128 - (snappedUiX / 64), 0, 127);
            byte tileZ = (byte)Math.Clamp(128 - (snappedUiZ / 64), 0, 127);

            if (_cableFirstPoint == null)
            {
                // First click - store start point
                _cableFirstPoint = (tileX, tileZ);
                CablePreviewLine = (snappedUiX, snappedUiZ, snappedUiX, snappedUiZ);

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Cable start: ({tileX},{tileZ}). Click end point. Right-click to cancel.";
                }
                return true;
            }
            else
            {
                // Second click - complete placement and return to window
                byte x0 = _cableFirstPoint.Value.x;
                byte z0 = _cableFirstPoint.Value.z;
                byte x1 = tileX;
                byte z1 = tileZ;

                // Return coordinates to the AddCableWindow
                _addCableWindow?.OnPlacementCompleted(x0, z0, x1, z1);

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Cable endpoints set: ({x0},{z0}) → ({x1},{z1}). Configure parameters and click Create.";
                }

                EndCablePlacement();
                return true;
            }
        }

        /// <summary>
        /// Updates the preview line endpoint as mouse moves during cable placement.
        /// </summary>
        public void UpdateCablePlacementPreview(int uiX, int uiZ)
        {
            if (!IsPlacingCable || _cableFirstPoint == null)
                return;

            // Snap to nearest vertex
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Get the first point in UI coords
            int firstUiX = (128 - _cableFirstPoint.Value.x) * 64;
            int firstUiZ = (128 - _cableFirstPoint.Value.z) * 64;

            CablePreviewLine = (firstUiX, firstUiZ, snappedUiX, snappedUiZ);
        }

        /// <summary>
        /// Cancels cable placement mode (right-click or escape).
        /// </summary>
        public void CancelCablePlacement()
        {
            if (!IsPlacingCable)
                return;

            _addCableWindow?.OnPlacementCancelled();

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.StatusMessage = "Cable placement cancelled.";
            }

            EndCablePlacement();
        }

        private void EndCablePlacement()
        {
            IsPlacingCable = false;
            CablePreviewLine = null;
            _cableFirstPoint = null;
            _addCableWindow = null;
        }


        /// <summary>
        /// Begins door placement mode. Called by AddDoorWindow when user clicks "Draw on Map".
        /// </summary>
        public void BeginDoorPlacement(AddDoorWindow window, DoorTemplate template)
        {
            _addDoorWindow = window;
            _doorTemplate = template;
            _doorFirstPoint = null;
            DoorPreviewLine = null;
            IsPlacingDoor = true;
        }

        /// <summary>
        /// Called by MapView when user clicks during door placement mode.
        /// Two clicks required: first = start, second = end (must be within 1 cell).
        /// Returns true if the click was handled.
        /// </summary>
        public bool HandleDoorPlacementClick(int uiX, int uiZ)
        {
            if (!IsPlacingDoor || _doorTemplate == null)
                return false;

            // Snap to nearest vertex (64px grid)
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Convert to tile coords (game uses bottom-right origin)
            byte tileX = (byte)Math.Clamp(128 - (snappedUiX / 64), 0, 127);
            byte tileZ = (byte)Math.Clamp(128 - (snappedUiZ / 64), 0, 127);

            if (_doorFirstPoint == null)
            {
                // First click - store start point
                _doorFirstPoint = (tileX, tileZ);
                DoorPreviewLine = (snappedUiX, snappedUiZ, snappedUiX, snappedUiZ);

                if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                {
                    mainVm.StatusMessage = $"Door start: ({tileX},{tileZ}). Click end point (must be within 1 cell). Right-click to cancel.";
                }
                return true;
            }
            else
            {
                // Second click - validate distance and complete
                byte x0 = _doorFirstPoint.Value.x;
                byte z0 = _doorFirstPoint.Value.z;
                byte x1 = tileX;
                byte z1 = tileZ;

                // Calculate distance in cells
                int deltaX = Math.Abs((int)x1 - (int)x0);
                int deltaZ = Math.Abs((int)z1 - (int)z0);

                // Doors must be exactly 1 cell in ONE direction and 0 in the other
                // Valid: (1,0) or (0,1) - i.e., horizontal or vertical, 1 cell long
                bool isValid = (deltaX == 1 && deltaZ == 0) || (deltaX == 0 && deltaZ == 1);

                if (!isValid)
                {
                    // Invalid - show error but don't cancel, let them try again
                    string errorMsg;
                    if (deltaX == 0 && deltaZ == 0)
                    {
                        errorMsg = "Start and end points are the same. Door must be 1 cell long.";
                    }
                    else if (deltaX > 1 || deltaZ > 1)
                    {
                        errorMsg = $"Door is {Math.Max(deltaX, deltaZ)} cells long. Maximum is 1 cell.";
                    }
                    else
                    {
                        errorMsg = $"Door must be horizontal OR vertical, not diagonal. Distance: ({deltaX},{deltaZ})";
                    }

                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"⚠️ {errorMsg} Try again.";
                    }

                    // Reset first point so they can start over
                    _doorFirstPoint = null;
                    DoorPreviewLine = null;
                    return true;
                }

                // Valid door - create it
                // Door: Height = 4 (1 storey), FHeight = 0, Flags = TwoTextured | Unclimbable
                var facetTemplate = new FacetTemplate
                {
                    Type = FacetType.Door,
                    Height = 4,           // 1 storey = 4 height units
                    FHeight = 0,
                    BlockHeight = _doorTemplate.BlockHeight,
                    Y0 = _doorTemplate.Y0,
                    Y1 = _doorTemplate.Y1,
                    Flags = FacetFlags.TwoTextured | FacetFlags.Unclimbable,
                    BuildingId1 = _doorTemplate.BuildingId1,
                    Storey = _doorTemplate.Storey
                };

                // Commit the door
                var adder = new BuildingAdder(MapDataService.Instance);
                var coords = new List<(byte, byte, byte, byte)> { (x0, z0, x1, z1) };
                var result = adder.TryAddFacets(_doorTemplate.BuildingId1, coords, facetTemplate);

                if (result.IsSuccess)
                {
                    if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
                    {
                        mainVm.StatusMessage = $"Door added at ({x0},{z0})->({x1},{z1}) to Building #{_doorTemplate.BuildingId1}.";
                    }

                    MessageBox.Show($"Door added successfully to Building #{_doorTemplate.BuildingId1}.\n\n" +
                        $"Position: ({x0},{z0}) → ({x1},{z1})",
                        "Door Added", MessageBoxButton.OK, MessageBoxImage.Information);

                    EndDoorPlacement(true);
                }
                else
                {
                    MessageBox.Show($"Failed to add door:\n\n{result.ErrorMessage}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    // Reset to let them try again
                    _doorFirstPoint = null;
                    DoorPreviewLine = null;
                }

                return true;
            }
        }

        /// <summary>
        /// Updates the preview line endpoint as mouse moves during door placement.
        /// </summary>
        public void UpdateDoorPlacementPreview(int uiX, int uiZ)
        {
            if (!IsPlacingDoor || _doorFirstPoint == null)
                return;

            // Snap to nearest vertex
            int snappedUiX = ((uiX + 32) / 64) * 64;
            int snappedUiZ = ((uiZ + 32) / 64) * 64;
            snappedUiX = Math.Clamp(snappedUiX, 0, 8192);
            snappedUiZ = Math.Clamp(snappedUiZ, 0, 8192);

            // Get the first point in UI coords
            int firstUiX = (128 - _doorFirstPoint.Value.x) * 64;
            int firstUiZ = (128 - _doorFirstPoint.Value.z) * 64;

            DoorPreviewLine = (firstUiX, firstUiZ, snappedUiX, snappedUiZ);
        }

        /// <summary>
        /// Cancels door placement mode (right-click or escape).
        /// </summary>
        public void CancelDoorPlacement()
        {
            if (!IsPlacingDoor)
                return;

            EndDoorPlacement(false);

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                mainVm.StatusMessage = "Door placement cancelled.";
            }
        }

        private void EndDoorPlacement(bool success)
        {
            IsPlacingDoor = false;
            DoorPreviewLine = null;
            _doorFirstPoint = null;

            if (_addDoorWindow != null)
            {
                if (success)
                    _addDoorWindow.OnPlacementCompleted();
                else
                    _addDoorWindow.OnPlacementCancelled();
            }

            _addDoorWindow = null;
            _doorTemplate = null;
        }

        /// <summary>
        /// Get currently selected facet IDs from the Buildings tab.
        /// Returns null if no facets are selected.
        /// </summary>
        public IEnumerable<int>? GetSelectedFacetIds()
        {
            // This should return selected facets from BuildingsTabViewModel or similar
            // Implementation depends on how facet selection is tracked in your app
            // For now, return null - implement based on your selection mechanism
            return _selectedFacetIds;
        }

        private List<int>? _selectedFacetIds;
        public void SetSelectedFacetIds(IEnumerable<int> facetIds)
        {
            _selectedFacetIds = facetIds?.ToList();
        }

        /// <summary>
        /// Refresh the altitude layer after changes.
        /// </summary>
        public void RefreshAltitudeLayer()
        {
            AltitudeChangeBus.Instance.NotifyAll();
        }

        /// <summary>
        /// Handle altitude tool click on a tile.
        /// </summary>
        public void HandleAltitudeToolClick(int tx, int ty)
        {
            if (_altitudeAccessor == null) return;

            switch (SelectedTool)
            {
                case EditorTool.SetAltitude:
                    // Apply altitude to brush-sized region
                    int half = (BrushSize - 1) / 2;
                    for (int dy = -half; dy <= half; dy++)
                    {
                        for (int dx = -half; dx <= half; dx++)
                        {
                            int ttx = tx + dx;
                            int tty = ty + dy;
                            if (ttx >= 0 && ttx < MapConstants.TilesPerSide &&
                                tty >= 0 && tty < MapConstants.TilesPerSide)
                            {
                                _altitudeAccessor.WriteWorldAltitude(ttx, tty, TargetAltitude);
                            }
                        }
                    }
                    break;

                case EditorTool.SampleAltitude:
                    // Read altitude from clicked cell
                    TargetAltitude = _altitudeAccessor.ReadWorldAltitude(tx, ty);
                    break;

                case EditorTool.ResetAltitude:
                    // Reset altitude to 0 for brush-sized region
                    half = (BrushSize - 1) / 2;
                    for (int dy = -half; dy <= half; dy++)
                    {
                        for (int dx = -half; dx <= half; dx++)
                        {
                            int ttx = tx + dx;
                            int tty = ty + dy;
                            if (ttx >= 0 && ttx < MapConstants.TilesPerSide &&
                                tty >= 0 && tty < MapConstants.TilesPerSide)
                            {
                                _altitudeAccessor.WriteWorldAltitude(ttx, tty, 0);
                            }
                        }
                    }
                    break;
            }
        }


        private static int ParseNumber(string relativeKey)
        {
            // e.g. "world20_208" -> 208, "shared_prims_081" -> 81
            var parts = relativeKey.Split('_');
            return int.TryParse(parts[^1], out var n) ? n : 0;
        }

        // ===== INotifyPropertyChanged =====
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
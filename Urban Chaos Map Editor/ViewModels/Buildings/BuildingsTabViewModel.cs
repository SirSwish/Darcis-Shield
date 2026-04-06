// /ViewModels/BuildingsTabViewModel.cs

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Roofs;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.Services.Styles;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.ViewModels.Buildings
{
    public sealed class BuildingsTabViewModel : INotifyPropertyChanged
    {
        // ---------- Public collections ----------

        /// <summary>Flat list of all buildings (for the building list).</summary>
        public ObservableCollection<BuildingVM> Buildings { get; } = new();

        /// <summary>Facet type groups for the currently selected building + storey (TreeView).</summary>
        public ObservableCollection<FacetTypeGroupVM> SelectedBuildingFacetGroups { get; } = new();

        /// <summary>Available storey ids for the selected building (ComboBox).</summary>
        public ObservableCollection<int> StoreyFilterOptions { get; } = new();

        // ---- Raw DBuildingRec for the selection card ----
        private DBuildingRec[] _rawBuildings = Array.Empty<DBuildingRec>();

        private DBuildingRec _selectedBuildingRec;
        public DBuildingRec SelectedBuildingRec
        {
            get => _selectedBuildingRec;
            private set
            {
                _selectedBuildingRec = value;
                OnPropertyChanged();
            }
        }

        private bool _hasSelectedBuildingRec;
        public bool HasSelectedBuildingRec
        {
            get => _hasSelectedBuildingRec;
            private set
            {
                _hasSelectedBuildingRec = value;
                OnPropertyChanged();
            }
        }

        private int _selectedBuildingFacetRangeCount;
        public int SelectedBuildingFacetRangeCount
        {
            get => _selectedBuildingFacetRangeCount;
            private set
            {
                _selectedBuildingFacetRangeCount = value;
                OnPropertyChanged();
            }
        }

        // Auto-build options - relay to MapViewModel
        private static MapViewModel? GetMapVm()
        {
            var shell = System.Windows.Application.Current.MainWindow?.DataContext as MainWindowViewModel;
            return shell?.Map;
        }

        public bool AutoDetectRoofs
        {
            get => GetMapVm()?.AutoDetectRoofs ?? true;
            set { var vm = GetMapVm(); if (vm != null) vm.AutoDetectRoofs = value; OnPropertyChanged(); }
        }

        public bool AutoPaintRoofTextures
        {
            get => GetMapVm()?.AutoPaintRoofTextures ?? false;
            set { var vm = GetMapVm(); if (vm != null) vm.AutoPaintRoofTextures = value; OnPropertyChanged(); }
        }

        public bool AutoCreateWalkables
        {
            get => GetMapVm()?.AutoCreateWalkables ?? true;
            set { var vm = GetMapVm(); if (vm != null) vm.AutoCreateWalkables = value; OnPropertyChanged(); }
        }

        /// <summary>Flat list of all cable facets (for the cables ListView).</summary>
        public ObservableCollection<FacetVM> CableFacets { get; } = new();

        /// <summary>Toggle: false = building facets view, true = cable list view.</summary>
        private bool _showCablesList;
        public bool ShowCablesList
        {
            get => _showCablesList;
            set
            {
                if (_showCablesList == value) return;
                _showCablesList = value;
                OnPropertyChanged();
            }
        }

        private bool _filterWalls;
        public bool FilterWalls
        {
            get => _filterWalls;
            set
            {
                if (_filterWalls == value) return;
                _filterWalls = value;
                OnPropertyChanged();
                RefreshSelectedBuildingFacetGroups();  // re-apply filter for current building
            }
        }

        private bool _filterFences;
        public bool FilterFences
        {
            get => _filterFences;
            set
            {
                if (_filterFences == value) return;
                _filterFences = value;
                OnPropertyChanged();
                RefreshSelectedBuildingFacetGroups();
            }
        }

        private bool _filterDoors;
        public bool FilterDoors
        {
            get => _filterDoors;
            set
            {
                if (_filterDoors == value) return;
                _filterDoors = value;
                OnPropertyChanged();
                RefreshSelectedBuildingFacetGroups();
            }
        }

        private bool _filterGates;
        public bool FilterGates
        {
            get => _filterGates;
            set
            {
                if (_filterGates == value) return;
                _filterGates = value;
                OnPropertyChanged();
                RefreshSelectedBuildingFacetGroups();
            }
        }

        private bool _filterLadders;
        public bool FilterLadders
        {
            get => _filterLadders;
            set
            {
                if (_filterLadders == value) return;
                _filterLadders = value;
                OnPropertyChanged();
                RefreshSelectedBuildingFacetGroups();
            }
        }
        public bool HasSelectedFacet => SelectedFacet != null;

        // Convenience: is *any* facet filter active?
        private bool AnyFacetFilterActive =>
            _filterWalls || _filterFences || _filterDoors || _filterGates || _filterLadders;

        // ---------- Selection state ----------

        private FacetVM? _selectedFacet;
        public FacetVM? SelectedFacet
        {
            get => _selectedFacet;
            set
            {
                if (_selectedFacet == value) return;
                _selectedFacet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedFacet)); // ADD THIS LINE

                if (value != null)
                {
                    // Keep building / storey selection in sync with the facet for UI purposes
                    SelectedBuildingId = value.BuildingId;
                    SelectedStoreyId = value.StoreyId;
                }
            }
        }

        private FacetTypeGroupVM? _selectedFacetTypeGroup;
        /// <summary>The currently selected facet type group in the Types list (middle column).</summary>
        public FacetTypeGroupVM? SelectedFacetTypeGroup
        {
            get => _selectedFacetTypeGroup;
            set
            {
                if (_selectedFacetTypeGroup != value)
                {
                    _selectedFacetTypeGroup = value;
                    OnPropertyChanged();
                }
            }
        }

        private BuildingVM? _selectedBuilding;
        public BuildingVM? SelectedBuilding
        {
            get => _selectedBuilding;
            set
            {
                if (_selectedBuilding == value)
                    return;

                _selectedBuilding = value;
                _selectedBuildingId = value?.Id ?? 0;

                // Clear facet type selection when building changes
                SelectedFacetTypeGroup = null;
                SelectedFacet = null;

                UpdateSelectedBuildingRec();
                DumpSelectedBuildingRawBytesToDebug();

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBuildingId));

                // Pick a default storey for this building
                if (_selectedBuilding != null)
                {
                    var defaultStorey = _selectedBuilding.Storeys
                        .OrderBy(s => s.StoreyId)
                        .FirstOrDefault();

                    _selectedStoreyId = defaultStorey?.StoreyId ?? 0;
                    OnPropertyChanged(nameof(SelectedStoreyId));
                }
                else
                {
                    _selectedStoreyId = 0;
                    OnPropertyChanged(nameof(SelectedStoreyId));
                }

                RefreshSelectedBuildingFacetGroups();
                SyncBuildingSelectionIntoMap();
            }
        }

        private int _selectedBuildingId;
        /// <summary>1-based DBuilding id used for map highlight and details.</summary>
        public int SelectedBuildingId
        {
            get => _selectedBuildingId;
            set
            {
                if (_selectedBuildingId == value) return;
                _selectedBuildingId = value;
                OnPropertyChanged();

                // Move to the matching BuildingVM if it exists
                var newB = Buildings.FirstOrDefault(b => b.Id == value);
                if (newB != null && newB != _selectedBuilding)
                {
                    SelectedBuilding = newB;
                }
                else if (value == 0)
                {
                    SelectedBuilding = null;
                }
            }
        }

        /// <summary>
        /// Storey currently selected in the filter combo (for the selected building).
        /// Drives which facet groups are shown.
        /// </summary>
        private int _selectedStoreyFilterId;
        public int SelectedStoreyFilterId
        {
            get => _selectedStoreyFilterId;
            set
            {
                if (_selectedStoreyFilterId == value) return;
                _selectedStoreyFilterId = value;
                OnPropertyChanged();

                RefreshSelectedBuildingFacetGroups();
                // Changing storey is still "whole-building/storey" highlight
                SyncBuildingSelectionIntoMap();
            }
        }

        /// <summary>
        /// Storey used for the details card / map highlight (mirrors filter or facet selection).
        /// </summary>
        private int _selectedStoreyId;
        public int SelectedStoreyId
        {
            get => _selectedStoreyId;
            set
            {
                if (_selectedStoreyId == value) return;
                _selectedStoreyId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Optional free text filter (not wired up right now - kept for future search).</summary>
        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText == value) return;
                _filterText = value;
                OnPropertyChanged();
                // Hook up custom searching later if you want.
            }
        }

        // ---------- Global style / paintmem cards ----------

        public ObservableCollection<StyleVM> Styles { get; } = new();
        public int StylesCount { get; private set; }
        public int PaintMemCount { get; private set; }
        public string PaintMemPreview { get; private set; } = string.Empty;

        public int DStoreysNext { get; private set; }          // raw -next-
        public int DStoreysTotal => Math.Max(0, DStoreysNext - 1);

        private short[] _lastDstyles = Array.Empty<short>();
        private short[] _styles = Array.Empty<short>();
        private byte[] _paintMem = Array.Empty<byte>();

        // ---- style.tma (world styles) ----
        public ObservableCollection<TmaStyleVM> TmaStyles { get; } = new();
        public int TmaStylesCount => TmaStyles.Count;

        private readonly StylesAccessor _stylesAcc
            = new(StyleDataService.Instance);

        // ---------- Accessors & backing lists ----------

        private readonly BuildingsAccessor _buildingsAcc;
        private readonly List<BuildingVM> _allBuildings = new();
        private readonly List<FacetVM> _allCables = new();

        public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(_ => Refresh());
        private RelayCommand? _refreshCommand;

        // ---------- ctor ----------

        public BuildingsTabViewModel()
        {
            _buildingsAcc = new BuildingsAccessor(MapDataService.Instance);
            _buildingsAcc.BuildingsBytesReset += (_, __) =>
                System.Windows.Application.Current.Dispatcher.Invoke(Refresh);

            // If a map is already loaded at startup, populate immediately
            if (MapDataService.Instance.IsLoaded)
                Refresh();

            // When styles bytes change, refresh StyleDisplay on existing facets
            StyleDataService.Instance.StylesBytesReset += (_, __) =>
            {
                foreach (var b in Buildings)
                    foreach (var s in b.Storeys)
                        foreach (var g in s.Groups)
                            foreach (var f in g.Facets)
                                f.StyleDisplay = StyleDisplayForVM(f);

                OnPropertyChanged(nameof(Buildings));
                OnPropertyChanged(nameof(SelectedFacet));

                if (TmaStyles.Count == 0)
                    SeedWorldStylesDemo();
            };

            // keep header in sync with collection changes
            TmaStyles.CollectionChanged += (_, __) => OnPropertyChanged(nameof(TmaStylesCount));

            if (TmaStyles.Count == 0)
            {
                SeedWorldStylesDemo();
            }

            LoadWorldStyles();
        }

        // ---------- World styles demo / loader ----------

        private void SeedWorldStylesDemo()
        {
            var demo = new TmaStyleVM
            {
                Index = 0,
                Name = "Demo style",
                FlagsSummary = "Opaque, NoAlpha"
            };
            demo.Entries.Add(new TmaEntryVM { Page = 1, Tx = 2, Ty = 3, FlipDisplay = "-" });
            TmaStyles.Add(demo);
        }

        private void LoadWorldStyles()
        {
            TmaStyles.Clear();
            // TODO: replace this with your real style.tma loader
            SeedWorldStylesDemo();
        }

        // ---------- Style formatting helpers ----------

        private string StyleDisplayForVM(FacetVM f)
        {
            if (f.Type == FacetType.Cable)
                return $"Cable: step1={f.CableStep1}, step2={f.CableStep2}, mode={f.FHeight}";

            return StyleDisplayFor(_lastDstyles, f.StyleIndex, DStoreysNext);
        }

        private string StyleDisplayFor(ushort facetStyleIndex)
        {
            if (_lastDstyles is null || facetStyleIndex >= _lastDstyles.Length)
                return $"{facetStyleIndex}";

            short dstyleValue = _lastDstyles[facetStyleIndex];
            if (dstyleValue >= 0)
                return _stylesAcc.Describe(dstyleValue);
            else
                return $"painted (storey={-dstyleValue})";
        }

        // Formats a single entry from the signed dstyles[] table.
        private string StyleDisplayFor(short[] stylesTable, ushort styleIndex, int nextDStorey)
        {
            if (stylesTable == null || stylesTable.Length == 0) return $"{styleIndex}";
            if (styleIndex < 0 || styleIndex >= stylesTable.Length) return $"{styleIndex}";

            short entry = stylesTable[styleIndex];

            if (entry >= 0)
            {
                return $"{styleIndex}  (raw=0x{((ushort)entry):X4})";
            }
            else
            {
                int dstoreyIdx = -entry;
                return $"{styleIndex}  (painted ? DStorey {dstoreyIdx})";
            }
        }

        private void UpdateSelectedBuildingRec()
        {
            int id1 = _selectedBuildingId;

            if (id1 > 0 && id1 <= _rawBuildings.Length)
            {
                var rec = _rawBuildings[id1 - 1];
                SelectedBuildingRec = rec;
                HasSelectedBuildingRec = true;

                // EndFacet is 1-based exclusive
                SelectedBuildingFacetRangeCount = Math.Max(0, rec.EndFacet - rec.StartFacet);
            }
            else
            {
                SelectedBuildingRec = default;
                HasSelectedBuildingRec = false;
                SelectedBuildingFacetRangeCount = 0;
            }
        }

        private void DumpSelectedBuildingRawBytesToDebug()
        {
            int id1 = _selectedBuildingId;
            if (id1 <= 0) return;

            if (_buildingsAcc.TryGetBuildingBytes(id1, out var raw, out int off))
            {
                // Hex dump: "AA BB CC ..."
                string hex = BitConverter.ToString(raw).Replace("-", " ");

                Debug.WriteLine($"[BuildingsTabVM] DBuilding#{id1} raw @ 0x{off:X}: {hex}");
            }
            else
            {
                Debug.WriteLine($"[BuildingsTabVM] DBuilding#{id1} raw: <unavailable>");
            }
        }

        // ---------- Refresh pipeline ----------
        public void Refresh()
        {
            Refresh(forceSelectBuildingId: null);
        }

        public void Refresh(int? forceSelectBuildingId = null)
        {
            Debug.WriteLine("=== [BuildingsTabVM] Refresh() ===");

            // --- Remember current selection so we can restore it after reload ---
            // If forceSelectBuildingId is provided, we want THAT building selected after refresh,
            // not the previous selection.
            bool isForced = forceSelectBuildingId.HasValue;
            int prevBuildingId = forceSelectBuildingId ?? SelectedBuildingId;
            int prevStoreyId = SelectedStoreyId;
            int? prevFacetId = SelectedFacet?.FacetId1;
            // Save group type now, before SelectedBuilding = null wipes SelectedFacetTypeGroup
            FacetType? prevGroupType = isForced ? null : SelectedFacetTypeGroup?.Type;

            Buildings.Clear();
            SelectedBuildingFacetGroups.Clear();
            StoreyFilterOptions.Clear();
            CableFacets.Clear();

            _allBuildings.Clear();
            _allCables.Clear();

            SelectedBuilding = null;
            SelectedFacet = null;
            ShowCablesList = false;

            var arrays = _buildingsAcc.ReadSnapshot();
            _rawBuildings = arrays.Buildings ?? Array.Empty<DBuildingRec>();

            Debug.WriteLine($"[BuildingsTabVM] region: start=0x{arrays.StartOffset:X} len=0x{arrays.Length:X}");
            Debug.WriteLine($"[BuildingsTabVM] counters: nextDBuilding={arrays.NextDBuilding} nextDFacet={arrays.NextDFacet} nextDStyle={arrays.NextDStyle} nextPaintMem={arrays.NextPaintMem} nextDStorey={arrays.NextDStorey} saveType={arrays.SaveType}");
            Debug.WriteLine($"[BuildingsTabVM] arrays: Buildings={arrays.Buildings.Length} Facets={arrays.Facets.Length} Styles={arrays.Styles.Length} PaintMem={arrays.PaintMem.Length} Storeys={arrays.Storeys.Length}");

            _lastDstyles = arrays.Styles ?? Array.Empty<short>();
            _styles = arrays.Styles ?? Array.Empty<short>();
            _paintMem = arrays.PaintMem ?? Array.Empty<byte>();
            DStoreysNext = arrays.NextDStorey;

            if (arrays.Facets.Length == 0 && arrays.Buildings.Length == 0)
            {
                Debug.WriteLine("[BuildingsTabVM] Nothing to show: no facets and no buildings.");
                Styles.Clear();
                StylesCount = 0;
                PaintMemCount = 0;
                PaintMemPreview = string.Empty;
                DStoreysNext = 0;

                OnPropertyChanged(nameof(StylesCount));
                OnPropertyChanged(nameof(PaintMemCount));
                OnPropertyChanged(nameof(PaintMemPreview));
                OnPropertyChanged(nameof(DStoreysNext));
                OnPropertyChanged(nameof(DStoreysTotal));
                Debug.WriteLine("=== [BuildingsTabVM] Refresh() done (empty) ===");
                return;
            }

            // Pre-index facets with their real 1-based ID from file order.
            var facetsIndexed = arrays.Facets.Select((f, i) => (Facet: f, Id1: i + 1)).ToArray();

            Debug.WriteLine($"[BuildingsTabVM] facets: total={facetsIndexed.Length}");
            if (facetsIndexed.Length > 0)
            {
                var f0 = facetsIndexed[0].Facet;
                Debug.WriteLine($"[BuildingsTabVM] sample facet[1]: type={f0.Type} bld={f0.Building} st={f0.Storey} styleIdx={f0.StyleIndex} xy=({f0.X0},{f0.Z0})->({f0.X1},{f0.Z1})");
            }

            // Split cables vs non-cables
            var nonCable = facetsIndexed.Where(t => t.Facet.Type != FacetType.Cable).ToArray();
            var cables = facetsIndexed.Where(t => t.Facet.Type == FacetType.Cable)
                                      .OrderBy(t => t.Id1)
                                      .ToList();

            Debug.WriteLine($"[BuildingsTabVM] nonCable={nonCable.Length} cables={cables.Count}");

            // Get ALL building IDs from the DBuildingRec array (1-based)
            // This includes buildings with no facets
            var allBuildingIds = new HashSet<int>();
            for (int i = 0; i < arrays.Buildings.Length; i++)
                allBuildingIds.Add(i + 1); // 1-based ID

            // Also get building IDs from facets (in case any facets reference buildings not in the array)
            var facetBuildingIds = nonCable
                .Select(t => (int)t.Facet.Building)
                .Where(id => id > 0)
                .Distinct();

            foreach (var id in facetBuildingIds)
                allBuildingIds.Add(id);

            var buildingIds = allBuildingIds.OrderBy(id => id).ToList();

            Debug.WriteLine($"[BuildingsTabVM] all building ids={buildingIds.Count} :: {string.Join(",", buildingIds.Take(16))}{(buildingIds.Count > 16 ? ",..." : "")}");

            foreach (int bId in buildingIds)
            {
                var bFacets = nonCable.Where(t => t.Facet.Building == bId).ToList();
                // NOTE: Don't skip if bFacets.Count == 0 - we want to show empty buildings!

                var bvm = new BuildingVM { Id = bId };

                // Get building type from DBuildingRec if available
                if (bId > 0 && bId <= arrays.Buildings.Length)
                {
                    var bRec = arrays.Buildings[bId - 1];
                    bvm.BuildingType = (BuildingType)bRec.Type;
                }

                foreach (var storeyGroup in bFacets.GroupBy(t => t.Facet.Storey).OrderBy(g => g.Key))
                {
                    var svm = new StoreyVM { StoreyId = storeyGroup.Key };

                    foreach (var typeGroup in storeyGroup.GroupBy(t => t.Facet.Type).OrderBy(g => g.Key))
                    {
                        var gvm = new FacetTypeGroupVM
                        {
                            Type = typeGroup.Key,
                            TypeName = FacetTypeName(typeGroup.Key)
                        };

                        foreach (var t in typeGroup.OrderBy(tt => tt.Id1))
                        {
                            var f = t.Facet;

                            var styleDisplay = StyleDisplayFor(_styles, f.StyleIndex, DStoreysNext);

                            bool isPainted =
                                !string.IsNullOrEmpty(styleDisplay) &&
                                styleDisplay.IndexOf("painted", StringComparison.OrdinalIgnoreCase) >= 0;

                            gvm.Facets.Add(new FacetVM
                            {
                                FacetId1 = t.Id1,
                                Type = f.Type,
                                StyleIndex = f.StyleIndex,
                                StyleDisplay = styleDisplay,
                                Coords = $"({f.X0},{f.Z0}) : ({f.X1},{f.Z1})",
                                Height = f.Height,
                                FHeight = f.FHeight,
                                Flags = $"0x{((ushort)f.Flags):X4}",
                                BuildingId = bId,
                                StoreyId = f.Storey,
                                Raw = f,
                                Y0 = f.Y0,
                                Y1 = f.Y1,
                                BlockHeight = f.BlockHeight,
                                Open = f.Open,
                                IsPainted = isPainted
                            });
                        }

                        gvm.Count = gvm.Facets.Count;
                        if (gvm.Count > 0)
                            svm.Groups.Add(gvm);
                    }

                    svm.UsageCount = svm.Groups.Sum(x => x.Count);
                    if (svm.UsageCount > 0)
                        bvm.Storeys.Add(svm);
                }

                bvm.FacetCount = bvm.Storeys.SelectMany(st => st.Groups).Sum(g => g.Count);
                _allBuildings.Add(bvm);
            }

            // Populate flat cables list
            _allCables.Clear();
            if (cables.Count > 0)
            {
                foreach (var t in cables)
                {
                    var f = t.Facet;
                    var cvm = new FacetVM
                    {
                        FacetId1 = t.Id1,
                        Type = FacetType.Cable,
                        StyleIndex = f.StyleIndex,
                        StyleDisplay = CableStyleDisplay(f),
                        Coords = $"({f.X0},{f.Z0}) : ({f.X1},{f.Z1})",
                        Height = f.Height,
                        FHeight = f.FHeight,
                        Flags = $"0x{((ushort)f.Flags):X4}",
                        BuildingId = 0,
                        StoreyId = 0,
                        Y0 = f.Y0,
                        Y1 = f.Y1,
                        BlockHeight = f.BlockHeight,
                        Open = f.Open,
                        Raw = f,
                        CableStep1 = unchecked((short)f.StyleIndex),
                        CableStep2 = unchecked((short)f.Building)
                    };

                    _allCables.Add(cvm);
                }

                Debug.WriteLine($"[BuildingsTabVM] cables collected into flat list: count={_allCables.Count}");
            }

            Debug.WriteLine($"[BuildingsTabVM] built _allBuildings={_allBuildings.Count} (total facets={_allBuildings.Sum(b => b.FacetCount)})");

            // Global data cards
            Styles.Clear();
            StylesCount = 0;
            PaintMemCount = 0;
            PaintMemPreview = string.Empty;

            if (arrays.Styles is { Length: > 0 })
            {
                for (int i = 0; i < arrays.Styles.Length; i++)
                    Styles.Add(new StyleVM { Index = i, Value = arrays.Styles[i] });
                StylesCount = arrays.Styles.Length;
            }

            if (arrays.PaintMem is { Length: > 0 })
            {
                PaintMemCount = arrays.PaintMem.Length;

                var take = Math.Min(128, arrays.PaintMem.Length);
                var sb = new StringBuilder(take * 3);
                for (int i = 0; i < take; i++)
                {
                    sb.Append(arrays.PaintMem[i].ToString("X2"));
                    if (i != take - 1) sb.Append(' ');
                }
                PaintMemPreview = sb.ToString();
            }

            // Push backing lists into observable collections
            Buildings.Clear();
            foreach (var b in _allBuildings)
                Buildings.Add(b);

            CableFacets.Clear();
            foreach (var c in _allCables)
                CableFacets.Add(c);

            // --- RESTORE / FORCE selection ---
            BuildingVM? restoredBuilding = null;
            if (prevBuildingId > 0)
                restoredBuilding = Buildings.FirstOrDefault(b => b.Id == prevBuildingId);

            if (restoredBuilding != null)
            {
                // Selecting building rebuilds facet groups etc (via your setters)
                SelectedBuilding = restoredBuilding;

                if (!isForced)
                {
                    // Try to restore storey if it still exists
                    var restoredStorey = restoredBuilding.Storeys
                        .FirstOrDefault(s => s.StoreyId == prevStoreyId);

                    if (restoredStorey != null)
                    {
                        SelectedStoreyId = restoredStorey.StoreyId;
                    }
                    else if (restoredBuilding.Storeys.Count > 0)
                    {
                        // Fallback: first storey on that building
                        SelectedStoreyId = restoredBuilding.Storeys
                            .OrderBy(s => s.StoreyId)
                            .First().StoreyId;
                    }

                    // Restore the facet type group — SelectedBuilding = restoredBuilding above
                    // reset it to FirstOrDefault because SelectedFacetTypeGroup had already been
                    // cleared before RefreshSelectedBuildingFacetGroups ran. Re-apply it here.
                    if (prevGroupType.HasValue)
                    {
                        var restoredGroup = SelectedBuildingFacetGroups
                            .FirstOrDefault(g => g.Type == prevGroupType.Value);
                        if (restoredGroup != null)
                            SelectedFacetTypeGroup = restoredGroup;
                    }

                    // Try to restore the exact facet if it still exists
                    if (prevFacetId.HasValue)
                    {
                        var restoredFacet = restoredBuilding.Storeys
                            .SelectMany(st => st.Groups)
                            .SelectMany(g => g.Facets)
                            .FirstOrDefault(f => f.FacetId1 == prevFacetId.Value);

                        if (restoredFacet != null)
                            SelectedFacet = restoredFacet;
                    }
                }
                else
                {
                    // Forced selection (e.g., newly added building):
                    // Don-t restore previous facet/storey. Keep facet null; storey set if exists.
                    if (restoredBuilding.Storeys.Count > 0)
                    {
                        SelectedStoreyId = restoredBuilding.Storeys
                            .OrderBy(s => s.StoreyId)
                            .First().StoreyId;
                    }
                    else
                    {
                        SelectedStoreyId = 0;
                    }

                    SelectedFacet = null;
                }
            }
            else
            {
                // Requested/previous building no longer exists: fall back to old behaviour
                SelectedBuilding =
                    Buildings.FirstOrDefault(b => !b.IsCablesBucket)
                    ?? Buildings.FirstOrDefault();
            }

            UpdateSelectedBuildingRec();

            OnPropertyChanged(nameof(StylesCount));
            OnPropertyChanged(nameof(PaintMemCount));
            OnPropertyChanged(nameof(PaintMemPreview));
            OnPropertyChanged(nameof(DStoreysNext));
            OnPropertyChanged(nameof(DStoreysTotal));

            var totalFacetsView = Buildings
                .SelectMany(b => b.Storeys)
                .SelectMany(s => s.Groups)
                .Sum(g => g.Count);

            Debug.WriteLine($"[BuildingsTabVM] AFTER POPULATE ? Buildings.Count={Buildings.Count}, facets(view)={totalFacetsView}");
            Debug.WriteLine("=== [BuildingsTabVM] Refresh() done ===");
        }

        public bool TrySelectFacetFromMap(int facetId1, int buildingId1)
        {
            // Cables live in the flat CableFacets list, not in the storey/group tree.
            // Handle them first so we switch to cable-list view and avoid a false-not-found.
            var cable = CableFacets.FirstOrDefault(c => c.FacetId1 == facetId1);
            if (cable != null)
            {
                // Set building BEFORE facet — the SelectedBuilding setter clears SelectedFacet.
                var cableBuilding = Buildings.FirstOrDefault(b => b.Id == buildingId1);
                if (cableBuilding != null && !ReferenceEquals(SelectedBuilding, cableBuilding))
                    SelectedBuilding = cableBuilding;
                SelectedFacet = cable;
                SyncSelectionIntoMapVm();
                return true;
            }

            var building = Buildings.FirstOrDefault(b => b.Id == buildingId1);
            if (building == null)
            {
                Debug.WriteLine($"[BuildingsTabVM] TrySelectFacetFromMap: building#{buildingId1} not found in Buildings (count={Buildings.Count}, ids=[{string.Join(",", Buildings.Select(b => b.Id))}])");
                return false;
            }

            ShowCablesList = false;

            if (!ReferenceEquals(SelectedBuilding, building))
                SelectedBuilding = building;

            var facet = building.Storeys
                .SelectMany(s => s.Groups)
                .SelectMany(g => g.Facets)
                .FirstOrDefault(f => f.FacetId1 == facetId1);

            if (facet == null)
            {
                Debug.WriteLine($"[BuildingsTabVM] TrySelectFacetFromMap: facet#{facetId1} not found in building#{buildingId1} storeys/groups");
                return false;
            }

            SelectedStoreyId = facet.StoreyId;

            var group = SelectedBuildingFacetGroups
                .FirstOrDefault(g => g.Facets.Any(f => f.FacetId1 == facetId1));

            if (group != null)
                SelectedFacetTypeGroup = group;

            SelectedFacet = facet;

            SyncSelectionIntoMapVm();
            return true;
        }


        /// <summary>
        /// Called from the RIGHT facet TreeView (or cable list) selection change in the view.
        /// Keeps SelectedFacet, SelectedBuilding and SelectedStoreyId in sync,
        /// then pushes the selection into MapViewModel as a SINGLE-FACET highlight.
        /// </summary>
        public void HandleTreeSelection(object? selection)
        {
            if (selection is FacetVM f)
            {
                // Cables live in the flat CableFacets list, not the building/storey tree.
                // Skip the group/storey sync to avoid RefreshSelectedBuildingFacetGroups clearing
                // SelectedFacet (cables are not in SelectedBuildingFacetGroups).
                if (f.Type == FacetType.Cable)
                {
                    SelectedFacet = f;
                    SyncSelectionIntoMapVm();
                    return;
                }

                // 1) Ensure the building matches the facet (for non-cable facets)
                //    SelectedBuilding setter clears SelectedFacetTypeGroup and SelectedFacet,
                //    so we do this first and re-apply them below.
                if (f.BuildingId > 0)
                {
                    var b = Buildings.FirstOrDefault(bb => bb.Id == f.BuildingId);
                    if (b != null && b != SelectedBuilding)
                        SelectedBuilding = b;
                }

                // 2) Select the type group that actually contains this facet,
                //    regardless of which group was selected before the building changed.
                var correctGroup = SelectedBuildingFacetGroups
                    .FirstOrDefault(g => g.Type == f.Type);
                if (correctGroup != null)
                    SelectedFacetTypeGroup = correctGroup;

                // 3) Set the selected facet (after group so the list binding is correct)
                SelectedFacet = f;

                // 4) Make sure the current storey matches the facet's storey
                SelectedStoreyId = f.StoreyId;
            }
            else if (selection is FacetTypeGroupVM g && g.Facets.Count > 0)
            {
                // When a type-group node is selected, use its first facet as the "sample"
                var first = g.Facets[0];

                if (first.BuildingId > 0)
                {
                    var b = Buildings.FirstOrDefault(bb => bb.Id == first.BuildingId);
                    if (b != null && b != SelectedBuilding)
                        SelectedBuilding = b;
                }

                SelectedFacetTypeGroup = g;
                SelectedFacet = first;
                SelectedStoreyId = first.StoreyId;
            }
            else
            {
                // No facet / group selected
                SelectedFacet = null;
            }

            // FACET MODE: single-facet highlight
            SyncSelectionIntoMapVm();
        }

        /// <summary>
        /// Called from the LEFT building/storey tree selection in the view.
        /// Interprets the node (BuildingVM or StoreyVM) and updates:
        ///  - SelectedBuilding
        ///  - SelectedStoreyId
        ///  - SelectedFacet (for details only)
        /// Then pushes a WHOLE-BUILDING/STOREY selection into MapViewModel
        /// so the overlay highlights all facets for that building.
        /// </summary>
        public void HandleBuildingTreeSelection(object? selection)
        {
            switch (selection)
            {
                case BuildingVM b:
                    {
                        // Building node selected
                        SelectedBuilding = b;

                        // Choose a sensible storey for this building (prefer 0, else smallest)
                        var storeys = b.Storeys
                                       .OrderBy(s => s.StoreyId)
                                       .ToList();

                        if (storeys.Count > 0)
                        {
                            var preferred = storeys.FirstOrDefault(s => s.StoreyId == 0) ?? storeys[0];
                            SelectedStoreyId = preferred.StoreyId;

                            // Auto-pick a first facet in that storey for the details pane only
                            var firstFacet = preferred.Groups.SelectMany(g => g.Facets).FirstOrDefault();
                            SelectedFacet = null;
                        }
                        else
                        {
                            SelectedStoreyId = 0;
                            SelectedFacet = null;
                        }
                        break;
                    }

                case StoreyVM s:
                    {
                        // Storey node selected (child under a building)
                        var building = Buildings.FirstOrDefault(bb => bb.Storeys.Contains(s));
                        if (building != null && !ReferenceEquals(SelectedBuilding, building))
                        {
                            SelectedBuilding = building;
                        }

                        SelectedStoreyId = s.StoreyId;

                        // Auto-pick a first facet for this storey for the details pane only
                        var firstInStorey = s.Groups.SelectMany(g => g.Facets).FirstOrDefault();
                        SelectedFacet = firstInStorey;
                        break;
                    }

                default:
                    // Unknown or cleared selection
                    SelectedBuilding = null;
                    SelectedStoreyId = 0;
                    SelectedFacet = null;
                    break;
            }

            // WHOLE BUILDING/STOREY: facet id is null here
            SyncBuildingSelectionIntoMap();

        }

        /// <summary>
        /// Rebuilds storey filter options for the currently SelectedBuilding.
        /// </summary>
        private void UpdateStoreyFilterOptionsForSelectedBuilding()
        {
            StoreyFilterOptions.Clear();

            if (SelectedBuilding == null)
            {
                _selectedStoreyFilterId = 0;
                OnPropertyChanged(nameof(SelectedStoreyFilterId));
                return;
            }

            var storeyIds = SelectedBuilding.Storeys
                .Select(st => st.StoreyId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (storeyIds.Count == 0)
            {
                _selectedStoreyFilterId = 0;
                StoreyFilterOptions.Add(0);
                OnPropertyChanged(nameof(SelectedStoreyFilterId));
                return;
            }

            foreach (var sid in storeyIds)
                StoreyFilterOptions.Add(sid);

            if (!storeyIds.Contains(_selectedStoreyFilterId))
            {
                _selectedStoreyFilterId = storeyIds.Contains(0) ? 0 : storeyIds[0];
                OnPropertyChanged(nameof(SelectedStoreyFilterId));
            }
        }

        /// <summary>
        /// Populates SelectedBuildingFacetGroups based on SelectedBuilding + SelectedStoreyId,
        /// then applies the Walls/Fences/Doors/Ladders filters.
        /// Also auto-selects a facet for the details pane if none is selected,
        /// but keeps the map in whole-building/storey mode.
        /// </summary>

        private void RefreshSelectedBuildingFacetGroups()
        {
            // Preserve current selection if possible
            int? keepFacetId1 = SelectedFacet?.FacetId1;
            var keepGroupType = SelectedFacetTypeGroup?.Type;

            SelectedBuildingFacetGroups.Clear();
            SelectedFacetTypeGroup = null;

            var b = SelectedBuilding;
            if (b == null || b.Storeys.Count == 0)
            {
                // No building => no facet selection
                SelectedFacet = null;
                SyncBuildingSelectionIntoMap();
                return;
            }

            // Collect ALL facets from ALL storeys, grouped by type
            var allFacetsByType = b.Storeys
                .SelectMany(s => s.Groups)
                .SelectMany(g => g.Facets.Select(f => (Type: g.Type, Facet: f)))
                .GroupBy(x => x.Type)
                .OrderBy(g => g.Key.ToString());

            foreach (var typeGroup in allFacetsByType)
            {
                if (AnyFacetFilterActive && !IsFacetTypeAllowedByFilter(typeGroup.Key))
                    continue;

                var gvm = new FacetTypeGroupVM
                {
                    Type = typeGroup.Key,
                    TypeName = FacetTypeName(typeGroup.Key),
                    Facets = new ObservableCollection<FacetVM>(
                        typeGroup.Select(x => x.Facet).OrderBy(f => f.FacetId1))
                };
                gvm.Count = gvm.Facets.Count;

                if (gvm.Count > 0)
                    SelectedBuildingFacetGroups.Add(gvm);
            }

            // Restore type group if possible, else pick first group so the Facets list has content
            if (keepGroupType.HasValue)
                SelectedFacetTypeGroup = SelectedBuildingFacetGroups.FirstOrDefault(g => g.Type == keepGroupType.Value);

            SelectedFacetTypeGroup ??= SelectedBuildingFacetGroups.FirstOrDefault();

            // Restore facet selection ONLY if it was already selected before
            if (keepFacetId1.HasValue)
            {
                var restoredFacet = SelectedBuildingFacetGroups
                    .SelectMany(g => g.Facets)
                    .FirstOrDefault(f => f.FacetId1 == keepFacetId1.Value);

                SelectedFacet = restoredFacet; // could be null if filtered out / deleted
            }
            else
            {
                // If we didn't have a facet selected explicitly, keep it null (building highlight mode)
                SelectedFacet = null;
            }

            // Push correct highlight mode into map
            if (SelectedFacet != null)
                SyncSelectionIntoMapVm();       // single facet
            else
                SyncBuildingSelectionIntoMap(); // whole building
        }


        /// <summary>
        /// Pushes the current facet selection into MapViewModel for overlay highlight.
        /// Used when the user explicitly selects a facet (tree / cable list).
        /// </summary>
        private void SyncSelectionIntoMapVm()
        {
            var shell = System.Windows.Application.Current.MainWindow?.DataContext as MainWindowViewModel;
            var map = shell?.Map;
            if (map == null)
                return;

            int buildingId;
            int storeyId;

            if (SelectedFacet != null)
            {
                // Cables store step_angle2 in BuildingId (not the real owner building).
                // Use SelectedBuilding?.Id for the correct map highlight.
                buildingId = (SelectedFacet.Type == FacetType.Cable)
                    ? (SelectedBuilding?.Id ?? 0)
                    : SelectedFacet.BuildingId;
                storeyId = SelectedFacet.StoreyId;
            }
            else
            {
                buildingId = SelectedBuilding?.Id ?? 0;
                storeyId = SelectedStoreyId;
            }

            map.SelectedBuildingId = buildingId;
            map.SelectedStoreyId = storeyId;
            map.SelectedFacetId = SelectedFacet?.FacetId1;   // non-null => single-facet highlight
        }

        /// <summary>
        /// Pushes only the building/storey selection to MapViewModel,
        /// with no specific facet selected (highlight ALL facets for that building).
        /// </summary>
        private void SyncBuildingSelectionIntoMap()
        {
            var shell = System.Windows.Application.Current.MainWindow?.DataContext as MainWindowViewModel;
            var map = shell?.Map;
            if (map == null)
                return;

            map.SelectedBuildingId = SelectedBuilding?.Id ?? 0;
            map.SelectedStoreyId = SelectedStoreyId;      // storey currently in view
            map.SelectedFacetId = null;                 // whole-building/storey highlight
        }

        // ---------- Type name / cable helpers ----------

        private static string FacetTypeName(FacetType code) => code switch
        {
            FacetType.Normal => "Wall",
            FacetType.Roof => "Roof",
            FacetType.Wall => "Wall",
            FacetType.RoofQuad => "RoofQuad",
            FacetType.FloorPoints => "FloorPoints",
            FacetType.FireEscape => "FireEscape",
            FacetType.Staircase => "Staircase",
            FacetType.Skylight => "Skylight",
            FacetType.Cable => "Cable",
            FacetType.Fence => "Fence",
            FacetType.FenceBrick => "FenceBrick",
            FacetType.Ladder => "Ladder",
            FacetType.FenceFlat => "FenceFlat",
            FacetType.Trench => "Trench",
            FacetType.JustCollision => "JustCollision",
            FacetType.Partition => "Partition",
            FacetType.Inside => "Inside",
            FacetType.Door => "Door",
            FacetType.InsideDoor => "InsideDoor",
            FacetType.OInside => "OInside",
            FacetType.OutsideDoor => "OutsideDoor",
            (FacetType)100 => "NormalFoundation",
            _ => $"Type{(byte)code}"
        };

        private static string CableStyleDisplay(DFacetRec f)
        {
            // Cable fields are repurposed:
            // StyleIndex  -> step_angle1 (SWORD)
            // Building    -> step_angle2 (SWORD)  <-- not a real building id for cables!
            // Height      -> count; mode encoded in low bits
            short step1 = unchecked((short)f.StyleIndex);
            short step2 = unchecked((short)f.Building);
            int mode = f.Height & 0x3;
            return $"Cable: step1={step1}, step2={step2}, mode={mode}";
        }

        /// <summary>
        /// Returns true if the given facet type should be shown under the
        /// current Walls/Fences/Doors/Ladders filter combo.
        /// If no filter is active, everything is allowed.
        /// </summary>
        private bool IsFacetTypeAllowedByFilter(FacetType type)
        {
            // No filter ==> show all groups
            if (!AnyFacetFilterActive)
                return true;

            // Walls pill: treat "wall-ish" as Wall (you can expand this later)
            if (_filterWalls)
            {
                if (type == FacetType.Wall ||
                    type == FacetType.Inside ||
                    type == FacetType.OInside ||
                    type == FacetType.NormalFoundation ||
                    type == FacetType.Inside ||
                    type == FacetType.Normal ||
                    type == FacetType.OInside)
                    return true;
            }

            // Fences pill: all fence-related types
            if (_filterFences)
            {
                if (type == FacetType.Fence ||
                    type == FacetType.FenceBrick ||
                    type == FacetType.FenceFlat)
                    return true;
            }

            // Doors pill: door types (not gates)
            if (_filterDoors)
            {
                if (type == FacetType.Door ||
                    type == FacetType.InsideDoor ||
                    type == FacetType.OInside)
                    return true;
            }

            // Gates pill: fence gate type
            if (_filterGates)
            {
                if (type == FacetType.OutsideDoor)
                    return true;
            }

            // Ladders pill
            if (_filterLadders)
            {
                if (type == FacetType.Ladder)
                    return true;
            }

            // If we got here: none of the active filters matched this type
            return false;
        }

        // ---------- Inner types, INotify, Relay ----------

        public sealed class BuildingVM
        {
            public int Id { get; set; }
            public BuildingType BuildingType { get; set; } = BuildingType.House;
            public int FacetCount { get; set; }
            public bool IsPhantom { get; set; }
            public bool IsCablesBucket { get; set; }

            public string DisplayName =>
                IsCablesBucket ? "Cables"
                : (IsPhantom ? $"B#{Id} (phantom)" : $"B#{Id}");

            /// <summary>Short display name for compact list (e.g., "B#1")</summary>
            public string ShortDisplayName => IsCablesBucket ? "Cables" : $"B#{Id}";

            public ObservableCollection<StoreyVM> Storeys { get; } = new();

            public IEnumerable<StoreyVM> VisibleStoreys =>
                Storeys.Count > 1 ? Storeys : Array.Empty<StoreyVM>();
        }

        public sealed class StoreyVM
        {
            public int StoreyId { get; set; }
            public int UsageCount { get; set; }
            public ObservableCollection<FacetTypeGroupVM> Groups { get; } = new();

            public StoreyVM() { }
            public StoreyVM(int id) { StoreyId = id; }
            // NEW: text used in the building tree
            public string DisplayName => $"Storey {StoreyId}";
        }

        public sealed class FacetTypeGroupVM
        {
            public FacetType Type { get; set; }
            public string TypeName { get; set; } = "";
            public int Count { get; set; }

            // CHANGED: List<FacetVM> to ObservableCollection<FacetVM> for binding
            public ObservableCollection<FacetVM> Facets { get; set; } = new();

            public FacetTypeGroupVM() { }
            public FacetTypeGroupVM(FacetType type)
            {
                Type = type;
                TypeName = type.ToString();
            }
        }

        public sealed class FacetVM
        {
            public int FacetId1 { get; set; }
            public FacetType Type { get; set; }
            public ushort StyleIndex { get; set; }
            public string StyleDisplay { get; set; } = "";   // shown in tree + details
            public bool IsPainted { get; init; }
            public string Coords { get; set; } = "";
            public byte Height { get; set; }
            public byte FHeight { get; set; }
            public string Flags { get; set; } = "";
            public int BuildingId { get; set; }  // DBuilding id (1-based)
            public int StoreyId { get; set; }
            public short Y0 { get; set; }
            public short Y1 { get; set; }
            public byte BlockHeight { get; set; }
            public byte Open { get; set; }

            // Raw cable angles for cable-style formatting
            public short CableStep1 { get; set; } // from StyleIndex (as S16)
            public short CableStep2 { get; set; } // from raw facet.Building (as S16)

            public FacetVM() { }

            public FacetVM(int id1, DFacetRec f)
            {
                FacetId1 = id1;
                Type = f.Type;
                StyleIndex = f.StyleIndex;
                StyleDisplay = $"{f.StyleIndex} (0x{f.StyleIndex:X4})";
                Coords = $"({f.X0},{f.Z0}) ? ({f.X1},{f.Z1})";
                Height = f.Height;
                FHeight = f.FHeight;
                Flags = $"0x{((ushort)f.Flags):X4}";
                BuildingId = f.Building;
                StoreyId = f.Storey;
                Y0 = f.Y0;
                Y1 = f.Y1;
                BlockHeight = f.BlockHeight;
                Open = f.Open;
            }

            public DFacetRec Raw { get; set; }
        }

        public sealed class StyleVM
        {
            public int Index { get; init; }      // 0-based index in dstyles
            public short Value { get; init; }    // signed entry
            public string Display
                => Value >= 0
                   ? $"[{Index}] raw=0x{((ushort)Value):X4}"
                   : $"[{Index}] painted ? DStorey {-Value}";
        }

        public sealed class TmaStyleVM
        {
            public int Index { get; set; }                  // 0-based or 1-based, your choice when you fill it
            public string Name { get; set; } = "";
            public string FlagsSummary { get; set; } = "";  // e.g., "Textured|TwoSided"
            public ObservableCollection<TmaEntryVM> Entries { get; } = new();
        }

        public sealed class TmaEntryVM
        {
            public byte Page { get; set; }
            public byte Tx { get; set; }
            public byte Ty { get; set; }
            public string FlipDisplay { get; set; } = "";   // e.g., "-", "X", "Y", "XY"
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private sealed class RelayCommand : ICommand
        {
            private readonly Action<object?> _exec;
            private readonly Func<object?, bool>? _can;
            public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
            public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
            public void Execute(object? p) => _exec(p);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}

// ============================================================
// MapEditor/Views/MapOverlays/BuildingLayer.cs
// ============================================================
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using UrbanChaosEditor.Shared.Views.MapOverlays;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.Views.Buildings.MapOverlays
{
    /// <summary>
    /// Building layer for Map Editor - Read-only display with selection highlighting.
    /// Ensures the overlay repaints when:
    ///  - map cleared/loaded/saved
    ///  - buildings/facets changed
    ///  - selection changed (building/facet selection drives "white highlight" visuals)
    /// </summary>
    public sealed class BuildingLayer : SharedBuildingLayer
    {
        private INotifyPropertyChanged? _mapVmNotify;
        private MapViewModel? _mapVm;

        // White pen for highlighting selected building/facet
        private static readonly Pen PenSelectedWhite;

        // Cache of building -> facet ranges for highlight lookup
        private List<(int StartFacet, int EndFacet)>? _buildingFacetRanges;

        static BuildingLayer()
        {
            PenSelectedWhite = new Pen(Brushes.White, 8.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            PenSelectedWhite.Freeze();
        }

        public BuildingLayer()
        {
            // Set up data provider
            SetDataProvider(new MapEditorBuildingProvider());

            // Explicitly subscribe to map events to ensure layer refreshes properly
            MapDataService.Instance.MapCleared += OnMapCleared;
            MapDataService.Instance.MapLoaded += OnMapLoaded;
            MapDataService.Instance.MapSaved += OnMapSaved;

            // Subscribe to building/facet changes (e.g., when facets are added/deleted/modified)
            BuildingsChangeBus.Instance.Changed += OnBuildingsChanged;

            // Hook/unhook selection change events safely
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HookSelectionEvents();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnhookSelectionEvents();
        }

        private void OnMapCleared(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ClearCacheSafe();
                _buildingFacetRanges = null;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void OnMapLoaded(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ClearCacheSafe();
                _buildingFacetRanges = null;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void OnMapSaved(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                // Save might change building bytes; safest to clear cache.
                ClearCacheSafe();
                _buildingFacetRanges = null;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void OnBuildingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ClearCacheSafe();
                _buildingFacetRanges = null;
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private void HookSelectionEvents()
        {
            if (_mapVmNotify != null) return;

            try
            {
                // This matches how you grab Map from MainWindow in other places.
                if (Application.Current?.MainWindow?.DataContext is MainWindowViewModel shell &&
                    shell.Map is MapViewModel mvm)
                {
                    _mapVm = mvm;
                    _mapVmNotify = mvm;
                    _mapVmNotify.PropertyChanged += OnMapVmPropertyChanged;
                    System.Diagnostics.Debug.WriteLine("[BuildingLayer] Successfully hooked MapViewModel for selection events");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[BuildingLayer] WARNING: Could not get MapViewModel reference");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingLayer] Exception hooking events: {ex.Message}");
            }
        }

        private void UnhookSelectionEvents()
        {
            if (_mapVmNotify == null) return;

            try
            {
                _mapVmNotify.PropertyChanged -= OnMapVmPropertyChanged;
            }
            catch
            {
                // ignore
            }
            finally
            {
                _mapVmNotify = null;
                _mapVm = null;
            }
        }

        private void OnMapVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Be robust: selection property names have moved around over time.
            // We repaint if anything selection-related changes.
            if (!IsSelectionPropertyName(e.PropertyName))
                return;

            System.Diagnostics.Debug.WriteLine($"[BuildingLayer] Selection property changed: {e.PropertyName}");

            if (_mapVm != null)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildingLayer]   Current: SelectedFacetId={_mapVm.SelectedFacetId?.ToString() ?? "null"}, SelectedBuildingId={_mapVm.SelectedBuildingId}");
            }

            Dispatcher.BeginInvoke(() =>
            {
                // Repaint to update white highlighting
                InvalidateVisual();
            }, DispatcherPriority.Render);
        }

        private static bool IsSelectionPropertyName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            // Typical names in your project history: SelectedFacetId1, SelectedBuildingId1, SelectedFacetId, etc.
            // Also handles future renames so the layer keeps repainting.
            return name.IndexOf("SelectedFacet", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("SelectedBuilding", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Override RenderFacet to add white selection highlighting.
        /// </summary>
        protected override void RenderFacet(DrawingContext dc, int index, byte facetType, Point p1, Point p2)
        {
            // Ensure building ranges are cached
            EnsureBuildingRangesCached();

            int facetId1 = index + 1; // Convert 0-based to 1-based
            bool isSelected = false;

            if (_mapVm != null)
            {
                // Debug: Log selection state for first few facets
                if (facetId1 <= 3)
                {
                    System.Diagnostics.Debug.WriteLine($"[BuildingLayer] Facet#{facetId1}: SelectedFacetId={_mapVm.SelectedFacetId?.ToString() ?? "null"}, SelectedBuildingId={_mapVm.SelectedBuildingId}");
                }

                // If a specific facet is selected, ONLY that facet gets highlighted
                if (_mapVm.SelectedFacetId.HasValue)
                {
                    isSelected = (_mapVm.SelectedFacetId.Value == facetId1);

                    if (facetId1 <= 3)
                        System.Diagnostics.Debug.WriteLine($"[BuildingLayer]   -> Facet selection mode: isSelected={isSelected}");
                }
                // If no facet selected but a building is selected, highlight all its facets
                else if (_mapVm.SelectedBuildingId > 0)
                {
                    int ownerBuilding = FindBuildingForFacet(facetId1);
                    isSelected = (ownerBuilding == _mapVm.SelectedBuildingId);

                    if (facetId1 <= 3)
                        System.Diagnostics.Debug.WriteLine($"[BuildingLayer]   -> Building selection mode: owner={ownerBuilding}, isSelected={isSelected}");
                }
            }
            else
            {
                if (facetId1 == 1)
                    System.Diagnostics.Debug.WriteLine("[BuildingLayer] WARNING: _mapVm is null!");
            }

            // Draw with white pen if selected, otherwise use standard color
            Pen pen = isSelected ? PenSelectedWhite : GetPenForFacet(facetType);
            dc.DrawLine(pen, p1, p2);

            // Draw direction arrow for ladders (type 12) and doors (type 18)
            // Draw direction arrow for ladders and doors
            if (facetType == 12) // Ladder
            {
                DrawDirectionArrow(dc, p1, p2, PenLadderArrow);
            }
            else if (facetType == 18) // Door
            {
                DrawDirectionArrow(dc, p1, p2, PenDoorPurple);
            }
        }

        /// <summary>
        /// Find which 1-based building ID owns the given 1-based facet ID.
        /// Returns 0 if not found.
        /// </summary>
        private int FindBuildingForFacet(int facetId1)
        {
            if (_buildingFacetRanges == null) return 0;

            for (int i = 0; i < _buildingFacetRanges.Count; i++)
            {
                var range = _buildingFacetRanges[i];
                if (facetId1 >= range.StartFacet && facetId1 < range.EndFacet)
                {
                    return i + 1; // 1-based building ID
                }
            }
            return 0;
        }

        /// <summary>
        /// Cache building -> facet ranges from map data.
        /// </summary>
        private void EnsureBuildingRangesCached()
        {
            if (_buildingFacetRanges != null) return;

            _buildingFacetRanges = new List<(int, int)>();

            if (CachedBytes == null || BuildingRegionStart < 0) return;

            // Parse building records to get facet ranges
            int buildingsOff = BuildingRegionStart + HeaderSize;

            for (int i = 0; i < TotalBuildings; i++)
            {
                int off = buildingsOff + i * DBuildingSize;
                if (off + 4 > CachedBytes.Length) break;

                ushort startFacet = ReadU16(CachedBytes, off + 0);
                ushort endFacet = ReadU16(CachedBytes, off + 2);

                _buildingFacetRanges.Add((startFacet, endFacet));
            }
        }

        /// <summary>
        /// Safely clears cached building region data in the shared layer, without ever throwing
        /// "parameter count mismatch" due to reflection picking the wrong overload.
        /// </summary>
        private void ClearCacheSafe()
        {
            try
            {
                var flags =
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic;

                // Choose parameterless ClearCache() only.
                var method = typeof(SharedBuildingLayer)
                    .GetMethods(flags)
                    .FirstOrDefault(m => m.Name == "ClearCache" && m.GetParameters().Length == 0);

                method?.Invoke(this, Array.Empty<object>());
            }
            catch
            {
                // ignore; InvalidateVisual will still cause a redraw
            }
        }
    }

    /// <summary>
    /// Adapter to connect MapDataService to IBuildingDataProvider.
    /// </summary>
    internal class MapEditorBuildingProvider : IBuildingDataProvider
    {
        public bool IsLoaded => MapDataService.Instance.IsLoaded;

        // NOTE: Shared interface says "copy" but existing code returns the backing array.
        // If the shared layer mutates it (it shouldn't), change to return MapDataService.Instance.GetBytesCopy().
        public byte[]? GetBytesCopy() => MapDataService.Instance.MapBytes;

        public void ComputeAndCacheBuildingRegion()
            => MapDataService.Instance.ComputeAndCacheBuildingRegion();

        public bool TryGetBuildingRegion(out int start, out int length)
            => MapDataService.Instance.TryGetBuildingRegion(out start, out length);

        public void SubscribeMapLoaded(Action callback)
        {
            MapDataService.Instance.MapLoaded += (sender, args) => callback();
        }

        public void SubscribeMapCleared(Action callback)
        {
            MapDataService.Instance.MapCleared += (sender, args) => callback();
        }
    }
}
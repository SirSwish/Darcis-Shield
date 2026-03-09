// ViewModels/Roofs/RoofsTabViewModel.cs
// Manages walkable entries, RoofFace4 entries, and cell altitude operations.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Roofs;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Roofs;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.ViewModels.Roofs
{
    public sealed class RoofsTabViewModel : INotifyPropertyChanged
    {
        // Raw arrays cached from MapDataService
        private DWalkableRec[] _rawWalkables = Array.Empty<DWalkableRec>();
        private RoofFace4Rec[] _rawRoofFaces4 = Array.Empty<RoofFace4Rec>();

        // Current building filter (0 = show all)
        private int _selectedBuildingId;
        public int SelectedBuildingId
        {
            get => _selectedBuildingId;
            set
            {
                if (_selectedBuildingId == value) return;
                _selectedBuildingId = value;
                OnPropertyChanged();
                RebuildWalkablesForBuilding();
                UpdateStatusText();
            }
        }

        /// <summary>Walkables for the current filter.</summary>
        public ObservableCollection<WalkableVM> Walkables { get; } = new();

        /// <summary>RoofFace4 entries for the currently selected walkable.</summary>
        public ObservableCollection<RoofFace4VM> RoofFaces4 { get; } = new();

        private WalkableVM? _selectedWalkable;
        public WalkableVM? SelectedWalkable
        {
            get => _selectedWalkable;
            set
            {
                if (_selectedWalkable == value) return;
                _selectedWalkable = value;
                OnPropertyChanged();
                RebuildRoofFaces4ForWalkable();
                SyncWalkableSelectionIntoMap();
                UpdateStatusText();
            }
        }

        private RoofFace4VM? _selectedRoofFace4;
        public RoofFace4VM? SelectedRoofFace4
        {
            get => _selectedRoofFace4;
            set
            {
                if (_selectedRoofFace4 == value) return;
                _selectedRoofFace4 = value;
                OnPropertyChanged();
            }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        // Reference to MapViewModel for syncing selection highlights
        private MapViewModel? _mapViewModel;
        public MapViewModel? MapViewModel
        {
            get => _mapViewModel;
            set { _mapViewModel = value; OnPropertyChanged(); }
        }

        public RoofsTabViewModel()
        {
            // Subscribe to changes
            RoofsChangeBus.Instance.Changed += (_, _) =>
                System.Windows.Application.Current?.Dispatcher?.Invoke(Refresh);

            // Also listen to BuildingsChangeBus since adding/deleting buildings
            // can affect which walkables are visible
            Services.Buildings.BuildingsChangeBus.Instance.Changed += (_, _) =>
                System.Windows.Application.Current?.Dispatcher?.Invoke(Refresh);

            // Listen to map load
            MapDataService.Instance.MapLoaded += (_, _) =>
                System.Windows.Application.Current?.Dispatcher?.Invoke(Refresh);

            MapDataService.Instance.MapCleared += (_, _) =>
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _rawWalkables = Array.Empty<DWalkableRec>();
                    _rawRoofFaces4 = Array.Empty<RoofFace4Rec>();
                    Walkables.Clear();
                    RoofFaces4.Clear();
                    SelectedWalkable = null;
                    UpdateStatusText();
                });
        }

        // ====================================================================
        // Public API
        // ====================================================================

        public void Refresh()
        {
            LoadFromService();
            RebuildWalkablesForBuilding();
            UpdateStatusText();
        }

        public void HandleWalkableSelection(object? selection)
        {
            if (selection is WalkableVM w)
                SelectedWalkable = w;
            else
                SelectedWalkable = null;
        }

        /// <summary>
        /// Called externally (e.g., from BuildingsTab) when building selection changes.
        /// </summary>
        public void SyncFromBuildingSelection(int buildingId)
        {
            SelectedBuildingId = buildingId;
        }

        // ====================================================================
        // Data Loading
        // ====================================================================

        private void LoadFromService()
        {
            if (!MapDataService.Instance.IsLoaded)
            {
                _rawWalkables = Array.Empty<DWalkableRec>();
                _rawRoofFaces4 = Array.Empty<RoofFace4Rec>();
                return;
            }

            var accessor = new RoofsAccessor(MapDataService.Instance);
            var (walkables, rf4s) = accessor.ReadSnapshot();
            _rawWalkables = walkables;
            _rawRoofFaces4 = rf4s;
        }

        // ====================================================================
        // Walkable List Building
        // ====================================================================

        private void RebuildWalkablesForBuilding()
        {
            Walkables.Clear();
            RoofFaces4.Clear();
            SelectedWalkable = null;

            if (_rawWalkables == null || _rawWalkables.Length <= 1) return;

            int bId = _selectedBuildingId;

            for (int i = 1; i < _rawWalkables.Length; i++)
            {
                var w = _rawWalkables[i];

                // Filter by building if set (0 = show all)
                if (bId > 0 && w.Building != bId) continue;

                // Skip empty rects
                if ((w.X1 | w.Z1 | w.X2 | w.Z2) == 0) continue;

                bool hasFace4 = w.EndFace4 > w.StartFace4;

                Walkables.Add(new WalkableVM
                {
                    WalkableId1 = i,
                    BuildingId = w.Building,
                    X1 = w.X1,
                    Z1 = w.Z1,
                    X2 = w.X2,
                    Z2 = w.Z2,
                    Y = w.Y,
                    StoreyY = w.StoreyY,
                    StartFace4 = w.StartFace4,
                    EndFace4 = w.EndFace4,
                    Next = w.Next,
                    HasRoofFaces4 = hasFace4,
                    StartPoint = w.StartPoint,
                    EndPoint = w.EndPoint,
                });
            }

            OnPropertyChanged(nameof(Walkables));

            // Auto-select first
            var first = Walkables.FirstOrDefault();
            if (first != null)
                SelectedWalkable = first;
        }

        // ====================================================================
        // RoofFace4 List Building
        // ====================================================================

        private void RebuildRoofFaces4ForWalkable()
        {
            RoofFaces4.Clear();

            var w = SelectedWalkable;
            if (w == null) return;

            if (w.EndFace4 <= w.StartFace4) return;
            if (_rawRoofFaces4 == null || _rawRoofFaces4.Length == 0) return;

            int start = Math.Max(0, (int)w.StartFace4);
            int endEx = Math.Min((int)w.EndFace4, _rawRoofFaces4.Length);

            for (int i = start; i < endEx; i++)
            {
                var rf = _rawRoofFaces4[i];

                RoofFaces4.Add(new RoofFace4VM
                {
                    FaceId = i,
                    Y = rf.Y,
                    DY0 = rf.DY0,
                    DY1 = rf.DY1,
                    DY2 = rf.DY2,
                    RX = rf.RX,
                    RZ = rf.RZ,
                    DrawFlags = rf.DrawFlags,
                    Next = rf.Next
                });
            }

            OnPropertyChanged(nameof(RoofFaces4));
        }

        // ====================================================================
        // Status
        // ====================================================================

        private void UpdateStatusText()
        {
            int totalWalkables = _rawWalkables.Length > 1 ? _rawWalkables.Length - 1 : 0;
            int totalRf4 = _rawRoofFaces4.Length > 0 ? _rawRoofFaces4.Length : 0;

            string filter = _selectedBuildingId > 0
                ? $"Building #{_selectedBuildingId}"
                : "All buildings";

            string walkInfo = SelectedWalkable != null
                ? $" | W#{SelectedWalkable.WalkableId1}: {RoofFaces4.Count} RF4"
                : "";

            StatusText = $"{filter}: {Walkables.Count}/{totalWalkables} walkables, {totalRf4} total RF4{walkInfo}";
        }

        // ====================================================================
        // Map Selection Sync
        // ====================================================================

        private void SyncWalkableSelectionIntoMap()
        {
            if (_mapViewModel == null) return;
            _mapViewModel.SelectedWalkableId1 = SelectedWalkable?.WalkableId1 ?? 0;
        }

        // ====================================================================
        // INotifyPropertyChanged
        // ====================================================================

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
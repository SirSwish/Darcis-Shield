using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class BuildingPreviewWindow : Window, INotifyPropertyChanged
    {
        private const int DBuildingSize = 24;

        // ============================================================
        // Identity / snapshot
        // ============================================================

        public int BuildingId1 { get; }
        public DBuildingRec Building { get; private set; }
        public int FacetCount => Math.Max(0, Building.EndFacet - Building.StartFacet);
        public string TypeDisplay => Building.TypeDisplay;

        // ============================================================
        // Editable fields
        // ============================================================

        private int _editWorldX;
        public int EditWorldX
        {
            get => _editWorldX;
            set { _editWorldX = value; OnPropertyChanged(); }
        }

        private int _editWorldY;
        public int EditWorldY
        {
            get => _editWorldY;
            set { _editWorldY = value; OnPropertyChanged(); }
        }

        private int _editWorldZ;
        public int EditWorldZ
        {
            get => _editWorldZ;
            set { _editWorldZ = value; OnPropertyChanged(); }
        }

        // ============================================================
        // Raw bytes display
        // ============================================================

        private string _rawBytesHex = "";
        public string RawBytesHex
        {
            get => _rawBytesHex;
            private set { _rawBytesHex = value; OnPropertyChanged(); }
        }

        private string _rawBytesOffsetHex = "";
        public string RawBytesOffsetHex
        {
            get => _rawBytesOffsetHex;
            private set { _rawBytesOffsetHex = value; OnPropertyChanged(); }
        }

        private int _fileOffset;

        // ============================================================
        // Constructor
        // ============================================================

        public BuildingPreviewWindow(DBuildingRec building, int buildingId1)
        {
            Building = building;
            BuildingId1 = buildingId1;

            _editWorldX = building.WorldX;
            _editWorldY = building.WorldY;
            _editWorldZ = building.WorldZ;

            InitializeComponent();
            DataContext = this;

            LoadRawBytes();
        }

        // ============================================================
        // Raw bytes
        // ============================================================

        private void LoadRawBytes()
        {
            RawBytesHex = "";
            RawBytesOffsetHex = "";

            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                RawBytesHex = "<no map loaded>";
                return;
            }

            var acc = new BuildingsAccessor(svc);
            if (!acc.TryGetBuildingBytes(BuildingId1, out var raw, out int off) || raw == null)
            {
                RawBytesHex = "<unavailable>";
                return;
            }

            _fileOffset = off;
            RawBytesOffsetHex = $"0x{off:X}";
            RawBytesHex = BitConverter.ToString(raw).Replace("-", " ");
        }

        // ============================================================
        // Apply — only world X/Y/Z
        // ============================================================

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            var svc = MapDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("No map loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var acc = new BuildingsAccessor(svc);
            if (!acc.TryGetBuildingBytes(BuildingId1, out _, out int off))
            {
                MessageBox.Show("Cannot locate building in file.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var bytes = svc.GetBytesCopy();
            if (off < 0 || off + DBuildingSize > bytes.Length)
            {
                MessageBox.Show("Building offset out of range.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // +4: X (S32)
            BitConverter.GetBytes(_editWorldX).CopyTo(bytes, off + 4);

            // +8: Y (S24) -- preserve byte +11 because that is Type
            bytes[off + 8] = (byte)(_editWorldY & 0xFF);
            bytes[off + 9] = (byte)((_editWorldY >> 8) & 0xFF);
            bytes[off + 10] = (byte)((_editWorldY >> 16) & 0xFF);

            // +12: Z (S32)
            BitConverter.GetBytes(_editWorldZ).CopyTo(bytes, off + 12);

            svc.ReplaceBytes(bytes);
            svc.MarkDirty();

            // Re-read snapshot so UI stays in sync
            var snap = acc.ReadSnapshot();
            if (BuildingId1 >= 1 && BuildingId1 <= snap.Buildings.Length)
            {
                Building = snap.Buildings[BuildingId1 - 1];
                EditWorldX = Building.WorldX;
                EditWorldY = Building.WorldY;
                EditWorldZ = Building.WorldZ;

                OnPropertyChanged(nameof(Building));
                OnPropertyChanged(nameof(FacetCount));
                OnPropertyChanged(nameof(TypeDisplay));
            }

            LoadRawBytes();

            BuildingsChangeBus.Instance.NotifyBuildingChanged(BuildingId1, BuildingChangeType.Modified);

            Debug.WriteLine($"[BuildingPreview] Applied XYZ changes to Building #{BuildingId1} @ 0x{off:X}");

            MessageBox.Show($"Building #{BuildingId1} position updated.", "Applied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ============================================================
        // INotifyPropertyChanged
        // ============================================================

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
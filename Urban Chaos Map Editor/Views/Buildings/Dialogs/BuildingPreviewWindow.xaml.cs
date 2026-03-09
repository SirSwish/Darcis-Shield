// Views/Buildings/Dialogs/BuildingPreviewWindow.xaml.cs
// Editable Building Editor — reads and writes all DBuildingRec fields.
//
// On-disk DBuilding layout (24 bytes):
//   +0:  StartFacet  U16       (read-only — managed by BuildingAdder/Deleter)
//   +2:  EndFacet    U16       (read-only — managed by BuildingAdder/Deleter)
//   +4:  X           S32       (world coordinate)
//   +8:  Y           S24       (3 bytes, world coordinate — lower 24 bits of the SLONG)
//   +11: Type        U8        (0=House, 1=Warehouse, 2=Office, 3=Apartment, etc.)
//   +12: Z           S32       (world coordinate)
//   +16: Walkable    U16       (1-based index into DWalkable; 0=none)
//   +18: Counter[0]  U8
//   +19: Counter[1]  U8
//   +20: Padding     U16
//   +22: Ware        U8        (index into WARE_ware[])
//   +23: (unused)    U8

using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class BuildingPreviewWindow : Window, INotifyPropertyChanged
    {
        private const int DBuildingSize = 24;

        // ====================================================================
        // Read-only identity
        // ====================================================================

        public int BuildingId1 { get; }
        public DBuildingRec Building { get; private set; }
        public int FacetCount => Math.Max(0, Building.EndFacet - Building.StartFacet);

        // ====================================================================
        // Editable fields (bound TwoWay from XAML)
        // ====================================================================

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

        private byte _editType;
        public byte EditType
        {
            get => _editType;
            set { _editType = value; OnPropertyChanged(); }
        }

        private ushort _editWalkable;
        public ushort EditWalkable
        {
            get => _editWalkable;
            set { _editWalkable = value; OnPropertyChanged(); }
        }

        private byte _editWare;
        public byte EditWare
        {
            get => _editWare;
            set { _editWare = value; OnPropertyChanged(); }
        }

        private byte _editCounter0;
        public byte EditCounter0
        {
            get => _editCounter0;
            set { _editCounter0 = value; OnPropertyChanged(); }
        }

        private byte _editCounter1;
        public byte EditCounter1
        {
            get => _editCounter1;
            set { _editCounter1 = value; OnPropertyChanged(); }
        }

        private ushort _editPadding;
        public ushort EditPadding
        {
            get => _editPadding;
            set { _editPadding = value; OnPropertyChanged(); }
        }

        // ====================================================================
        // Raw bytes display
        // ====================================================================

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

        // ====================================================================
        // Type combo items
        // ====================================================================

        private static readonly (byte Value, string Label)[] BuildingTypes =
        {
            (0, "0 — House"),
            (1, "1 — Warehouse"),
            (2, "2 — Office"),
            (3, "3 — Apartment"),
            (4, "4 — Crate (In)"),
            (5, "5 — Crate (Out)"),
        };

        // ====================================================================
        // Constructor
        // ====================================================================

        public BuildingPreviewWindow(DBuildingRec building, int buildingId1)
        {
            InitializeComponent();
            DataContext = this;

            Building = building;
            BuildingId1 = buildingId1;

            // Populate editable fields from snapshot
            _editWorldX = building.WorldX;
            _editWorldY = building.WorldY;
            _editWorldZ = building.WorldZ;
            _editType = building.Type;
            _editWalkable = building.Walkable;
            _editWare = building.Ware;
            _editCounter0 = building.Counter0;
            _editCounter1 = building.Counter1;
            _editPadding = 0; // Read from file below

            // Populate type combo
            foreach (var (val, label) in BuildingTypes)
                CmbType.Items.Add(new ComboBoxItem { Content = label, Tag = val });

            // Select current type
            int typeIdx = Array.FindIndex(BuildingTypes, t => t.Value == building.Type);
            CmbType.SelectedIndex = typeIdx >= 0 ? typeIdx : 0;

            // Type display
            TxtTypeDisplay.Text = building.TypeDisplay;

            // Read padding from file (offset +20, 2 bytes)
            LoadRawBytes();
        }

        // ====================================================================
        // Raw bytes + padding read
        // ====================================================================

        private int _fileOffset;

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

            // Read padding from raw bytes (offset +20 within the 24-byte struct)
            if (raw.Length >= 22)
                _editPadding = (ushort)(raw[20] | (raw[21] << 8));

            OnPropertyChanged(nameof(EditPadding));
        }

        // ====================================================================
        // Type combo handler
        // ====================================================================

        private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbType.SelectedItem is ComboBoxItem item && item.Tag is byte val)
            {
                EditType = val;
                TxtTypeDisplay.Text = ((BuildingType)val) switch
                {
                    BuildingType.House => "House",
                    BuildingType.Warehouse => "Warehouse",
                    BuildingType.Office => "Office",
                    BuildingType.Apartment => "Apartment",
                    _ => $"Type {val}"
                };
            }
        }

        // ====================================================================
        // Apply — write all editable fields back to the file buffer
        // ====================================================================

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
            if (off + DBuildingSize > bytes.Length)
            {
                MessageBox.Show("Building offset out of range.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Write all fields at their confirmed on-disk offsets
            // +0:  StartFacet (U16) — NOT editable, skip
            // +2:  EndFacet (U16)   — NOT editable, skip
            // +4:  X (S32)
            BitConverter.GetBytes(_editWorldX).CopyTo(bytes, off + 4);
            // +8:  Y (S24 — lower 3 bytes of S32, upper byte is Type)
            bytes[off + 8] = (byte)(_editWorldY & 0xFF);
            bytes[off + 9] = (byte)((_editWorldY >> 8) & 0xFF);
            bytes[off + 10] = (byte)((_editWorldY >> 16) & 0xFF);
            // +11: Type (U8)
            bytes[off + 11] = _editType;
            // +12: Z (S32)
            BitConverter.GetBytes(_editWorldZ).CopyTo(bytes, off + 12);
            // +16: Walkable (U16)
            bytes[off + 16] = (byte)(_editWalkable & 0xFF);
            bytes[off + 17] = (byte)(_editWalkable >> 8);
            // +18: Counter[0], Counter[1]
            bytes[off + 18] = _editCounter0;
            bytes[off + 19] = _editCounter1;
            // +20: Padding (U16)
            bytes[off + 20] = (byte)(_editPadding & 0xFF);
            bytes[off + 21] = (byte)(_editPadding >> 8);
            // +22: Ware (U8)
            bytes[off + 22] = _editWare;
            // +23: unused — leave as-is

            svc.ReplaceBytes(bytes);
            svc.MarkDirty();

            // Refresh display
            var newSnap = acc.ReadSnapshot();
            if (BuildingId1 >= 1 && BuildingId1 <= newSnap.Buildings.Length)
            {
                Building = newSnap.Buildings[BuildingId1 - 1];
                OnPropertyChanged(nameof(Building));
                OnPropertyChanged(nameof(FacetCount));
            }

            LoadRawBytes();

            // Notify the rest of the app
            BuildingsChangeBus.Instance.NotifyBuildingChanged(BuildingId1, BuildingChangeType.Modified);

            Debug.WriteLine($"[BuildingEditor] Applied changes to Building #{BuildingId1} @ 0x{off:X}");

            MessageBox.Show($"Building #{BuildingId1} updated.", "Applied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ====================================================================
        // INotifyPropertyChanged
        // ====================================================================

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
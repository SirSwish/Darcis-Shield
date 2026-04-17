// /Views/Terrain/Dialogs/PapHiCellEditorDialog.xaml.cs
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UrbanChaosMapEditor.Services.Core;

namespace UrbanChaosMapEditor.Views.Heights.Dialogs
{
    public partial class PapHiCellEditorDialog : Window
    {
        private const int HeaderBytes = 8;
        private const int BytesPerTile = 6;
        private const int TilesPerSide = 128;
        private const int Off_Flags = 2;

        private readonly int _gameX;
        private readonly int _gameZ;
        private bool _suppressFlagSync;
        private CheckBox[] _flagCheckBoxes = null!;

        /// <summary>True if the user clicked Apply (caller should refresh the layer).</summary>
        public bool Applied { get; private set; }

        public PapHiCellEditorDialog(int gameX, int gameZ)
        {
            InitializeComponent();

            _gameX = gameX;
            _gameZ = gameZ;

            _flagCheckBoxes = new CheckBox[]
            {
                ChkShadow1, ChkShadow2, ChkShadow3, ChkReflective,
                ChkHidden,  ChkSinkSquare, ChkSinkPoint, ChkNoUpper,
                ChkNoGo,    ChkRoofExists, ChkZone1, ChkZone2,
                ChkZone3,   ChkZone4, ChkFlatRoof, ChkWater
            };

            LoadFlags();
        }

        private static int CalcTileOffset(int gx, int gz)
        {
            return HeaderBytes + (gx * TilesPerSide + gz) * BytesPerTile;
        }

        private void LoadFlags()
        {
            TxtCellInfo.Text = $"Cell ({_gameX}, {_gameZ})";

            if (!MapDataService.Instance.IsLoaded)
            {
                TxtFlagsHexDisplay.Text = "No map loaded";
                return;
            }

            var bytes = MapDataService.Instance.GetBytesCopy();
            int offset = CalcTileOffset(_gameX, _gameZ);

            if (offset + BytesPerTile > bytes.Length)
            {
                TxtFlagsHexDisplay.Text = "Offset out of range";
                return;
            }

            ushort flags = (ushort)(bytes[offset + Off_Flags] | (bytes[offset + Off_Flags + 1] << 8));
            TxtFlagsHexDisplay.Text = $"Flags: 0x{flags:X4}";
            SyncCheckBoxesFromFlags(flags);
        }

        private void SyncCheckBoxesFromFlags(ushort flags)
        {
            _suppressFlagSync = true;
            for (int bit = 0; bit < 16; bit++)
                _flagCheckBoxes[bit].IsChecked = (flags & (1 << bit)) != 0;
            _suppressFlagSync = false;
        }

        private ushort ReadFlagsFromCheckBoxes()
        {
            ushort flags = 0;
            for (int bit = 0; bit < 16; bit++)
            {
                if (_flagCheckBoxes[bit].IsChecked == true)
                    flags |= (ushort)(1 << bit);
            }
            return flags;
        }

        private void FlagCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressFlagSync) return;
            ushort flags = ReadFlagsFromCheckBoxes();
            TxtFlagsHexDisplay.Text = $"Flags: 0x{flags:X4}";
            WriteFlags(flags);
        }

        private void WriteFlags(ushort flags)
        {
            if (!MapDataService.Instance.IsLoaded) return;

            var bytes = MapDataService.Instance.GetBytesCopy();
            int offset = CalcTileOffset(_gameX, _gameZ) + Off_Flags;
            if (offset + 1 >= bytes.Length) return;

            bytes[offset]     = (byte)(flags & 0xFF);
            bytes[offset + 1] = (byte)(flags >> 8);
            MapDataService.Instance.ReplaceBytes(bytes);
            Applied = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
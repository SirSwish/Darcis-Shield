// /Views/Heights/Dialogs/HeightStampCreatorDialog.xaml.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosMapEditor.Models.Heights;
using UrbanChaosMapEditor.Services.Heights;

namespace UrbanChaosMapEditor.Views.Heights.Dialogs
{
    public partial class HeightStampCreatorDialog : Window
    {
        // ── Cell view-model ────────────────────────────────────────────────────
        public sealed class CellVm : INotifyPropertyChanged
        {
            public int Col { get; }
            public int Row { get; }

            private string _value = "0";
            public string Value
            {
                get => _value;
                set { if (_value != value) { _value = value; OnPropertyChanged(); } }
            }

            public string ToolTip => $"col {Col}, row {Row}";

            public CellVm(int col, int row, sbyte initial = 0)
            {
                Col = col;
                Row = row;
                _value = initial.ToString();
            }

            public sbyte ParsedValue()
            {
                if (sbyte.TryParse(Value, out sbyte v)) return v;
                return 0;
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? n = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        // ── State ──────────────────────────────────────────────────────────────
        private static readonly Regex _signedInt = new(@"^-?\d*$");

        /// <summary>The saved stamp — non-null only after successful save.</summary>
        public HeightStamp? Result { get; private set; }

        // ── Constructor ────────────────────────────────────────────────────────
        public HeightStampCreatorDialog()
        {
            InitializeComponent();
        }

        // ── Grid generation ───────────────────────────────────────────────────
        private void GenerateGrid_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtWidth.Text, out int w) || w < 1 || w > 32 ||
                !int.TryParse(TxtHeight.Text, out int h) || h < 1 || h > 32)
            {
                TxtStatus.Text = "Width and Height must be 1–32.";
                return;
            }

            // Preserve existing cell values where grid overlaps
            var existing = CollectExistingValues();

            TxtGridHint.Visibility = Visibility.Collapsed;

            var rows = new List<List<CellVm>>();
            for (int row = 0; row < h; row++)
            {
                var rowList = new List<CellVm>();
                for (int col = 0; col < w; col++)
                {
                    sbyte prev = existing.TryGetValue((col, row), out sbyte v) ? v : (sbyte)0;
                    rowList.Add(new CellVm(col, row, prev));
                }
                rows.Add(rowList);
            }

            GridRows.ItemsSource = rows;
            TxtStatus.Text = $"Grid {w}×{h} ready. Centre: col {w / 2}, row {h / 2}.";
        }

        private Dictionary<(int col, int row), sbyte> CollectExistingValues()
        {
            var dict = new Dictionary<(int, int), sbyte>();
            if (GridRows.ItemsSource is not List<List<CellVm>> rows) return dict;
            foreach (var row in rows)
                foreach (var cell in row)
                    dict[(cell.Col, cell.Row)] = cell.ParsedValue();
            return dict;
        }

        // ── Input validation ──────────────────────────────────────────────────
        private void Size_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }

        private void Cell_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) return;
            var prospective = tb.Text.Remove(tb.SelectionStart, tb.SelectionLength)
                                     .Insert(tb.SelectionStart, e.Text);
            // Allow partial entries like "-" or "-1" etc.
            e.Handled = !_signedInt.IsMatch(prospective);
        }

        private void Cell_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private void Cell_LostFocus(object sender, RoutedEventArgs e)
        {
            // Clamp on focus loss
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not CellVm cell) return;

            if (!sbyte.TryParse(tb.Text, out sbyte clamped))
            {
                // Try clamping if out of range
                if (int.TryParse(tb.Text, out int raw))
                    clamped = (sbyte)Math.Clamp(raw, -128, 127);
                else
                    clamped = 0;
            }
            cell.Value = clamped.ToString();
        }

        // ── Save / Cancel ─────────────────────────────────────────────────────
        private void SaveStamp_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtStampName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TxtStatus.Text = "Please enter a stamp name.";
                return;
            }

            if (GridRows.ItemsSource is not List<List<CellVm>> rows || rows.Count == 0)
            {
                TxtStatus.Text = "Generate the grid first.";
                return;
            }

            int h = rows.Count;
            int w = rows[0].Count;
            var values = new sbyte[w * h];

            for (int row = 0; row < h; row++)
                for (int col = 0; col < w; col++)
                    values[row * w + col] = rows[row][col].ParsedValue();

            var stamp = new HeightStamp
            {
                Name = name,
                Width = w,
                Height = h,
                Values = values
            };

            try
            {
                StampLibraryService.Instance.SaveCustomStamp(stamp);
                Result = stamp;
                TxtStatus.Text = $"Saved '{name}'.";
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Save failed: {ex.Message}";
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

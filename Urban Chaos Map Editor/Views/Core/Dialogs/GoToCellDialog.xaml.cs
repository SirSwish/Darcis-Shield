// /Views/Dialogs/Map/GoToCellDialog.xaml.cs
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace UrbanChaosMapEditor.Views.Core.Dialogs
{
    public partial class GoToCellDialog : Window
    {
        private static readonly Regex _digits = new(@"^\d+$");
        public int Tx { get; private set; } = 0;
        public int Ty { get; private set; } = 0;

        public GoToCellDialog(int initTx = 0, int initTy = 0)
        {
            InitializeComponent();
            TxtX.Text = initTx.ToString();
            TxtY.Text = initTy.ToString();
            Loaded += (_, __) => TxtX.Focus();
        }

        private void DigitsOnly(object sender, TextCompositionEventArgs e) => e.Handled = !_digits.IsMatch(e.Text);

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text)) { e.CancelCommand(); return; }
            var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
            if (!_digits.IsMatch(text)) e.CancelCommand();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtX.Text, out var tx) || !int.TryParse(TxtY.Text, out var ty)) return;
            if (tx < 0 || tx > 127 || ty < 0 || ty > 127)
            {
                MessageBox.Show("Please enter values between 0 and 127.", "Out of range",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Tx = tx; Ty = ty;
            DialogResult = true;
        }
    }
}

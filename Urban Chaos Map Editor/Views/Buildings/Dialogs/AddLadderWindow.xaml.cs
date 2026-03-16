using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using UrbanChaosMapEditor.ViewModels.Core;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public partial class AddLadderWindow : Window
    {
        private static readonly Regex _digitsOnly = new Regex(@"^[0-9]+$");
        private static readonly Regex _signedDigitsOnly = new Regex(@"^-?[0-9]*$");

        private readonly int _buildingId1;

        public bool WasCancelled { get; private set; } = true;

        // Properties for the ladder template
        public byte Height { get; private set; } = 4;
        public byte FHeight { get; private set; } = 0;
        public byte BlockHeight { get; private set; } = 16;
        public short Y0 { get; private set; } = 0;
        public short Y1 { get; private set; } = 0;
        public ushort StyleIndex { get; private set; } = 1;

        public AddLadderWindow(int buildingId1)
        {
            InitializeComponent();
            _buildingId1 = buildingId1;

            TxtBuildingInfo.Text = $"Building #{buildingId1}";
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !_digitsOnly.IsMatch(e.Text);
        }

        private void SignedNumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as System.Windows.Controls.TextBox;
            string newText = textBox?.Text.Insert(textBox.SelectionStart, e.Text) ?? e.Text;
            e.Handled = !string.IsNullOrEmpty(newText) &&
                        newText != "-" &&
                        !_signedDigitsOnly.IsMatch(newText);
        }

        private bool ParseAndValidate()
        {
            if (!byte.TryParse(TxtHeight.Text, out byte height)) height = 4;
            Height = height;

            if (!short.TryParse(TxtY0.Text, out short y0)) y0 = 0;
            Y0 = y0;
            Y1 = y0;       // mirror Y0 into Y1

            FHeight = 0;   // forced
            BlockHeight = 16; // forced
            StyleIndex = 1;   // forced

            return true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            WasCancelled = true;
            Close();
        }

        private void BtnPlace_Click(object sender, RoutedEventArgs e)
        {
            if (!ParseAndValidate())
                return;

            if (Application.Current.MainWindow?.DataContext is MainWindowViewModel mainVm)
            {
                var template = new LadderTemplate
                {
                    Height = Height,
                    FHeight = FHeight,
                    BlockHeight = BlockHeight,
                    Y0 = Y0,
                    Y1 = Y1,
                    StyleIndex = StyleIndex,
                    BuildingId1 = _buildingId1,
                    Storey = 0
                };

                mainVm.Map.BeginLadderPlacement(this, template);

                Hide();

                mainVm.StatusMessage = $"Click to place ladder for Building #{_buildingId1}. Right-click to cancel.";
            }

            WasCancelled = false;
        }

        /// <summary>
        /// Called when placement is cancelled (right-click).
        /// </summary>
        public void OnPlacementCancelled()
        {
            WasCancelled = true;
            Close();
        }

        /// <summary>
        /// Called when ladder is placed successfully.
        /// </summary>
        public void OnPlacementCompleted()
        {
            WasCancelled = false;
            Close();
        }
    }

    /// <summary>
    /// Template for creating a new ladder.
    /// </summary>
    public sealed class LadderTemplate
    {
        public byte Height { get; init; }
        public byte FHeight { get; init; }
        public byte BlockHeight { get; init; }
        public short Y0 { get; init; }
        public short Y1 { get; init; }
        public ushort StyleIndex { get; init; }
        public int BuildingId1 { get; init; }
        public int Storey { get; init; }
    }
}
using System.Windows;

namespace UrbanChaosMapEditor.Views.Buildings.Dialogs
{
    public enum MoveBuildingResult { None, Move, Copy }

    public partial class MoveBuildingDialog : Window
    {
        public MoveBuildingResult Choice { get; private set; } = MoveBuildingResult.None;

        public MoveBuildingDialog()
        {
            InitializeComponent();
        }

        private void BtnMove_Click(object sender, RoutedEventArgs e)
        {
            Choice = MoveBuildingResult.Move;
            DialogResult = true;
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Choice = MoveBuildingResult.Copy;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = MoveBuildingResult.None;
            DialogResult = false;
        }
    }
}

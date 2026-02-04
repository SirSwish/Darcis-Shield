using System.Windows;
using UrbanChaosStoryboardEditor.Models;

namespace UrbanChaosStoryboardEditor.Views.Dialogs
{
    public partial class DistrictEditDialog : Window
    {
        private readonly District _district;

        public DistrictEditDialog(District district)
        {
            InitializeComponent();
            _district = district;

            TxtId.Text = district.DistrictId.ToString();
            TxtName.Text = district.DistrictName;
            TxtXPos.Text = district.XPos.ToString();
            TxtYPos.Text = district.YPos.ToString();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _district.DistrictName = TxtName.Text;

            if (int.TryParse(TxtXPos.Text, out int x))
                _district.XPos = x;

            if (int.TryParse(TxtYPos.Text, out int y))
                _district.YPos = y;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
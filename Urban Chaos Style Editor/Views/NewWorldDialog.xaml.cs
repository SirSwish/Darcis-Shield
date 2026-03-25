// /Views/NewWorldDialog.xaml.cs
using System;
using System.Windows;

namespace UrbanChaosStyleEditor.Views
{
    public partial class NewWorldDialog : Window
    {
        public int WorldNumber { get; private set; }

        public NewWorldDialog()
        {
            InitializeComponent();
            TxtWorldNumber.Focus();
            TxtWorldNumber.SelectAll();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtWorldNumber.Text.Trim(), out int num) || num < 0 || num > 255)
            {
                MessageBox.Show("Please enter a valid number between 0 and 255.",
                    "Invalid Number", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WorldNumber = num;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.Dialogs
{
    /// <summary>
    /// Read-only dialog for viewing EventPoint details
    /// </summary>
    public partial class EventPointDetailDialog : Window
    {
        public EventPointDetailDialog(EventPointViewModel eventPoint)
        {
            InitializeComponent();
            DataContext = eventPoint;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Show the dialog for an EventPoint
        /// </summary>
        public static void ShowDialog(EventPointViewModel eventPoint, Window owner)
        {
            var dialog = new EventPointDetailDialog(eventPoint)
            {
                Owner = owner
            };
            dialog.ShowDialog();
        }
    }
}

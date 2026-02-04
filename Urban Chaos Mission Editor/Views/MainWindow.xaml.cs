using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using UrbanChaosMissionEditor.ViewModels;
using UrbanChaosMissionEditor.Views.Dialogs;

namespace UrbanChaosMissionEditor.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            // Handle command line arguments
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && System.IO.File.Exists(args[1]))
            {
                ((MainViewModel)DataContext).LoadMission(args[1]);
            }
        }

        private void CategoryPill_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.DataContext is CategoryFilterViewModel filter)
            {
                filter.Toggle();
            }
        }

        private void EventPointsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.SelectedEventPoint != null)
            {
                EventPointDetailDialog.ShowDialog(vm.SelectedEventPoint, this);
            }
        }
    }
}
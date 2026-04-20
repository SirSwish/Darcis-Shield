using System.Reflection;
using System.Windows;
using UrbanChaosLightEditor.ViewModels;

namespace UrbanChaosLightEditor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var asm = Assembly.GetExecutingAssembly();
            string version =
                asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString()
                ?? "Unknown";

            MessageBox.Show(
                "Urban Chaos Light Editor\n\n" +
                $"Version: {version}\n" +
                "Part of the Darcis Shield modding toolkit.",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes to the lights file.\n\nSave before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    vm.SaveLightsCommand.Execute(null);
                }
            }

            base.OnClosing(e);
        }
    }
}
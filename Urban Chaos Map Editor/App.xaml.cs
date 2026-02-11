// /App.xaml.cs
using System.Windows;
using UrbanChaosMapEditor.Services;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.ViewModels;
using UrbanChaosMapEditor.Views;

namespace UrbanChaosMapEditor
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // load recent list first
            RecentFilesService.Instance.Load();

            var vm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = vm };
            this.MainWindow = window;
            this.ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();

            // Kick off background preload of textures
            TextureCacheService.Instance.Progress += (_, args) =>
            {
                // marshal to UI to update status bar
                Dispatcher.Invoke(() =>
                {
                    vm.StatusMessage = $"Caching textures… {args.Done}/{args.Total} ({args.Percent:0}%)";
                });
            };

            TextureCacheService.Instance.Completed += (_, __) =>
            {
                Dispatcher.Invoke(() =>
                {
                    vm.IsBusy = false; // stop spinner
                    vm.StatusMessage = $"Textures cached: {TextureCacheService.Instance.Count}";
                });
            };

            // Don’t block UI — fire and forget
            vm.IsBusy = true;
            _ = TextureCacheService.Instance.PreloadAllAsync(decodeSize: 64);
        }
    }
}

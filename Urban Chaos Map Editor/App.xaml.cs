// /App.xaml.cs
using System.Windows;
using UrbanChaosEditor.Shared.Services.Textures;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.ViewModels.Core;
using UrbanChaosMapEditor.Views.Core;

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

            // Subscribe before Show() so the Loaded event is not missed
            if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
            {
                var path = e.Args[0];
                window.Loaded += (_, __) => vm.OpenMapFromPath(path);
            }

            window.Show();

            // Kick off background preload of textures
            TextureCacheService.Instance.Progress += (_, args) =>
            {
                // marshal to UI to update status bar
                Dispatcher.Invoke(() =>
                {
                    vm.StatusMessage = $"Caching textures... {args.Done}/{args.Total} ({args.Percent:0}%)";
                });
            };

            TextureCacheService.Instance.Completed += (_, __) =>
            {
                Dispatcher.Invoke(() =>
                {
                    vm.IsBusy = false; // stop spinner
                    vm.StatusMessage = $"Textures cached: {TextureCacheService.Instance.Count}";

                    // If a map was pre-loaded via command-line arg, the texture cache was still
                    // filling when MapLoaded fired, so the texture lists came up empty.
                    // Now that the cache is complete, refresh them.
                    if (vm.Map.IsMapLoaded)
                        vm.Map.RefreshTextureLists();
                });
            };

            // Don't block UI - fire and forget
            vm.IsBusy = true;
            _ = TextureCacheService.Instance.PreloadAllAsync(decodeSize: 64);
        }
    }
}

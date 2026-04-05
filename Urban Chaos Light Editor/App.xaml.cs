using System.Diagnostics;
using System.Windows;
using UrbanChaosLightEditor.Services;
using UrbanChaosLightEditor.ViewModels;

namespace UrbanChaosLightEditor
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Debug.WriteLine("[App] Starting texture preload...");
            await TextureCacheService.Instance.PreloadAllAsync(64);
            Debug.WriteLine($"[App] Texture preload complete. Loaded {TextureCacheService.Instance.Count} textures.");

            // Auto-load file passed as command-line argument
            Debug.WriteLine($"[App] Args count: {e.Args.Length}");
            if (e.Args.Length > 0)
            {
                var path = e.Args[0];
                Debug.WriteLine($"[App] Arg[0]: {path}");
                Debug.WriteLine($"[App] File exists: {System.IO.File.Exists(path)}");
                Debug.WriteLine($"[App] MainWindow: {MainWindow?.GetType().Name ?? "null"}");
                Debug.WriteLine($"[App] DataContext: {MainWindow?.DataContext?.GetType().Name ?? "null"}");

                if (System.IO.File.Exists(path) && MainWindow?.DataContext is MainWindowViewModel vm)
                    await vm.OpenLightsFromPathAsync(path);
            }
        }
    }
}

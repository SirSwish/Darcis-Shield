using System.Diagnostics;
using System.Windows;
using UrbanChaosLightEditor.Services;

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
        }
    }
}
using System.Windows;
using UrbanChaosEditor.Shared.Services.Textures;

namespace UrbanChaosMissionEditor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await TextureCacheService.Instance.PreloadAllAsync();
    }
}
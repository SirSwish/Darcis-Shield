using System.Windows;
using UrbanChaosPrimEditor.Services;

namespace UrbanChaosPrimEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            PrimDirectoryService.Instance.Load();
        }
    }
}

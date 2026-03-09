using Microsoft.Win32;
using System.Windows;

namespace UrbanChaosMapEditor.Services.Core
{
    public sealed class WpfDialogService : IUiDialogService
    {
        public string? OpenFile(string title, string filter)
        {
            var dlg = new OpenFileDialog { Title = title, Filter = filter };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string? SaveFile(string title, string filter, string? suggestedName = null)
        {
            var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = suggestedName ?? "" };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public bool Confirm(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public void Info(string message, string caption)
            => MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

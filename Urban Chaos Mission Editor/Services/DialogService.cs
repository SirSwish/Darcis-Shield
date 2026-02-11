using Microsoft.Win32;
using System.Windows;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Service for showing dialogs
/// </summary>
public interface IDialogService
{
    string? ShowOpenFileDialog(string title, string filter, string? initialDirectory = null);
    string? ShowSaveFileDialog(string title, string filter, string? initialDirectory = null, string? defaultFileName = null);
    void ShowMessage(string message, string title, MessageBoxImage icon = MessageBoxImage.Information);
    bool ShowConfirmation(string message, string title);
    void ShowError(string message, string title = "Error");
    /// <summary>
    /// Shows a Yes/No/Cancel dialog. Returns true for Yes, false for No, null for Cancel.
    /// </summary>
    bool? ShowYesNoCancelDialog(string title, string message);
}

/// <summary>
/// WPF implementation of dialog service
/// </summary>
public class WpfDialogService : IDialogService
{
    public string? ShowOpenFileDialog(string title, string filter, string? initialDirectory = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string title, string filter, string? initialDirectory = null, string? defaultFileName = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = defaultFileName ?? string.Empty
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void ShowMessage(string message, string title, MessageBoxImage icon = MessageBoxImage.Information)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    public bool ShowConfirmation(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool? ShowYesNoCancelDialog(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }
}
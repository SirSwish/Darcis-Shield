using System.Windows;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Dialog window for editing EventPoint properties
/// </summary>
public partial class EventPointEditorWindow : Window
{
    /// <summary>
    /// Set to true when the user wants to select position from map.
    /// The caller should handle this by doing map selection and reopening the dialog.
    /// </summary>
    public bool NeedsPositionSelection { get; private set; }

    public EventPointEditorWindow()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SelectPositionOnMap_Click(object sender, RoutedEventArgs e)
    {
        // Signal that position selection is needed and close the dialog
        NeedsPositionSelection = true;
        DialogResult = null; // Neither OK nor Cancel
        Close();
    }
}
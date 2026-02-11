using System.Windows;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Dialog window for editing mission properties
/// </summary>
public partial class MissionPropertiesWindow : Window
{
    public MissionPropertiesWindow()
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
}

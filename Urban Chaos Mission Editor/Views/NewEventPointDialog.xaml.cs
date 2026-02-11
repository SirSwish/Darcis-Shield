using System.Windows;
using System.Windows.Input;

namespace UrbanChaosMissionEditor.Views;

/// <summary>
/// Dialog for selecting a new EventPoint type to create
/// </summary>
public partial class NewEventPointDialog : Window
{
    public NewEventPointDialog()
    {
        InitializeComponent();

        // Focus search box on load
        Loaded += (s, e) => SearchBox.Focus();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.NewEventPointDialogViewModel vm && vm.HasSelection)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TypeListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.NewEventPointDialogViewModel vm && vm.HasSelection)
        {
            DialogResult = true;
            Close();
        }
    }
}
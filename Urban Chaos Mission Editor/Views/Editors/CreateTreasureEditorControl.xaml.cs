using System.Windows;
using System.Windows.Controls;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Create Treasure (WPT_TREASURE) EventPoints.
/// Creates a collectible treasure item worth points.
/// 
/// Data layout from treasuresetup.cpp:
/// Data[0] = treasure_value (0-10000, point value)
/// </summary>
public partial class CreateTreasureEditorControl : UserControl
{
    public CreateTreasureEditorControl()
    {
        InitializeComponent();
    }

    private EventPointEditorViewModel? GetViewModel()
    {
        return DataContext as EventPointEditorViewModel;
    }

    private void SetValue100_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 100;
    }

    private void SetValue250_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 250;
    }

    private void SetValue500_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 500;
    }

    private void SetValue1000_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 1000;
    }

    private void SetValue2500_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 2500;
    }

    private void SetValue5000_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm != null) vm.TreasureValue = 5000;
    }
}
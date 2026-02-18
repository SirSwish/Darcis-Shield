using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Move Thing (WPT_MOVE_THING) EventPoints.
/// Moves a target EventPoint to this waypoint's position when triggered.
/// </summary>
public partial class MoveThingEditorControl : UserControl
{
    public MoveThingEditorControl()
    {
        InitializeComponent();
    }
}
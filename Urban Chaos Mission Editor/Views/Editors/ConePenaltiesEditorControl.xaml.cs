using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Cone Penalties (WPT_CONE_PENALTIES) EventPoints.
/// Enables cone penalties for driving missions when triggered.
/// 
/// This is a simple toggle waypoint with no configurable Data fields.
/// When triggered, hitting traffic cones will result in point/time penalties.
/// </summary>
public partial class ConePenaltiesEditorControl : UserControl
{
    public ConePenaltiesEditorControl()
    {
        InitializeComponent();
    }
}
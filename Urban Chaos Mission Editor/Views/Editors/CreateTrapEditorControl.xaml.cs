using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Create Trap (WPT_TRAP) EventPoints.
/// Creates an environmental trap like a steam jet.
/// 
/// Data layout from TrapSetup.cpp:
/// Data[0] = trap_type (0=Steam Jet)
/// Data[1] = trap_speed (1-32, animation speed)
/// Data[2] = trap_steps (1-32, number of animation steps)
/// Data[3] = trap_mask (bitmask for which steps are active)
/// Data[4] = trap_axis (0=Forward, 1=Up, 2=Down)
/// Data[5] = trap_range (1-32, effect range)
/// </summary>
public partial class CreateTrapEditorControl : UserControl
{
    public CreateTrapEditorControl()
    {
        InitializeComponent();
    }
}
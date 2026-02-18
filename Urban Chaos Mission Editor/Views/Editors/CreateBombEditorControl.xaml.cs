using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Create Bomb (WPT_BOMB) EventPoints.
/// Creates a bomb that explodes when triggered.
/// 
/// Data layout from bombSetup.cpp:
/// Data[0] = bomb_type (0=Dynamite Stick, 1=Egg Timer, 2=Hi-Tech LED)
/// Data[1] = bomb_size (0-1024, explosion radius)
/// Data[2] = bomb_fx (bitmask for visual effects)
/// </summary>
public partial class CreateBombEditorControl : UserControl
{
    public CreateBombEditorControl()
    {
        InitializeComponent();
    }
}
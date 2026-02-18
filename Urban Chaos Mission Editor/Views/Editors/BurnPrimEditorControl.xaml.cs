using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Burn Prim (WPT_BURN_PRIM) EventPoints.
/// Sets a prim on fire with various fire effects.
/// 
/// Data layout from burnsetup.cpp:
/// Data[0] = burn_type (bitmask for fire effects)
///           Bit 0 = Flickering flames
///           Bit 1 = Bonfires all over
///           Bit 2 = Thick flames
///           Bit 3 = Thick smoke
///           Bit 4 = Static
/// </summary>
public partial class BurnPrimEditorControl : UserControl
{
    public BurnPrimEditorControl()
    {
        InitializeComponent();
    }
}
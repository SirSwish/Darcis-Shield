using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Activate Prim (WPT_ACTIVATE_PRIM) EventPoints.
/// Activates interactive map elements like doors, fences, cameras, or animated prims.
/// 
/// Data layout from activatesetup.cpp:
/// Data[0] = prim_type (0=Door, 1=Electric Fence, 2=Security Camera, 3=Anim Prim)
/// Data[1] = prim_anim (1-99, animation number - only used when prim_type is 3)
/// </summary>
public partial class ActivatePrimEditorControl : UserControl
{
    public ActivatePrimEditorControl()
    {
        InitializeComponent();
    }
}
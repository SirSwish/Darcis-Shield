using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Enemy Flags (WPT_ENEMY_FLAGS) EventPoints.
/// Modifies the behavior flags of an existing Create Enemies EventPoint when triggered.
/// 
/// Data layout from enemyflagsetup.cpp:
/// Data[0] = enemyf_to_change (EP index of Create Enemies to modify)
/// Data[1] = enemyf_flags (bitmask of flags to apply)
///           Bit 0  = Lazy
///           Bit 1  = Diligent
///           Bit 2  = Gang
///           Bit 3  = Fight Back
///           Bit 4  = Just Kill Player
///           Bit 5  = Robotic
///           Bit 6  = Restricted Movement
///           Bit 7  = Only Player Kills
///           Bit 8  = Blue Zone
///           Bit 9  = Cyan Zone
///           Bit 10 = Yellow Zone
///           Bit 11 = Magenta Zone
///           Bit 12 = Invulnerable
///           Bit 13 = Guilty
///           Bit 14 = Fake Wandering
///           Bit 15 = Can Be Carried
/// </summary>
public partial class EnemyFlagsEditorControl : UserControl
{
    public EnemyFlagsEditorControl()
    {
        InitializeComponent();
    }
}
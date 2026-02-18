using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for End Game Lose (WPT_END_GAME_LOSE) EventPoints.
/// Ends the mission in failure when triggered.
/// </summary>
public partial class EndGameLoseEditorControl : UserControl
{
    public EndGameLoseEditorControl()
    {
        InitializeComponent();
    }
}
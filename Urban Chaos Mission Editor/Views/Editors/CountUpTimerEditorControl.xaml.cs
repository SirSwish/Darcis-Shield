using System.Windows.Controls;

namespace UrbanChaosMissionEditor.Views.Editors;

/// <summary>
/// Editor control for Count Up Timer (WPT_COUNT_UP_TIMER) EventPoints.
/// Starts a visible stopwatch-style timer that counts UP from zero.
/// 
/// This is the opposite of the Visible Countdown trigger type which counts down.
/// 
/// Data layout:
/// No Data[] fields are used according to the original editor source.
/// The timer simply starts counting when triggered.
/// </summary>
public partial class CountUpTimerEditorControl : UserControl
{
    public CountUpTimerEditorControl()
    {
        InitializeComponent();
    }
}
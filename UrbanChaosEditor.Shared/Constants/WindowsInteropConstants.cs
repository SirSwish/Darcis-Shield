// ============================================================
// UrbanChaosEditor.Shared/Constants/WindowsInteropConstants.cs
// ============================================================
// Win32 / DWM constants shared across all editors.
// Centralizes the values previously duplicated in every
// MainWindow.xaml.cs and AboutWindow.xaml.cs file.

namespace UrbanChaosEditor.Shared.Constants;

/// <summary>
/// DWM (Desktop Window Manager) attribute identifiers used by
/// <c>DwmSetWindowAttribute</c> for dark title-bar support.
/// </summary>
public static class WindowsInteropConstants
{
    /// <summary>Pre-20H1 immersive dark mode attribute (Windows 10 builds &lt; 18985).</summary>
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    /// <summary>Immersive dark-mode attribute (Windows 10 20H1+ / Windows 11).</summary>
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>Custom caption (title bar) color attribute (Windows 11 22000+).</summary>
    public const int DWMWA_CAPTION_COLOR = 35;

    /// <summary>Custom caption text color attribute (Windows 11 22000+).</summary>
    public const int DWMWA_TEXT_COLOR = 36;
}

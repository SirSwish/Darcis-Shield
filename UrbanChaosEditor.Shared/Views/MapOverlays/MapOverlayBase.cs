// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/MapOverlayBase.cs
// ============================================================
// Base class for all map overlay layers

using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Base class for all map overlay layers.
/// Provides common sizing, hit testing configuration, and refresh functionality.
/// </summary>
public abstract class MapOverlayBase : FrameworkElement
{
    protected MapOverlayBase()
    {
        Width = SharedMapConstants.MapPixelSize;
        Height = SharedMapConstants.MapPixelSize;
        IsHitTestVisible = false; // Override in derived classes if needed
    }

    /// <summary>
    /// Force a redraw of the overlay.
    /// </summary>
    public virtual void Refresh()
    {
        InvalidateVisual();
    }

    /// <summary>
    /// Refresh on the UI thread (safe to call from any thread).
    /// </summary>
    public void RefreshOnUiThread()
    {
        Dispatcher.Invoke(InvalidateVisual);
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(SharedMapConstants.MapPixelSize, SharedMapConstants.MapPixelSize);
}
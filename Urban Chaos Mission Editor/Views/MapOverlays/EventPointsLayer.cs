using System.Windows;
using System.Windows.Media;
using UrbanChaosMissionEditor.ViewModels;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Renders EventPoints as colored circles with direction arrows and selection highlighting.
/// Supports drag-to-move and drag-to-rotate interactions.
/// </summary>
public class EventPointsLayer : FrameworkElement
{
    public static readonly DependencyProperty EventPointsProperty =
        DependencyProperty.Register(nameof(EventPoints), typeof(IEnumerable<EventPointViewModel>), typeof(EventPointsLayer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedEventPointProperty =
        DependencyProperty.Register(nameof(SelectedEventPoint), typeof(EventPointViewModel), typeof(EventPointsLayer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowDirectionArrowsProperty =
        DependencyProperty.Register(nameof(ShowDirectionArrows), typeof(bool), typeof(EventPointsLayer),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<EventPointViewModel>? EventPoints
    {
        get => (IEnumerable<EventPointViewModel>?)GetValue(EventPointsProperty);
        set => SetValue(EventPointsProperty, value);
    }

    public EventPointViewModel? SelectedEventPoint
    {
        get => (EventPointViewModel?)GetValue(SelectedEventPointProperty);
        set => SetValue(SelectedEventPointProperty, value);
    }

    public bool ShowDirectionArrows
    {
        get => (bool)GetValue(ShowDirectionArrowsProperty);
        set => SetValue(ShowDirectionArrowsProperty, value);
    }

    // Rendering constants
    private const double PointRadius = 8.0;
    private const double SelectionRadius = 12.0;
    private const double ArrowLength = 20.0;
    private const double ArrowHeadSize = 6.0;
    private const double RotationHandleRadius = 5.0;

    // Pens and brushes
    private static readonly Pen NormalPen = new(new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), 1.5);
    private static readonly Pen SelectedPen = new(Brushes.White, 3.0);
    private static readonly Pen SelectionRingPen = new(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 2.0);
    private static readonly Pen ArrowPen = new(Brushes.Yellow, 2.0);
    private static readonly Pen SelectedArrowPen = new(Brushes.Orange, 2.5);
    private static readonly Brush RotationHandleBrush = new SolidColorBrush(Color.FromArgb(200, 255, 165, 0));
    private static readonly Pen RotationHandlePen = new(Brushes.White, 1.5);

    static EventPointsLayer()
    {
        NormalPen.Freeze();
        SelectedPen.Freeze();
        SelectionRingPen.Freeze();
        ArrowPen.Freeze();
        SelectedArrowPen.Freeze();
        RotationHandleBrush.Freeze();
        RotationHandlePen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var points = EventPoints;
        if (points == null) return;

        // First pass: draw all non-selected points
        foreach (var ep in points)
        {
            if (!ep.IsVisible) continue;
            if (ep == SelectedEventPoint) continue;

            DrawEventPoint(dc, ep, false);
        }

        // Second pass: draw selected point on top
        if (SelectedEventPoint != null && SelectedEventPoint.IsVisible)
        {
            DrawEventPoint(dc, SelectedEventPoint, true);
        }
    }

    private void DrawEventPoint(DrawingContext dc, EventPointViewModel ep, bool isSelected)
    {
        var center = new Point(ep.PixelX, ep.PixelZ);
        var brush = ep.PointBrush;

        if (isSelected)
        {
            // Draw selection ring
            dc.DrawEllipse(null, SelectionRingPen, center, SelectionRadius, SelectionRadius);
            // Draw the point
            dc.DrawEllipse(brush, SelectedPen, center, PointRadius, PointRadius);
        }
        else
        {
            dc.DrawEllipse(brush, NormalPen, center, PointRadius, PointRadius);
        }

        // Draw direction arrow
        if (ShowDirectionArrows)
        {
            DrawDirectionArrow(dc, ep, isSelected);
        }
    }

    private void DrawDirectionArrow(DrawingContext dc, EventPointViewModel ep, bool isSelected)
    {
        var center = new Point(ep.PixelX, ep.PixelZ);

        // Convert Direction (0-255) to radians
        // 0 = North (up), increases clockwise
        double angleRadians = (ep.Direction / 255.0) * 2 * Math.PI;

        // Calculate arrow endpoint (start from edge of circle)
        // Note: In screen coordinates, Y increases downward, so we negate the Y component
        double startX = center.X + Math.Sin(angleRadians) * PointRadius;
        double startY = center.Y - Math.Cos(angleRadians) * PointRadius;
        double endX = center.X + Math.Sin(angleRadians) * (PointRadius + ArrowLength);
        double endY = center.Y - Math.Cos(angleRadians) * (PointRadius + ArrowLength);

        var pen = isSelected ? SelectedArrowPen : ArrowPen;

        // Draw arrow shaft
        dc.DrawLine(pen, new Point(startX, startY), new Point(endX, endY));

        // Draw arrowhead
        double headAngle1 = angleRadians - Math.PI * 0.75;
        double headAngle2 = angleRadians + Math.PI * 0.75;

        var head1 = new Point(
            endX + Math.Sin(headAngle1) * ArrowHeadSize,
            endY - Math.Cos(headAngle1) * ArrowHeadSize);
        var head2 = new Point(
            endX + Math.Sin(headAngle2) * ArrowHeadSize,
            endY - Math.Cos(headAngle2) * ArrowHeadSize);

        dc.DrawLine(pen, new Point(endX, endY), head1);
        dc.DrawLine(pen, new Point(endX, endY), head2);

        // Draw rotation handle for selected point
        if (isSelected)
        {
            dc.DrawEllipse(RotationHandleBrush, RotationHandlePen,
                new Point(endX, endY), RotationHandleRadius, RotationHandleRadius);
        }
    }

    /// <summary>
    /// Check if a point is within the rotation handle of the selected EventPoint
    /// </summary>
    public bool IsOverRotationHandle(Point position)
    {
        if (SelectedEventPoint == null || !ShowDirectionArrows) return false;

        var arrowEnd = GetArrowEndPoint(SelectedEventPoint);
        double dx = position.X - arrowEnd.X;
        double dy = position.Y - arrowEnd.Y;
        return Math.Sqrt(dx * dx + dy * dy) <= RotationHandleRadius + 3;
    }

    /// <summary>
    /// Check if a point is within an EventPoint circle
    /// </summary>
    public bool IsOverEventPoint(Point position, EventPointViewModel ep)
    {
        double dx = position.X - ep.PixelX;
        double dy = position.Y - ep.PixelZ;
        return Math.Sqrt(dx * dx + dy * dy) <= PointRadius + 2;
    }

    /// <summary>
    /// Get the arrow endpoint for an EventPoint
    /// </summary>
    public Point GetArrowEndPoint(EventPointViewModel ep)
    {
        double angleRadians = (ep.Direction / 255.0) * 2 * Math.PI;
        return new Point(
            ep.PixelX + Math.Sin(angleRadians) * (PointRadius + ArrowLength),
            ep.PixelZ - Math.Cos(angleRadians) * (PointRadius + ArrowLength));
    }

    /// <summary>
    /// Calculate direction byte from center to a target point
    /// </summary>
    public static byte CalculateDirectionFromPoints(Point center, Point target)
    {
        double dx = target.X - center.X;
        double dy = center.Y - target.Y; // Invert Y since screen Y is down

        // Calculate angle in radians (0 = up, clockwise positive)
        double angleRadians = Math.Atan2(dx, dy);
        if (angleRadians < 0) angleRadians += 2 * Math.PI;

        // Subtract PI to account for the arrow inversion in rendering
        angleRadians -= Math.PI;
        if (angleRadians < 0) angleRadians += 2 * Math.PI;

        // Convert to 0-255 range
        return (byte)((angleRadians / (2 * Math.PI)) * 255);
    }

    public void Refresh()
    {
        InvalidateVisual();
    }
}
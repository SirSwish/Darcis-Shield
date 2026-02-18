using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.Controls;

/// <summary>
/// A visual timeline control for editing cutscene data.
/// Displays channels as rows and packets as draggable/resizable blocks.
/// </summary>
public partial class TimelineControl : UserControl
{
    // ========================================
    // Constants
    // ========================================
    private const double ChannelHeight = 40;
    private const double HeaderWidth = 120;
    private const double RulerHeight = 30;
    private const double MinPacketWidth = 10;
    private const int SnapGridSize = 5; // Snap to every 5 frames

    // ========================================
    // Colors
    // ========================================
    private static readonly SolidColorBrush ChannelEvenBrush = new(Color.FromRgb(0x2A, 0x2A, 0x2E));
    private static readonly SolidColorBrush ChannelOddBrush = new(Color.FromRgb(0x25, 0x25, 0x28));
    private static readonly SolidColorBrush ChannelSelectedBrush = new(Color.FromRgb(0x3A, 0x3A, 0x50));
    private static readonly SolidColorBrush GridLineBrush = new(Color.FromRgb(0x3E, 0x3E, 0x42));
    private static readonly SolidColorBrush GridLineMajorBrush = new(Color.FromRgb(0x50, 0x50, 0x55));
    private static readonly SolidColorBrush PlayheadBrush = new(Color.FromRgb(0xFF, 0x50, 0x50));
    private static readonly SolidColorBrush RulerTextBrush = new(Color.FromRgb(0xAA, 0xAA, 0xAA));
    private static readonly SolidColorBrush SelectionBrush = new(Color.FromArgb(0x40, 0x50, 0x80, 0xFF));
    private static readonly SolidColorBrush SelectionBorderBrush = new(Color.FromRgb(0x50, 0x80, 0xFF));

    // Packet type colors
    private static readonly Dictionary<CutscenePacketType, SolidColorBrush> PacketColors = new()
    {
        { CutscenePacketType.Animation, new SolidColorBrush(Color.FromRgb(0x4A, 0x7C, 0xB0)) },
        { CutscenePacketType.Action, new SolidColorBrush(Color.FromRgb(0xB0, 0x7C, 0x4A)) },
        { CutscenePacketType.Sound, new SolidColorBrush(Color.FromRgb(0xB0, 0xA0, 0x4A)) },
        { CutscenePacketType.Camera, new SolidColorBrush(Color.FromRgb(0x4A, 0xB0, 0x6A)) },
        { CutscenePacketType.Text, new SolidColorBrush(Color.FromRgb(0x9A, 0x4A, 0xB0)) },
        { CutscenePacketType.Unused, new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)) }
    };

    private static readonly Dictionary<CutscenePacketType, SolidColorBrush> PacketSelectedColors = new()
    {
        { CutscenePacketType.Animation, new SolidColorBrush(Color.FromRgb(0x6A, 0x9C, 0xD0)) },
        { CutscenePacketType.Action, new SolidColorBrush(Color.FromRgb(0xD0, 0x9C, 0x6A)) },
        { CutscenePacketType.Sound, new SolidColorBrush(Color.FromRgb(0xD0, 0xC0, 0x6A)) },
        { CutscenePacketType.Camera, new SolidColorBrush(Color.FromRgb(0x6A, 0xD0, 0x8A)) },
        { CutscenePacketType.Text, new SolidColorBrush(Color.FromRgb(0xBA, 0x6A, 0xD0)) },
        { CutscenePacketType.Unused, new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)) }
    };

    // ========================================
    // State
    // ========================================
    private CutsceneData? _cutscene;
    private double _pixelsPerFrame = 5.0;
    private int _currentFrame = 0;
    private int _totalFrames = 100;
    private bool _isPlaying = false;
    private DispatcherTimer? _playbackTimer;

    // Selection
    private CutscenePacket? _selectedPacket;
    private CutsceneChannel? _selectedChannel;
    private int _selectedChannelIndex = -1;

    // Drag state
    private enum DragMode { None, MovePacket, ResizePacketStart, ResizePacketEnd, MovePlayhead, SelectRegion }
    private DragMode _dragMode = DragMode.None;
    private Point _dragStartPoint;
    private int _dragStartFrame;
    private int _dragStartLength;
    private CutscenePacket? _dragPacket;
    private CutsceneChannel? _dragPacketChannel;

    // Visual elements cache
    private readonly Dictionary<CutscenePacket, Border> _packetVisuals = new();
    private Line? _playheadLine;

    // ========================================
    // Events
    // ========================================
    public event EventHandler<CutscenePacket?>? PacketSelected;
    public event EventHandler<CutsceneChannel?>? ChannelSelected;
    public event EventHandler<int>? PlayheadMoved;
    public event EventHandler? CutsceneModified;

    // ========================================
    // Constructor
    // ========================================
    public TimelineControl()
    {
        InitializeComponent();

        // Setup playback timer
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;
    }

    // ========================================
    // Public Properties
    // ========================================

    /// <summary>
    /// The cutscene data being edited
    /// </summary>
    public CutsceneData? Cutscene
    {
        get => _cutscene;
        set
        {
            _cutscene = value;
            _selectedPacket = null;
            _selectedChannel = null;
            _selectedChannelIndex = -1;
            _currentFrame = 0;
            CalculateTotalFrames();
            RefreshTimeline();
        }
    }

    /// <summary>
    /// Current playhead position in frames
    /// </summary>
    public int CurrentFrame
    {
        get => _currentFrame;
        set
        {
            _currentFrame = Math.Max(0, Math.Min(value, _totalFrames));
            UpdatePlayhead();
            TxtCurrentFrame.Text = _currentFrame.ToString();
            PlayheadMoved?.Invoke(this, _currentFrame);
        }
    }

    /// <summary>
    /// Currently selected packet
    /// </summary>
    public CutscenePacket? SelectedPacket => _selectedPacket;

    /// <summary>
    /// Currently selected channel
    /// </summary>
    public CutsceneChannel? SelectedChannel => _selectedChannel;

    /// <summary>
    /// Pixels per frame (zoom level)
    /// </summary>
    public double PixelsPerFrame
    {
        get => _pixelsPerFrame;
        set
        {
            _pixelsPerFrame = Math.Max(1, Math.Min(20, value));
            ZoomSlider.Value = _pixelsPerFrame;
            TxtZoomLevel.Text = $"{_pixelsPerFrame:F0} px/f";
            RefreshTimeline();
        }
    }

    // ========================================
    // Public Methods
    // ========================================

    /// <summary>
    /// Refresh the entire timeline display
    /// </summary>
    public void RefreshTimeline()
    {
        if (_cutscene == null) return;

        CalculateTotalFrames();
        UpdateCanvasSizes();

        // Clear everything first to avoid stale visuals
        ChannelHeadersPanel.Children.Clear();
        GridLinesCanvas.Children.Clear();
        TracksCanvas.Children.Clear();
        RulerCanvas.Children.Clear();
        PlayheadCanvas.Children.Clear();
        _packetVisuals.Clear();

        DrawChannelHeaders();
        DrawGridLines();
        DrawRuler();
        DrawPackets();
        UpdatePlayhead();

        TxtTotalFrames.Text = $" / {_totalFrames}";

        // Reset scroll positions to ensure sync
        HeaderScrollViewer.ScrollToVerticalOffset(0);
        TimelineScrollViewer.ScrollToVerticalOffset(0);
    }

    /// <summary>
    /// Select a specific packet
    /// </summary>
    public void SelectPacket(CutscenePacket? packet, CutsceneChannel? channel = null)
    {
        _selectedPacket = packet;
        _selectedChannel = channel;

        if (channel != null && _cutscene != null)
        {
            _selectedChannelIndex = _cutscene.Channels.IndexOf(channel);
        }

        RefreshPacketVisuals();
        PacketSelected?.Invoke(this, packet);
    }

    /// <summary>
    /// Select a channel
    /// </summary>
    public void SelectChannel(CutsceneChannel? channel)
    {
        _selectedChannel = channel;
        _selectedPacket = null;

        if (channel != null && _cutscene != null)
        {
            _selectedChannelIndex = _cutscene.Channels.IndexOf(channel);
        }
        else
        {
            _selectedChannelIndex = -1;
        }

        RefreshPacketVisuals();
        ChannelSelected?.Invoke(this, channel);
    }

    // ========================================
    // Private Methods - Layout
    // ========================================

    private void CalculateTotalFrames()
    {
        if (_cutscene == null)
        {
            _totalFrames = 100;
            return;
        }

        int maxEnd = 100; // Minimum timeline length
        foreach (var channel in _cutscene.Channels)
        {
            foreach (var packet in channel.Packets)
            {
                int end;

                // Camera packets use Length field for lens/fade, not duration
                // They are keyframes, so just use Start position + small duration
                if (packet.Type == CutscenePacketType.Camera)
                {
                    end = packet.Start + 10; // Camera keyframes display as small blocks
                }
                else
                {
                    // Sanity check - cap at reasonable max to avoid UI hang
                    int length = Math.Min((int)packet.Length, 1000);
                    end = packet.Start + length;
                }

                if (end > maxEnd) maxEnd = end;
            }
        }

        // Cap total frames to something reasonable
        _totalFrames = Math.Min(maxEnd + 50, 5000);
    }

    private void UpdateCanvasSizes()
    {
        if (_cutscene == null) return;

        double width = _totalFrames * _pixelsPerFrame + 50;
        double height = Math.Max(_cutscene.Channels.Count * ChannelHeight, 100);

        TracksCanvas.Width = width;
        TracksCanvas.Height = height;
        GridLinesCanvas.Width = width;
        GridLinesCanvas.Height = height;
        PlayheadCanvas.Width = width;
        PlayheadCanvas.Height = height;
        RulerCanvas.Width = width;

        // Update the ChannelHeadersPanel minimum height to match canvas
        ChannelHeadersPanel.MinHeight = height;

        // Force layout update before drawing
        UpdateLayout();
    }

    // ========================================
    // Private Methods - Drawing
    // ========================================

    private void DrawChannelHeaders()
    {
        ChannelHeadersPanel.Children.Clear();

        if (_cutscene == null) return;

        for (int i = 0; i < _cutscene.Channels.Count; i++)
        {
            var channel = _cutscene.Channels[i];
            var header = CreateChannelHeader(channel, i);
            ChannelHeadersPanel.Children.Add(header);
        }
    }

    private Border CreateChannelHeader(CutsceneChannel channel, int index)
    {
        var isSelected = index == _selectedChannelIndex;

        var border = new Border
        {
            Height = ChannelHeight,
            Background = isSelected ? ChannelSelectedBrush : (index % 2 == 0 ? ChannelEvenBrush : ChannelOddBrush),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 0, 8, 0),
            Tag = channel,
            Cursor = Cursors.Hand
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Icon
        panel.Children.Add(new TextBlock
        {
            Text = GetChannelIcon(channel.Type),
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Name
        panel.Children.Add(new TextBlock
        {
            Text = channel.DisplayName,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 80
        });

        border.Child = panel;

        border.MouseLeftButtonDown += (s, e) =>
        {
            SelectChannel(channel);
            e.Handled = true;
        };

        return border;
    }

    private static string GetChannelIcon(CutsceneChannelType type) => type switch
    {
        CutsceneChannelType.Character => "👤",
        CutsceneChannelType.Camera => "🎥",
        CutsceneChannelType.Sound => "🔊",
        CutsceneChannelType.Subtitles => "💬",
        CutsceneChannelType.VisualFX => "✨",
        _ => "?"
    };

    private void DrawGridLines()
    {
        GridLinesCanvas.Children.Clear();

        if (_cutscene == null) return;

        double height = _cutscene.Channels.Count * ChannelHeight;

        // Draw channel row backgrounds
        for (int i = 0; i < _cutscene.Channels.Count; i++)
        {
            var rect = new Rectangle
            {
                Width = GridLinesCanvas.Width,
                Height = ChannelHeight,
                Fill = i == _selectedChannelIndex ? ChannelSelectedBrush : (i % 2 == 0 ? ChannelEvenBrush : ChannelOddBrush)
            };
            Canvas.SetLeft(rect, 0);
            Canvas.SetTop(rect, i * ChannelHeight);
            GridLinesCanvas.Children.Add(rect);
        }

        // Calculate grid interval based on zoom
        int minorInterval = _pixelsPerFrame >= 10 ? 1 : (_pixelsPerFrame >= 5 ? 5 : 10);
        int majorInterval = minorInterval * 10;

        // Draw vertical grid lines
        for (int frame = 0; frame <= _totalFrames; frame += minorInterval)
        {
            bool isMajor = frame % majorInterval == 0;
            double x = frame * _pixelsPerFrame;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = isMajor ? GridLineMajorBrush : GridLineBrush,
                StrokeThickness = isMajor ? 1 : 0.5
            };
            GridLinesCanvas.Children.Add(line);
        }

        // Draw horizontal channel separators
        for (int i = 1; i < _cutscene.Channels.Count; i++)
        {
            double y = i * ChannelHeight;
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = GridLinesCanvas.Width,
                Y2 = y,
                Stroke = GridLineBrush,
                StrokeThickness = 1
            };
            GridLinesCanvas.Children.Add(line);
        }
    }

    private void DrawRuler()
    {
        RulerCanvas.Children.Clear();

        // Calculate label interval based on zoom
        int labelInterval = _pixelsPerFrame >= 10 ? 10 : (_pixelsPerFrame >= 5 ? 20 : 50);
        int tickInterval = labelInterval / 5;

        for (int frame = 0; frame <= _totalFrames; frame += tickInterval)
        {
            double x = frame * _pixelsPerFrame;
            bool isLabel = frame % labelInterval == 0;

            // Tick mark
            var tick = new Line
            {
                X1 = x,
                Y1 = isLabel ? 5 : 15,
                X2 = x,
                Y2 = 25,
                Stroke = RulerTextBrush,
                StrokeThickness = isLabel ? 1 : 0.5
            };
            RulerCanvas.Children.Add(tick);

            // Label
            if (isLabel)
            {
                var label = new TextBlock
                {
                    Text = frame.ToString(),
                    Foreground = RulerTextBrush,
                    FontSize = 10
                };
                Canvas.SetLeft(label, x + 3);
                Canvas.SetTop(label, 2);
                RulerCanvas.Children.Add(label);
            }
        }

        // Draw playhead marker on ruler
        DrawRulerPlayhead();
    }

    private void DrawRulerPlayhead()
    {
        double x = _currentFrame * _pixelsPerFrame;

        // Triangle marker
        var triangle = new Polygon
        {
            Points = new PointCollection
            {
                new Point(x - 6, 0),
                new Point(x + 6, 0),
                new Point(x, 10)
            },
            Fill = PlayheadBrush
        };
        RulerCanvas.Children.Add(triangle);
    }

    private void DrawPackets()
    {
        TracksCanvas.Children.Clear();
        _packetVisuals.Clear();

        if (_cutscene == null) return;

        for (int channelIndex = 0; channelIndex < _cutscene.Channels.Count; channelIndex++)
        {
            var channel = _cutscene.Channels[channelIndex];
            double y = channelIndex * ChannelHeight + 4;

            foreach (var packet in channel.Packets)
            {
                var visual = CreatePacketVisual(packet, channel, y);
                TracksCanvas.Children.Add(visual);
                _packetVisuals[packet] = visual;
            }
        }
    }

    private Border CreatePacketVisual(CutscenePacket packet, CutsceneChannel channel, double y)
    {
        double x = packet.Start * _pixelsPerFrame;

        // Camera packets use Length for lens/fade settings, not duration
        // Display them as small keyframe markers
        double visualLength;
        if (packet.Type == CutscenePacketType.Camera)
        {
            visualLength = 10; // Fixed width for camera keyframes
        }
        else
        {
            // Cap length to reasonable value for display
            visualLength = Math.Min((int)packet.Length, 500);
        }

        double width = Math.Max(visualLength * _pixelsPerFrame, MinPacketWidth);
        bool isSelected = packet == _selectedPacket;

        var color = PacketColors.GetValueOrDefault(packet.Type, PacketColors[CutscenePacketType.Unused]);
        var selectedColor = PacketSelectedColors.GetValueOrDefault(packet.Type, PacketSelectedColors[CutscenePacketType.Unused]);

        var border = new Border
        {
            Width = width,
            Height = ChannelHeight - 8,
            Background = isSelected ? selectedColor : color,
            BorderBrush = isSelected ? SelectionBorderBrush : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            CornerRadius = new CornerRadius(3),
            Tag = new PacketInfo { Packet = packet, Channel = channel },
            Cursor = Cursors.Hand,
            ClipToBounds = true
        };

        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);

        // Content
        var textBlock = new TextBlock
        {
            Text = GetPacketLabel(packet),
            Foreground = Brushes.White,
            FontSize = 10,
            Margin = new Thickness(4, 2, 4, 2),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = textBlock;

        // Resize handles (visual only, hit testing done in mouse handlers)
        // Left and right edges act as resize handles

        return border;
    }

    private static string GetPacketLabel(CutscenePacket packet) => packet.Type switch
    {
        CutscenePacketType.Animation => $"Anim {packet.Index}",
        CutscenePacketType.Action => "Action",
        CutscenePacketType.Sound => $"Sound {packet.Index}",
        CutscenePacketType.Camera => "Cam",
        CutscenePacketType.Text => string.IsNullOrEmpty(packet.Text) ? "Text" :
            (packet.Text.Length > 10 ? packet.Text.Substring(0, 10) + "..." : packet.Text),
        _ => "?"
    };

    private void RefreshPacketVisuals()
    {
        foreach (var kvp in _packetVisuals)
        {
            var packet = kvp.Key;
            var border = kvp.Value;
            bool isSelected = packet == _selectedPacket;

            var color = PacketColors.GetValueOrDefault(packet.Type, PacketColors[CutscenePacketType.Unused]);
            var selectedColor = PacketSelectedColors.GetValueOrDefault(packet.Type, PacketSelectedColors[CutscenePacketType.Unused]);

            border.Background = isSelected ? selectedColor : color;
            border.BorderBrush = isSelected ? SelectionBorderBrush : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            border.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }

        // Refresh channel headers for selection state
        DrawChannelHeaders();
        DrawGridLines();
    }

    private void UpdatePlayhead()
    {
        PlayheadCanvas.Children.Clear();

        double x = _currentFrame * _pixelsPerFrame;
        double height = PlayheadCanvas.Height;

        _playheadLine = new Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = height,
            Stroke = PlayheadBrush,
            StrokeThickness = 2
        };
        PlayheadCanvas.Children.Add(_playheadLine);

        // Also update ruler playhead
        DrawRuler();
    }

    // ========================================
    // Private Methods - Hit Testing
    // ========================================

    private (CutscenePacket? packet, CutsceneChannel? channel, DragMode mode) HitTestPacket(Point point)
    {
        if (_cutscene == null) return (null, null, DragMode.None);

        int channelIndex = (int)(point.Y / ChannelHeight);
        if (channelIndex < 0 || channelIndex >= _cutscene.Channels.Count)
            return (null, null, DragMode.None);

        var channel = _cutscene.Channels[channelIndex];

        foreach (var packet in channel.Packets)
        {
            double packetLeft = packet.Start * _pixelsPerFrame;
            double packetRight = (packet.Start + packet.Length) * _pixelsPerFrame;
            double packetTop = channelIndex * ChannelHeight + 4;
            double packetBottom = packetTop + ChannelHeight - 8;

            if (point.X >= packetLeft && point.X <= packetRight &&
                point.Y >= packetTop && point.Y <= packetBottom)
            {
                // Determine if we're on a resize edge (5 pixels from edge)
                const double edgeSize = 5;

                if (point.X <= packetLeft + edgeSize)
                    return (packet, channel, DragMode.ResizePacketStart);
                if (point.X >= packetRight - edgeSize)
                    return (packet, channel, DragMode.ResizePacketEnd);

                return (packet, channel, DragMode.MovePacket);
            }
        }

        return (null, channel, DragMode.None);
    }

    private int PointToFrame(double x)
    {
        int frame = (int)(x / _pixelsPerFrame);

        if (ChkSnap.IsChecked == true)
        {
            frame = (int)Math.Round((double)frame / SnapGridSize) * SnapGridSize;
        }

        return Math.Max(0, frame);
    }

    // ========================================
    // Event Handlers - Mouse
    // ========================================

    private void TracksCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(TracksCanvas);
        var (packet, channel, mode) = HitTestPacket(point);

        if (packet != null)
        {
            SelectPacket(packet, channel);
            _dragMode = mode;
            _dragPacket = packet;
            _dragPacketChannel = channel;
            _dragStartPoint = point;
            _dragStartFrame = packet.Start;
            _dragStartLength = packet.Length;
            TracksCanvas.CaptureMouse();

            // Update cursor based on mode
            TracksCanvas.Cursor = mode switch
            {
                DragMode.ResizePacketStart => Cursors.SizeWE,
                DragMode.ResizePacketEnd => Cursors.SizeWE,
                _ => Cursors.SizeAll
            };
        }
        else if (channel != null)
        {
            SelectChannel(channel);
        }
        else
        {
            SelectPacket(null);
            SelectChannel(null);
        }

        Focus();
        e.Handled = true;
    }

    private void TracksCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(TracksCanvas);

        if (_dragMode == DragMode.None)
        {
            // Update cursor based on hover
            var (packet, _, mode) = HitTestPacket(point);
            TracksCanvas.Cursor = mode switch
            {
                DragMode.ResizePacketStart => Cursors.SizeWE,
                DragMode.ResizePacketEnd => Cursors.SizeWE,
                DragMode.MovePacket => Cursors.Hand,
                _ => Cursors.Arrow
            };
            return;
        }

        if (_dragPacket == null) return;

        double deltaX = point.X - _dragStartPoint.X;
        int deltaFrames = (int)(deltaX / _pixelsPerFrame);

        switch (_dragMode)
        {
            case DragMode.MovePacket:
                int newStart = _dragStartFrame + deltaFrames;
                if (ChkSnap.IsChecked == true)
                    newStart = (int)Math.Round((double)newStart / SnapGridSize) * SnapGridSize;
                _dragPacket.Start = (ushort)Math.Max(0, newStart);
                break;

            case DragMode.ResizePacketStart:
                int newStartResize = _dragStartFrame + deltaFrames;
                int endFrame = _dragStartFrame + _dragStartLength;
                if (ChkSnap.IsChecked == true)
                    newStartResize = (int)Math.Round((double)newStartResize / SnapGridSize) * SnapGridSize;
                newStartResize = Math.Max(0, Math.Min(newStartResize, endFrame - 1));
                _dragPacket.Start = (ushort)newStartResize;
                _dragPacket.Length = (ushort)(endFrame - newStartResize);
                break;

            case DragMode.ResizePacketEnd:
                int newLength = _dragStartLength + deltaFrames;
                if (ChkSnap.IsChecked == true)
                {
                    int newEnd = _dragStartFrame + newLength;
                    newEnd = (int)Math.Round((double)newEnd / SnapGridSize) * SnapGridSize;
                    newLength = newEnd - _dragStartFrame;
                }
                _dragPacket.Length = (ushort)Math.Max(1, newLength);
                break;
        }

        // Update visual
        if (_packetVisuals.TryGetValue(_dragPacket, out var visual))
        {
            Canvas.SetLeft(visual, _dragPacket.Start * _pixelsPerFrame);
            visual.Width = Math.Max(_dragPacket.Length * _pixelsPerFrame, MinPacketWidth);
        }
    }

    private void TracksCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode != DragMode.None)
        {
            TracksCanvas.ReleaseMouseCapture();
            TracksCanvas.Cursor = Cursors.Arrow;

            if (_dragPacket != null)
            {
                CalculateTotalFrames();
                CutsceneModified?.Invoke(this, EventArgs.Empty);
            }

            _dragMode = DragMode.None;
            _dragPacket = null;
            _dragPacketChannel = null;
        }
    }

    private void TracksCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(TracksCanvas);
        var (packet, channel, _) = HitTestPacket(point);

        if (packet != null)
        {
            SelectPacket(packet, channel);
            // Could show context menu here
        }
    }

    private void RulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(RulerCanvas);
        CurrentFrame = PointToFrame(point.X);
        _dragMode = DragMode.MovePlayhead;
        RulerCanvas.CaptureMouse();
    }

    private void RulerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.MovePlayhead)
        {
            var point = e.GetPosition(RulerCanvas);
            CurrentFrame = PointToFrame(point.X);
        }
    }

    private void RulerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.MovePlayhead)
        {
            RulerCanvas.ReleaseMouseCapture();
            _dragMode = DragMode.None;
        }
    }

    // ========================================
    // Event Handlers - Keyboard
    // ========================================

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
                DeleteSelectedPacket();
                e.Handled = true;
                break;
            case Key.Left:
                CurrentFrame = Math.Max(0, _currentFrame - (Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1));
                e.Handled = true;
                break;
            case Key.Right:
                CurrentFrame = Math.Min(_totalFrames, _currentFrame + (Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1));
                e.Handled = true;
                break;
            case Key.Home:
                CurrentFrame = 0;
                e.Handled = true;
                break;
            case Key.End:
                CurrentFrame = _totalFrames;
                e.Handled = true;
                break;
            case Key.Space:
                TogglePlayback();
                e.Handled = true;
                break;
        }
    }

    private void DeleteSelectedPacket()
    {
        if (_selectedPacket != null && _selectedChannel != null)
        {
            _selectedChannel.Packets.Remove(_selectedPacket);
            _selectedPacket = null;
            RefreshTimeline();
            CutsceneModified?.Invoke(this, EventArgs.Empty);
        }
    }

    // ========================================
    // Event Handlers - Toolbar
    // ========================================

    private void GoToStart_Click(object sender, RoutedEventArgs e) => CurrentFrame = 0;

    private void GoToEnd_Click(object sender, RoutedEventArgs e) => CurrentFrame = _totalFrames;

    private void StepBack_Click(object sender, RoutedEventArgs e) =>
        CurrentFrame = Math.Max(0, _currentFrame - 1);

    private void StepForward_Click(object sender, RoutedEventArgs e) =>
        CurrentFrame = Math.Min(_totalFrames, _currentFrame + 1);

    private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePlayback();

    private void TogglePlayback()
    {
        _isPlaying = !_isPlaying;
        BtnPlayPause.Content = _isPlaying ? "⏸" : "▶";

        if (_isPlaying)
        {
            if (_currentFrame >= _totalFrames)
                CurrentFrame = 0;
            _playbackTimer?.Start();
        }
        else
        {
            _playbackTimer?.Stop();
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentFrame >= _totalFrames)
        {
            TogglePlayback();
            return;
        }
        CurrentFrame++;
    }

    private void TxtCurrentFrame_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (int.TryParse(TxtCurrentFrame.Text, out int frame))
            {
                CurrentFrame = frame;
            }
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsLoaded)
        {
            PixelsPerFrame = e.NewValue;
        }
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync vertical scroll between headers and tracks
        HeaderScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    // ========================================
    // Helper Classes
    // ========================================

    private class PacketInfo
    {
        public CutscenePacket? Packet { get; set; }
        public CutsceneChannel? Channel { get; set; }
    }
}
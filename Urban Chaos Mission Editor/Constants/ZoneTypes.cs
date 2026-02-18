using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace UrbanChaosMissionEditor.Constants;

[Flags]
public enum ZoneType : byte
{
    None = 0,
    Inside = 1 << 0, // 1
    Reverb = 1 << 1, // 2
    NoWander = 1 << 2, // 4
    Blue = 1 << 3, // 8
    Cyan = 1 << 4, // 16
    Yellow = 1 << 5, // 32
    Magenta = 1 << 6, // 64
    NoGo = 1 << 7, // 128
}

public static class ZoneColors
{
    // Matches MapView.cpp zone_colours[]
    private static Color FromRgb(uint rgb, byte a = 128)
    {
        byte r = (byte)((rgb >> 16) & 0xFF);
        byte g = (byte)((rgb >> 8) & 0xFF);
        byte b = (byte)(rgb & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    public static readonly Dictionary<ZoneType, Color> ZoneColorMap = new()
    {
        { ZoneType.Inside,   FromRgb(0x000000) },
        { ZoneType.Reverb,   FromRgb(0x7f7f7f) },
        { ZoneType.NoWander, FromRgb(0xff0000) },
        { ZoneType.Blue,    FromRgb(0x0000ff) },
        { ZoneType.Cyan,    FromRgb(0x00ffff) },
        { ZoneType.Yellow,    FromRgb(0xffff00) },
        { ZoneType.Magenta,    FromRgb(0xff00ff) },
        { ZoneType.NoGo,     FromRgb(0x000000) },
        { ZoneType.None,     Colors.Transparent }
    };

    public static Color GetColor(ZoneType type) =>
        ZoneColorMap.TryGetValue(type, out var c) ? c : Colors.Transparent;

    public static SolidColorBrush GetBrush(ZoneType type) => new SolidColorBrush(GetColor(type));
}
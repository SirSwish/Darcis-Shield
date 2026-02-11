using System.Windows.Media;

namespace UrbanChaosMissionEditor.Models;

/// <summary>
/// Light entry from .lgt file
/// </summary>
public struct LightEntry
{
    public byte Range;
    public sbyte Red;
    public sbyte Green;
    public sbyte Blue;
    public byte Next;
    public byte Used;
    public byte Flags;
    public byte Padding;
    public int X;
    public int Y;
    public int Z;

    /// <summary>
    /// Index in the lights array (set during loading).
    /// </summary>
    public int Index { get; set; }

    public bool IsUsed => Used == 1;

    /// <summary>
    /// Gets the UI pixel X coordinate (for 8192x8192 map display).
    /// Inverted to match engine rendering.
    /// </summary>
    public int PixelX => (32768 - X) / 4;

    /// <summary>
    /// Gets the UI pixel Z coordinate (for 8192x8192 map display).
    /// Inverted to match engine rendering.
    /// </summary>
    public int PixelZ => (32768 - Z) / 4;

    /// <summary>
    /// Gets the display radius in pixels.
    /// </summary>
    public int PixelRadius => Math.Max(4, Range / 2);

    /// <summary>
    /// Gets the light color for display.
    /// </summary>
    public Color GetDisplayColor()
    {
        // Convert signed RGB values to unsigned
        byte r = (byte)Math.Clamp(Red + 128, 0, 255);
        byte g = (byte)Math.Clamp(Green + 128, 0, 255);
        byte b = (byte)Math.Clamp(Blue + 128, 0, 255);
        return Color.FromRgb(r, g, b);
    }
}

/// <summary>
/// Light file header
/// </summary>
public struct LightHeader
{
    public int SizeOfEdLight;
    public int EdMaxLights;
    public int SizeOfNightColour;
}

/// <summary>
/// Light properties block
/// </summary>
public struct LightProperties
{
    public int EdLightFree;
    public uint NightFlag;
    public uint NightAmbD3DColour;
    public uint NightAmbD3DSpecular;
    public int NightAmbRed;
    public int NightAmbGreen;
    public int NightAmbBlue;
    public sbyte NightLampostRed;
    public sbyte NightLampostGreen;
    public sbyte NightLampostBlue;
    public byte Padding;
    public int NightLampostRadius;
}

/// <summary>
/// Night colour
/// </summary>
public struct LightNightColour
{
    public byte Red;
    public byte Green;
    public byte Blue;
}
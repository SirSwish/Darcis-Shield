using System;

namespace UrbanChaosLightEditor.Models
{
    public class LightHeader
    {
        public int SizeOfEdLight { get; set; }
        public int EdMaxLights { get; set; }
        public int SizeOfNightColour { get; set; }

        public ushort SizeOfEdLightLower => (ushort)(SizeOfEdLight & 0xFFFF);
        public ushort Version => (ushort)((SizeOfEdLight >> 16) & 0xFFFF);
    }

    public class LightEntry
    {
        public byte Range { get; set; }
        public sbyte Red { get; set; }
        public sbyte Green { get; set; }
        public sbyte Blue { get; set; }
        public byte Next { get; set; }
        public byte Used { get; set; }
        public byte Flags { get; set; }
        public byte Padding { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    public struct LightProperties
    {
        public int EdLightFree { get; set; }
        public uint NightFlag { get; set; }
        public uint NightAmbD3DColour { get; set; }
        public uint NightAmbD3DSpecular { get; set; }
        public int NightAmbRed { get; set; }
        public int NightAmbGreen { get; set; }
        public int NightAmbBlue { get; set; }
        public sbyte NightLampostRed { get; set; }
        public sbyte NightLampostGreen { get; set; }
        public sbyte NightLampostBlue { get; set; }
        public byte Padding { get; set; }
        public int NightLampostRadius { get; set; }

        public byte D3DAlpha => (byte)((NightAmbD3DColour >> 24) & 0xFF);
        public byte D3DRed => (byte)((NightAmbD3DColour >> 16) & 0xFF);
        public byte D3DGreen => (byte)((NightAmbD3DColour >> 8) & 0xFF);
        public byte D3DBlue => (byte)((NightAmbD3DColour) & 0xFF);

        public byte SpecularAlpha => (byte)((NightAmbD3DSpecular >> 24) & 0xFF);
        public byte SpecularRed => (byte)((NightAmbD3DSpecular >> 16) & 0xFF);
        public byte SpecularGreen => (byte)((NightAmbD3DSpecular >> 8) & 0xFF);
        public byte SpecularBlue => (byte)((NightAmbD3DSpecular) & 0xFF);
    }

    public struct LightNightColour
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }
}
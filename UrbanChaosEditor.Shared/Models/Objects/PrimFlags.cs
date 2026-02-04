// /Models/Objects/PrimFlags.cs
using System;

namespace UrbanChaosMapEditor.Models
{
    public readonly struct PrimFlags
    {
        public bool OnFloor { get; init; }      // bit 0
        public bool Searchable { get; init; }   // bit 1
        public bool NotOnPsx { get; init; }     // bit 2
        public bool Damaged { get; init; }      // bit 3
        public bool Warehouse { get; init; }    // bit 4
        public bool HiddenItem { get; init; }   // bit 5
        public bool Reserved1 { get; init; }    // bit 6
        public bool Reserved2 { get; init; }    // bit 7

        public static PrimFlags FromByte(byte b) => new PrimFlags
        {
            OnFloor = (b & (1 << 0)) != 0,
            Searchable = (b & (1 << 1)) != 0,
            NotOnPsx = (b & (1 << 2)) != 0,
            Damaged = (b & (1 << 3)) != 0,
            Warehouse = (b & (1 << 4)) != 0,
            HiddenItem = (b & (1 << 5)) != 0,
            Reserved1 = (b & (1 << 6)) != 0,
            Reserved2 = (b & (1 << 7)) != 0,
        };

        public byte ToByte()
        {
            byte b = 0;
            if (OnFloor) b |= 1 << 0;
            if (Searchable) b |= 1 << 1;
            if (NotOnPsx) b |= 1 << 2;
            if (Damaged) b |= 1 << 3;
            if (Warehouse) b |= 1 << 4;
            if (HiddenItem) b |= 1 << 5;
            if (Reserved1) b |= 1 << 6;
            if (Reserved2) b |= 1 << 7;
            return b;
        }

        public override string ToString()
        {
            // short summary for status bar
            static string on(bool b, string s) => b ? s : null!;
            var parts = new[]
            {
                on(OnFloor, "Floor"),
                on(Searchable, "Search"),
                on(NotOnPsx, "NoPSX"),
                on(Damaged, "Damaged"),
                on(Warehouse, "Warehouse"),
                on(HiddenItem, "Hidden"),
                on(Reserved1, "R1"),
                on(Reserved2, "R2"),
            };
            return string.Join(",", Array.FindAll(parts, p => !string.IsNullOrEmpty(p)));
        }
    }
}

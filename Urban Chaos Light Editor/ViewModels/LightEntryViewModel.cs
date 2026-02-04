// /ViewModels/LightEntryViewModel.cs
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using UrbanChaosLightEditor.Models;
using UrbanChaosEditor.Shared.ViewModels;

namespace UrbanChaosLightEditor.ViewModels
{
    [DebuggerDisplay("Light[{Index}] Range={Range} RGB=({Red},{Green},{Blue}) Used={IsUsed} Pos=({X},{Y},{Z})")]
    public sealed class LightEntryViewModel : BaseViewModel
    {
        // Index within the 0..254 entries array
        public int Index { get; }

        // Backing fields (match on-disk types but expose int-friendly props for WPF binding)
        private byte _range;
        private sbyte _red;
        private sbyte _green;
        private sbyte _blue;
        private byte _next;
        private byte _used;
        private byte _flags;
        private byte _padding;
        private int _x;
        private int _y;
        private int _z;

        public LightEntryViewModel(int index, LightEntry model)
        {
            Index = index;
            ApplyFromModel(model);
        }

        public void ApplyFromModel(LightEntry m)
        {
            _range = m.Range;
            _red = m.Red;
            _green = m.Green;
            _blue = m.Blue;
            _next = m.Next;
            _used = m.Used;
            _flags = m.Flags;
            _padding = m.Padding;
            _x = m.X;
            _y = m.Y;
            _z = m.Z;

            // notify all once after bulk update
            OnPropertyChanged(string.Empty);
        }

        public LightEntry ToModel() => new LightEntry
        {
            Range = _range,
            Red = _red,
            Green = _green,
            Blue = _blue,
            Next = _next,
            Used = _used,
            Flags = _flags,
            Padding = _padding,
            X = _x,
            Y = _y,
            Z = _z
        };

        // --- Properties for binding (int-friendly where it helps sliders/text) ---

        public int Range
        {
            get => _range;
            set
            {
                var v = (byte)Math.Clamp(value, 0, 255);
                if (_range == v) return;
                _range = v;
                OnPropertyChanged();
            }
        }

        public int Red
        {
            get => _red;
            set
            {
                var v = (sbyte)Math.Clamp(value, -127, 127);
                if (_red == v) return;
                _red = v;
                OnPropertyChanged();
            }
        }

        public int Green
        {
            get => _green;
            set
            {
                var v = (sbyte)Math.Clamp(value, -127, 127);
                if (_green == v) return;
                _green = v;
                OnPropertyChanged();
            }
        }

        public int Blue
        {
            get => _blue;
            set
            {
                var v = (sbyte)Math.Clamp(value, -127, 127);
                if (_blue == v) return;
                _blue = v;
                OnPropertyChanged();
            }
        }

        /// <summary>Index of next free light in the free-list; normally maintained by packer.</summary>
        public int Next
        {
            get => _next;
            set
            {
                var v = (byte)Math.Clamp(value, 0, 255);
                if (_next == v) return;
                _next = v;
                OnPropertyChanged();
            }
        }

        public bool IsUsed
        {
            get => _used != 0;
            set
            {
                var v = (byte)(value ? 1 : 0);
                if (_used == v) return;
                _used = v;
                OnPropertyChanged();
            }
        }

        public byte Flags
        {
            get => _flags;
            set
            {
                if (_flags == value) return;
                _flags = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Padding for alignment (kept for completeness; usually zero).</summary>
        public byte Padding
        {
            get => _padding;
            set
            {
                if (_padding == value) return;
                _padding = value;
                OnPropertyChanged();
            }
        }

        // Positions are raw SLONGs from file (game units). Your layer/view translates these to canvas.
        public int X
        {
            get => _x;
            set
            {
                if (_x == value) return;
                _x = value;
                OnPropertyChanged();
            }
        }

        public int Y
        {
            get => _y;
            set
            {
                if (_y == value) return;
                _y = value;
                OnPropertyChanged();
            }
        }

        public int Z
        {
            get => _z;
            set
            {
                if (_z == value) return;
                _z = value;
                OnPropertyChanged();
            }
        }

        // Convenience: ARGB preview (0..255) for UI color swatches
        public byte PreviewR => unchecked((byte)(_red + 128));
        public byte PreviewG => unchecked((byte)(_green + 128));
        public byte PreviewB => unchecked((byte)(_blue + 128));

        // If you want live updates to preview fields, raise changed when RGB changes
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => RaisePropertyChanged(name);
    }
}

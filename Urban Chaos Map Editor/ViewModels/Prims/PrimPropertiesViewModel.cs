using System.ComponentModel;
using System.Runtime.CompilerServices;
using UrbanChaosMapEditor.Models.Prims;

namespace UrbanChaosMapEditor.ViewModels.Prims
{
    public sealed class PrimPropertiesViewModel : INotifyPropertyChanged
    {
        public int PrimArrayIndex { get; } // zero-based index in array

        private bool _onFloor;
        private bool _searchable;
        private bool _notOnPsx;
        private bool _damaged;
        private bool _warehouse;
        private bool _hiddenItem;
        private bool _reserved1;
        private bool _reserved2;
        private int _insideIndex;

        public bool OnFloor { get => _onFloor; set { _onFloor = value; OnChanged(); } }
        public bool Searchable { get => _searchable; set { _searchable = value; OnChanged(); } }
        public bool NotOnPsx { get => _notOnPsx; set { _notOnPsx = value; OnChanged(); } }
        public bool Damaged { get => _damaged; set { _damaged = value; OnChanged(); } }
        public bool Warehouse { get => _warehouse; set { _warehouse = value; OnChanged(); } }
        public bool HiddenItem { get => _hiddenItem; set { _hiddenItem = value; OnChanged(); } }
        public bool Reserved1 { get => _reserved1; set { _reserved1 = value; OnChanged(); } }
        public bool Reserved2 { get => _reserved2; set { _reserved2 = value; OnChanged(); } }

        public int InsideIndex  // 0..255
        {
            get => _insideIndex;
            set
            {
                var v = value < 0 ? 0 : (value > 255 ? 255 : value);
                if (_insideIndex != v) { _insideIndex = v; OnChanged(); }
            }
        }

        public PrimPropertiesViewModel(int primArrayIndex, byte flags, byte insideIndex)
        {
            PrimArrayIndex = primArrayIndex;

            var f = PrimFlags.FromByte(flags);
            _onFloor = f.OnFloor;
            _searchable = f.Searchable;
            _notOnPsx = f.NotOnPsx;
            _damaged = f.Damaged;
            _warehouse = f.Warehouse;
            _hiddenItem = f.HiddenItem;
            _reserved1 = f.Reserved1;
            _reserved2 = f.Reserved2;

            _insideIndex = insideIndex;
        }

        public byte EncodeFlags()
        {
            var f = new PrimFlags
            {
                OnFloor = OnFloor,
                Searchable = Searchable,
                NotOnPsx = NotOnPsx,
                Damaged = Damaged,
                Warehouse = Warehouse,
                HiddenItem = HiddenItem,
                Reserved1 = Reserved1,
                Reserved2 = Reserved2
            };
            return f.ToByte();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

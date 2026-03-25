// /Models/TextureSlot.cs
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace UrbanChaosStyleEditor.Models
{
    public sealed class TextureSlot : INotifyPropertyChanged
    {
        private int _index;
        private BitmapSource? _image;
        private string? _sourceFilePath;

        // TexType flags
        private bool _transparent;      // T - black is transparent (colour key)
        private bool _wrapping;         // W - texture wraps
        private bool _additiveAlpha;    // A - additive alpha
        private bool _illuminationMap;  // I - next page is self-illumination map
        private bool _selfIlluminating; // S - whole page is self illuminating
        private bool _excludeFadeout;   // F - excluded from fadeout
        private bool _alphaMasked;      // D - alpha masked, second texture is solid
        private bool _alphaTransparent; // M - uses alpha channel for transparency

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); OnPropertyChanged(nameof(DisplayName)); }
        }

        public BitmapSource? Image
        {
            get => _image;
            set { _image = value; OnPropertyChanged(nameof(Image)); OnPropertyChanged(nameof(IsOccupied)); }
        }

        public string? SourceFilePath
        {
            get => _sourceFilePath;
            set { _sourceFilePath = value; OnPropertyChanged(nameof(SourceFilePath)); }
        }

        public bool Transparent
        {
            get => _transparent;
            set { _transparent = value; OnPropertyChanged(nameof(Transparent)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool Wrapping
        {
            get => _wrapping;
            set { _wrapping = value; OnPropertyChanged(nameof(Wrapping)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool AdditiveAlpha
        {
            get => _additiveAlpha;
            set { _additiveAlpha = value; OnPropertyChanged(nameof(AdditiveAlpha)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool IlluminationMap
        {
            get => _illuminationMap;
            set { _illuminationMap = value; OnPropertyChanged(nameof(IlluminationMap)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool SelfIlluminating
        {
            get => _selfIlluminating;
            set { _selfIlluminating = value; OnPropertyChanged(nameof(SelfIlluminating)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool ExcludeFadeout
        {
            get => _excludeFadeout;
            set { _excludeFadeout = value; OnPropertyChanged(nameof(ExcludeFadeout)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool AlphaMasked
        {
            get => _alphaMasked;
            set { _alphaMasked = value; OnPropertyChanged(nameof(AlphaMasked)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool AlphaTransparent
        {
            get => _alphaTransparent;
            set { _alphaTransparent = value; OnPropertyChanged(nameof(AlphaTransparent)); OnPropertyChanged(nameof(HasAnyFlags)); OnPropertyChanged(nameof(FlagsSummary)); }
        }

        public bool IsOccupied => _image != null;

        public bool HasAnyFlags => _transparent || _wrapping || _additiveAlpha ||
                                    _illuminationMap || _selfIlluminating ||
                                    _excludeFadeout || _alphaMasked || _alphaTransparent;

        public string FlagsSummary
        {
            get
            {
                var flags = "";
                if (_transparent) flags += "T";
                if (_wrapping) flags += "W";
                if (_additiveAlpha) flags += "A";
                if (_illuminationMap) flags += "I";
                if (_selfIlluminating) flags += "S";
                if (_excludeFadeout) flags += "F";
                if (_alphaMasked) flags += "D";
                if (_alphaTransparent) flags += "M";
                return flags;
            }
        }

        public string DisplayName => $"tex{_index:D3}hi";

        // Sound FX group assignment
        private string? _soundGroup;
        private int _soundGroupIndex = -1;

        public string? SoundGroup
        {
            get => _soundGroup;
            set { _soundGroup = value; OnPropertyChanged(nameof(SoundGroup)); OnPropertyChanged(nameof(SoundGroupDisplay)); }
        }

        public int SoundGroupIndex
        {
            get => _soundGroupIndex;
            set { _soundGroupIndex = value; OnPropertyChanged(nameof(SoundGroupIndex)); OnPropertyChanged(nameof(SoundGroupDisplay)); }
        }

        public string SoundGroupDisplay =>
            !string.IsNullOrEmpty(_soundGroup) ? _soundGroup
            : _soundGroupIndex >= 0 ? $"Group {_soundGroupIndex}"
            : "(none)";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
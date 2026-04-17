// /Models/StyleProject.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace UrbanChaosStyleEditor.Models
{
    public sealed class StyleProject : INotifyPropertyChanged
    {
        private int _worldNumber = 21;
        private string _projectName = "New World";
        private BitmapSource? _skyImage;
        private string? _skySourcePath;

        public string ProjectName
        {
            get => _projectName;
            set { _projectName = value; OnPropertyChanged(nameof(ProjectName)); }
        }

        public int WorldNumber
        {
            get => _worldNumber;
            set { _worldNumber = value; OnPropertyChanged(nameof(WorldNumber)); }
        }

        public ObservableCollection<TextureSlot> Slots { get; } = new();

        public ObservableCollection<StyleEntry> Styles { get; } = new();

        public BitmapSource? SkyImage
        {
            get => _skyImage;
            set { _skyImage = value; OnPropertyChanged(nameof(SkyImage)); OnPropertyChanged(nameof(HasSky)); }
        }

        public string? SkySourcePath
        {
            get => _skySourcePath;
            set { _skySourcePath = value; OnPropertyChanged(nameof(SkySourcePath)); }
        }

        public bool HasSky => _skyImage != null;

        public Services.SoundFxData? SoundFxData { get; set; }

        public StyleProject()
        {
            for (int i = 0; i < 256; i++)
                Slots.Add(new TextureSlot { Index = i });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class StyleEntry : INotifyPropertyChanged
    {
        private string _name = "";
        private int _index;

        public int Index
        {
            get => _index;
            set { _index = value; OnPropertyChanged(nameof(Index)); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string DisplayName => string.IsNullOrWhiteSpace(_name)
            ? $"Style #{_index + 1}: Unnamed Style"
            : $"Style #{_index + 1}: {_name}";

        public ObservableCollection<StylePiece> Pieces { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class StylePiece : INotifyPropertyChanged
    {
        private byte _page;
        private byte _tx;
        private byte _ty;
        private byte _flip;
        private byte _flag = 0x03; // POLY_GT default (Gouraud | Textured)

        public byte Page
        {
            get => _page;
            set
            {
                if (_page == value) return;
                _page = value;
                OnPropertyChanged(nameof(Page));
                OnPropertyChanged(nameof(TextureIndex));
            }
        }

        public byte Tx
        {
            get => _tx;
            set
            {
                if (_tx == value) return;
                _tx = value;
                OnPropertyChanged(nameof(Tx));
                OnPropertyChanged(nameof(TextureIndex));
            }
        }

        public byte Ty
        {
            get => _ty;
            set
            {
                if (_ty == value) return;
                _ty = value;
                OnPropertyChanged(nameof(Ty));
                OnPropertyChanged(nameof(TextureIndex));
            }
        }

        public byte Flip
        {
            get => _flip;
            set
            {
                if (_flip == value) return;
                _flip = value;
                OnPropertyChanged(nameof(Flip));
            }
        }

        /// <summary>
        /// Raw TMA/poly draw flag byte for this style entry.
        /// Default is POLY_GT (0x03 = Gouraud | Textured).
        /// </summary>
        public byte Flag
        {
            get => _flag;
            set
            {
                if (_flag == value) return;
                _flag = value;
                OnPropertyChanged(nameof(Flag));
            }
        }

        public int TextureIndex => Page * 64 + Ty * 8 + Tx;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
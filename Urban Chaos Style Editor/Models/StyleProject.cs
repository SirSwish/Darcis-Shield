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
            ? $"Style #{_index}"
            : $"Style #{_index}: {_name}";

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

        public byte Page
        {
            get => _page;
            set { _page = value; OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(TextureIndex)); }
        }

        public byte Tx
        {
            get => _tx;
            set { _tx = value; OnPropertyChanged(nameof(Tx)); OnPropertyChanged(nameof(TextureIndex)); }
        }

        public byte Ty
        {
            get => _ty;
            set { _ty = value; OnPropertyChanged(nameof(Ty)); OnPropertyChanged(nameof(TextureIndex)); }
        }

        public byte Flip
        {
            get => _flip;
            set { _flip = value; OnPropertyChanged(nameof(Flip)); }
        }

        public int TextureIndex => Page * 64 + Ty * 8 + Tx;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
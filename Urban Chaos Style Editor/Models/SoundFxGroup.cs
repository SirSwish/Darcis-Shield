// /Models/SoundFxGroup.cs
using System.ComponentModel;

namespace UrbanChaosStyleEditor.Models
{
    public sealed class SoundFxGroup : INotifyPropertyChanged
    {
        private string _name = "";
        private int _legacy1 = -1;
        private int _legacy2 = -1;
        private int _legacy3 = -1;
        private int _legacy4 = -1;
        private int _sampleLow = -1;
        private int _sampleHigh = -1;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        // First 4 values are ignored by the engine but preserved for file compatibility
        public int Legacy1 { get => _legacy1; set => _legacy1 = value; }
        public int Legacy2 { get => _legacy2; set => _legacy2 = value; }
        public int Legacy3 { get => _legacy3; set => _legacy3 = value; }
        public int Legacy4 { get => _legacy4; set => _legacy4 = value; }

        // These are the only values the engine reads: a random sample is picked in [Low..High]
        public int SampleLow
        {
            get => _sampleLow;
            set { _sampleLow = value; OnPropertyChanged(nameof(SampleLow)); OnPropertyChanged(nameof(RangeDisplay)); }
        }

        public int SampleHigh
        {
            get => _sampleHigh;
            set { _sampleHigh = value; OnPropertyChanged(nameof(SampleHigh)); OnPropertyChanged(nameof(RangeDisplay)); }
        }

        public string RangeDisplay => $"Samples {_sampleLow}-{_sampleHigh}";

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
using System.ComponentModel;

namespace UrbanChaosStoryboardEditor.Models
{
    /// <summary>
    /// Represents a district entry in the [districts] section of .sty files.
    /// Format: DistrictName = XPos,YPos
    /// </summary>
    public class District : INotifyPropertyChanged
    {
        private int _districtId;
        private string _districtName = string.Empty;
        private int _xPos;
        private int _yPos;

        public int DistrictId
        {
            get => _districtId;
            set { _districtId = value; OnPropertyChanged(nameof(DistrictId)); }
        }

        public string DistrictName
        {
            get => _districtName;
            set { _districtName = value ?? string.Empty; OnPropertyChanged(nameof(DistrictName)); }
        }

        public int XPos
        {
            get => _xPos;
            set { _xPos = value; OnPropertyChanged(nameof(XPos)); }
        }

        public int YPos
        {
            get => _yPos;
            set { _yPos = value; OnPropertyChanged(nameof(YPos)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public District Clone()
        {
            return new District
            {
                DistrictId = this.DistrictId,
                DistrictName = this.DistrictName,
                XPos = this.XPos,
                YPos = this.YPos
            };
        }
    }
}
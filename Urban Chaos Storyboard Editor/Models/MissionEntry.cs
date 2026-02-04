using System.ComponentModel;

namespace UrbanChaosStoryboardEditor.Models
{
    /// <summary>
    /// Represents a mission entry in .sty files.
    /// Format: ObjectID : GroupID : Parent : ParentIsGroup : Type : Flags : District : Filename : Title : Briefing
    /// </summary>
    public class MissionEntry : INotifyPropertyChanged
    {
        private int _objectId;
        private int _groupId;
        private int _parent;
        private int _parentIsGroup;
        private int _type = 1;
        private int _flags;
        private int _district;
        private string _missionFile = string.Empty;
        private string _missionName = string.Empty;
        private string _missionBriefing = string.Empty;
        private string _briefingAudioFilePath = string.Empty;

        public int ObjectId
        {
            get => _objectId;
            set { _objectId = value; OnPropertyChanged(nameof(ObjectId)); }
        }

        public int GroupId
        {
            get => _groupId;
            set { _groupId = value; OnPropertyChanged(nameof(GroupId)); }
        }

        public int Parent
        {
            get => _parent;
            set { _parent = value; OnPropertyChanged(nameof(Parent)); }
        }

        public int ParentIsGroup
        {
            get => _parentIsGroup;
            set { _parentIsGroup = value; OnPropertyChanged(nameof(ParentIsGroup)); }
        }

        public int Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(Type)); }
        }

        public int Flags
        {
            get => _flags;
            set { _flags = value; OnPropertyChanged(nameof(Flags)); }
        }

        public int District
        {
            get => _district;
            set { _district = value; OnPropertyChanged(nameof(District)); }
        }

        public string MissionFile
        {
            get => _missionFile;
            set { _missionFile = value ?? string.Empty; OnPropertyChanged(nameof(MissionFile)); }
        }

        public string MissionName
        {
            get => _missionName;
            set { _missionName = value ?? string.Empty; OnPropertyChanged(nameof(MissionName)); }
        }

        public string MissionBriefing
        {
            get => _missionBriefing;
            set { _missionBriefing = value ?? string.Empty; OnPropertyChanged(nameof(MissionBriefing)); }
        }

        public string BriefingAudioFilePath
        {
            get => _briefingAudioFilePath;
            set { _briefingAudioFilePath = value ?? string.Empty; OnPropertyChanged(nameof(BriefingAudioFilePath)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MissionEntry Clone()
        {
            return new MissionEntry
            {
                ObjectId = this.ObjectId,
                GroupId = this.GroupId,
                Parent = this.Parent,
                ParentIsGroup = this.ParentIsGroup,
                Type = this.Type,
                Flags = this.Flags,
                District = this.District,
                MissionFile = this.MissionFile,
                MissionName = this.MissionName,
                MissionBriefing = this.MissionBriefing,
                BriefingAudioFilePath = this.BriefingAudioFilePath
            };
        }
    }
}
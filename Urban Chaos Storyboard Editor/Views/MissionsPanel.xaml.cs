using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UrbanChaosStoryboardEditor.Models;
using UrbanChaosStoryboardEditor.Services;
using UrbanChaosStoryboardEditor.Views.Dialogs;

namespace UrbanChaosStoryboardEditor.Views
{
    public partial class MissionsPanel : UserControl
    {
        private const int MaxMissions = 20;
        private const int StartingMissionId = 14;

        public MissionsPanel()
        {
            InitializeComponent();
        }

        private void AddMission_Click(object sender, RoutedEventArgs e)
        {
            var svc = StyDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("Please create or open a storyboard file first.", "No File",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (svc.Missions.Count >= MaxMissions)
            {
                MessageBox.Show($"Maximum of {MaxMissions} missions allowed.\nThis is because only {MaxMissions} levels have modifiable briefing audios.",
                    "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int nextId = GetNextMissionId();
            int parentId = nextId > StartingMissionId ? nextId - 1 : 1;

            var newMission = new MissionEntry
            {
                ObjectId = nextId,
                MissionName = "New Mission",
                GroupId = 0,
                Parent = parentId,
                ParentIsGroup = 0,
                Type = 1,
                Flags = 0,
                District = 0,
                MissionFile = "newLevel.ucm",
                MissionBriefing = "This is a sample mission briefing.\r\r- Replace with your own content\r- Have a nice day.",
                BriefingAudioFilePath = AudioFileLookup.GetFileNameById(nextId)
            };

            svc.Missions.Add(newMission);
            svc.MarkDirty();
        }

        private void EditMission_Click(object sender, RoutedEventArgs e)
        {
            if (MissionsGrid.SelectedItem is MissionEntry mission)
            {
                OpenEditDialog(mission);
            }
            else
            {
                MessageBox.Show("Please select a mission to edit.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteMission_Click(object sender, RoutedEventArgs e)
        {
            if (MissionsGrid.SelectedItem is MissionEntry mission)
            {
                var result = MessageBox.Show($"Delete mission '{mission.MissionName}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StyDataService.Instance.Missions.Remove(mission);
                    StyDataService.Instance.MarkDirty();
                }
            }
            else
            {
                MessageBox.Show("Please select a mission to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateTemplate_Click(object sender, RoutedEventArgs e)
        {
            var svc = StyDataService.Instance;
            if (!svc.IsLoaded)
            {
                MessageBox.Show("Please create or open a storyboard file first.", "No File",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (svc.Missions.Count > 0)
            {
                MessageBox.Show("Please delete all missions first to create from template.",
                    "Missions Exist", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                int missionId = StartingMissionId + i;
                int parentId = i == 0 ? 1 : missionId - 1;

                var mission = new MissionEntry
                {
                    ObjectId = missionId,
                    MissionName = $"Mission {i + 1}",
                    GroupId = 0,
                    Parent = parentId,
                    ParentIsGroup = 0,
                    Type = 1,
                    Flags = 0,
                    District = 0,
                    MissionFile = $"mission{i + 1}.ucm",
                    MissionBriefing = $"Briefing for Mission {i + 1}.\r\r- Add your mission details here.",
                    BriefingAudioFilePath = AudioFileLookup.GetFileNameById(missionId)
                };

                svc.Missions.Add(mission);
            }

            svc.MarkDirty();
            MessageBox.Show("Created 10-mission template.", "Template Created",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MissionsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MissionsGrid.SelectedItem is MissionEntry mission)
            {
                OpenEditDialog(mission);
            }
        }

        private void OpenEditDialog(MissionEntry mission)
        {
            var dialog = new MissionEditDialog(mission)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                StyDataService.Instance.MarkDirty();
            }
        }

        private int GetNextMissionId()
        {
            var svc = StyDataService.Instance;
            if (svc.Missions.Count == 0)
                return StartingMissionId;

            return svc.Missions.Max(m => m.ObjectId) + 1;
        }
    }
}
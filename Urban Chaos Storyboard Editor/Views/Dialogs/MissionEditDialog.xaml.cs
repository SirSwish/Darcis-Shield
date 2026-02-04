using System.Windows;
using Microsoft.Win32;
using UrbanChaosStoryboardEditor.Models;

namespace UrbanChaosStoryboardEditor.Views.Dialogs
{
    public partial class MissionEditDialog : Window
    {
        private readonly MissionEntry _mission;

        public MissionEditDialog(MissionEntry mission)
        {
            InitializeComponent();
            _mission = mission;

            TxtId.Text = mission.ObjectId.ToString();
            TxtName.Text = mission.MissionName;
            TxtFile.Text = mission.MissionFile;
            TxtDistrict.Text = mission.District.ToString();
            TxtParent.Text = mission.Parent.ToString();
            TxtAudio.Text = mission.BriefingAudioFilePath;
            TxtBriefing.Text = mission.MissionBriefing.Replace("\r", "\r\n");

            CboType.SelectedIndex = mission.Type == 1 ? 0 : 1;
            CboFlags.SelectedIndex = mission.Flags == 0 ? 0 : 1;
        }

        private void BrowseAudio_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Briefing Audio File",
                Filter = "WAV Files (*.wav)|*.wav|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                TxtAudio.Text = dlg.FileName;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _mission.MissionName = TxtName.Text;
            _mission.MissionFile = TxtFile.Text;
            _mission.BriefingAudioFilePath = TxtAudio.Text;

            // Convert newlines back to game format
            _mission.MissionBriefing = TxtBriefing.Text.Replace("\r\n", "\r").Replace("\n", "\r");

            if (int.TryParse(TxtDistrict.Text, out int district))
                _mission.District = district;

            if (int.TryParse(TxtParent.Text, out int parent))
                _mission.Parent = parent;

            _mission.Type = CboType.SelectedIndex == 0 ? 1 : 0;
            _mission.Flags = CboFlags.SelectedIndex == 0 ? 0 : 1;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
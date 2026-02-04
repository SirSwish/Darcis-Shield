using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace DarciShield.Launcher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MapEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Adjust if your editor exe is in a subfolder
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string editorExe = Path.Combine(baseDir, "UrbanChaosMapEditor.exe");

                if (!File.Exists(editorExe))
                {
                    MessageBox.Show(
                        $"Could not find Map Editor executable:\n{editorExe}",
                        "Map Editor not found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = editorExe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error launching Map Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void LightEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string lightEditorExe = Path.Combine(baseDir, "Urban Chaos Light Editor.exe");

                if (!File.Exists(lightEditorExe))
                {
                    MessageBox.Show(
                        $"Could not find Light Editor executable:\n{lightEditorExe}",
                        "Light Editor not found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = lightEditorExe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error launching Light Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void StoryEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string storyEditorExe = Path.Combine(baseDir, "Urban Chaos Storyboard Editor.exe");

                if (!File.Exists(storyEditorExe))
                {
                    MessageBox.Show(
                        $"Could not find Story Editor executable:\n{storyEditorExe}",
                        "Story Editor not found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = storyEditorExe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error launching Light Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void MissionEditor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string storyEditorExe = Path.Combine(baseDir, "UrbanChaosMissionEditor.exe");

                if (!File.Exists(storyEditorExe))
                {
                    MessageBox.Show(
                        $"Could not find Mission Editor executable:\n{storyEditorExe}",
                        "Mission Editor not found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = storyEditorExe,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Error launching Mission Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void Window_DragMove(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close(); // or Application.Current.Shutdown();
        }

    }
}

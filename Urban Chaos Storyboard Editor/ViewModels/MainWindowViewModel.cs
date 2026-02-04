using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UrbanChaosStoryboardEditor.Services;

namespace UrbanChaosStoryboardEditor.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly StyDataService _svc = StyDataService.Instance;

        private string _windowTitle = "Urban Chaos Storyboard Editor";
        private string _statusText = "Ready";
        private bool _isLoaded;
        private int _selectedTabIndex;

        public MainWindowViewModel()
        {
            Debug.WriteLine("[MainWindowViewModel] Constructor starting...");

            _svc.FileLoaded += (_, __) =>
            {
                IsLoaded = true;
                UpdateWindowTitle();
                StatusText = $"Loaded: {Path.GetFileName(_svc.CurrentPath)}";
            };

            _svc.FileCleared += (_, __) =>
            {
                IsLoaded = false;
                UpdateWindowTitle();
                StatusText = "Ready";
            };

            _svc.DirtyStateChanged += (_, __) => UpdateWindowTitle();

            NewCommand = new RelayCommand(_ => New());
            OpenCommand = new RelayCommand(_ => _ = OpenAsync());
            SaveCommand = new RelayCommand(_ => _ = SaveAsync(), _ => IsLoaded);
            SaveAsCommand = new RelayCommand(_ => _ = SaveAsAsync(), _ => IsLoaded);
            ExitCommand = new RelayCommand(_ => Application.Current.MainWindow?.Close());

            Debug.WriteLine("[MainWindowViewModel] Constructor complete");
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsLoaded
        {
            get => _isLoaded;
            set => SetProperty(ref _isLoaded, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public ICommand NewCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand ExitCommand { get; }

        private void UpdateWindowTitle()
        {
            string title = "Urban Chaos Storyboard Editor";
            if (_svc.IsLoaded && _svc.CurrentPath != null)
            {
                title = $"{Path.GetFileName(_svc.CurrentPath)} - {title}";
            }
            if (_svc.HasChanges)
            {
                title = "* " + title;
            }
            WindowTitle = title;
        }

        private void New()
        {
            if (_svc.HasChanges)
            {
                var result = MessageBox.Show("Save changes before creating new file?", "Unsaved Changes",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes) _ = SaveAsync();
            }

            _svc.New();
            StatusText = "New storyboard created";
        }

        private async Task OpenAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open Storyboard File",
                Filter = "Storyboard Files (*.sty)|*.sty|All Files (*.*)|*.*",
                DefaultExt = ".sty"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                StatusText = "Loading...";
                await _svc.LoadAsync(dlg.FileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel.OpenAsync] Error: {ex.Message}");
                MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Load failed";
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrEmpty(_svc.CurrentPath))
            {
                await SaveAsAsync();
                return;
            }

            try
            {
                StatusText = "Saving...";
                await _svc.SaveAsync();
                StatusText = "Saved";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel.SaveAsync] Error: {ex.Message}");
                MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Save failed";
            }
        }

        private async Task SaveAsAsync()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Storyboard File",
                Filter = "Storyboard Files (*.sty)|*.sty|All Files (*.*)|*.*",
                DefaultExt = ".sty"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                StatusText = "Saving...";
                await _svc.SaveAsAsync(dlg.FileName);
                StatusText = $"Saved: {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindowViewModel.SaveAsAsync] Error: {ex.Message}");
                MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "Save failed";
            }
        }
    }
}
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UrbanChaosLightEditor.Services;
using UrbanChaosLightEditor.Models;

namespace UrbanChaosLightEditor.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ===== Services =====
        private readonly LightsDataService _lightsSvc = LightsDataService.Instance;

        // ===== Commands =====
        public ICommand OpenMapCommand { get; }
        public ICommand OpenLightsCommand { get; }
        public ICommand NewLightsCommand { get; }
        public ICommand SaveLightsCommand { get; }
        public ICommand SaveLightsAsCommand { get; }
        public ICommand DeleteLightCommand { get; }
        public ICommand CopyLightCommand { get; }
        public ICommand PasteLightCommand { get; }
        public ICommand SelectAllLightsCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }

        public MainWindowViewModel()
        {
            Debug.WriteLine("[MainWindowViewModel] Constructor starting...");

            OpenMapCommand = new RelayCommand(async _ => await OpenMapAsync());
            OpenLightsCommand = new RelayCommand(async _ => await OpenLightsAsync());
            NewLightsCommand = new RelayCommand(_ => NewLights());
            SaveLightsCommand = new RelayCommand(async _ => await SaveLightsAsync(), _ => IsLightsLoaded);
            SaveLightsAsCommand = new RelayCommand(async _ => await SaveLightsAsAsync(), _ => IsLightsLoaded);
            DeleteLightCommand = new RelayCommand(_ => DeleteSelectedLight(), _ => HasSelectedLight);
            CopyLightCommand = new RelayCommand(_ => CopyLight(), _ => HasSelectedLight);
            PasteLightCommand = new RelayCommand(_ => PasteLight(), _ => CanPasteLight);
            SelectAllLightsCommand = new RelayCommand(_ => { /* TODO */ });
            DeselectAllCommand = new RelayCommand(_ => SelectedLightIndex = -1);
            ZoomInCommand = new RelayCommand(_ => Zoom = Math.Min(4.0, Zoom * 1.25));
            ZoomOutCommand = new RelayCommand(_ => Zoom = Math.Max(0.1, Zoom / 1.25));
            ResetZoomCommand = new RelayCommand(_ => Zoom = 1.0);

            // Subscribe to service events
            _lightsSvc.LightsBytesReset += (_, __) =>
            {
                Debug.WriteLine("[MainWindowViewModel] LightsBytesReset event received");
                Application.Current?.Dispatcher.Invoke(RefreshLightsList);
            };
            _lightsSvc.DirtyStateChanged += (_, __) => Application.Current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(WindowTitle));
            });

            Debug.WriteLine("[MainWindowViewModel] Constructor complete");
        }

        // ===== State Properties =====
        private bool _isLightsLoaded;
        public bool IsLightsLoaded
        {
            get => _isLightsLoaded;
            private set
            {
                _isLightsLoaded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        // Keep this (already exists):
        private bool _isMapViewLoaded;
        public bool IsMapViewLoaded
        {
            get => _isMapViewLoaded;
            private set { _isMapViewLoaded = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool HasUnsavedChanges => _lightsSvc.HasChanges;

        public string WindowTitle => IsLightsLoaded
            ? $"Urban Chaos Light Editor - {Path.GetFileName(_lightsSvc.CurrentPath ?? "Untitled")}{(HasUnsavedChanges ? "*" : "")}"
            : "Urban Chaos Light Editor";

        private string _statusMessage = "Ready. Open a lights file (.lgt) to begin editing.";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _lightsFileName = "(none)";
        public string LightsFileName
        {
            get => _lightsFileName;
            private set { _lightsFileName = value; OnPropertyChanged(); }
        }

        // Alias for backwards compatibility
        public string MapFileName => LightsFileName;

        public int LightCount => Lights.Count;

        private string _cursorPosition = "";
        public string CursorPosition
        {
            get => _cursorPosition;
            set { _cursorPosition = value; OnPropertyChanged(); }
        }

        // ===== Layer Visibility =====
        private bool _showTextures = true;
        public bool ShowTextures
        {
            get => _showTextures;
            set { _showTextures = value; OnPropertyChanged(); }
        }

        private bool _showBuildings = true;
        public bool ShowBuildings
        {
            get => _showBuildings;
            set { _showBuildings = value; OnPropertyChanged(); }
        }

        private bool _showPrims = true;
        public bool ShowPrims
        {
            get => _showPrims;
            set { _showPrims = value; OnPropertyChanged(); }
        }

        private bool _showLights = true;
        public bool ShowLights
        {
            get => _showLights;
            set { _showLights = value; OnPropertyChanged(); }
        }

        private bool _showLightRanges = true;
        public bool ShowLightRanges
        {
            get => _showLightRanges;
            set { _showLightRanges = value; OnPropertyChanged(); }
        }

        private bool _showGridLines = false;
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; OnPropertyChanged(); }
        }

        private bool _showPrimGraphics = true;
        public bool ShowPrimGraphics
        {
            get => _showPrimGraphics;
            set { _showPrimGraphics = value; OnPropertyChanged(); }
        }

        private double _zoom = 1.0;
        public double Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Clamp(value, 0.1, 4.0);
                OnPropertyChanged();
            }
        }

        private string _mapFileName = "(none)";
        public string LoadedMapFileName
        {
            get => _mapFileName;
            private set { _mapFileName = value; OnPropertyChanged(); }
        }

        // ===== Light Tool Modes =====
        private bool _isAddingLight;
        public bool IsAddingLight
        {
            get => _isAddingLight;
            set
            {
                _isAddingLight = value;
                if (value) _isMovingLight = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMovingLight));
                StatusMessage = value ? "Click on the map to place a new light." : "Ready.";
            }
        }

        private bool _isMovingLight;
        public bool IsMovingLight
        {
            get => _isMovingLight;
            set
            {
                _isMovingLight = value;
                if (value) _isAddingLight = false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAddingLight));
                StatusMessage = value ? "Drag the selected light to move it." : "Ready.";
            }
        }

        // ===== Lights Collection =====
        public ObservableCollection<LightListItem> Lights { get; } = new();

        private int _selectedLightIndex = -1;
        public int SelectedLightIndex
        {
            get => _selectedLightIndex;
            set
            {
                _selectedLightIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedLight));
                OnPropertyChanged(nameof(SelectedLight));
            }
        }

        public bool HasSelectedLight => _selectedLightIndex >= 0;

        public LightListItem? SelectedLight =>
            _selectedLightIndex >= 0 && _selectedLightIndex < Lights.Count
                ? Lights[_selectedLightIndex]
                : null;

        // ===== Clipboard =====
        private LightEntry? _copiedLight;
        public bool CanPasteLight => _copiedLight != null && IsLightsLoaded;

        // ===== Light Placement Parameters =====
        public byte PlacementRange { get; set; } = 128;
        public sbyte PlacementRed { get; set; } = 0;
        public sbyte PlacementGreen { get; set; } = 0;
        public sbyte PlacementBlue { get; set; } = 0;
        public int PlacementY { get; set; } = 0;

        // ===== File Operations =====

        /// <summary>
        /// Open a .lgt file directly
        /// </summary>
        private async Task OpenLightsAsync()
        {
            Debug.WriteLine("[OpenLightsAsync] Starting...");

            // Check for unsaved changes
            if (HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Save before opening a new file?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    await SaveLightsAsync();
                }
            }

            var ofd = new OpenFileDialog
            {
                Title = "Open Lights File",
                Filter = "LGT Light Files (*.lgt)|*.lgt|All Files (*.*)|*.*",
                DefaultExt = ".lgt"
            };

            if (ofd.ShowDialog() != true)
            {
                Debug.WriteLine("[OpenLightsAsync] User cancelled dialog");
                return;
            }

            Debug.WriteLine($"[OpenLightsAsync] Selected file: {ofd.FileName}");

            IsLoading = true;
            StatusMessage = "Loading lights file...";

            try
            {
                Debug.WriteLine($"[OpenLightsAsync] File exists: {File.Exists(ofd.FileName)}");

                var fileInfo = new FileInfo(ofd.FileName);
                Debug.WriteLine($"[OpenLightsAsync] File size: {fileInfo.Length} bytes");

                Debug.WriteLine("[OpenLightsAsync] Calling _lightsSvc.LoadAsync...");
                await _lightsSvc.LoadAsync(ofd.FileName);
                Debug.WriteLine("[OpenLightsAsync] LoadAsync completed successfully");

                LightsFileName = Path.GetFileName(ofd.FileName);
                IsLightsLoaded = true;

                Debug.WriteLine("[OpenLightsAsync] Calling RefreshLightsList...");
                RefreshLightsList();
                Debug.WriteLine($"[OpenLightsAsync] RefreshLightsList complete. Light count: {LightCount}");

                StatusMessage = $"Loaded {LightsFileName} with {LightCount} lights.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenLightsAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[OpenLightsAsync] Stack trace: {ex.StackTrace}");

                MessageBox.Show($"Failed to load lights file:\n\n{ex.Message}\n\nSee debug output for details.", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Load failed.";
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine("[OpenLightsAsync] Complete");
            }
        }

        /// <summary>
        /// Create a new empty lights file from template
        /// </summary>
        private void NewLights()
        {
            Debug.WriteLine("[NewLights] Starting...");

            // Check for unsaved changes
            if (HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Save before creating a new file?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel) return;
                if (result == MessageBoxResult.Yes)
                {
                    SaveLightsCommand.Execute(null);
                }
            }

            try
            {
                Debug.WriteLine("[NewLights] Loading default template bytes...");
                var defaultBytes = LightsDataService.LoadDefaultResourceBytes();
                Debug.WriteLine($"[NewLights] Got {defaultBytes.Length} bytes");

                Debug.WriteLine("[NewLights] Calling NewFromTemplate...");
                _lightsSvc.NewFromTemplate(defaultBytes);
                Debug.WriteLine("[NewLights] NewFromTemplate complete");

                LightsFileName = "Untitled.lgt";
                IsLightsLoaded = true;

                Debug.WriteLine("[NewLights] Calling RefreshLightsList...");
                RefreshLightsList();
                Debug.WriteLine($"[NewLights] Complete. Light count: {LightCount}");

                StatusMessage = "Created new lights file.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NewLights] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[NewLights] Stack trace: {ex.StackTrace}");

                MessageBox.Show($"Failed to create new lights file:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Open a .iam map file for display (textures/buildings/prims layer viewing)
        /// </summary>
        private async Task OpenMapAsync()
        {
            Debug.WriteLine("[OpenMapAsync] Starting...");

            var ofd = new OpenFileDialog
            {
                Title = "Open Map File",
                Filter = "IAM Map Files (*.iam)|*.iam|All Files (*.*)|*.*",
                DefaultExt = ".iam"
            };

            if (ofd.ShowDialog() != true)
            {
                Debug.WriteLine("[OpenMapAsync] User cancelled dialog");
                return;
            }

            Debug.WriteLine($"[OpenMapAsync] Selected file: {ofd.FileName}");

            IsLoading = true;
            StatusMessage = "Loading map...";

            try
            {
                // Load the map for display layers
                Debug.WriteLine("[OpenMapAsync] Loading map via ReadOnlyMapDataService...");
                await ReadOnlyMapDataService.Instance.LoadAsync(ofd.FileName);

                LoadedMapFileName = Path.GetFileName(ofd.FileName);
                IsMapViewLoaded = true;

                StatusMessage = $"Loaded map: {LoadedMapFileName}";
                Debug.WriteLine($"[OpenMapAsync] Map loaded successfully: {LoadedMapFileName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OpenMapAsync] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[OpenMapAsync] Stack trace: {ex.StackTrace}");

                MessageBox.Show($"Failed to load map:\n\n{ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Map load failed.";
            }
            finally
            {
                IsLoading = false;
                Debug.WriteLine("[OpenMapAsync] Complete");
            }
        }

        private async Task SaveLightsAsync()
        {
            if (!_lightsSvc.IsLoaded) return;

            try
            {
                if (string.IsNullOrEmpty(_lightsSvc.CurrentPath))
                {
                    await SaveLightsAsAsync();
                    return;
                }

                await _lightsSvc.SaveAsync();
                StatusMessage = $"Saved {Path.GetFileName(_lightsSvc.CurrentPath)}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save lights:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveLightsAsAsync()
        {
            if (!_lightsSvc.IsLoaded) return;

            var sfd = new SaveFileDialog
            {
                Title = "Save Lights File",
                Filter = "LGT Light Files (*.lgt)|*.lgt|All Files (*.*)|*.*",
                DefaultExt = ".lgt",
                FileName = Path.GetFileName(_lightsSvc.CurrentPath ?? "lights.lgt")
            };

            if (sfd.ShowDialog() != true) return;

            try
            {
                await _lightsSvc.SaveAsAsync(sfd.FileName);
                LightsFileName = Path.GetFileName(sfd.FileName);
                StatusMessage = $"Saved {LightsFileName}.";
                OnPropertyChanged(nameof(WindowTitle));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save lights:\n\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Light Operations =====
        public void RefreshLightsList()
        {
            Debug.WriteLine("[RefreshLightsList] Starting...");

            Lights.Clear();
            Debug.WriteLine("[RefreshLightsList] Cleared lights collection");

            if (!_lightsSvc.IsLoaded)
            {
                Debug.WriteLine("[RefreshLightsList] Service not loaded, returning early");
                return;
            }

            try
            {
                Debug.WriteLine("[RefreshLightsList] Creating LightsAccessor...");
                var acc = new LightsAccessor(_lightsSvc);

                Debug.WriteLine("[RefreshLightsList] Reading all entries...");
                var entries = acc.ReadAllEntries();
                Debug.WriteLine($"[RefreshLightsList] Read {entries.Count} entries");

                int usedCount = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];

                    if (e.Used != 1) continue;

                    usedCount++;
                    Debug.WriteLine($"[RefreshLightsList] Entry {i}: Used={e.Used}, Range={e.Range}, RGB=({e.Red},{e.Green},{e.Blue}), Pos=({e.X},{e.Y},{e.Z})");

                    try
                    {
                        int uiX = LightsAccessor.WorldXToUiX(e.X);
                        int uiZ = LightsAccessor.WorldZToUiZ(e.Z);

                        Lights.Add(new LightListItem
                        {
                            Index = i,
                            Range = e.Range,
                            Red = e.Red,
                            Green = e.Green,
                            Blue = e.Blue,
                            X = uiX,
                            Y = e.Y,
                            Z = uiZ
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RefreshLightsList] EXCEPTION adding entry {i}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"[RefreshLightsList] Found {usedCount} used lights, added {Lights.Count} to collection");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RefreshLightsList] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[RefreshLightsList] Stack trace: {ex.StackTrace}");
            }

            OnPropertyChanged(nameof(LightCount));
            Debug.WriteLine("[RefreshLightsList] Complete");
        }

        public void AddLightAt(int uiX, int uiZ)
        {
            if (!IsLightsLoaded) return;

            var acc = new LightsAccessor(_lightsSvc);
            int freeIdx = acc.FindFirstFreeIndex();
            if (freeIdx < 0)
            {
                StatusMessage = "No free light slots (255 max).";
                MessageBox.Show("All 255 light slots are used.", "Lights Full",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var entry = new LightEntry
            {
                Range = PlacementRange,
                Red = PlacementRed,
                Green = PlacementGreen,
                Blue = PlacementBlue,
                Used = 1,
                X = LightsAccessor.UiXToWorldX(uiX),
                Y = PlacementY,
                Z = LightsAccessor.UiZToWorldZ(uiZ)
            };

            acc.WriteEntry(freeIdx, entry);
            SelectedLightIndex = freeIdx;
            IsAddingLight = false;
            StatusMessage = $"Added light at ({uiX}, {uiZ}).";
        }

        private void DeleteSelectedLight()
        {
            if (_selectedLightIndex < 0) return;

            try
            {
                var acc = new LightsAccessor(_lightsSvc);
                acc.DeleteLight(_selectedLightIndex);
                SelectedLightIndex = -1;
                StatusMessage = "Deleted light.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to delete: {ex.Message}";
            }
        }

        private void CopyLight()
        {
            if (_selectedLightIndex < 0) return;

            try
            {
                var acc = new LightsAccessor(_lightsSvc);
                _copiedLight = acc.ReadEntry(_selectedLightIndex);
                OnPropertyChanged(nameof(CanPasteLight));
                StatusMessage = "Copied light to clipboard.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to copy: {ex.Message}";
            }
        }

        private void PasteLight()
        {
            if (_copiedLight == null || !IsLightsLoaded) return;

            try
            {
                var acc = new LightsAccessor(_lightsSvc);
                int freeIdx = acc.FindFirstFreeIndex();
                if (freeIdx < 0)
                {
                    StatusMessage = "No free light slots.";
                    return;
                }

                // Paste with same properties but offset position slightly
                var entry = new LightEntry
                {
                    Range = _copiedLight.Range,
                    Red = _copiedLight.Red,
                    Green = _copiedLight.Green,
                    Blue = _copiedLight.Blue,
                    Used = 1,
                    X = _copiedLight.X + 256, // Offset by 1 tile
                    Y = _copiedLight.Y,
                    Z = _copiedLight.Z + 256
                };

                acc.WriteEntry(freeIdx, entry);
                SelectedLightIndex = freeIdx;
                StatusMessage = "Pasted light.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to paste: {ex.Message}";
            }
        }

        public void MoveLightTo(int lightIndex, int uiX, int uiZ)
        {
            if (lightIndex < 0 || lightIndex >= LightsAccessor.EntryCount) return;

            try
            {
                var acc = new LightsAccessor(_lightsSvc);
                var entry = acc.ReadEntry(lightIndex);
                entry.X = LightsAccessor.UiXToWorldX(uiX);
                entry.Z = LightsAccessor.UiZToWorldZ(uiZ);
                acc.WriteEntry(lightIndex, entry);
                StatusMessage = $"Moved light to ({uiX}, {uiZ}).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to move: {ex.Message}";
            }
        }
    }

    // ===== Helper Classes =====
    public class LightListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Range { get; set; }
        public sbyte Red { get; set; }
        public sbyte Green { get; set; }
        public sbyte Blue { get; set; }

        // Preview color for UI (convert signed -128..127 to unsigned 0..255)
        public byte PreviewR => unchecked((byte)(Red + 128));
        public byte PreviewG => unchecked((byte)(Green + 128));
        public byte PreviewB => unchecked((byte)(Blue + 128));

        // Color property for direct binding in XAML
        public System.Windows.Media.Color PreviewColor =>
            System.Windows.Media.Color.FromRgb(PreviewR, PreviewG, PreviewB);
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}
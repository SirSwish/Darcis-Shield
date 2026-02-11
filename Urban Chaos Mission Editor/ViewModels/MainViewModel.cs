using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// Main ViewModel for the UCM Editor application
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly UcmFileService _fileService;
    private readonly IDialogService _dialogService;

    private Mission? _currentMission;
    private string? _currentFilePath;
    private EventPointViewModel? _selectedEventPoint;
    private string _statusMessage = "Ready";
    private double _mapZoom = 1.0;
    private double _mapOffsetX = 0;
    private double _mapOffsetY = 0;
    private bool _isLoading;
    private bool _isDirty;

    // Layer visibility
    private bool _showTextures = true;
    private bool _showGridLines = false;
    private bool _showBuildings = true;
    private bool _showPrimGraphics = true;
    private bool _showLights = false;
    private bool _showLightRanges = false;
    private bool _showEventPoints = true;

    public MainViewModel() : this(new UcmFileService(), new WpfDialogService())
    {
    }

    public MainViewModel(UcmFileService fileService, IDialogService dialogService)
    {
        _fileService = fileService;
        _dialogService = dialogService;

        // Subscribe to service events
        ReadOnlyMapDataService.Instance.MapLoaded += (s, e) => OnPropertyChanged(nameof(IsMapLoaded));
        ReadOnlyMapDataService.Instance.MapCleared += (s, e) => OnPropertyChanged(nameof(IsMapLoaded));
        ReadOnlyLightsDataService.Instance.LightsLoaded += (s, e) => OnPropertyChanged(nameof(IsLightsLoaded));
        ReadOnlyLightsDataService.Instance.LightsCleared += (s, e) => OnPropertyChanged(nameof(IsLightsLoaded));

        // Initialize commands
        NewFileCommand = new RelayCommand(ExecuteNewFile);
        OpenFileCommand = new RelayCommand(ExecuteOpenFile);
        SaveFileCommand = new RelayCommand(ExecuteSaveFile, () => HasMission);
        SaveAsFileCommand = new RelayCommand(ExecuteSaveAsFile, () => HasMission);
        CloseFileCommand = new RelayCommand(ExecuteCloseFile, () => HasMission);
        ExitCommand = new RelayCommand(ExecuteExit);

        OpenMapCommand = new RelayCommand(ExecuteOpenMap);
        OpenLightsCommand = new RelayCommand(ExecuteOpenLights);
        MissionPropertiesCommand = new RelayCommand(ExecuteMissionProperties, () => HasMission);

        ZoomInCommand = new RelayCommand(ExecuteZoomIn);
        ZoomOutCommand = new RelayCommand(ExecuteZoomOut);
        ResetZoomCommand = new RelayCommand(ExecuteResetZoom);
        CenterOnSelectedCommand = new RelayCommand(ExecuteCenterOnSelected, () => SelectedEventPoint != null);

        ToggleCategoryCommand = new RelayCommand<CategoryFilterViewModel>(ExecuteToggleCategory);
        ShowAllCategoriesCommand = new RelayCommand(ExecuteShowAllCategories);
        HideAllCategoriesCommand = new RelayCommand(ExecuteHideAllCategories);
        EditEventPointCommand = new RelayCommand(ExecuteEditEventPoint, () => SelectedEventPoint != null);
        AddEventPointCommand = new RelayCommand(ExecuteAddEventPoint, () => HasMission);
        DeleteEventPointCommand = new RelayCommand(ExecuteDeleteEventPoint, () => SelectedEventPoint != null);

        // Initialize category filters
        InitializeCategoryFilters();
    }

    // Collections

    public ObservableCollection<EventPointViewModel> EventPoints { get; } = new();
    public ObservableCollection<CategoryFilterViewModel> CategoryFilters { get; } = new();

    // Commands

    public ICommand NewFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveAsFileCommand { get; }
    public ICommand CloseFileCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenMapCommand { get; }
    public ICommand OpenLightsCommand { get; }
    public ICommand MissionPropertiesCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }
    public ICommand CenterOnSelectedCommand { get; }
    public ICommand ToggleCategoryCommand { get; }
    public ICommand ShowAllCategoriesCommand { get; }
    public ICommand HideAllCategoriesCommand { get; }
    public ICommand EditEventPointCommand { get; }
    public ICommand AddEventPointCommand { get; }
    public ICommand DeleteEventPointCommand { get; }

    // Layer Visibility Properties

    public bool ShowTextures
    {
        get => _showTextures;
        set => SetProperty(ref _showTextures, value);
    }

    public bool ShowGridLines
    {
        get => _showGridLines;
        set => SetProperty(ref _showGridLines, value);
    }

    public bool ShowBuildings
    {
        get => _showBuildings;
        set => SetProperty(ref _showBuildings, value);
    }

    public bool ShowPrimGraphics
    {
        get => _showPrimGraphics;
        set => SetProperty(ref _showPrimGraphics, value);
    }

    public bool ShowLights
    {
        get => _showLights;
        set => SetProperty(ref _showLights, value);
    }

    public bool ShowLightRanges
    {
        get => _showLightRanges;
        set => SetProperty(ref _showLightRanges, value);
    }

    public bool ShowEventPoints
    {
        get => _showEventPoints;
        set => SetProperty(ref _showEventPoints, value);
    }

    // Loading state

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public bool IsMapLoaded => ReadOnlyMapDataService.Instance.IsLoaded;
    public bool IsLightsLoaded => ReadOnlyLightsDataService.Instance.IsLoaded;

    public string LoadedMapFileName => ReadOnlyMapDataService.Instance.CurrentPath != null
        ? Path.GetFileName(ReadOnlyMapDataService.Instance.CurrentPath)
        : "(none)";

    public string LoadedLightsFileName => ReadOnlyLightsDataService.Instance.CurrentPath != null
        ? Path.GetFileName(ReadOnlyLightsDataService.Instance.CurrentPath)
        : "(none)";

    // Mission Properties

    public Mission? CurrentMission
    {
        get => _currentMission;
        private set
        {
            if (SetProperty(ref _currentMission, value))
            {
                OnPropertyChanged(nameof(HasMission));
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(MissionName));
                OnPropertyChanged(nameof(MapName));
                OnPropertyChanged(nameof(LightMapName));
                OnPropertyChanged(nameof(EventPointCount));
                OnPropertyChanged(nameof(CrimeRate));
                OnPropertyChanged(nameof(CivsRate));
                OnPropertyChanged(nameof(BoredomRate));
                OnPropertyChanged(nameof(CarsRate));
                OnPropertyChanged(nameof(MusicWorld));
                OnPropertyChanged(nameof(Version));
            }
        }
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public EventPointViewModel? SelectedEventPoint
    {
        get => _selectedEventPoint;
        set
        {
            var oldSelection = _selectedEventPoint;
            if (SetProperty(ref _selectedEventPoint, value))
            {
                if (oldSelection != null)
                    oldSelection.IsSelected = false;
                if (value != null)
                    value.IsSelected = true;

                OnPropertyChanged(nameof(HasSelection));
                ((RelayCommand)CenterOnSelectedCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public double MapZoom
    {
        get => _mapZoom;
        set => SetProperty(ref _mapZoom, Math.Clamp(value, 0.1, 10.0));
    }

    public double MapOffsetX
    {
        get => _mapOffsetX;
        set => SetProperty(ref _mapOffsetX, value);
    }

    public double MapOffsetY
    {
        get => _mapOffsetY;
        set => SetProperty(ref _mapOffsetY, value);
    }

    // Computed properties

    public bool HasMission => _currentMission != null;
    public bool HasSelection => _selectedEventPoint != null;

    public string WindowTitle
    {
        get
        {
            if (_currentFilePath == null)
            {
                // New unsaved mission
                if (_currentMission != null)
                    return _isDirty ? "UCM Editor - New Mission *" : "UCM Editor - New Mission";
                return "UCM Editor";
            }

            string fileName = Path.GetFileName(_currentFilePath);
            return _isDirty ? $"UCM Editor - {fileName} *" : $"UCM Editor - {fileName}";
        }
    }

    public string FileName => _currentFilePath != null
        ? Path.GetFileName(_currentFilePath)
        : (_currentMission != null ? "New Mission" : string.Empty);

    public string MissionName => _currentMission?.MissionName ?? string.Empty;
    public string MapName => _currentMission?.MapName ?? string.Empty;
    public string LightMapName => _currentMission?.LightMapName ?? string.Empty;
    public int EventPointCount => _currentMission?.UsedEventPointCount ?? 0;
    public byte CrimeRate => _currentMission?.CrimeRate ?? 0;
    public byte CivsRate => _currentMission?.CivsRate ?? 0;
    public byte BoredomRate => _currentMission?.BoredomRate ?? 0;
    public byte CarsRate => _currentMission?.CarsRate ?? 0;
    public byte MusicWorld => _currentMission?.MusicWorld ?? 0;
    public uint Version => _currentMission?.Version ?? 0;

    // Visible EventPoints (filtered)
    public IEnumerable<EventPointViewModel> VisibleEventPoints =>
        EventPoints.Where(ep => ep.IsVisible);

    // Category filter initialization

    private void InitializeCategoryFilters()
    {
        CategoryFilters.Clear();
        foreach (WaypointCategory category in Enum.GetValues<WaypointCategory>())
        {
            var filter = new CategoryFilterViewModel(category);
            filter.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CategoryFilterViewModel.IsEnabled))
                {
                    ApplyFilters();
                }
            };
            CategoryFilters.Add(filter);
        }
    }

    // Command implementations

    private void ExecuteNewFile()
    {
        // Check for unsaved changes
        if (IsDirty)
        {
            var result = _dialogService.ShowYesNoCancelDialog(
                "Save Changes",
                "Do you want to save changes before creating a new mission?");

            if (result == true) // Yes
            {
                ExecuteSaveFile();
            }
            else if (result == null) // Cancel
            {
                return;
            }
        }

        // Create a new blank mission
        var mission = CreateBlankMission();

        CurrentMission = mission;
        CurrentFilePath = null; // No file path yet - will need Save As
        IsDirty = true; // Mark as dirty since it's unsaved

        // Clear and reload
        EventPoints.Clear();
        LoadEventPoints();
        UpdateCategoryCounts();
        ApplyFilters();

        // Clear map and lights
        ReadOnlyMapDataService.Instance.Clear();
        ReadOnlyLightsDataService.Instance.Clear();

        OnPropertyChanged(nameof(LoadedMapFileName));
        OnPropertyChanged(nameof(LoadedLightsFileName));

        StatusMessage = "Created new mission";
    }

    /// <summary>
    /// Creates a blank mission with default values
    /// </summary>
    private Mission CreateBlankMission()
    {
        var mission = new Mission
        {
            Version = Mission.CurrentVersion,
            Flags = MissionFlags.Used,
            BriefName = string.Empty,
            LightMapName = @"Data\Lighting\newmap.lgt",
            MapName = @"Data\newmap.iam",
            MissionName = "newmission",
            CitSezMapName = string.Empty,
            MapIndex = 0,
            FreeEPoints = 1, // First free slot (1-based)
            UsedEPoints = 0, // No used slots yet
            CrimeRate = 4,
            CivsRate = 4,
            BoredomRate = 4,
            CarsRate = 2,
            MusicWorld = 0,
            SkillLevels = new byte[254]
        };

        // Initialize EventPoints array with empty slots
        mission.InitializeEventPoints();

        // Initialize MissionZones to zero (no zones set)
        mission.MissionZones = new byte[128, 128];

        return mission;
    }

    private void ExecuteOpenFile()
    {
        // Check for unsaved changes
        if (IsDirty)
        {
            var result = _dialogService.ShowYesNoCancelDialog(
                "Save Changes",
                "Do you want to save changes before opening a new file?");

            if (result == true) // Yes
            {
                ExecuteSaveFile();
            }
            else if (result == null) // Cancel
            {
                return;
            }
        }

        var filePath = _dialogService.ShowOpenFileDialog(
            "Open UCM Mission File",
            "UCM Files (*.ucm)|*.ucm|All Files (*.*)|*.*",
            @"C:\Fallen\Levels"
        );

        if (filePath != null)
        {
            LoadMission(filePath);
        }
    }

    private void ExecuteSaveFile()
    {
        if (_currentMission == null) return;

        // If no file path yet (new mission), use Save As
        if (_currentFilePath == null)
        {
            ExecuteSaveAsFile();
            return;
        }

        try
        {
            _fileService.WriteMission(_currentMission, _currentFilePath);
            IsDirty = false;
            StatusMessage = $"Saved {Path.GetFileName(_currentFilePath)}";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to save file:\n\n{ex.Message}");
        }
    }

    private void ExecuteSaveAsFile()
    {
        if (_currentMission == null) return;

        var filePath = _dialogService.ShowSaveFileDialog(
            "Save UCM Mission File",
            "UCM Files (*.ucm)|*.ucm|All Files (*.*)|*.*",
            _currentFilePath != null ? Path.GetDirectoryName(_currentFilePath) : @"C:\Fallen\Levels",
            _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "mission.ucm"
        );

        if (filePath != null)
        {
            try
            {
                _fileService.WriteMission(_currentMission, filePath);
                CurrentFilePath = filePath;
                IsDirty = false;
                StatusMessage = $"Saved as {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Failed to save file:\n\n{ex.Message}");
            }
        }
    }

    private void ExecuteCloseFile()
    {
        // Check for unsaved changes
        if (IsDirty)
        {
            var result = _dialogService.ShowYesNoCancelDialog(
                "Save Changes",
                "Do you want to save changes before closing?");

            if (result == true) // Yes
            {
                ExecuteSaveFile();
            }
            else if (result == null) // Cancel
            {
                return;
            }
        }

        CurrentMission = null;
        CurrentFilePath = null;
        IsDirty = false;
        EventPoints.Clear();
        SelectedEventPoint = null;
        UpdateCategoryCounts();

        // Clear map and lights
        ReadOnlyMapDataService.Instance.Clear();
        ReadOnlyLightsDataService.Instance.Clear();

        OnPropertyChanged(nameof(LoadedMapFileName));
        OnPropertyChanged(nameof(LoadedLightsFileName));

        StatusMessage = "File closed";
    }

    private void ExecuteExit()
    {
        // Check for unsaved changes
        if (IsDirty)
        {
            var result = _dialogService.ShowYesNoCancelDialog(
                "Save Changes",
                "Do you want to save changes before exiting?");

            if (result == true) // Yes
            {
                ExecuteSaveFile();
            }
            else if (result == null) // Cancel
            {
                return;
            }
        }

        System.Windows.Application.Current.Shutdown();
    }

    private async void ExecuteOpenMap()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "Open Map File",
            "IAM Map Files (*.iam)|*.iam|All Files (*.*)|*.*",
            _currentFilePath != null ? Path.GetDirectoryName(_currentFilePath) : null
        );

        if (filePath != null)
        {
            await LoadMapFileAsync(filePath);
        }
    }

    private async void ExecuteOpenLights()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "Open Lights File",
            "LGT Light Files (*.lgt)|*.lgt|All Files (*.*)|*.*",
            _currentFilePath != null ? Path.GetDirectoryName(_currentFilePath) : null
        );

        if (filePath != null)
        {
            await LoadLightsFileAsync(filePath);
        }
    }

    private void ExecuteZoomIn()
    {
        MapZoom *= 1.25;
    }

    private void ExecuteZoomOut()
    {
        MapZoom /= 1.25;
    }

    private void ExecuteResetZoom()
    {
        MapZoom = 1.0;
        MapOffsetX = 0;
        MapOffsetY = 0;
    }

    private void ExecuteCenterOnSelected()
    {
        if (SelectedEventPoint == null) return;
        // View handles scrolling via property change
    }

    private void ExecuteToggleCategory(CategoryFilterViewModel? filter)
    {
        filter?.Toggle();
    }

    private void ExecuteShowAllCategories()
    {
        foreach (var filter in CategoryFilters)
            filter.IsEnabled = true;
    }

    private void ExecuteHideAllCategories()
    {
        foreach (var filter in CategoryFilters)
            filter.IsEnabled = false;
    }

    private void ExecuteEditEventPoint()
    {
        if (SelectedEventPoint == null) return;

        // Use a loop to handle position selection reopening
        var editorVm = new EventPointEditorViewModel(SelectedEventPoint.Model);

        while (true)
        {
            var window = new Views.EventPointEditorWindow
            {
                DataContext = editorVm,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = window.ShowDialog();

            // Check if user wants to select position from map
            if (window.NeedsPositionSelection)
            {
                // Get the MainWindow and MapViewControl
                var mainWindow = System.Windows.Application.Current.MainWindow as Views.MainWindow;
                if (mainWindow?.MapViewControl != null)
                {
                    var positionSelected = false;
                    var selectedX = 0;
                    var selectedZ = 0;

                    // Enter position selection mode
                    mainWindow.MapViewControl.EnterPositionSelectionMode((worldX, worldZ) =>
                    {
                        selectedX = worldX;
                        selectedZ = worldZ;
                        positionSelected = true;
                    });

                    // Hook escape to cancel
                    void OnKeyDown(object s, System.Windows.Input.KeyEventArgs e)
                    {
                        if (e.Key == System.Windows.Input.Key.Escape)
                        {
                            mainWindow.MapViewControl.ExitPositionSelectionMode();
                            positionSelected = false;
                            e.Handled = true;
                        }
                    }
                    mainWindow.PreviewKeyDown += OnKeyDown;

                    // Process events until position is selected or cancelled
                    while (mainWindow.MapViewControl.IsInPositionSelectionMode)
                    {
                        // Process WPF message queue
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(delegate { }));
                        System.Threading.Thread.Sleep(10);
                    }

                    mainWindow.PreviewKeyDown -= OnKeyDown;

                    // Update position if selected
                    if (positionSelected)
                    {
                        editorVm.WorldX = selectedX;
                        editorVm.WorldZ = selectedZ;
                    }

                    // Loop back to reopen dialog
                    continue;
                }
            }

            if (result == true)
            {
                // Changes were applied directly to the model
                // Refresh the view
                OnPropertyChanged(nameof(SelectedEventPoint));
                StatusMessage = $"Updated EventPoint {SelectedEventPoint.Index}";

                // Mark mission as modified
                IsDirty = true;
            }
            else
            {
                // User cancelled - revert changes
                editorVm.RevertChanges();
            }

            break; // Exit loop after handling OK/Cancel
        }
    }

    private void ExecuteAddEventPoint()
    {
        if (_currentMission == null) return;

        // Step 1: Show type selection dialog
        var typeDialogVm = new NewEventPointDialogViewModel();
        var typeDialog = new Views.NewEventPointDialog
        {
            DataContext = typeDialogVm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (typeDialog.ShowDialog() != true || typeDialogVm.SelectedWaypointType == null)
        {
            return; // User cancelled type selection
        }

        var selectedType = typeDialogVm.SelectedWaypointType.Value;

        // Step 2: Find a free slot in the EventPoints array
        int freeIndex = FindFreeEventPointSlot();
        if (freeIndex < 0)
        {
            _dialogService.ShowError("No free EventPoint slots available. Maximum is 512.");
            return;
        }

        // Step 3: Create new EventPoint with the selected type
        var newEventPoint = new Models.EventPoint
        {
            Index = freeIndex + 1, // 1-based index
            WaypointType = selectedType,
            Used = true,
            Group = 0, // Default to group A
            Colour = 2, // Default to Red
            Direction = 0,
            Flags = Constants.WaypointFlags.None,
            TriggeredBy = Constants.TriggerType.None,
            OnTrigger = Constants.OnTriggerBehavior.None,
            // Set to center of map initially
            // PixelX = 8192 - (X / 4) = 8192 - (16384 / 4) = 8192 - 4096 = 4096 (center)
            X = 16384,
            Y = 0,
            Z = 16384,
        };

        // Step 4: Open the editor for the new EventPoint
        var editorVm = new EventPointEditorViewModel(newEventPoint);
        var editorWindow = new Views.EventPointEditorWindow
        {
            DataContext = editorVm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var result = editorWindow.ShowDialog();

        if (result == true)
        {
            // Step 5: Add to mission and refresh
            _currentMission.EventPoints[freeIndex] = newEventPoint;

            // Update linked list pointers (simplified - just mark as used)
            // In a full implementation, we'd update FreeEPoints/UsedEPoints linked lists

            // Add to our observable collection
            var viewModel = new EventPointViewModel(newEventPoint);
            EventPoints.Add(viewModel);

            // Update category counts
            UpdateCategoryCounts();
            ApplyFilters();

            // Select the new event point
            SelectedEventPoint = viewModel;

            StatusMessage = $"Created new EventPoint {freeIndex + 1}: {Constants.EditorStrings.GetWaypointTypeName(selectedType)}";
            IsDirty = true;
        }
        // If cancelled, we don't add anything
    }

    /// <summary>
    /// Find a free slot in the EventPoints array (first unused entry)
    /// </summary>
    private int FindFreeEventPointSlot()
    {
        if (_currentMission == null) return -1;

        for (int i = 0; i < Models.Mission.MaxEventPoints; i++)
        {
            var ep = _currentMission.EventPoints[i];
            if (ep == null || !ep.Used)
            {
                return i;
            }
        }

        return -1; // No free slots
    }

    private void ExecuteDeleteEventPoint()
    {
        if (_currentMission == null || SelectedEventPoint == null) return;

        var ep = SelectedEventPoint;
        var typeName = Constants.EditorStrings.GetWaypointTypeName(ep.Model.WaypointType);

        // Confirm deletion
        if (!_dialogService.ShowConfirmation(
            $"Are you sure you want to delete EventPoint {ep.Index} ({typeName})?",
            "Delete EventPoint"))
        {
            return;
        }

        // Find the array index (Index is 1-based, array is 0-based)
        int arrayIndex = ep.Index - 1;

        if (arrayIndex >= 0 && arrayIndex < Models.Mission.MaxEventPoints)
        {
            // Mark as unused in the mission data
            var eventPoint = _currentMission.EventPoints[arrayIndex];
            if (eventPoint != null)
            {
                eventPoint.Used = false;
                // Clear position and other data
                eventPoint.X = 0;
                eventPoint.Y = 0;
                eventPoint.Z = 0;
                eventPoint.WaypointType = Constants.WaypointType.None;
            }
        }

        // Remove from observable collection
        EventPoints.Remove(ep);

        // Clear selection
        SelectedEventPoint = null;

        // Update counts and refresh
        UpdateCategoryCounts();
        OnPropertyChanged(nameof(EventPointCount));

        StatusMessage = $"Deleted EventPoint {ep.Index}: {typeName}";
        IsDirty = true;
    }

    private void ExecuteMissionProperties()
    {
        if (_currentMission == null) return;

        var propertiesVm = new MissionPropertiesViewModel(_currentMission);
        var window = new Views.MissionPropertiesWindow
        {
            DataContext = propertiesVm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        var result = window.ShowDialog();

        if (result == true)
        {
            // Changes were applied
            OnPropertyChanged(nameof(MapName));
            OnPropertyChanged(nameof(LightMapName));
            StatusMessage = "Mission properties updated";
            IsDirty = true;
        }
        else
        {
            propertiesVm.RevertChanges();
        }
    }

    // Loading

    public async void LoadMission(string filePath)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading mission...";

            var mission = _fileService.ReadMission(filePath);

            CurrentMission = mission;
            CurrentFilePath = filePath;
            IsDirty = false; // Fresh load, no changes yet

            LoadEventPoints();
            UpdateCategoryCounts();
            ApplyFilters();

            StatusMessage = $"Loaded {EventPointCount} event points from {FileName}";

            // Try to auto-load map and lights
            await TryAutoLoadMapAndLightsAsync(filePath);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to load mission file:\n\n{ex.Message}");
            StatusMessage = "Failed to load file";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TryAutoLoadMapAndLightsAsync(string ucmFilePath)
    {
        if (_currentMission == null) return;

        var ucmDir = Path.GetDirectoryName(ucmFilePath);
        if (ucmDir == null) return;

        // Try to find the game root (parent of 'levels' folder)
        var gameRoot = FindGameRoot(ucmDir);

        // Try to load map file
        var mapPath = _currentMission.MapName;
        if (!string.IsNullOrWhiteSpace(mapPath))
        {
            var resolvedMapPath = ResolveFilePath(mapPath, ucmDir, gameRoot);
            if (resolvedMapPath != null && File.Exists(resolvedMapPath))
            {
                await LoadMapFileAsync(resolvedMapPath);
            }
            else
            {
                Debug.WriteLine($"[MainViewModel] Map file not found: {mapPath}");
                StatusMessage = $"Map file not found: {Path.GetFileName(mapPath)} - use View > Open Map to load manually";
            }
        }

        // Try to load lights file
        var lightsPath = _currentMission.LightMapName;
        if (!string.IsNullOrWhiteSpace(lightsPath))
        {
            var resolvedLightsPath = ResolveFilePath(lightsPath, ucmDir, gameRoot);
            if (resolvedLightsPath != null && File.Exists(resolvedLightsPath))
            {
                await LoadLightsFileAsync(resolvedLightsPath);
            }
            else
            {
                Debug.WriteLine($"[MainViewModel] Lights file not found: {lightsPath}");
            }
        }
    }

    private static string? FindGameRoot(string startDir)
    {
        // Walk up from the UCM directory looking for a parent that looks like a game root
        var current = startDir;
        while (current != null)
        {
            // Check if this looks like the game root (has 'data' subfolder or is parent of 'levels')
            var dataDir = Path.Combine(current, "data");
            if (Directory.Exists(dataDir))
            {
                return current;
            }

            // Check if parent directory name is 'levels'
            var dirName = Path.GetFileName(current);
            if (string.Equals(dirName, "levels", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(current);
            }

            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private static string? ResolveFilePath(string relativePath, string ucmDir, string? gameRoot)
    {
        // Normalize path separators
        var normalizedPath = relativePath.Replace('/', '\\').TrimStart('\\');

        // Try 1: Relative to UCM file directory
        var path1 = Path.Combine(ucmDir, normalizedPath);
        if (File.Exists(path1)) return path1;

        // Try 2: Relative to game root
        if (gameRoot != null)
        {
            var path2 = Path.Combine(gameRoot, normalizedPath);
            if (File.Exists(path2)) return path2;
        }

        // Try 3: Go up from UCM dir and try relative path
        var parent = Path.GetDirectoryName(ucmDir);
        if (parent != null)
        {
            var path3 = Path.Combine(parent, normalizedPath);
            if (File.Exists(path3)) return path3;
        }

        // Try 4: Just the filename in the UCM directory
        var fileName = Path.GetFileName(normalizedPath);
        var path4 = Path.Combine(ucmDir, fileName);
        if (File.Exists(path4)) return path4;

        return null;
    }

    private async Task LoadMapFileAsync(string path)
    {
        try
        {
            StatusMessage = $"Loading map: {Path.GetFileName(path)}...";
            await ReadOnlyMapDataService.Instance.LoadAsync(path);

            OnPropertyChanged(nameof(LoadedMapFileName));
            OnPropertyChanged(nameof(IsMapLoaded));
            StatusMessage = $"Loaded map: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Failed to load map: {ex.Message}");
            StatusMessage = $"Failed to load map: {ex.Message}";
        }
    }

    private async Task LoadLightsFileAsync(string path)
    {
        try
        {
            StatusMessage = $"Loading lights: {Path.GetFileName(path)}...";
            await ReadOnlyLightsDataService.Instance.LoadAsync(path);
            OnPropertyChanged(nameof(LoadedLightsFileName));
            OnPropertyChanged(nameof(IsLightsLoaded));
            StatusMessage = $"Loaded lights: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] Failed to load lights: {ex.Message}");
            StatusMessage = $"Failed to load lights: {ex.Message}";
        }
    }

    private void LoadEventPoints()
    {
        EventPoints.Clear();
        SelectedEventPoint = null;

        if (_currentMission == null) return;

        foreach (var ep in _currentMission.UsedEventPoints.OrderBy(e => e.Index))
        {
            var vm = new EventPointViewModel(ep);
            EventPoints.Add(vm);
        }
    }

    private void UpdateCategoryCounts()
    {
        var counts = _currentMission?.GetCategoryCounts() ?? [];

        foreach (var filter in CategoryFilters)
        {
            filter.Count = counts.TryGetValue(filter.Category, out var count) ? count : 0;
        }
    }

    private void ApplyFilters()
    {
        var enabledCategories = CategoryFilters
            .Where(f => f.IsEnabled)
            .Select(f => f.Category)
            .ToHashSet();

        foreach (var ep in EventPoints)
        {
            ep.IsVisible = enabledCategories.Contains(ep.Category);
        }

        OnPropertyChanged(nameof(VisibleEventPoints));
    }

    /// <summary>
    /// Select an EventPoint by its index
    /// </summary>
    public void SelectEventPointByIndex(int index)
    {
        SelectedEventPoint = EventPoints.FirstOrDefault(ep => ep.Index == index);
    }

    /// <summary>
    /// Find EventPoint at pixel coordinates
    /// </summary>
    public EventPointViewModel? FindEventPointAtPosition(double pixelX, double pixelY, double tolerance = 10)
    {
        return VisibleEventPoints
            .Where(ep =>
            {
                double dx = ep.PixelX - pixelX;
                double dy = ep.PixelZ - pixelY;
                return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
            })
            .OrderBy(ep =>
            {
                double dx = ep.PixelX - pixelX;
                double dy = ep.PixelZ - pixelY;
                return dx * dx + dy * dy;
            })
            .FirstOrDefault();
    }
}
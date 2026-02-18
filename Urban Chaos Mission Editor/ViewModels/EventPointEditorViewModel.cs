using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Views.Editors;


namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// ViewModel for editing a single EventPoint
/// </summary>
public class EventPointEditorViewModel : BaseViewModel
{
    private readonly EventPoint _model;
    private readonly EventPoint _originalState;

    // Type-specific editor content
    private FrameworkElement? _typeSpecificEditor;

    public EventPointEditorViewModel(EventPoint eventPoint)
    {
        _model = eventPoint ?? throw new ArgumentNullException(nameof(eventPoint));

        // Store original state for cancel/revert
        _originalState = CloneEventPoint(eventPoint);

        // Initialize options
        InitializeOptions();

        // Create type-specific editor
        CreateTypeSpecificEditor();
    }

    private void InitializeOptions()
    {
        // Group options (A-Z)
        GroupOptions = new ObservableCollection<string>();
        for (char c = 'A'; c <= 'Z'; c++)
            GroupOptions.Add(c.ToString());

        // Color options
        ColorOptions = new ObservableCollection<string>(WaypointColors.ColorNames);

        // Trigger type options
        TriggerTypeOptions = new ObservableCollection<TriggerTypeOption>();
        foreach (TriggerType tt in Enum.GetValues<TriggerType>())
        {
            TriggerTypeOptions.Add(new TriggerTypeOption(tt, EditorStrings.GetTriggerTypeName(tt)));
        }

        // On Trigger options
        OnTriggerOptions = new ObservableCollection<OnTriggerOption>();
        foreach (OnTriggerBehavior ot in Enum.GetValues<OnTriggerBehavior>())
        {
            if ((int)ot < EditorStrings.OnTriggerNames.Length)
                OnTriggerOptions.Add(new OnTriggerOption(ot, EditorStrings.OnTriggerNames[(int)ot]));
        }
    }

    private void CreateTypeSpecificEditor()
    {
        TypeSpecificEditor = _model.WaypointType switch
        {
            WaypointType.CreatePlayer => new PlayerEditorControl { DataContext = this },
            WaypointType.CreateEnemies => new EnemyEditorControl { DataContext = this },
            WaypointType.CreateItem => new ItemEditorControl { DataContext = this },
            WaypointType.CreateBarrel => new BarrelEditorControl { DataContext = this },
            WaypointType.CreateCreature => new CreatureEditorControl { DataContext = this },
            WaypointType.CreateVehicle => new VehicleEditorControl { DataContext = this },
            WaypointType.VisualEffect => new VisualEffectEditorControl { DataContext = this },
            WaypointType.AdjustEnemy => new AdjustEnemyEditorControl { DataContext = this },
            WaypointType.MoveThing => new MoveThingEditorControl { DataContext = this },
            WaypointType.CreateTarget => new CreateTargetEditorControl { DataContext = this },
            WaypointType.Simple => new SimpleWaypointEditorControl { DataContext = this },
            WaypointType.Conversation => new ConversationEditorControl { DataContext = this },
            WaypointType.Message => new MessageEditorControl { DataContext = this },
            WaypointType.BonusPoints => new BonusPointsEditorControl { DataContext = this },
            WaypointType.EndGameWin => new EndGameWinEditorControl { DataContext = this },
            WaypointType.EndGameLose => new EndGameLoseEditorControl { DataContext = this },
            WaypointType.CreateCamera => new CameraEditorControl { DataContext = this },
            WaypointType.CameraWaypoint => new CameraWaypointEditorControl { DataContext = this },
            WaypointType.CreateBomb => new CreateBombEditorControl { DataContext = this },
            WaypointType.CreateTrap => new CreateTrapEditorControl { DataContext = this },
            WaypointType.BurnPrim => new BurnPrimEditorControl { DataContext = this },
            WaypointType.EnemyFlags => new EnemyFlagsEditorControl { DataContext = this },
            WaypointType.CreateTreasure => new CreateTreasureEditorControl { DataContext = this },
            WaypointType.ActivatePrim => new ActivatePrimEditorControl { DataContext = this },
            WaypointType.ConePenalties => new ConePenaltiesEditorControl { DataContext = this },
            WaypointType.CountUpTimer => new CountUpTimerEditorControl { DataContext = this },
            WaypointType.CreateMist => new CreateMistEditorControl { DataContext = this },
            WaypointType.DynamicLight => new DynamicLightEditorControl { DataContext = this },
            WaypointType.SpotEffect => new SpotEffectEditorControl { DataContext = this },
            WaypointType.Extend => new ExtendTimerEditorControl { DataContext = this },
            WaypointType.MakePersonPee => new MakePersonPeeEditorControl { DataContext = this },
            WaypointType.WareFX => new WareFXEditorControl { DataContext = this },
            WaypointType.MakeSearchable => new MakeSearchableEditorControl { DataContext = this },
            WaypointType.StallCar => new StallCarEditorControl { DataContext = this },
            WaypointType.TransferPlayer => new TransferPlayerEditorControl { DataContext = this },
            WaypointType.NavBeacon => new NavBeaconEditorControl { DataContext = this },
            WaypointType.SoundEffect => new SoundEffectEditorControl { DataContext = this },
            WaypointType.LockVehicle => new LockVehicleEditorControl { DataContext = this },
            WaypointType.Increment => new IncrementEditorControl { DataContext = this },
            WaypointType.ResetCounter => new ResetCounterEditorControl { DataContext = this },
            WaypointType.GroupLife => new GroupLifeEditorControl { DataContext = this },
            WaypointType.GroupDeath => new GroupDeathEditorControl { DataContext = this },
            WaypointType.GroupReset => new GroupResetEditorControl { DataContext = this },
            WaypointType.KillWaypoint => new KillWaypointEditorControl { DataContext = this },
            WaypointType.NoFloor => new NoFloorEditorControl { DataContext = this },
            WaypointType.Autosave => new AutosaveEditorControl { DataContext = this },
            WaypointType.ShakeCamera => new ShakeCameraEditorControl { DataContext = this },
            WaypointType.Teleport => new TeleportEditorControl { DataContext = this },
            WaypointType.TeleportTarget => new TeleportTargetEditorControl { DataContext = this },
            WaypointType.LinkPlatform => new LinkPlatformEditorControl { DataContext = this },
            WaypointType.TargetWaypoint => new CreateTargetEditorControl { DataContext = this },
            WaypointType.CutScene => new CutsceneEditorControl { DataContext = this },
            WaypointType.Sign => new SignEditorControl { DataContext = this },

            // Add more type-specific editors here as needed
            _ => CreateGenericDataEditor()
        };
        if (_model.WaypointType == WaypointType.SoundEffect)
        {
            RebuildSfxSoundList();
        }
    }

    private FrameworkElement CreateGenericDataEditor()
    {
        // Generic editor showing raw Data array
        return new GenericDataEditorControl { DataContext = this };
    }

    // Header properties
    public string HeaderText => $"Edit EventPoint {_model.Index}{_model.GroupLetter}";
    public string SubHeaderText => $"{EditorStrings.GetWaypointTypeName(_model.WaypointType)} - {_model.Category}";

    // Common properties
    public int Index => _model.Index;
    public string WaypointTypeName => EditorStrings.GetWaypointTypeName(_model.WaypointType);
    public WaypointType WaypointType => _model.WaypointType;

    // Group
    public ObservableCollection<string> GroupOptions { get; private set; } = new();
    public string SelectedGroup
    {
        get => ((char)('A' + _model.Group)).ToString();
        set
        {
            if (!string.IsNullOrEmpty(value) && value.Length == 1)
            {
                _model.Group = (byte)(value[0] - 'A');
                OnPropertyChanged();
            }
        }
    }

    // Color
    public ObservableCollection<string> ColorOptions { get; private set; } = new();
    public int ColorIndex
    {
        get => _model.Colour;
        set
        {
            _model.Colour = (byte)Math.Clamp(value, 0, 14);
            OnPropertyChanged();
        }
    }

    // Position
    // The editor shows game world coordinates (same as hover display)
    // These are stored directly in the model

    public int WorldX
    {
        get => _model.X;
        set { _model.X = value; OnPropertyChanged(); OnPropertyChanged(nameof(GridPositionText)); }
    }

    public int WorldY
    {
        get => _model.Y;
        set { _model.Y = value; OnPropertyChanged(); }
    }

    public int WorldZ
    {
        get => _model.Z;
        set { _model.Z = value; OnPropertyChanged(); OnPropertyChanged(nameof(GridPositionText)); }
    }

    public string GridPositionText => $"({_model.MapX}, {_model.MapZ})";

    // Direction
    public byte Direction
    {
        get => _model.Direction;
        set { _model.Direction = value; OnPropertyChanged(); OnPropertyChanged(nameof(DirectionDegreesText)); }
    }

    public string DirectionDegreesText => $"({_model.DirectionDegrees:F1}°)";

    // Trigger settings
    public ObservableCollection<TriggerTypeOption> TriggerTypeOptions { get; private set; } = new();
    public TriggerTypeOption? SelectedTriggerType
    {
        get => TriggerTypeOptions.FirstOrDefault(t => t.Value == _model.TriggeredBy);
        set
        {
            if (value != null)
            {
                _model.TriggeredBy = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<OnTriggerOption> OnTriggerOptions { get; private set; } = new();
    public OnTriggerOption? SelectedOnTrigger
    {
        get => OnTriggerOptions.FirstOrDefault(o => o.Value == _model.OnTrigger);
        set
        {
            if (value != null)
            {
                _model.OnTrigger = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public ushort EPRef
    {
        get => _model.EPRef;
        set { _model.EPRef = value; OnPropertyChanged(); }
    }

    public int Radius
    {
        get => _model.Radius;
        set { _model.Radius = value; OnPropertyChanged(); }
    }

    // Type-specific editor
    public FrameworkElement? TypeSpecificEditor
    {
        get => _typeSpecificEditor;
        private set => SetProperty(ref _typeSpecificEditor, value);
    }

    // Data array access for type-specific editors
    public int[] Data => _model.Data;

    // ExtraText for text-based EventPoints (Message, Conversation, Shout, etc.)
    public string? ExtraText
    {
        get => _model.ExtraText;
        set
        {
            _model.ExtraText = value;
            OnPropertyChanged();
        }
    }

    public int Data0 { get => _model.Data[0]; set { _model.Data[0] = value; OnPropertyChanged(); } }
    public int Data1 { get => _model.Data[1]; set { _model.Data[1] = value; OnPropertyChanged(); } }
    public int Data2 { get => _model.Data[2]; set { _model.Data[2] = value; OnPropertyChanged(); } }
    public int Data3 { get => _model.Data[3]; set { _model.Data[3] = value; OnPropertyChanged(); } }
    public int Data4 { get => _model.Data[4]; set { _model.Data[4] = value; OnPropertyChanged(); } }
    public int Data5 { get => _model.Data[5]; set { _model.Data[5] = value; OnPropertyChanged(); } }
    public int Data6 { get => _model.Data[6]; set { _model.Data[6] = value; OnPropertyChanged(); } }
    public int Data7 { get => _model.Data[7]; set { _model.Data[7] = value; OnPropertyChanged(); } }
    public int Data8 { get => _model.Data[8]; set { _model.Data[8] = value; OnPropertyChanged(); } }
    public int Data9 { get => _model.Data[9]; set { _model.Data[9] = value; OnPropertyChanged(); } }

    // Player-specific properties
    public int PlayerType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlayerTypeName));
        }
    }

    public string PlayerTypeName => PlayerType >= 1 && PlayerType <= EditorStrings.PlayerTypeNames.Length
        ? EditorStrings.PlayerTypeNames[PlayerType - 1]
        : "Unknown";

    // Camera-specific properties (CreateCamera)
    // Data[0] = Camera Type (Normal, Security Cam, Camcorder, News, Targetting)
    // Data[1] = Target Type (Normal, Attached, Nearest Living)
    // Data[2] = Follow player flag
    public ObservableCollection<string> CameraTypeOptions { get; } = new(EditorStrings.CameraTypeNames);
    public ObservableCollection<string> TargetTypeOptions { get; } = new(EditorStrings.TargetTypeNames);

    public int CameraType
    {
        get => _model.Data[0];
        set { _model.Data[0] = value; OnPropertyChanged(); }
    }

    public int TargetType
    {
        get => _model.Data[1];
        set { _model.Data[1] = value; OnPropertyChanged(); }
    }

    public bool FollowPlayer
    {
        get => _model.Data[2] != 0;
        set { _model.Data[2] = value ? 1 : 0; OnPropertyChanged(); }
    }

    // Camera Waypoint-specific properties
    // Data[0] = Move type (Normal, Smooth, Wobbly)
    // Data[1] = Dwell time (how long to stay at this waypoint)
    public ObservableCollection<string> CameraMoveTypeOptions { get; } = new(EditorStrings.CameraMoveNames);

    public int CameraMoveType
    {
        get => _model.Data[0];
        set { _model.Data[0] = value; OnPropertyChanged(); }
    }

    public int DwellTime
    {
        get => _model.Data[1];
        set { _model.Data[1] = value; OnPropertyChanged(); }
    }
    // ============================================================
    // CUTSCENE PROPERTIES (WPT_CUT_SCENE / WaypointType.CutScene)
    // ============================================================
    // Cutscene data is stored separately from the Data[] array.
    // It uses a complex nested structure of channels and packets.

    /// <summary>
    /// Access to the cutscene data
    /// </summary>
    public CutsceneData? Cutscene => _model.CutsceneData;

    /// <summary>
    /// Cutscene format version
    /// </summary>
    public string CutsceneVersion => Cutscene?.Version.ToString() ?? "N/A";

    /// <summary>
    /// Number of channels in the cutscene
    /// </summary>
    public string CutsceneChannelCount => Cutscene?.Channels.Count.ToString() ?? "0";

    /// <summary>
    /// Total duration of the cutscene in frames
    /// </summary>
    public string CutsceneDuration => Cutscene?.Duration.ToString() ?? "0";

    /// <summary>
    /// Whether the cutscene needs to be initialized
    /// </summary>
    public bool NeedsCutsceneInit => _model.CutsceneData == null;

    /// <summary>
    /// Whether there are no channels
    /// </summary>
    public bool HasNoChannels => Cutscene?.Channels.Count == 0;

    /// <summary>
    /// Whether nothing is currently selected
    /// </summary>
    public bool NothingSelected => _selectedCutsceneItem == null;

    /// <summary>
    /// Observable collection of channel view models for binding
    /// </summary>
    public ObservableCollection<CutsceneChannelViewModel> CutsceneChannels { get; } = new();

    /// <summary>
    /// Currently selected item in the cutscene tree
    /// </summary>
    private object? _selectedCutsceneItem;

    /// <summary>
    /// Properties panel content for the selected item
    /// </summary>
    private FrameworkElement? _selectedItemProperties;
    public FrameworkElement? SelectedItemProperties
    {
        get => _selectedItemProperties;
        set => SetProperty(ref _selectedItemProperties, value);
    }

    /// <summary>
    /// Initialize cutscene data if not present
    /// </summary>
    public void EnsureCutsceneData()
    {
        if (_model.CutsceneData == null)
        {
            _model.CutsceneData = CutsceneData.CreateNew();
            RefreshCutsceneChannels();
            OnPropertyChanged(nameof(Cutscene));
            OnPropertyChanged(nameof(CutsceneVersion));
            OnPropertyChanged(nameof(CutsceneChannelCount));
            OnPropertyChanged(nameof(CutsceneDuration));
            OnPropertyChanged(nameof(NeedsCutsceneInit));
            OnPropertyChanged(nameof(HasNoChannels));
        }
    }

    /// <summary>
    /// Refresh the CutsceneChannels collection from the model
    /// </summary>
    private void RefreshCutsceneChannels()
    {
        CutsceneChannels.Clear();
        if (Cutscene != null)
        {
            foreach (var channel in Cutscene.Channels)
            {
                CutsceneChannels.Add(new CutsceneChannelViewModel(channel));
            }
        }
        OnPropertyChanged(nameof(HasNoChannels));
        OnPropertyChanged(nameof(CutsceneChannelCount));
        OnPropertyChanged(nameof(CutsceneDuration));
    }

    /// <summary>
    /// Add a new channel to the cutscene
    /// </summary>
    public void AddCutsceneChannel(CutsceneChannelType channelType, ushort personType = 1)
    {
        EnsureCutsceneData();

        CutsceneChannel? newChannel = channelType switch
        {
            CutsceneChannelType.Character => Cutscene!.AddCharacterChannel(personType),
            CutsceneChannelType.Camera => Cutscene!.AddCameraChannel(),
            CutsceneChannelType.Sound => Cutscene!.AddSoundChannel(),
            CutsceneChannelType.Subtitles => Cutscene!.AddSubtitleChannel(),
            _ => null
        };

        if (newChannel != null)
        {
            CutsceneChannels.Add(new CutsceneChannelViewModel(newChannel));
            OnPropertyChanged(nameof(HasNoChannels));
            OnPropertyChanged(nameof(CutsceneChannelCount));
        }
    }

    /// <summary>
    /// Remove selected item from cutscene (channel or packet)
    /// </summary>
    public void RemoveSelectedCutsceneItem(object? item)
    {
        if (item == null || Cutscene == null) return;

        if (item is CutsceneChannelViewModel channelVm)
        {
            Cutscene.Channels.Remove(channelVm.Model);
            CutsceneChannels.Remove(channelVm);
        }
        else if (item is CutscenePacketViewModel packetVm)
        {
            // Find parent channel and remove packet
            foreach (var channelViewModel in CutsceneChannels)
            {
                if (channelViewModel.Packets.Contains(packetVm))
                {
                    channelViewModel.Model.Packets.Remove(packetVm.Model);
                    channelViewModel.Packets.Remove(packetVm);
                    break;
                }
            }
        }

        OnPropertyChanged(nameof(HasNoChannels));
        OnPropertyChanged(nameof(CutsceneChannelCount));
        OnPropertyChanged(nameof(CutsceneDuration));
    }

    /// <summary>
    /// Handle selection change in the cutscene tree
    /// </summary>
    public void OnCutsceneSelectionChanged(object? newSelection)
    {
        _selectedCutsceneItem = newSelection;
        OnPropertyChanged(nameof(NothingSelected));

        // Create appropriate property panel
        if (newSelection is CutsceneChannelViewModel channelVm)
        {
            SelectedItemProperties = new CutsceneChannelPropertiesPanel(channelVm);
        }
        else if (newSelection is CutscenePacketViewModel packetVm)
        {
            SelectedItemProperties = new CutscenePacketPropertiesPanel(packetVm);
        }
        else
        {
            SelectedItemProperties = null;
        }
    }
    // ============================================================
    // LINK PLATFORM (WPT_LINK_PLATFORM / WaypointType.LinkPlatform)
    // ============================================================
    // platformSetup.cpp:
    // Data[0] = platform_speed
    // Data[1] = platform_flags bitmask (bit0 Lock axis, bit1 Lock rotation, bit2 Rocket)

    public int PlatformSpeed
    {
        get => _model.Data[0];
        set { _model.Data[0] = value; OnPropertyChanged(); }
    }

    public bool PlatformLockToAxis
    {
        get => (_model.Data[1] & 1) != 0;
        set
        {
            if (value) _model.Data[1] |= 1;
            else _model.Data[1] &= ~1;
            OnPropertyChanged();
        }
    }

    public bool PlatformLockRotation
    {
        get => (_model.Data[1] & 2) != 0;
        set
        {
            if (value) _model.Data[1] |= 2;
            else _model.Data[1] &= ~2;
            OnPropertyChanged();
        }
    }

    public bool PlatformIsRocket
    {
        get => (_model.Data[1] & 4) != 0;
        set
        {
            if (value) _model.Data[1] |= 4;
            else _model.Data[1] &= ~4;
            OnPropertyChanged();
        }
    }
    // ============================================================
    // SIGN PROPERTIES (WPT_SIGN / WaypointType.Sign)
    // ============================================================
    // From signsetup.cpp:
    // Data[0] = sign type (0..3)
    // Data[1] = flip flags (bit1=LR, bit2=TB)

    public ObservableCollection<string> SignTypeOptions { get; } = new(EditorStrings.SignTypeNames);

    public int SignType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.SignTypeNames.Length - 1);
            OnPropertyChanged();
        }
    }

    public bool SignFlipLeftRight
    {
        get => (_model.Data[1] & 1) != 0;
        set
        {
            if (value) _model.Data[1] |= 1;
            else _model.Data[1] &= ~1;
            OnPropertyChanged();
        }
    }

    public bool SignFlipTopBottom
    {
        get => (_model.Data[1] & 2) != 0;
        set
        {
            if (value) _model.Data[1] |= 2;
            else _model.Data[1] &= ~2;
            OnPropertyChanged();
        }
    }
    // ============================================================
    // KILL WAYPOINT (WPT_KILL_WAYPOINT / WaypointType.KillWaypoint)
    // ============================================================
    // Data[0] = target EP index to kill (must be non-zero)

    public int KillWaypointTarget
    {
        get => _model.Data[0] > 0 ? _model.Data[0] : 1;
        set
        {
            // Clamp to valid EP reference range (1..MaxEventPoints-1)
            _model.Data[0] = Math.Clamp(value, 1, Mission.MaxEventPoints - 1);
            OnPropertyChanged();
        }
    }
    // ============================================================
    // RESET COUNTER PROPERTIES (WPT_RESET_COUNTER / WaypointType.ResetCounter)
    // ============================================================
    // Mission.cpp valid_ep:
    // Data[0] must be within 0..9 (10 counters)
    // Editor shows 1..10, stored as Data[0] = (index-1)

    public int ResetCounterIndex
    {
        get => Math.Clamp(_model.Data[0], 0, 9) + 1;
        set
        {
            int v = Math.Clamp(value, 1, 10);
            _model.Data[0] = v - 1;
            OnPropertyChanged();
        }
    }
    // ============================================================
    // INCREMENT PROPERTIES (WPT_INCREMENT / WaypointType.Increment)
    // ============================================================
    // Data layout from countersetup.cpp:
    // Data[0] = amount (0..256)
    // Data[1] = counter index (1..10)

    public int IncrementBy
    {
        get => Math.Clamp(_model.Data[0], 0, 256);
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, 256);
            OnPropertyChanged();
        }
    }

    public int IncrementCounterIndex
    {
        get => _model.Data[1] >= 1 ? _model.Data[1] : 1;
        set
        {
            _model.Data[1] = Math.Clamp(value, 1, 10);
            OnPropertyChanged();
        }
    }
    // ============================================================
    // LOCK VEHICLE PROPERTIES (WPT_LOCK / WaypointType.LockVehicle)
    // ============================================================
    // Data layout from locksetup.cpp:
    // Data[0] = which_vehicle (1..2048)
    // Data[1] = lock_unlock   (0=unlocked, 1=locked)

    public ObservableCollection<string> LockVehicleStateOptions { get; } =
        new(new[] { "Unlocked", "Locked" });

    public int LockVehicleVehicle
    {
        get => _model.Data[0] > 0 ? _model.Data[0] : 1;
        set
        {
            _model.Data[0] = Math.Clamp(value, 1, 2048);
            OnPropertyChanged();
        }
    }

    public int LockVehicleState
    {
        get => _model.Data[1] != 0 ? 1 : 0;
        set
        {
            _model.Data[1] = value != 0 ? 1 : 0;
            OnPropertyChanged();
        }
    }
    // ============================================================
    // SOUND EFFECT PROPERTIES (WPT_SFX / WaypointType.SoundEffect)
    // ============================================================
    // From SfxSetup.cpp:
    // Data[0] = sfx_type (0=Sound FX, 1=Music)
    // Data[1] = sfx_id   (absolute index into sound_list[])

    private readonly List<int> _sfxFilteredIds = new();
    private int _sfxSoundSelectedIndex = -1;

    public ObservableCollection<string> SfxTypeOptions { get; } =
        new ObservableCollection<string>(new[] { "Sound FX", "Music" });

    public ObservableCollection<string> SfxSoundOptions { get; } = new();

    public int SfxType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, 1);
            OnPropertyChanged();
            RebuildSfxSoundList();
        }
    }

    public int SfxId
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = value; // don’t clamp; list size comes from sound_list[]
            OnPropertyChanged();
            SyncSfxSelectionFromId();
        }
    }

    public int SfxSoundSelectedIndex
    {
        get => _sfxSoundSelectedIndex;
        set
        {
            if (_sfxSoundSelectedIndex == value) return;

            _sfxSoundSelectedIndex = value;

            if (value >= 0 && value < _sfxFilteredIds.Count)
            {
                _model.Data[1] = _sfxFilteredIds[value];
                OnPropertyChanged(nameof(SfxId));
            }

            OnPropertyChanged();
        }
    }

    private void RebuildSfxSoundList()
    {
        // Match original C logic: is_music = strstr(name, "music") ? 1 : 0; (case-sensitive)
        bool wantMusic = (SfxType == 1);

        _sfxFilteredIds.Clear();
        SfxSoundOptions.Clear();

        var list = EditorStrings.SoundList;
        for (int i = 0; i < list.Length; i++)
        {
            string s = list[i];
            bool isMusic = s.Contains("music", StringComparison.Ordinal);
            if (isMusic == wantMusic)
            {
                _sfxFilteredIds.Add(i);
                SfxSoundOptions.Add(s);
            }
        }

        SyncSfxSelectionFromId();
    }

    private void SyncSfxSelectionFromId()
    {
        int id = _model.Data[1];
        int idx = _sfxFilteredIds.IndexOf(id);

        _sfxSoundSelectedIndex = idx; // set backing field to avoid writing Data[1] again
        OnPropertyChanged(nameof(SfxSoundSelectedIndex));
    }
    // ============================================================
    // TRANSFER PLAYER PROPERTIES (WPT_TRANSFER / WaypointType.TransferPlayer)
    // ============================================================
    // Data layout from TransferSetup.cpp:
    // Data[0] = transfer_to (range 1..2048)

    public int TransferPlayerTo
    {
        get => _model.Data[0] > 0 ? _model.Data[0] : 1;
        set
        {
            _model.Data[0] = Math.Clamp(value, 1, 2048);
            OnPropertyChanged();
        }
    }
    // ============================================================
    // NAV BEACON PROPERTIES (WPT_NAV_BEACON / WaypointType.NavBeacon)
    // ============================================================
    // Layout from NavSetup.cpp:
    //   Text stored as Data[0] pointer in old editor -> in this C# project it's stored in ExtraText
    //   Data[1] = nav_person (EP index, 0 = none)

    public int NavBeaconPersonEP
    {
        get => _model.Data[1];
        set
        {
            // Allow 0 = none. Clamp to valid EP range (1..512) when non-zero.
            int v = value;
            if (v < 0) v = 0;
            if (v > 512) v = 512;

            _model.Data[1] = v;
            OnPropertyChanged();
        }
    }
    // ============================================================
    // WAREHOUSE FX PROPERTIES (WPT_WAREFX / WaypointType.WareFX)
    // ============================================================
    // Data layout from warefxsetup.cpp:
    // Data[0] = warefx_type (index into EditorStrings.WareFXNames)

    public ObservableCollection<string> WareFXTypeOptions { get; } = new(EditorStrings.WareFXNames);

    public int WareFXType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.WareFXNames.Length - 1);
            OnPropertyChanged();
        }
    }
    // ============================================================
    // STALL CAR PROPERTIES (WPT_STALL / WaypointType.StallCar)
    // ============================================================
    // Data layout from stallsetup.cpp:
    // Data[0] = which_vehicle (range 1..2048)

    public int StallCarVehicle
    {
        get => _model.Data[0] > 0 ? _model.Data[0] : 1;
        set
        {
            _model.Data[0] = Math.Clamp(value, 1, 2048);
            OnPropertyChanged();
        }
    }
    // ============================================================
    // EXTEND TIMER PROPERTIES (WPT_EXTEND / WaypointType.Extend)
    // ============================================================
    // Evidence (Mission.cpp valid_ep):
    // Data[0] = Target EP index (must reference an EP whose TriggeredBy == VisibleCountdown)
    // Data[1] = Extend amount (must be non-zero)

    public int ExtendTimerTargetEP
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public int ExtendTimerAmount
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = value; // do not clamp; only rule we know is "must be non-zero"
            OnPropertyChanged();
        }
    }

    // ============================================================
    // MAKE PERSON PEE PROPERTIES (WPT_PEE / WaypointType.MakePersonPee)
    // ============================================================
    // Data layout from PeeSetup.cpp:
    // Data[0] = which_waypoint (range 1..2048)

    public int PeeWaypoint
    {
        get => _model.Data[0] > 0 ? _model.Data[0] : 1;
        set
        {
            _model.Data[0] = Math.Clamp(value, 1, 2048);
            OnPropertyChanged();
        }
    }

    // ============================================================
    // SPOT EFFECT PROPERTIES (WPT_SPOTFX / WaypointType.SpotEffect)
    // ============================================================
    // Data layout from spotfxSetup.cpp:
    // Data[0] = spotfx_type (index into SpotFXNames)
    // Data[1] = spotfx_scale (0-1024)

    public ObservableCollection<string> SpotFXTypeOptions { get; } = new(EditorStrings.SpotFXNames);

    public int SpotFXType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.SpotFXNames.Length - 1);
            OnPropertyChanged();
        }
    }

    public int SpotFXScale
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Clamp(value, 0, 1024);
            OnPropertyChanged();
        }
    }

    // ============================================================
    // DYNAMIC LIGHT PROPERTIES (WPT_DLIGHT / WaypointType.DynamicLight)
    // ============================================================
    // Data layout from dlightSetup.cpp:
    // Data[0] = lite_type (0=Flashing On/Off, 1=Two Tone, 2=Disco, 3=Flame)
    // Data[1] = lite_speed (1-32)
    // Data[2] = lite_steps (1-32, defaults to 1)
    // Data[3] = lite_mask  (bitmask of active steps)
    // Data[4] = lite_rgbA  (Windows COLORREF integer)
    // Data[5] = lite_rgbB  (Windows COLORREF integer)

    public ObservableCollection<string> DynamicLightTypeOptions { get; } = new(EditorStrings.DynamicLightNames);

    public int DynamicLightType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.DynamicLightNames.Length - 1);
            OnPropertyChanged();
        }
    }

    public int DynamicLightSpeed
    {
        get => _model.Data[1] > 0 ? _model.Data[1] : 1;
        set
        {
            _model.Data[1] = Math.Clamp(value, 1, 32);
            OnPropertyChanged();
        }
    }

    public int DynamicLightSteps
    {
        get => _model.Data[2] > 0 ? _model.Data[2] : 1;
        set
        {
            _model.Data[2] = Math.Clamp(value, 1, 32);
            OnPropertyChanged();
            NotifyDynamicLightStepVisibilityChanged();
        }
    }

    public int DynamicLightMask
    {
        get => _model.Data[3];
        set
        {
            _model.Data[3] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DynamicLightStepPatternText));
        }
    }

    public int DynamicLightColorA
    {
        get => _model.Data[4];
        set { _model.Data[4] = value; OnPropertyChanged(); }
    }

    public int DynamicLightColorB
    {
        get => _model.Data[5];
        set { _model.Data[5] = value; OnPropertyChanged(); }
    }

    public string DynamicLightStepPatternText
    {
        get
        {
            int steps = DynamicLightSteps;
            int mask = DynamicLightMask;
            var activeSteps = new List<int>();

            for (int i = 0; i < steps; i++)
            {
                if ((mask & (1 << i)) != 0)
                    activeSteps.Add(i + 1);
            }

            if (activeSteps.Count == 0) return "None";
            if (activeSteps.Count == steps) return "All";

            return string.Join(", ", activeSteps);
        }
    }

    // Individual step properties (1-32)
    public bool DynamicLightStep1 { get => GetDynamicLightStep(0); set => SetDynamicLightStep(0, value); }
    public bool DynamicLightStep2 { get => GetDynamicLightStep(1); set => SetDynamicLightStep(1, value); }
    public bool DynamicLightStep3 { get => GetDynamicLightStep(2); set => SetDynamicLightStep(2, value); }
    public bool DynamicLightStep4 { get => GetDynamicLightStep(3); set => SetDynamicLightStep(3, value); }
    public bool DynamicLightStep5 { get => GetDynamicLightStep(4); set => SetDynamicLightStep(4, value); }
    public bool DynamicLightStep6 { get => GetDynamicLightStep(5); set => SetDynamicLightStep(5, value); }
    public bool DynamicLightStep7 { get => GetDynamicLightStep(6); set => SetDynamicLightStep(6, value); }
    public bool DynamicLightStep8 { get => GetDynamicLightStep(7); set => SetDynamicLightStep(7, value); }
    public bool DynamicLightStep9 { get => GetDynamicLightStep(8); set => SetDynamicLightStep(8, value); }
    public bool DynamicLightStep10 { get => GetDynamicLightStep(9); set => SetDynamicLightStep(9, value); }
    public bool DynamicLightStep11 { get => GetDynamicLightStep(10); set => SetDynamicLightStep(10, value); }
    public bool DynamicLightStep12 { get => GetDynamicLightStep(11); set => SetDynamicLightStep(11, value); }
    public bool DynamicLightStep13 { get => GetDynamicLightStep(12); set => SetDynamicLightStep(12, value); }
    public bool DynamicLightStep14 { get => GetDynamicLightStep(13); set => SetDynamicLightStep(13, value); }
    public bool DynamicLightStep15 { get => GetDynamicLightStep(14); set => SetDynamicLightStep(14, value); }
    public bool DynamicLightStep16 { get => GetDynamicLightStep(15); set => SetDynamicLightStep(15, value); }
    public bool DynamicLightStep17 { get => GetDynamicLightStep(16); set => SetDynamicLightStep(16, value); }
    public bool DynamicLightStep18 { get => GetDynamicLightStep(17); set => SetDynamicLightStep(17, value); }
    public bool DynamicLightStep19 { get => GetDynamicLightStep(18); set => SetDynamicLightStep(18, value); }
    public bool DynamicLightStep20 { get => GetDynamicLightStep(19); set => SetDynamicLightStep(19, value); }
    public bool DynamicLightStep21 { get => GetDynamicLightStep(20); set => SetDynamicLightStep(20, value); }
    public bool DynamicLightStep22 { get => GetDynamicLightStep(21); set => SetDynamicLightStep(21, value); }
    public bool DynamicLightStep23 { get => GetDynamicLightStep(22); set => SetDynamicLightStep(22, value); }
    public bool DynamicLightStep24 { get => GetDynamicLightStep(23); set => SetDynamicLightStep(23, value); }
    public bool DynamicLightStep25 { get => GetDynamicLightStep(24); set => SetDynamicLightStep(24, value); }
    public bool DynamicLightStep26 { get => GetDynamicLightStep(25); set => SetDynamicLightStep(25, value); }
    public bool DynamicLightStep27 { get => GetDynamicLightStep(26); set => SetDynamicLightStep(26, value); }
    public bool DynamicLightStep28 { get => GetDynamicLightStep(27); set => SetDynamicLightStep(27, value); }
    public bool DynamicLightStep29 { get => GetDynamicLightStep(28); set => SetDynamicLightStep(28, value); }
    public bool DynamicLightStep30 { get => GetDynamicLightStep(29); set => SetDynamicLightStep(29, value); }
    public bool DynamicLightStep31 { get => GetDynamicLightStep(30); set => SetDynamicLightStep(30, value); }
    public bool DynamicLightStep32 { get => GetDynamicLightStep(31); set => SetDynamicLightStep(31, value); }

    private bool GetDynamicLightStep(int index) => (_model.Data[3] & (1 << index)) != 0;

    private void SetDynamicLightStep(int index, bool value)
    {
        if (value)
            _model.Data[3] |= (1 << index);
        else
            _model.Data[3] &= ~(1 << index);

        OnPropertyChanged(nameof(DynamicLightStepPatternText));
    }

    // Step visibility properties (show only up to DynamicLightSteps count)
    public Visibility DynamicLightStep1Visibility => DynamicLightSteps >= 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep2Visibility => DynamicLightSteps >= 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep3Visibility => DynamicLightSteps >= 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep4Visibility => DynamicLightSteps >= 4 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep5Visibility => DynamicLightSteps >= 5 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep6Visibility => DynamicLightSteps >= 6 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep7Visibility => DynamicLightSteps >= 7 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep8Visibility => DynamicLightSteps >= 8 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep9Visibility => DynamicLightSteps >= 9 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep10Visibility => DynamicLightSteps >= 10 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep11Visibility => DynamicLightSteps >= 11 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep12Visibility => DynamicLightSteps >= 12 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep13Visibility => DynamicLightSteps >= 13 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep14Visibility => DynamicLightSteps >= 14 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep15Visibility => DynamicLightSteps >= 15 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep16Visibility => DynamicLightSteps >= 16 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep17Visibility => DynamicLightSteps >= 17 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep18Visibility => DynamicLightSteps >= 18 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep19Visibility => DynamicLightSteps >= 19 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep20Visibility => DynamicLightSteps >= 20 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep21Visibility => DynamicLightSteps >= 21 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep22Visibility => DynamicLightSteps >= 22 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep23Visibility => DynamicLightSteps >= 23 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep24Visibility => DynamicLightSteps >= 24 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep25Visibility => DynamicLightSteps >= 25 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep26Visibility => DynamicLightSteps >= 26 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep27Visibility => DynamicLightSteps >= 27 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep28Visibility => DynamicLightSteps >= 28 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep29Visibility => DynamicLightSteps >= 29 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep30Visibility => DynamicLightSteps >= 30 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep31Visibility => DynamicLightSteps >= 31 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DynamicLightStep32Visibility => DynamicLightSteps >= 32 ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyDynamicLightStepVisibilityChanged()
    {
        for (int i = 1; i <= 32; i++)
            OnPropertyChanged($"DynamicLightStep{i}Visibility");

        OnPropertyChanged(nameof(DynamicLightStepPatternText));
    }

    // ============================================================
    // CREATE MIST PROPERTIES (WPT_MIST / WaypointType.CreateMist)
    // ============================================================
    // Data layout from VfxSetup.cpp (shared with Visual Effect):
    // Data[0]: vfx_types - Bitmask of visual effect types
    //   Bit 0: Flare
    //   Bit 1: Fire Dome
    //   Bit 2: Shockwave
    //   Bit 3: Smoke Trails
    //   Bit 4: Bonfire
    // Data[1]: vfx_scale - Effect scale/intensity (0-1024)

    /// <summary>
    /// Mist FX types bitmask (Data[0])
    /// </summary>
    private int MistFxTypes
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = value;
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Flare effect flag (bit 0)
    /// </summary>
    public bool MistFxFlare
    {
        get => (MistFxTypes & (1 << 0)) != 0;
        set
        {
            MistFxTypes = value ? MistFxTypes | (1 << 0) : MistFxTypes & ~(1 << 0);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Fire Dome effect flag (bit 1)
    /// </summary>
    public bool MistFxFireDome
    {
        get => (MistFxTypes & (1 << 1)) != 0;
        set
        {
            MistFxTypes = value ? MistFxTypes | (1 << 1) : MistFxTypes & ~(1 << 1);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Shockwave effect flag (bit 2)
    /// </summary>
    public bool MistFxShockwave
    {
        get => (MistFxTypes & (1 << 2)) != 0;
        set
        {
            MistFxTypes = value ? MistFxTypes | (1 << 2) : MistFxTypes & ~(1 << 2);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Smoke Trails effect flag (bit 3)
    /// </summary>
    public bool MistFxSmokeTrails
    {
        get => (MistFxTypes & (1 << 3)) != 0;
        set
        {
            MistFxTypes = value ? MistFxTypes | (1 << 3) : MistFxTypes & ~(1 << 3);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Bonfire effect flag (bit 4)
    /// </summary>
    public bool MistFxBonfire
    {
        get => (MistFxTypes & (1 << 4)) != 0;
        set
        {
            MistFxTypes = value ? MistFxTypes | (1 << 4) : MistFxTypes & ~(1 << 4);
            OnPropertyChanged();
            OnPropertyChanged(nameof(MistFxDescription));
        }
    }

    /// <summary>
    /// Human-readable description of selected mist effects
    /// </summary>
    public string MistFxDescription
    {
        get
        {
            var effects = new List<string>();
            if (MistFxFlare) effects.Add("Flare");
            if (MistFxFireDome) effects.Add("Fire Dome");
            if (MistFxShockwave) effects.Add("Shockwave");
            if (MistFxSmokeTrails) effects.Add("Smoke Trails");
            if (MistFxBonfire) effects.Add("Bonfire");

            return effects.Count > 0 ? string.Join(", ", effects) : "None";
        }
    }

    /// <summary>
    /// Mist effect scale/intensity (Data[1], range 0-1024)
    /// </summary>
    public int MistScale
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Clamp(value, 0, 1024);
            OnPropertyChanged();
        }
    }

    // ============================================================
    // ACTIVATE PRIM PROPERTIES (WPT_ACTIVATE_PRIM / WaypointType.ActivatePrim)
    // ============================================================
    // Data layout from activatesetup.cpp:
    // Data[0] = prim_type (0=Door, 1=Electric Fence, 2=Security Camera, 3=Anim Prim)
    // Data[1] = prim_anim (1-99, animation number - only used when prim_type is 3)

    public ObservableCollection<string> ActivatePrimTypeOptions { get; } = new(EditorStrings.ActivatePrimNames);

    /// <summary>
    /// Prim type to activate (Data[0])
    /// </summary>
    public int ActivatePrimType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.ActivatePrimNames.Length - 1);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActivatePrimAnimVisibility));
            OnPropertyChanged(nameof(ActivatePrimDescription));
        }
    }

    /// <summary>
    /// Animation number for Anim Prim type (Data[1], range 1-99)
    /// </summary>
    public int ActivatePrimAnim
    {
        get => _model.Data[1] > 0 ? _model.Data[1] : 1;
        set
        {
            _model.Data[1] = Math.Clamp(value, 1, 99);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Visibility for animation settings (only visible when type is Anim Prim = 3)
    /// </summary>
    public Visibility ActivatePrimAnimVisibility =>
        _model.Data[0] == 3 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Description of what the current prim type does
    /// </summary>
    public string ActivatePrimDescription
    {
        get
        {
            return _model.Data[0] switch
            {
                0 => "Door: Opens or closes a door prim. The door will toggle between open and closed states when triggered.",
                1 => "Electric Fence: Enables or disables an electric fence. When active, the fence damages anyone who touches it.",
                2 => "Security Camera: Activates a security camera. The camera will begin scanning and can detect the player.",
                3 => $"Anim Prim: Plays animation #{ActivatePrimAnim} on an animated prim. Used for custom animated map elements.",
                _ => "Unknown prim type."
            };
        }
    }

    // ============================================================
    // ENEMY FLAGS PROPERTIES (WPT_ENEMY_FLAGS / WaypointType.EnemyFlags)
    // ============================================================
    // Data layout from enemyflagsetup.cpp:
    // Data[0] = enemyf_to_change (EP index of Create Enemies to modify)
    // Data[1] = enemyf_flags (bitmask of flags to apply)

    /// <summary>
    /// Target enemy EP index to modify (Data[0])
    /// </summary>
    public int EnemyFlagsTargetEP
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Flags bitmask to apply (Data[1])
    /// </summary>
    public int EnemyFlagsValue
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = value;
            OnPropertyChanged();
            NotifyAllEnemyFlagsChanged();
        }
    }

    // Individual flag properties (bits 0-15)
    public bool EnemyFlagsLazy
    {
        get => (_model.Data[1] & (1 << 0)) != 0;
        set { SetEnemyFlagsFlag(0, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsDiligent
    {
        get => (_model.Data[1] & (1 << 1)) != 0;
        set { SetEnemyFlagsFlag(1, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsGang
    {
        get => (_model.Data[1] & (1 << 2)) != 0;
        set { SetEnemyFlagsFlag(2, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsFightBack
    {
        get => (_model.Data[1] & (1 << 3)) != 0;
        set { SetEnemyFlagsFlag(3, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsJustKillPlayer
    {
        get => (_model.Data[1] & (1 << 4)) != 0;
        set { SetEnemyFlagsFlag(4, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsRobotic
    {
        get => (_model.Data[1] & (1 << 5)) != 0;
        set { SetEnemyFlagsFlag(5, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsRestrictedMovement
    {
        get => (_model.Data[1] & (1 << 6)) != 0;
        set { SetEnemyFlagsFlag(6, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsOnlyPlayerKills
    {
        get => (_model.Data[1] & (1 << 7)) != 0;
        set { SetEnemyFlagsFlag(7, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsBlueZone
    {
        get => (_model.Data[1] & (1 << 8)) != 0;
        set { SetEnemyFlagsFlag(8, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsCyanZone
    {
        get => (_model.Data[1] & (1 << 9)) != 0;
        set { SetEnemyFlagsFlag(9, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsYellowZone
    {
        get => (_model.Data[1] & (1 << 10)) != 0;
        set { SetEnemyFlagsFlag(10, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsMagentaZone
    {
        get => (_model.Data[1] & (1 << 11)) != 0;
        set { SetEnemyFlagsFlag(11, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsInvulnerable
    {
        get => (_model.Data[1] & (1 << 12)) != 0;
        set { SetEnemyFlagsFlag(12, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsGuilty
    {
        get => (_model.Data[1] & (1 << 13)) != 0;
        set { SetEnemyFlagsFlag(13, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsFakeWandering
    {
        get => (_model.Data[1] & (1 << 14)) != 0;
        set { SetEnemyFlagsFlag(14, value); OnPropertyChanged(); }
    }

    public bool EnemyFlagsCanBeCarried
    {
        get => (_model.Data[1] & (1 << 15)) != 0;
        set { SetEnemyFlagsFlag(15, value); OnPropertyChanged(); }
    }

    private void SetEnemyFlagsFlag(int bit, bool value)
    {
        if (value)
            _model.Data[1] |= (1 << bit);
        else
            _model.Data[1] &= ~(1 << bit);
    }

    private void NotifyAllEnemyFlagsChanged()
    {
        OnPropertyChanged(nameof(EnemyFlagsLazy));
        OnPropertyChanged(nameof(EnemyFlagsDiligent));
        OnPropertyChanged(nameof(EnemyFlagsGang));
        OnPropertyChanged(nameof(EnemyFlagsFightBack));
        OnPropertyChanged(nameof(EnemyFlagsJustKillPlayer));
        OnPropertyChanged(nameof(EnemyFlagsRobotic));
        OnPropertyChanged(nameof(EnemyFlagsRestrictedMovement));
        OnPropertyChanged(nameof(EnemyFlagsOnlyPlayerKills));
        OnPropertyChanged(nameof(EnemyFlagsBlueZone));
        OnPropertyChanged(nameof(EnemyFlagsCyanZone));
        OnPropertyChanged(nameof(EnemyFlagsYellowZone));
        OnPropertyChanged(nameof(EnemyFlagsMagentaZone));
        OnPropertyChanged(nameof(EnemyFlagsInvulnerable));
        OnPropertyChanged(nameof(EnemyFlagsGuilty));
        OnPropertyChanged(nameof(EnemyFlagsFakeWandering));
        OnPropertyChanged(nameof(EnemyFlagsCanBeCarried));
    }

    // ============================================================
    // CREATE BOMB PROPERTIES (WPT_BOMB / WaypointType.CreateBomb)
    // ============================================================
    // Data layout from bombSetup.cpp:
    // Data[0] = bomb_type (0=Dynamite Stick, 1=Egg Timer, 2=Hi-Tech LED)
    // Data[1] = bomb_size (0-1024, explosion radius)
    // Data[2] = bomb_fx (bitmask for visual effects)

    public ObservableCollection<string> BombTypeOptions { get; } = new(EditorStrings.BombTypeNames);

    /// <summary>
    /// Bomb type (Data[0]) - visual appearance
    /// </summary>
    public int BombType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.BombTypeNames.Length - 1);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Explosion size/radius (Data[1], range 0-1024)
    /// </summary>
    public int BombSize
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Clamp(value, 0, 1024);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Bomb FX bitmask (Data[2])
    /// Bit 0 = Flare, Bit 1 = Fire Dome, Bit 2 = Shockwave, Bit 3 = Smoke Trails, Bit 4 = Bonfire
    /// </summary>
    public int BombFx
    {
        get => _model.Data[2];
        set
        {
            _model.Data[2] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BombFxFlare));
            OnPropertyChanged(nameof(BombFxFireDome));
            OnPropertyChanged(nameof(BombFxShockwave));
            OnPropertyChanged(nameof(BombFxSmokeTrails));
            OnPropertyChanged(nameof(BombFxBonfire));
        }
    }

    public bool BombFxFlare
    {
        get => (_model.Data[2] & (1 << 0)) != 0;
        set { SetBombFxFlag(0, value); OnPropertyChanged(); }
    }

    public bool BombFxFireDome
    {
        get => (_model.Data[2] & (1 << 1)) != 0;
        set { SetBombFxFlag(1, value); OnPropertyChanged(); }
    }

    public bool BombFxShockwave
    {
        get => (_model.Data[2] & (1 << 2)) != 0;
        set { SetBombFxFlag(2, value); OnPropertyChanged(); }
    }

    public bool BombFxSmokeTrails
    {
        get => (_model.Data[2] & (1 << 3)) != 0;
        set { SetBombFxFlag(3, value); OnPropertyChanged(); }
    }

    public bool BombFxBonfire
    {
        get => (_model.Data[2] & (1 << 4)) != 0;
        set { SetBombFxFlag(4, value); OnPropertyChanged(); }
    }

    private void SetBombFxFlag(int bit, bool value)
    {
        if (value)
            _model.Data[2] |= (1 << bit);
        else
            _model.Data[2] &= ~(1 << bit);
    }

    // ============================================================
    // BURN PRIM PROPERTIES (WPT_BURN_PRIM / WaypointType.BurnPrim)
    // ============================================================
    // Data layout from burnsetup.cpp:
    // Data[0] = burn_type (bitmask for fire effects)
    //           Bit 0 = Flickering flames
    //           Bit 1 = Bonfires all over
    //           Bit 2 = Thick flames
    //           Bit 3 = Thick smoke
    //           Bit 4 = Static

    /// <summary>
    /// Burn type bitmask (Data[0])
    /// </summary>
    public int BurnType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BurnFlickeringFlames));
            OnPropertyChanged(nameof(BurnBonfiresAllOver));
            OnPropertyChanged(nameof(BurnThickFlames));
            OnPropertyChanged(nameof(BurnThickSmoke));
            OnPropertyChanged(nameof(BurnStatic));
        }
    }

    public bool BurnFlickeringFlames
    {
        get => (_model.Data[0] & (1 << 0)) != 0;
        set { SetBurnFlag(0, value); OnPropertyChanged(); }
    }

    public bool BurnBonfiresAllOver
    {
        get => (_model.Data[0] & (1 << 1)) != 0;
        set { SetBurnFlag(1, value); OnPropertyChanged(); }
    }

    public bool BurnThickFlames
    {
        get => (_model.Data[0] & (1 << 2)) != 0;
        set { SetBurnFlag(2, value); OnPropertyChanged(); }
    }

    public bool BurnThickSmoke
    {
        get => (_model.Data[0] & (1 << 3)) != 0;
        set { SetBurnFlag(3, value); OnPropertyChanged(); }
    }

    public bool BurnStatic
    {
        get => (_model.Data[0] & (1 << 4)) != 0;
        set { SetBurnFlag(4, value); OnPropertyChanged(); }
    }

    private void SetBurnFlag(int bit, bool value)
    {
        if (value)
            _model.Data[0] |= (1 << bit);
        else
            _model.Data[0] &= ~(1 << bit);
    }

    // ============================================================
    // CREATE TRAP PROPERTIES (WPT_TRAP / WaypointType.CreateTrap)
    // ============================================================
    // Data layout from TrapSetup.cpp:
    // Data[0] = trap_type (0=Steam Jet)
    // Data[1] = trap_speed (1-32, animation speed)
    // Data[2] = trap_steps (1-32, number of animation steps)
    // Data[3] = trap_mask (bitmask for which steps are active)
    // Data[4] = trap_axis (0=Forward, 1=Up, 2=Down)
    // Data[5] = trap_range (1-32, effect range)

    public ObservableCollection<string> TrapTypeOptions { get; } = new(EditorStrings.TrapTypeNames);
    public ObservableCollection<string> TrapAxisOptions { get; } = new(EditorStrings.TrapAxisNames);

    /// <summary>
    /// Trap type (Data[0])
    /// </summary>
    public int TrapType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, EditorStrings.TrapTypeNames.Length - 1);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Animation speed (Data[1], range 1-32)
    /// </summary>
    public int TrapSpeed
    {
        get => _model.Data[1] > 0 ? _model.Data[1] : 1;
        set
        {
            _model.Data[1] = Math.Clamp(value, 1, 32);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Number of animation steps (Data[2], range 1-32)
    /// </summary>
    public int TrapSteps
    {
        get => _model.Data[2] > 0 ? _model.Data[2] : 1;
        set
        {
            _model.Data[2] = Math.Clamp(value, 1, 32);
            OnPropertyChanged();
            // Notify visibility changes for all step checkboxes
            NotifyTrapStepVisibilityChanged();
        }
    }

    /// <summary>
    /// Step activation bitmask (Data[3])
    /// </summary>
    public int TrapMask
    {
        get => _model.Data[3];
        set
        {
            _model.Data[3] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrapStepPatternText));
        }
    }

    /// <summary>
    /// Trap direction/axis (Data[4])
    /// </summary>
    public int TrapAxis
    {
        get => _model.Data[4];
        set
        {
            _model.Data[4] = Math.Clamp(value, 0, EditorStrings.TrapAxisNames.Length - 1);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Effect range (Data[5], range 1-32)
    /// </summary>
    public int TrapRange
    {
        get => _model.Data[5] > 0 ? _model.Data[5] : 1;
        set
        {
            _model.Data[5] = Math.Clamp(value, 1, 32);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Human-readable description of the active step pattern
    /// </summary>
    public string TrapStepPatternText
    {
        get
        {
            int steps = TrapSteps;
            int mask = TrapMask;
            var activeSteps = new List<int>();

            for (int i = 0; i < steps; i++)
            {
                if ((mask & (1 << i)) != 0)
                    activeSteps.Add(i + 1);
            }

            if (activeSteps.Count == 0) return "None";
            if (activeSteps.Count == steps) return "All";

            return string.Join(", ", activeSteps);
        }
    }

    // Individual step properties (1-32)
    public bool TrapStep1 { get => GetTrapStep(0); set => SetTrapStep(0, value); }
    public bool TrapStep2 { get => GetTrapStep(1); set => SetTrapStep(1, value); }
    public bool TrapStep3 { get => GetTrapStep(2); set => SetTrapStep(2, value); }
    public bool TrapStep4 { get => GetTrapStep(3); set => SetTrapStep(3, value); }
    public bool TrapStep5 { get => GetTrapStep(4); set => SetTrapStep(4, value); }
    public bool TrapStep6 { get => GetTrapStep(5); set => SetTrapStep(5, value); }
    public bool TrapStep7 { get => GetTrapStep(6); set => SetTrapStep(6, value); }
    public bool TrapStep8 { get => GetTrapStep(7); set => SetTrapStep(7, value); }
    public bool TrapStep9 { get => GetTrapStep(8); set => SetTrapStep(8, value); }
    public bool TrapStep10 { get => GetTrapStep(9); set => SetTrapStep(9, value); }
    public bool TrapStep11 { get => GetTrapStep(10); set => SetTrapStep(10, value); }
    public bool TrapStep12 { get => GetTrapStep(11); set => SetTrapStep(11, value); }
    public bool TrapStep13 { get => GetTrapStep(12); set => SetTrapStep(12, value); }
    public bool TrapStep14 { get => GetTrapStep(13); set => SetTrapStep(13, value); }
    public bool TrapStep15 { get => GetTrapStep(14); set => SetTrapStep(14, value); }
    public bool TrapStep16 { get => GetTrapStep(15); set => SetTrapStep(15, value); }
    public bool TrapStep17 { get => GetTrapStep(16); set => SetTrapStep(16, value); }
    public bool TrapStep18 { get => GetTrapStep(17); set => SetTrapStep(17, value); }
    public bool TrapStep19 { get => GetTrapStep(18); set => SetTrapStep(18, value); }
    public bool TrapStep20 { get => GetTrapStep(19); set => SetTrapStep(19, value); }
    public bool TrapStep21 { get => GetTrapStep(20); set => SetTrapStep(20, value); }
    public bool TrapStep22 { get => GetTrapStep(21); set => SetTrapStep(21, value); }
    public bool TrapStep23 { get => GetTrapStep(22); set => SetTrapStep(22, value); }
    public bool TrapStep24 { get => GetTrapStep(23); set => SetTrapStep(23, value); }
    public bool TrapStep25 { get => GetTrapStep(24); set => SetTrapStep(24, value); }
    public bool TrapStep26 { get => GetTrapStep(25); set => SetTrapStep(25, value); }
    public bool TrapStep27 { get => GetTrapStep(26); set => SetTrapStep(26, value); }
    public bool TrapStep28 { get => GetTrapStep(27); set => SetTrapStep(27, value); }
    public bool TrapStep29 { get => GetTrapStep(28); set => SetTrapStep(28, value); }
    public bool TrapStep30 { get => GetTrapStep(29); set => SetTrapStep(29, value); }
    public bool TrapStep31 { get => GetTrapStep(30); set => SetTrapStep(30, value); }
    public bool TrapStep32 { get => GetTrapStep(31); set => SetTrapStep(31, value); }

    private bool GetTrapStep(int index) => (_model.Data[3] & (1 << index)) != 0;

    private void SetTrapStep(int index, bool value)
    {
        if (value)
            _model.Data[3] |= (1 << index);
        else
            _model.Data[3] &= ~(1 << index);
        OnPropertyChanged(nameof(TrapStepPatternText));
    }

    // Step visibility properties (show only up to TrapSteps count)
    public Visibility TrapStep1Visibility => TrapSteps >= 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep2Visibility => TrapSteps >= 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep3Visibility => TrapSteps >= 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep4Visibility => TrapSteps >= 4 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep5Visibility => TrapSteps >= 5 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep6Visibility => TrapSteps >= 6 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep7Visibility => TrapSteps >= 7 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep8Visibility => TrapSteps >= 8 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep9Visibility => TrapSteps >= 9 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep10Visibility => TrapSteps >= 10 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep11Visibility => TrapSteps >= 11 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep12Visibility => TrapSteps >= 12 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep13Visibility => TrapSteps >= 13 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep14Visibility => TrapSteps >= 14 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep15Visibility => TrapSteps >= 15 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep16Visibility => TrapSteps >= 16 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep17Visibility => TrapSteps >= 17 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep18Visibility => TrapSteps >= 18 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep19Visibility => TrapSteps >= 19 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep20Visibility => TrapSteps >= 20 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep21Visibility => TrapSteps >= 21 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep22Visibility => TrapSteps >= 22 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep23Visibility => TrapSteps >= 23 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep24Visibility => TrapSteps >= 24 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep25Visibility => TrapSteps >= 25 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep26Visibility => TrapSteps >= 26 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep27Visibility => TrapSteps >= 27 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep28Visibility => TrapSteps >= 28 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep29Visibility => TrapSteps >= 29 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep30Visibility => TrapSteps >= 30 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep31Visibility => TrapSteps >= 31 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TrapStep32Visibility => TrapSteps >= 32 ? Visibility.Visible : Visibility.Collapsed;

    private void NotifyTrapStepVisibilityChanged()
    {
        for (int i = 1; i <= 32; i++)
        {
            OnPropertyChanged($"TrapStep{i}Visibility");
        }
        OnPropertyChanged(nameof(TrapStepPatternText));
    }

    // ============================================================
    // CREATE TREASURE PROPERTIES (WPT_TREASURE / WaypointType.CreateTreasure)
    // ============================================================
    // Data layout from treasuresetup.cpp:
    // Data[0] = treasure_value (0-10000, point value)

    /// <summary>
    /// Treasure point value (Data[0], range 0-10000)
    /// </summary>
    public int TreasureValue
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Clamp(value, 0, 10000);
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Enemy-specific properties (WPT_CREATE_ENEMIES)
    // ============================================================
    // Data layout from EnemySetup.cpp:
    // Data[0]: LOWORD = enemy_type (1-based), HIWORD = enemy_count
    // Data[1]: enemy_follow (EP index to follow)
    // Data[2]: LOWORD = enemy_constitution (health), HIWORD = enemy_has (HAS_* flags)
    // Data[3]: enemy_move (movement type index)
    // Data[4]: enemy_flags (behavior flags bitmask)
    // Data[5]: LOWORD = enemy_ai (AI type), HIWORD = ability level
    // Data[6]: enemy_to_change (for WPT_ADJUST_ENEMY only)
    // Data[7]: LOWORD = enemy_guard (target EP), HIWORD = enemy_combat flags
    // Data[8]: enemy_weaps (weapon items bitmask)
    // Data[9]: enemy_items (other items bitmask)

    public ObservableCollection<string> EnemyTypeOptions { get; } = new(EditorStrings.EnemyTypeNames);
    public ObservableCollection<string> EnemyMoveOptions { get; } = new(EditorStrings.EnemyMoveNames);
    public ObservableCollection<string> EnemyAIOptions { get; } = new(EditorStrings.EnemyAINames);
    public ObservableCollection<string> EnemyAbilityOptions { get; } = new(EditorStrings.EnemyAbilityNames);

    // Enemy type (1-based in file, 0-based for ComboBox)
    public int EnemyType
    {
        get => (_model.Data[0] & 0xFFFF) - 1;  // LOWORD, convert to 0-based
        set
        {
            _model.Data[0] = (_model.Data[0] & unchecked((int)0xFFFF0000)) | ((value + 1) & 0xFFFF);
            OnPropertyChanged();
        }
    }

    // Enemy count (stored in HIWORD of Data[0])
    public int EnemyCount
    {
        get => (_model.Data[0] >> 16) & 0xFFFF;
        set
        {
            int count = Math.Max(1, Math.Min(99, value));
            _model.Data[0] = (_model.Data[0] & 0xFFFF) | (count << 16);
            OnPropertyChanged();
        }
    }

    // Follow target EP (Data[1])
    public int EnemyFollowTarget
    {
        get => _model.Data[1];
        set { _model.Data[1] = value; OnPropertyChanged(); }
    }

    // Constitution/Health (LOWORD of Data[2])
    public int EnemyConstitution
    {
        get => _model.Data[2] & 0xFFFF;
        set
        {
            int health = Math.Max(0, Math.Min(100, value));
            _model.Data[2] = (_model.Data[2] & unchecked((int)0xFFFF0000)) | (health & 0xFFFF);
            OnPropertyChanged();
        }
    }

    // HAS_* flags (HIWORD of Data[2])
    private int EnemyHasFlags
    {
        get => (_model.Data[2] >> 16) & 0xFFFF;
        set { _model.Data[2] = (_model.Data[2] & 0xFFFF) | (value << 16); }
    }

    public bool HasPistol
    {
        get => (EnemyHasFlags & (1 << 0)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 0) : EnemyHasFlags & ~(1 << 0); OnPropertyChanged(); }
    }
    public bool HasShotgun
    {
        get => (EnemyHasFlags & (1 << 1)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 1) : EnemyHasFlags & ~(1 << 1); OnPropertyChanged(); }
    }
    public bool HasAK47
    {
        get => (EnemyHasFlags & (1 << 2)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 2) : EnemyHasFlags & ~(1 << 2); OnPropertyChanged(); }
    }
    public bool HasGrenade
    {
        get => (EnemyHasFlags & (1 << 3)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 3) : EnemyHasFlags & ~(1 << 3); OnPropertyChanged(); }
    }
    public bool HasBalloon
    {
        get => (EnemyHasFlags & (1 << 4)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 4) : EnemyHasFlags & ~(1 << 4); OnPropertyChanged(); }
    }
    public bool HasKnife
    {
        get => (EnemyHasFlags & (1 << 5)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 5) : EnemyHasFlags & ~(1 << 5); OnPropertyChanged(); }
    }
    public bool HasBat
    {
        get => (EnemyHasFlags & (1 << 6)) != 0;
        set { EnemyHasFlags = value ? EnemyHasFlags | (1 << 6) : EnemyHasFlags & ~(1 << 6); OnPropertyChanged(); }
    }

    // Movement type (Data[3])
    public int EnemyMoveType
    {
        get => _model.Data[3];
        set { _model.Data[3] = value; OnPropertyChanged(); }
    }

    // Enemy flags (Data[4]) - behavior flags
    public int EnemyFlags
    {
        get => _model.Data[4];
        set { _model.Data[4] = value; OnPropertyChanged(); }
    }

    // Individual flag properties
    public bool FlagLazy { get => (EnemyFlags & (1 << 0)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 0) : EnemyFlags & ~(1 << 0); OnPropertyChanged(); } }
    public bool FlagDiligent { get => (EnemyFlags & (1 << 1)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 1) : EnemyFlags & ~(1 << 1); OnPropertyChanged(); } }
    public bool FlagGang { get => (EnemyFlags & (1 << 2)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 2) : EnemyFlags & ~(1 << 2); OnPropertyChanged(); } }
    public bool FlagFightBack { get => (EnemyFlags & (1 << 3)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 3) : EnemyFlags & ~(1 << 3); OnPropertyChanged(); } }
    public bool FlagJustKillPlayer { get => (EnemyFlags & (1 << 4)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 4) : EnemyFlags & ~(1 << 4); OnPropertyChanged(); } }
    public bool FlagRobotic { get => (EnemyFlags & (1 << 5)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 5) : EnemyFlags & ~(1 << 5); OnPropertyChanged(); } }
    public bool FlagRestricted { get => (EnemyFlags & (1 << 6)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 6) : EnemyFlags & ~(1 << 6); OnPropertyChanged(); } }
    public bool FlagOnlyPlayerKills { get => (EnemyFlags & (1 << 7)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 7) : EnemyFlags & ~(1 << 7); OnPropertyChanged(); } }
    public bool FlagBlueZone { get => (EnemyFlags & (1 << 8)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 8) : EnemyFlags & ~(1 << 8); OnPropertyChanged(); } }
    public bool FlagCyanZone { get => (EnemyFlags & (1 << 9)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 9) : EnemyFlags & ~(1 << 9); OnPropertyChanged(); } }
    public bool FlagYellowZone { get => (EnemyFlags & (1 << 10)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 10) : EnemyFlags & ~(1 << 10); OnPropertyChanged(); } }
    public bool FlagMagentaZone { get => (EnemyFlags & (1 << 11)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 11) : EnemyFlags & ~(1 << 11); OnPropertyChanged(); } }
    public bool FlagInvulnerable { get => (EnemyFlags & (1 << 12)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 12) : EnemyFlags & ~(1 << 12); OnPropertyChanged(); } }
    public bool FlagGuilty { get => (EnemyFlags & (1 << 13)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 13) : EnemyFlags & ~(1 << 13); OnPropertyChanged(); } }
    public bool FlagFakeWander { get => (EnemyFlags & (1 << 14)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 14) : EnemyFlags & ~(1 << 14); OnPropertyChanged(); } }
    public bool FlagCanCarry { get => (EnemyFlags & (1 << 15)) != 0; set { EnemyFlags = value ? EnemyFlags | (1 << 15) : EnemyFlags & ~(1 << 15); OnPropertyChanged(); } }

    // AI type (LOWORD of Data[5])
    public int EnemyAIType
    {
        get => _model.Data[5] & 0xFFFF;
        set { _model.Data[5] = (_model.Data[5] & unchecked((int)0xFFFF0000)) | (value & 0xFFFF); OnPropertyChanged(); }
    }

    // Ability level (HIWORD of Data[5])
    public int EnemyAbilityLevel
    {
        get => (_model.Data[5] >> 16) & 0xFFFF;
        set { _model.Data[5] = (_model.Data[5] & 0xFFFF) | (value << 16); OnPropertyChanged(); }
    }

    // Guard target EP (LOWORD of Data[7])
    public int EnemyGuardTarget
    {
        get => _model.Data[7] & 0xFFFF;
        set { _model.Data[7] = (_model.Data[7] & unchecked((int)0xFFFF0000)) | (value & 0xFFFF); OnPropertyChanged(); }
    }

    // Combat flags (HIWORD of Data[7])
    private int EnemyCombatFlags
    {
        get => (_model.Data[7] >> 16) & 0xFFFF;
        set { _model.Data[7] = (_model.Data[7] & 0xFFFF) | (value << 16); }
    }

    public bool CombatSlide { get => (EnemyCombatFlags & (1 << 0)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 0) : EnemyCombatFlags & ~(1 << 0); OnPropertyChanged(); } }
    public bool CombatComboPPP { get => (EnemyCombatFlags & (1 << 1)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 1) : EnemyCombatFlags & ~(1 << 1); OnPropertyChanged(); } }
    public bool CombatComboKKK { get => (EnemyCombatFlags & (1 << 2)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 2) : EnemyCombatFlags & ~(1 << 2); OnPropertyChanged(); } }
    public bool CombatComboAny { get => (EnemyCombatFlags & (1 << 3)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 3) : EnemyCombatFlags & ~(1 << 3); OnPropertyChanged(); } }
    public bool CombatGrapple { get => (EnemyCombatFlags & (1 << 4)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 4) : EnemyCombatFlags & ~(1 << 4); OnPropertyChanged(); } }
    public bool CombatSideKick { get => (EnemyCombatFlags & (1 << 5)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 5) : EnemyCombatFlags & ~(1 << 5); OnPropertyChanged(); } }
    public bool CombatBackKick { get => (EnemyCombatFlags & (1 << 6)) != 0; set { EnemyCombatFlags = value ? EnemyCombatFlags | (1 << 6) : EnemyCombatFlags & ~(1 << 6); OnPropertyChanged(); } }

    // ============================================================
    // Item-specific properties (WPT_CREATE_ITEM)
    // ============================================================
    // Data layout from ItemSetup.cpp:
    // Data[0]: item_type (1-based index into witem_strings)
    // Data[1]: item_count (number of items, 1-99)
    // Data[2]: item_flags (bitmask: bit 0 = follows person, bit 1 = hidden in prim)

    public ObservableCollection<string> ItemTypeOptions { get; } = new(EditorStrings.ItemTypeNames);

    // Item type (1-based in file, 0-based for ComboBox)
    public int ItemType
    {
        get => _model.Data[0] - 1;  // Convert to 0-based
        set
        {
            _model.Data[0] = value + 1;  // Store as 1-based
            OnPropertyChanged();
        }
    }

    // Item count (Data[1])
    public int ItemCount
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Max(1, Math.Min(99, value));
            OnPropertyChanged();
        }
    }

    // Item flags (Data[2])
    private int ItemFlags
    {
        get => _model.Data[2];
        set { _model.Data[2] = value; }
    }

    public bool ItemFlagFollowsPerson
    {
        get => (ItemFlags & (1 << 0)) != 0;
        set { ItemFlags = value ? ItemFlags | (1 << 0) : ItemFlags & ~(1 << 0); OnPropertyChanged(); }
    }

    public bool ItemFlagHiddenInPrim
    {
        get => (ItemFlags & (1 << 1)) != 0;
        set { ItemFlags = value ? ItemFlags | (1 << 1) : ItemFlags & ~(1 << 1); OnPropertyChanged(); }
    }

    // ============================================================
    // Barrel-specific properties (WPT_CREATE_BARREL)
    // ============================================================
    // Data layout from barrelSetup.cpp:
    // Data[0]: barrel_type (0-based index into wbarrel_type_strings)

    public ObservableCollection<string> BarrelTypeOptions { get; } = new(EditorStrings.BarrelTypeNames);

    // Barrel type (0-based)
    public int BarrelType
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = value;
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Creature-specific properties (WPT_CREATE_CREATURE)
    // ============================================================
    // Data layout from CreatureSetup.cpp:
    // Data[0]: creature_type (1-based index into wcreature_strings)
    // Data[1]: creature_count (number of creatures, 1-99)

    public ObservableCollection<string> CreatureTypeOptions { get; } = new(EditorStrings.CreatureTypeNames);

    // Creature type (1-based in file, 0-based for ComboBox)
    public int CreatureType
    {
        get => _model.Data[0] - 1;  // Convert to 0-based
        set
        {
            _model.Data[0] = value + 1;  // Store as 1-based
            OnPropertyChanged();
        }
    }

    // Creature count (Data[1])
    public int CreatureCount
    {
        get => _model.Data[1] > 0 ? _model.Data[1] : 1;  // Default to 1 if 0
        set
        {
            _model.Data[1] = Math.Max(1, Math.Min(99, value));
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Vehicle-specific properties (WPT_CREATE_VEHICLE)
    // ============================================================
    // Data layout from VehicleSetup.cpp:
    // Data[0]: veh_type (1-based index into wvehicle_strings)
    // Data[1]: veh_move (0-based index into wvehicle_behaviour_strings)
    // Data[2]: veh_targ (EP index for tracking, only used when veh_move == 3)
    // Data[3]: veh_key (0-based index into wvehicle_key_strings)

    public ObservableCollection<string> VehicleTypeOptions { get; } = new(EditorStrings.VehicleTypeNames);
    public ObservableCollection<string> VehicleBehaviorOptions { get; } = new(EditorStrings.VehicleBehaviorNames);
    public ObservableCollection<string> VehicleKeyOptions { get; } = new(EditorStrings.VehicleKeyNames);

    // Vehicle type (1-based in file, 0-based for ComboBox)
    public int VehicleType
    {
        get => _model.Data[0] - 1;  // Convert to 0-based
        set
        {
            _model.Data[0] = value + 1;  // Store as 1-based
            OnPropertyChanged();
        }
    }

    // Vehicle behavior (0-based)
    public int VehicleBehavior
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVehicleTargetEnabled));
        }
    }

    // Vehicle target EP (Data[2]) - only used when behavior == 3 (Track Target)
    public int VehicleTarget
    {
        get => _model.Data[2];
        set
        {
            _model.Data[2] = value;
            OnPropertyChanged();
        }
    }

    // Vehicle key requirement (0-based)
    public int VehicleKey
    {
        get => _model.Data[3];
        set
        {
            _model.Data[3] = value;
            OnPropertyChanged();
        }
    }

    // Is target field enabled? Only when behavior == 3 (Track Target)
    public bool IsVehicleTargetEnabled => VehicleBehavior == 3;

    // ============================================================
    // Visual Effect properties (WPT_VISUAL_EFFECT)
    // ============================================================
    // Data layout from VfxSetup.cpp:
    // Data[0]: vfx_types (bitmask of effect types)
    // Data[1]: vfx_scale (0-1024)

    // Effect types bitmask (Data[0])
    private int VfxTypes
    {
        get => _model.Data[0];
        set { _model.Data[0] = value; }
    }

    // Individual effect type flags
    public bool VfxFlare
    {
        get => (VfxTypes & (1 << 0)) != 0;
        set { VfxTypes = value ? VfxTypes | (1 << 0) : VfxTypes & ~(1 << 0); OnPropertyChanged(); }
    }

    public bool VfxFireDome
    {
        get => (VfxTypes & (1 << 1)) != 0;
        set { VfxTypes = value ? VfxTypes | (1 << 1) : VfxTypes & ~(1 << 1); OnPropertyChanged(); }
    }

    public bool VfxShockwave
    {
        get => (VfxTypes & (1 << 2)) != 0;
        set { VfxTypes = value ? VfxTypes | (1 << 2) : VfxTypes & ~(1 << 2); OnPropertyChanged(); }
    }

    public bool VfxSmokeTrails
    {
        get => (VfxTypes & (1 << 3)) != 0;
        set { VfxTypes = value ? VfxTypes | (1 << 3) : VfxTypes & ~(1 << 3); OnPropertyChanged(); }
    }

    public bool VfxBonfire
    {
        get => (VfxTypes & (1 << 4)) != 0;
        set { VfxTypes = value ? VfxTypes | (1 << 4) : VfxTypes & ~(1 << 4); OnPropertyChanged(); }
    }

    // Effect scale (Data[1])
    public int VfxScale
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Max(0, Math.Min(1024, value));
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Adjust Enemy properties (WPT_ADJUST_ENEMY)
    // ============================================================
    // Uses same data layout as Create Enemies, plus:
    // Data[6]: enemy_to_change (EP index of enemy to adjust)
    // Note: Enemy properties (type, count, constitution, movement, AI, flags, etc.)
    // are already defined in the Enemy-specific properties section above.

    // Target enemy EP to adjust (Data[6])
    public int AdjustEnemyTarget
    {
        get => _model.Data[6];
        set
        {
            _model.Data[6] = value;
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Move Thing properties (WPT_MOVE_THING)
    // ============================================================
    // Data layout from MoveSetup.cpp:
    // Data[0]: which_waypoint (target EP index to move, 1-2048)

    // Target waypoint to move (Data[0])
    public int MoveThingTarget
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Max(1, Math.Min(2048, value));
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Create Target properties (WPT_CREATE_TARGET)
    // ============================================================
    // Data layout from CamTargetSetup.cpp:
    // Data[0]: target_move (1-based index into wcammove_strings)
    // Data[1]: target_type (1-based index into wcamtarg_strings)
    // Data[2]: target_speed
    // Data[3]: target_delay
    // Data[4]: camera_zoom
    // Data[5]: camera_rotate (boolean)

    public ObservableCollection<string> TargetMoveOptions { get; } = new(EditorStrings.CameraMoveNames);
    public ObservableCollection<string> CamTargetTypeOptions { get; } = new(EditorStrings.TargetTypeNames);

    // Target move type (1-based in file, 0-based for ComboBox)
    public int TargetMoveType
    {
        get => _model.Data[0] - 1;
        set
        {
            _model.Data[0] = value + 1;
            OnPropertyChanged();
        }
    }

    // Target type (1-based in file, 0-based for ComboBox)
    public int CamTargetType
    {
        get => _model.Data[1] - 1;
        set
        {
            _model.Data[1] = value + 1;
            OnPropertyChanged();
        }
    }

    // Target speed (Data[2])
    public int TargetSpeed
    {
        get => _model.Data[2];
        set
        {
            _model.Data[2] = value;
            OnPropertyChanged();
        }
    }

    // Target delay (Data[3])
    public int TargetDelay
    {
        get => _model.Data[3];
        set
        {
            _model.Data[3] = value;
            OnPropertyChanged();
        }
    }

    // Camera zoom (Data[4])
    public int TargetCameraZoom
    {
        get => _model.Data[4];
        set
        {
            _model.Data[4] = value;
            OnPropertyChanged();
        }
    }

    // Camera rotate flag (Data[5])
    public bool TargetCameraRotate
    {
        get => _model.Data[5] != 0;
        set
        {
            _model.Data[5] = value ? 1 : 0;
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Simple Waypoint properties (WPT_SIMPLE)
    // ============================================================
    // Data layout from waypointSetup.cpp:
    // Data[0]: waypoint_delay (0-10000, in tenths of a second)

    // Waypoint delay in tenths of a second (Data[0])
    public int SimpleWaypointDelay
    {
        get => _model.Data[0];
        set
        {
            _model.Data[0] = Math.Max(0, Math.Min(10000, value));
            OnPropertyChanged();
            OnPropertyChanged(nameof(SimpleWaypointDelaySeconds));
        }
    }

    // Delay converted to seconds for display
    public double SimpleWaypointDelaySeconds => _model.Data[0] / 10.0;

    // ============================================================
    // Conversation properties (WPT_CONVERSATION)
    // ============================================================
    // Data layout from converse.cpp:
    // Data[0]: pointer to text (stored as ExtraText in file)
    // Data[1]: converse_p1 (EP index of first participant)
    // Data[2]: converse_p2 (EP index of second participant)
    // Data[3]: converse_grab_camera (boolean)

    // Person 1 EP index (Data[1])
    public int ConversePerson1
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = value;
            OnPropertyChanged();
        }
    }

    // Person 2 EP index (Data[2])
    public int ConversePerson2
    {
        get => _model.Data[2];
        set
        {
            _model.Data[2] = value;
            OnPropertyChanged();
        }
    }

    // Grab camera flag (Data[3])
    public bool ConverseGrabCamera
    {
        get => _model.Data[3] != 0;
        set
        {
            _model.Data[3] = value ? 1 : 0;
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Message properties (WPT_MESSAGE)
    // ============================================================
    // Data layout from MessageSetup.cpp:
    // Data[0]: pointer to text (stored as ExtraText in file)
    // Data[1]: message_time (duration in seconds, defaults to 4)
    // Data[2]: message_who - who speaks:
    //          0 = Radio
    //          0xFFFF = Place/Street-name
    //          0xFFFE = Tutorial help message
    //          Other = EP index of speaker (player/enemy)

    // Special speaker values
    private const int SPEAKER_RADIO = 0;
    private const int SPEAKER_PLACE_NAME = 0xFFFF;
    private const int SPEAKER_TUTORIAL = 0xFFFE;

    public ObservableCollection<string> MessageSpeakerOptions { get; } = new()
    {
        "Radio",
        "Place/Street Name",
        "Tutorial Help",
        "Character (EP)"
    };

    // Speaker type index for ComboBox (0-3)
    public int MessageSpeakerIndex
    {
        get
        {
            return _model.Data[2] switch
            {
                SPEAKER_RADIO => 0,
                SPEAKER_PLACE_NAME => 1,
                SPEAKER_TUTORIAL => 2,
                _ => 3  // Character EP
            };
        }
        set
        {
            int oldValue = _model.Data[2];
            _model.Data[2] = value switch
            {
                0 => SPEAKER_RADIO,
                1 => SPEAKER_PLACE_NAME,
                2 => SPEAKER_TUTORIAL,
                _ => (oldValue != SPEAKER_RADIO && oldValue != SPEAKER_PLACE_NAME && oldValue != SPEAKER_TUTORIAL)
                     ? oldValue : 1  // Keep existing EP or default to 1
            };
            OnPropertyChanged();
            OnPropertyChanged(nameof(MessageSpeakerEPVisibility));
            OnPropertyChanged(nameof(MessageSpeakerEP));
        }
    }

    // EP index when speaker is a character
    public int MessageSpeakerEP
    {
        get => (_model.Data[2] != SPEAKER_RADIO && _model.Data[2] != SPEAKER_PLACE_NAME && _model.Data[2] != SPEAKER_TUTORIAL)
               ? _model.Data[2] : 1;
        set
        {
            if (MessageSpeakerIndex == 3)  // Only set when "Character EP" is selected
            {
                _model.Data[2] = Math.Max(1, value);
                OnPropertyChanged();
            }
        }
    }

    // Visibility of EP field (only visible when "Character EP" is selected)
    public Visibility MessageSpeakerEPVisibility => MessageSpeakerIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

    // Message display time in seconds (Data[1])
    public int MessageTime
    {
        get => _model.Data[1] > 0 ? _model.Data[1] : 4;  // Default to 4 seconds
        set
        {
            _model.Data[1] = Math.Max(1, value);
            OnPropertyChanged();
        }
    }

    // ============================================================
    // Bonus Points properties (WPT_BONUS_POINTS)
    // ============================================================
    // Data layout from BonusSetup.cpp:
    // Data[0]: pointer to text (stored as ExtraText in file)
    // Data[1]: bonus_pts (points value, 0-1000)
    // Data[2]: bonus_type (0=Primary, 1=Secondary, 2=Bonus)
    // Data[3]: bonus_gender (0=Male, 1=Female - for translation)

    public ObservableCollection<string> BonusTypeOptions { get; } = new(EditorStrings.BonusPointTypeNames);

    // Bonus type (Data[2])
    public int BonusType
    {
        get => _model.Data[2];
        set
        {
            _model.Data[2] = Math.Max(0, Math.Min(2, value));
            OnPropertyChanged();
        }
    }

    // Bonus points (Data[1])
    public int BonusPoints
    {
        get => _model.Data[1];
        set
        {
            _model.Data[1] = Math.Max(0, Math.Min(1000, value));
            OnPropertyChanged();
        }
    }

    // Gender for translation - Male (Data[3] == 0)
    public bool BonusGenderMale
    {
        get => _model.Data[3] == 0;
        set
        {
            if (value)
            {
                _model.Data[3] = 0;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BonusGenderFemale));
            }
        }
    }

    // Gender for translation - Female (Data[3] == 1)
    public bool BonusGenderFemale
    {
        get => _model.Data[3] != 0;
        set
        {
            if (value)
            {
                _model.Data[3] = 1;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BonusGenderMale));
            }
        }
    }

    // Revert to original state (for cancel)
    public void RevertChanges()
    {
        CopyEventPoint(_originalState, _model);
    }

    // Clone helper
    private static EventPoint CloneEventPoint(EventPoint source)
    {
        var clone = new EventPoint
        {
            Index = source.Index,
            Colour = source.Colour,
            Group = source.Group,
            WaypointType = source.WaypointType,
            Used = source.Used,
            TriggeredBy = source.TriggeredBy,
            OnTrigger = source.OnTrigger,
            Direction = source.Direction,
            Flags = source.Flags,
            EPRef = source.EPRef,
            EPRefBool = source.EPRefBool,
            AfterTimer = source.AfterTimer,
            Radius = source.Radius,
            X = source.X,
            Y = source.Y,
            Z = source.Z,
            Next = source.Next,
            Prev = source.Prev,
            ExtraText = source.ExtraText,
            TriggerText = source.TriggerText
        };
        Array.Copy(source.Data, clone.Data, 10);
        return clone;
    }

    private static void CopyEventPoint(EventPoint source, EventPoint target)
    {
        target.Colour = source.Colour;
        target.Group = source.Group;
        target.WaypointType = source.WaypointType;
        target.Used = source.Used;
        target.TriggeredBy = source.TriggeredBy;
        target.OnTrigger = source.OnTrigger;
        target.Direction = source.Direction;
        target.Flags = source.Flags;
        target.EPRef = source.EPRef;
        target.EPRefBool = source.EPRefBool;
        target.AfterTimer = source.AfterTimer;
        target.Radius = source.Radius;
        target.X = source.X;
        target.Y = source.Y;
        target.Z = source.Z;
        target.Next = source.Next;
        target.Prev = source.Prev;
        target.ExtraText = source.ExtraText;
        target.TriggerText = source.TriggerText;
        Array.Copy(source.Data, target.Data, 10);
    }
}

// Helper classes for ComboBox binding
public class TriggerTypeOption
{
    public TriggerType Value { get; }
    public string DisplayName { get; }

    public TriggerTypeOption(TriggerType value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}

public class OnTriggerOption
{
    public OnTriggerBehavior Value { get; }
    public string DisplayName { get; }

    public OnTriggerOption(OnTriggerBehavior value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;

}

/// <summary>
/// ViewModel for a cutscene channel in the tree view
/// </summary>
public class CutsceneChannelViewModel : BaseViewModel
{
    public CutsceneChannel Model { get; }
    public ObservableCollection<CutscenePacketViewModel> Packets { get; } = new();

    public CutsceneChannelViewModel(CutsceneChannel model)
    {
        Model = model;
        foreach (var packet in model.Packets)
        {
            Packets.Add(new CutscenePacketViewModel(packet));
        }
    }

    public string DisplayName => Model.DisplayName;

    public string ChannelIcon => Model.Type switch
    {
        CutsceneChannelType.Character => "👤",
        CutsceneChannelType.Camera => "🎥",
        CutsceneChannelType.Sound => "🔊",
        CutsceneChannelType.Subtitles => "💬",
        CutsceneChannelType.VisualFX => "✨",
        _ => "?"
    };

    public string PacketCountText => $"({Packets.Count} packets)";

    public CutsceneChannelType Type
    {
        get => Model.Type;
        set { Model.Type = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public ushort Index
    {
        get => Model.Index;
        set { Model.Index = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    /// <summary>
    /// Add a new packet to this channel
    /// </summary>
    public CutscenePacketViewModel AddPacket()
    {
        var packet = new CutscenePacket
        {
            Type = Model.Type switch
            {
                CutsceneChannelType.Character => CutscenePacketType.Animation,
                CutsceneChannelType.Camera => CutscenePacketType.Camera,
                CutsceneChannelType.Sound => CutscenePacketType.Sound,
                CutsceneChannelType.Subtitles => CutscenePacketType.Text,
                _ => CutscenePacketType.Unused
            },
            Start = 0,
            Length = 30
        };

        Model.Packets.Add(packet);
        var vm = new CutscenePacketViewModel(packet);
        Packets.Add(vm);
        OnPropertyChanged(nameof(PacketCountText));
        return vm;
    }
}

/// <summary>
/// ViewModel for a cutscene packet in the tree view
/// </summary>
public class CutscenePacketViewModel : BaseViewModel
{
    public CutscenePacket Model { get; }

    public CutscenePacketViewModel(CutscenePacket model)
    {
        Model = model;
    }

    public string DisplayName => Model.Type switch
    {
        CutscenePacketType.Animation => $"Anim #{Model.Index}",
        CutscenePacketType.Action => "Action",
        CutscenePacketType.Sound => $"Sound #{Model.Index}",
        CutscenePacketType.Camera => "Camera Keyframe",
        CutscenePacketType.Text => string.IsNullOrEmpty(Model.Text) ? "Subtitle" : $"\"{Model.Text.Substring(0, Math.Min(20, Model.Text.Length))}...\"",
        _ => "Unknown"
    };

    public string PacketIcon => Model.Type switch
    {
        CutscenePacketType.Animation => "🏃",
        CutscenePacketType.Action => "⚡",
        CutscenePacketType.Sound => "🔊",
        CutscenePacketType.Camera => "📍",
        CutscenePacketType.Text => "📝",
        _ => "?"
    };

    public string TimingText => $"@{Model.Start} ({Model.Length}f)";

    // Packet properties
    public CutscenePacketType Type
    {
        get => Model.Type;
        set { Model.Type = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public ushort Index
    {
        get => Model.Index;
        set { Model.Index = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public ushort Start
    {
        get => Model.Start;
        set { Model.Start = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimingText)); }
    }

    public ushort Length
    {
        get => Model.Length;
        set { Model.Length = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimingText)); }
    }

    public int PosX
    {
        get => Model.PosX;
        set { Model.PosX = value; OnPropertyChanged(); }
    }

    public int PosY
    {
        get => Model.PosY;
        set { Model.PosY = value; OnPropertyChanged(); }
    }

    public int PosZ
    {
        get => Model.PosZ;
        set { Model.PosZ = value; OnPropertyChanged(); }
    }

    public ushort Angle
    {
        get => Model.Angle;
        set { Model.Angle = value; OnPropertyChanged(); }
    }

    public ushort Pitch
    {
        get => Model.Pitch;
        set { Model.Pitch = value; OnPropertyChanged(); }
    }

    public string? Text
    {
        get => Model.Text;
        set { Model.Text = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public PacketFlags Flags
    {
        get => Model.Flags;
        set { Model.Flags = value; OnPropertyChanged(); }
    }

    // Flag helpers
    public bool InterpolatePosition
    {
        get => Model.InterpolatePosition;
        set { Model.InterpolatePosition = value; OnPropertyChanged(); }
    }

    public bool InterpolateRotation
    {
        get => Model.InterpolateRotation;
        set { Model.InterpolateRotation = value; OnPropertyChanged(); }
    }
}

/// <summary>
/// Property panel for editing channel properties
/// </summary>
public class CutsceneChannelPropertiesPanel : StackPanel
{
    public CutsceneChannelPropertiesPanel(CutsceneChannelViewModel channel)
    {
        Margin = new Thickness(0);

        // Header
        Children.Add(new TextBlock
        {
            Text = "Channel Properties",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Type
        AddPropertyRow("Type:", channel.Type.ToString());

        // Index (for characters)
        if (channel.Type == CutsceneChannelType.Character)
        {
            AddPropertyRow("Person Type:", channel.DisplayName);
        }

        // Packet count
        AddPropertyRow("Packets:", channel.Packets.Count.ToString());

        // Add Packet button
        var addButton = new Button
        {
            Content = "Add Packet",
            Margin = new Thickness(0, 15, 0, 0),
            Padding = new Thickness(10, 5, 10, 5)
        };
        addButton.Click += (s, e) => channel.AddPacket();
        Children.Add(addButton);
    }

    private void AddPropertyRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
        };
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        Children.Add(grid);
    }
}

/// <summary>
/// Property panel for editing packet properties
/// </summary>
public class CutscenePacketPropertiesPanel : StackPanel
{
    private readonly CutscenePacketViewModel _packet;

    public CutscenePacketPropertiesPanel(CutscenePacketViewModel packet)
    {
        _packet = packet;
        Margin = new Thickness(0);
        DataContext = packet;

        // Header
        Children.Add(new TextBlock
        {
            Text = "Packet Properties",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Type
        AddReadOnlyRow("Type:", packet.Type.ToString());

        // Timing
        AddEditableRow("Start:", nameof(packet.Start));
        AddEditableRow("Length:", nameof(packet.Length));

        // Position
        Children.Add(new TextBlock
        {
            Text = "Position",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 10, 0, 5)
        });
        AddEditableRow("X:", nameof(packet.PosX));
        AddEditableRow("Y:", nameof(packet.PosY));
        AddEditableRow("Z:", nameof(packet.PosZ));

        // Rotation
        Children.Add(new TextBlock
        {
            Text = "Rotation",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 10, 0, 5)
        });
        AddEditableRow("Angle:", nameof(packet.Angle));
        AddEditableRow("Pitch:", nameof(packet.Pitch));

        // Index (for animations/sounds)
        if (packet.Type == CutscenePacketType.Animation || packet.Type == CutscenePacketType.Sound)
        {
            AddEditableRow("Index:", nameof(packet.Index));
        }

        // Text (for subtitles)
        if (packet.Type == CutscenePacketType.Text)
        {
            Children.Add(new TextBlock
            {
                Text = "Subtitle Text",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Margin = new Thickness(0, 10, 0, 5)
            });

            var textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 60,
                Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55))
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(packet.Text))
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            Children.Add(textBox);
        }

        // Interpolation flags
        Children.Add(new TextBlock
        {
            Text = "Interpolation",
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 10, 0, 5)
        });
        AddCheckboxRow("Interpolate Position", nameof(packet.InterpolatePosition));
        AddCheckboxRow("Interpolate Rotation", nameof(packet.InterpolateRotation));
    }

    private void AddReadOnlyRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
        };
        Grid.SetColumn(valueBlock, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        Children.Add(grid);
    }

    private void AddEditableRow(string label, string bindingPath)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(labelBlock, 0);

        var textBox = new TextBox
        {
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            Padding = new Thickness(4, 2, 4, 2)
        };
        textBox.SetBinding(TextBox.TextProperty, new Binding(bindingPath)
        {
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        Grid.SetColumn(textBox, 1);

        grid.Children.Add(labelBlock);
        grid.Children.Add(textBox);
        Children.Add(grid);
    }

    private void AddCheckboxRow(string label, string bindingPath)
    {
        var checkBox = new CheckBox
        {
            Content = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 2, 0, 2)
        };
        checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(bindingPath));
        Children.Add(checkBox);
    }
}


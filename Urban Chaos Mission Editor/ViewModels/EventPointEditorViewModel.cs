using System.Collections.ObjectModel;
using System.Windows;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;

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
            WaypointType.CreatePlayer => new Views.Editors.PlayerEditorControl { DataContext = this },
            WaypointType.CreateEnemies => new Views.Editors.EnemyEditorControl { DataContext = this },
            WaypointType.CreateCamera => new Views.Editors.CameraEditorControl { DataContext = this },
            WaypointType.CameraWaypoint => new Views.Editors.CameraWaypointEditorControl { DataContext = this },
            // Add more type-specific editors here as needed
            _ => CreateGenericDataEditor()
        };
    }

    private FrameworkElement CreateGenericDataEditor()
    {
        // Generic editor showing raw Data array
        return new Views.Editors.GenericDataEditorControl { DataContext = this };
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
using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// ViewModel wrapper for EventPoint display in the list and map
/// </summary>
public class EventPointViewModel : BaseViewModel
{
    private readonly EventPoint _model;
    private bool _isSelected;
    private bool _isVisible = true;
    private bool _isHovered;

    public EventPointViewModel(EventPoint model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// The underlying EventPoint model
    /// </summary>
    public EventPoint Model => _model;

    /// <summary>
    /// Index in the EventPoints array
    /// </summary>
    public int Index => _model.Index;

    /// <summary>
    /// Whether this EventPoint is currently selected
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Whether this EventPoint is visible (based on filters)
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    /// <summary>
    /// Whether the mouse is hovering over this EventPoint
    /// </summary>
    public bool IsHovered
    {
        get => _isHovered;
        set => SetProperty(ref _isHovered, value);
    }

    // Delegate properties to model

    public string DisplayName => _model.DisplayName;
    public string Summary => _model.GetSummary();
    public WaypointType WaypointType => _model.WaypointType;
    public WaypointCategory Category => _model.Category;
    public char GroupLetter => _model.GroupLetter;
    public byte ColorIndex => _model.Colour;
    public bool IsValid => _model.IsValid;
    public bool Used => _model.Used;

    // Map display properties

    public double PixelX => _model.PixelX;
    public double PixelZ => _model.PixelZ;
    public int MapX => _model.MapX;
    public int MapZ => _model.MapZ;
    public int WorldX => _model.X;
    public int WorldY => _model.Y;
    public int WorldZ => _model.Z;

    // Color properties for display

    public Color PointColor => WaypointColors.GetColor(_model.Colour);
    public SolidColorBrush PointBrush => WaypointColors.GetBrush(_model.Colour);
    public Color CategoryColor => WaypointColors.GetCategoryColor(_model.Category);
    public SolidColorBrush CategoryBrush => WaypointColors.GetCategoryBrush(_model.Category);

    // String representations

    public string WaypointTypeName => EditorStrings.GetWaypointTypeName(_model.WaypointType);
    public string TriggerTypeName => EditorStrings.GetTriggerTypeName(_model.TriggeredBy);
    public string ColorName => WaypointColors.GetColorName(_model.Colour);
    public string CategoryName => _model.Category.ToString();

    // Position display string
    public string PositionString => $"({MapX}, {MapZ})";
    public string WorldPositionString => $"({WorldX}, {WorldY}, {WorldZ})";

    // Trigger information
    public TriggerType TriggeredBy => _model.TriggeredBy;
    public OnTriggerBehavior OnTrigger => _model.OnTrigger;
    public string OnTriggerName => EditorStrings.OnTriggerNames[(int)_model.OnTrigger];

    // References
    public ushort EPRef => _model.EPRef;
    public ushort EPRefBool => _model.EPRefBool;
    public int Radius => _model.Radius;

    // Extra text
    public string? ExtraText => _model.ExtraText;
    public string? TriggerText => _model.TriggerText;

    // Flags
    public WaypointFlags Flags => _model.Flags;
    public bool HasSucksFlag => _model.Flags.HasFlag(WaypointFlags.Sucks);
    public bool HasInverseFlag => _model.Flags.HasFlag(WaypointFlags.Inverse);
    public bool HasInsideFlag => _model.Flags.HasFlag(WaypointFlags.Inside);

    // Direction
    public byte Direction => _model.Direction;
    public double DirectionDegrees => _model.DirectionDegrees;

    // Data array access
    public int[] Data => _model.Data;
    public int Data0 => _model.Data[0];
    public int Data1 => _model.Data[1];
    public int Data2 => _model.Data[2];
    public int Data3 => _model.Data[3];
    public int Data4 => _model.Data[4];
    public int Data5 => _model.Data[5];
    public int Data6 => _model.Data[6];
    public int Data7 => _model.Data[7];
    public int Data8 => _model.Data[8];
    public int Data9 => _model.Data[9];

    // ============================================================
    // Item-specific properties (WPT_CREATE_ITEM)
    // ============================================================
    // Data[0]: item_type (1-based)
    // Data[1]: item_count
    // Data[2]: item_flags

    /// <summary>Item type index (0-based for display)</summary>
    public int ItemType => _model.Data[0] - 1;

    /// <summary>Item type name</summary>
    public string ItemTypeName =>
        (_model.Data[0] >= 1 && _model.Data[0] <= EditorStrings.ItemTypeNames.Length)
            ? EditorStrings.ItemTypeNames[_model.Data[0] - 1]
            : $"Item {_model.Data[0]}";

    /// <summary>Number of items</summary>
    public int ItemCount => _model.Data[1];

    /// <summary>Item flags bitmask</summary>
    public int ItemFlags => _model.Data[2];

    /// <summary>Item follows person flag</summary>
    public bool ItemFlagFollowsPerson => (_model.Data[2] & (1 << 0)) != 0;

    /// <summary>Item hidden in prim flag</summary>
    public bool ItemFlagHiddenInPrim => (_model.Data[2] & (1 << 1)) != 0;

    // ============================================================
    // Barrel-specific properties (WPT_CREATE_BARREL)
    // ============================================================
    // Data[0]: barrel_type (0-based)

    /// <summary>Barrel type index (0-based)</summary>
    public int BarrelType => _model.Data[0];

    /// <summary>Barrel type name</summary>
    public string BarrelTypeName =>
        (_model.Data[0] >= 0 && _model.Data[0] < EditorStrings.BarrelTypeNames.Length)
            ? EditorStrings.BarrelTypeNames[_model.Data[0]]
            : $"Barrel {_model.Data[0]}";

    // ============================================================
    // Creature-specific properties (WPT_CREATE_CREATURE)
    // ============================================================
    // Data[0]: creature_type (1-based)
    // Data[1]: creature_count

    /// <summary>Creature type index (0-based for display)</summary>
    public int CreatureType => _model.Data[0] - 1;

    /// <summary>Creature type name</summary>
    public string CreatureTypeName =>
        (_model.Data[0] >= 1 && _model.Data[0] <= EditorStrings.CreatureTypeNames.Length)
            ? EditorStrings.CreatureTypeNames[_model.Data[0] - 1]
            : $"Creature {_model.Data[0]}";

    /// <summary>Number of creatures to spawn</summary>
    public int CreatureCount => _model.Data[1] > 0 ? _model.Data[1] : 1;

    // ============================================================
    // Vehicle-specific properties (WPT_CREATE_VEHICLE)
    // ============================================================
    // Data[0]: veh_type (1-based)
    // Data[1]: veh_move (behaviour, 0-based)
    // Data[2]: veh_targ (target EP index)
    // Data[3]: veh_key (key requirement, 0-based)

    /// <summary>Vehicle type index (0-based for display)</summary>
    public int VehicleType => _model.Data[0] - 1;

    /// <summary>Vehicle type name</summary>
    public string VehicleTypeName =>
        (_model.Data[0] >= 1 && _model.Data[0] <= EditorStrings.VehicleTypeNames.Length)
            ? EditorStrings.VehicleTypeNames[_model.Data[0] - 1]
            : $"Vehicle {_model.Data[0]}";

    /// <summary>Vehicle behavior index (0-based)</summary>
    public int VehicleBehavior => _model.Data[1];

    /// <summary>Vehicle behavior name</summary>
    public string VehicleBehaviorName =>
        (_model.Data[1] >= 0 && _model.Data[1] < EditorStrings.VehicleBehaviorNames.Length)
            ? EditorStrings.VehicleBehaviorNames[_model.Data[1]]
            : $"Behavior {_model.Data[1]}";

    /// <summary>Vehicle target EventPoint index</summary>
    public int VehicleTarget => _model.Data[2];

    /// <summary>Vehicle key requirement index (0-based)</summary>
    public int VehicleKey => _model.Data[3];

    /// <summary>Vehicle key requirement name</summary>
    public string VehicleKeyName =>
        (_model.Data[3] >= 0 && _model.Data[3] < EditorStrings.VehicleKeyNames.Length)
            ? EditorStrings.VehicleKeyNames[_model.Data[3]]
            : $"Key {_model.Data[3]}";

    // ============================================================
    // Visual Effect properties (WPT_VISUAL_EFFECT)
    // ============================================================
    // Data[0]: vfx_types (bitmask)
    // Data[1]: vfx_scale (0-1024)

    /// <summary>Visual effect types bitmask</summary>
    public int VfxTypes => _model.Data[0];

    /// <summary>Visual effect scale</summary>
    public int VfxScale => _model.Data[1];

    /// <summary>Has Flare effect</summary>
    public bool VfxFlare => (_model.Data[0] & (1 << 0)) != 0;

    /// <summary>Has Fire Dome effect</summary>
    public bool VfxFireDome => (_model.Data[0] & (1 << 1)) != 0;

    /// <summary>Has Shockwave effect</summary>
    public bool VfxShockwave => (_model.Data[0] & (1 << 2)) != 0;

    /// <summary>Has Smoke Trails effect</summary>
    public bool VfxSmokeTrails => (_model.Data[0] & (1 << 3)) != 0;

    /// <summary>Has Bonfire effect</summary>
    public bool VfxBonfire => (_model.Data[0] & (1 << 4)) != 0;

    /// <summary>Summary of selected visual effects</summary>
    public string VfxSummary
    {
        get
        {
            var effects = new List<string>();
            if (VfxFlare) effects.Add("Flare");
            if (VfxFireDome) effects.Add("Fire Dome");
            if (VfxShockwave) effects.Add("Shockwave");
            if (VfxSmokeTrails) effects.Add("Smoke Trails");
            if (VfxBonfire) effects.Add("Bonfire");
            return effects.Count > 0 ? string.Join(", ", effects) : "None";
        }
    }

    // ============================================================
    // Enemy-specific properties (WPT_CREATE_ENEMIES)
    // ============================================================
    // Data[0]: LOWORD = enemy_type (1-based), HIWORD = enemy_count
    // Data[2]: LOWORD = constitution, HIWORD = HAS_* flags
    // Data[3]: enemy_move
    // Data[4]: enemy_flags
    // Data[5]: LOWORD = AI type, HIWORD = ability level

    /// <summary>Enemy type index (0-based for display)</summary>
    public int EnemyType => (_model.Data[0] & 0xFFFF) - 1;

    /// <summary>Enemy type name</summary>
    public string EnemyTypeName
    {
        get
        {
            int type = (_model.Data[0] & 0xFFFF);
            return (type >= 1 && type <= EditorStrings.EnemyTypeNames.Length)
                ? EditorStrings.EnemyTypeNames[type - 1]
                : $"Enemy {type}";
        }
    }

    /// <summary>Number of enemies to spawn</summary>
    public int EnemyCount => (_model.Data[0] >> 16) & 0xFFFF;

    /// <summary>Enemy constitution/health</summary>
    public int EnemyConstitution => _model.Data[2] & 0xFFFF;

    /// <summary>Enemy movement type index</summary>
    public int EnemyMoveType => _model.Data[3];

    /// <summary>Enemy movement type name</summary>
    public string EnemyMoveName =>
        (_model.Data[3] >= 0 && _model.Data[3] < EditorStrings.EnemyMoveNames.Length)
            ? EditorStrings.EnemyMoveNames[_model.Data[3]]
            : $"Move {_model.Data[3]}";

    /// <summary>Enemy AI type index</summary>
    public int EnemyAIType => _model.Data[5] & 0xFFFF;

    /// <summary>Enemy AI type name</summary>
    public string EnemyAIName =>
        (EnemyAIType >= 0 && EnemyAIType < EditorStrings.EnemyAINames.Length)
            ? EditorStrings.EnemyAINames[EnemyAIType]
            : $"AI {EnemyAIType}";

    // ============================================================
    // Adjust Enemy properties (WPT_ADJUST_ENEMY)
    // ============================================================
    // Uses same data as Create Enemies, plus:
    // Data[6]: enemy_to_change (target EP index)

    /// <summary>Target enemy EP to adjust</summary>
    public int AdjustEnemyTarget => _model.Data[6];

    // ============================================================
    // Move Thing properties (WPT_MOVE_THING)
    // ============================================================
    // Data[0]: which_waypoint (target EP index to move)

    /// <summary>Target waypoint to move to this location</summary>
    public int MoveThingTarget => _model.Data[0];

    // ============================================================
    // Create Target properties (WPT_CREATE_TARGET)
    // ============================================================
    // Data[0]: target_move (1-based)
    // Data[1]: target_type (1-based)
    // Data[2]: target_speed
    // Data[3]: target_delay
    // Data[4]: camera_zoom
    // Data[5]: camera_rotate

    /// <summary>Target move type index (0-based for display)</summary>
    public int TargetMoveType => _model.Data[0] - 1;

    /// <summary>Target move type name</summary>
    public string TargetMoveTypeName =>
        (_model.Data[0] >= 1 && _model.Data[0] <= EditorStrings.CameraMoveNames.Length)
            ? EditorStrings.CameraMoveNames[_model.Data[0] - 1]
            : $"Move {_model.Data[0]}";

    /// <summary>Target type index (0-based for display)</summary>
    public int CamTargetTypeIndex => _model.Data[1] - 1;

    /// <summary>Target type name</summary>
    public string CamTargetTypeName =>
        (_model.Data[1] >= 1 && _model.Data[1] <= EditorStrings.TargetTypeNames.Length)
            ? EditorStrings.TargetTypeNames[_model.Data[1] - 1]
            : $"Target {_model.Data[1]}";

    /// <summary>Target speed</summary>
    public int TargetSpeed => _model.Data[2];

    /// <summary>Target delay</summary>
    public int TargetDelay => _model.Data[3];

    /// <summary>Camera zoom level</summary>
    public int TargetCameraZoom => _model.Data[4];

    /// <summary>Camera rotate flag</summary>
    public bool TargetCameraRotate => _model.Data[5] != 0;

    // ============================================================
    // Simple Waypoint properties (WPT_SIMPLE)
    // ============================================================
    // Data[0]: waypoint_delay (in tenths of a second)

    /// <summary>Waypoint delay in tenths of a second</summary>
    public int SimpleWaypointDelay => _model.Data[0];

    /// <summary>Waypoint delay converted to seconds</summary>
    public double SimpleWaypointDelaySeconds => _model.Data[0] / 10.0;

    // ============================================================
    // Conversation properties (WPT_CONVERSATION)
    // ============================================================
    // Data[1]: converse_p1 (first participant EP)
    // Data[2]: converse_p2 (second participant EP)
    // Data[3]: converse_grab_camera

    /// <summary>First participant EventPoint index</summary>
    public int ConversePerson1 => _model.Data[1];

    /// <summary>Second participant EventPoint index</summary>
    public int ConversePerson2 => _model.Data[2];

    /// <summary>Grab camera during conversation</summary>
    public bool ConverseGrabCamera => _model.Data[3] != 0;

    // ============================================================
    // Message properties (WPT_MESSAGE)
    // ============================================================
    // Data[1]: message_time (duration)
    // Data[2]: message_who (speaker: 0=Radio, 0xFFFF=Place, 0xFFFE=Tutorial, other=EP)

    /// <summary>Message display time in seconds</summary>
    public int MessageTime => _model.Data[1] > 0 ? _model.Data[1] : 4;

    /// <summary>Message speaker value</summary>
    public int MessageWho => _model.Data[2];

    /// <summary>Message speaker description</summary>
    public string MessageSpeakerName
    {
        get
        {
            return _model.Data[2] switch
            {
                0 => "Radio",
                0xFFFF => "Place/Street Name",
                0xFFFE => "Tutorial Help",
                _ => $"EP {_model.Data[2]}"
            };
        }
    }

    // ============================================================
    // Bonus Points properties (WPT_BONUS_POINTS)
    // ============================================================
    // Data[1]: bonus_pts
    // Data[2]: bonus_type (0=Primary, 1=Secondary, 2=Bonus)
    // Data[3]: bonus_gender

    /// <summary>Bonus points value</summary>
    public int BonusPoints => _model.Data[1];

    /// <summary>Bonus type index</summary>
    public int BonusType => _model.Data[2];

    /// <summary>Bonus type name</summary>
    public string BonusTypeName =>
        (_model.Data[2] >= 0 && _model.Data[2] < EditorStrings.BonusPointTypeNames.Length)
            ? EditorStrings.BonusPointTypeNames[_model.Data[2]]
            : $"Type {_model.Data[2]}";

    /// <summary>Bonus gender (for translation)</summary>
    public bool BonusGenderFemale => _model.Data[3] != 0;

    /// <summary>
    /// Tooltip text for map hover
    /// </summary>
    public string TooltipText => $"{DisplayName}\n{Summary}\nPosition: {PositionString}";

    /// <summary>
    /// Refresh all display properties after the underlying model has changed
    /// </summary>
    public void Refresh()
    {
        // Notify all display properties have changed
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(WaypointType));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(GroupLetter));
        OnPropertyChanged(nameof(ColorIndex));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(Used));
        OnPropertyChanged(nameof(PixelX));
        OnPropertyChanged(nameof(PixelZ));
        OnPropertyChanged(nameof(MapX));
        OnPropertyChanged(nameof(MapZ));
        OnPropertyChanged(nameof(WorldX));
        OnPropertyChanged(nameof(WorldY));
        OnPropertyChanged(nameof(WorldZ));
        OnPropertyChanged(nameof(PointColor));
        OnPropertyChanged(nameof(PointBrush));
        OnPropertyChanged(nameof(CategoryColor));
        OnPropertyChanged(nameof(CategoryBrush));
        OnPropertyChanged(nameof(WaypointTypeName));
        OnPropertyChanged(nameof(TriggerTypeName));
        OnPropertyChanged(nameof(ColorName));
        OnPropertyChanged(nameof(CategoryName));
        OnPropertyChanged(nameof(PositionString));
        OnPropertyChanged(nameof(WorldPositionString));
        OnPropertyChanged(nameof(TriggeredBy));
        OnPropertyChanged(nameof(OnTrigger));
        OnPropertyChanged(nameof(OnTriggerName));
        OnPropertyChanged(nameof(EPRef));
        OnPropertyChanged(nameof(EPRefBool));
        OnPropertyChanged(nameof(Radius));
        OnPropertyChanged(nameof(ExtraText));
        OnPropertyChanged(nameof(TriggerText));
        OnPropertyChanged(nameof(Flags));
        OnPropertyChanged(nameof(HasSucksFlag));
        OnPropertyChanged(nameof(HasInverseFlag));
        OnPropertyChanged(nameof(HasInsideFlag));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(DirectionDegrees));
        OnPropertyChanged(nameof(Data));
        OnPropertyChanged(nameof(Data0));
        OnPropertyChanged(nameof(Data1));
        OnPropertyChanged(nameof(Data2));
        OnPropertyChanged(nameof(Data3));
        OnPropertyChanged(nameof(Data4));
        OnPropertyChanged(nameof(Data5));
        OnPropertyChanged(nameof(Data6));
        OnPropertyChanged(nameof(Data7));
        OnPropertyChanged(nameof(Data8));
        OnPropertyChanged(nameof(Data9));
        OnPropertyChanged(nameof(TooltipText));

        // Item-specific properties
        OnPropertyChanged(nameof(ItemType));
        OnPropertyChanged(nameof(ItemTypeName));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(ItemFlags));
        OnPropertyChanged(nameof(ItemFlagFollowsPerson));
        OnPropertyChanged(nameof(ItemFlagHiddenInPrim));

        // Barrel-specific properties
        OnPropertyChanged(nameof(BarrelType));
        OnPropertyChanged(nameof(BarrelTypeName));

        // Creature-specific properties
        OnPropertyChanged(nameof(CreatureType));
        OnPropertyChanged(nameof(CreatureTypeName));
        OnPropertyChanged(nameof(CreatureCount));

        // Vehicle-specific properties
        OnPropertyChanged(nameof(VehicleType));
        OnPropertyChanged(nameof(VehicleTypeName));
        OnPropertyChanged(nameof(VehicleBehavior));
        OnPropertyChanged(nameof(VehicleBehaviorName));
        OnPropertyChanged(nameof(VehicleTarget));
        OnPropertyChanged(nameof(VehicleKey));
        OnPropertyChanged(nameof(VehicleKeyName));

        // Visual Effect properties
        OnPropertyChanged(nameof(VfxTypes));
        OnPropertyChanged(nameof(VfxScale));
        OnPropertyChanged(nameof(VfxFlare));
        OnPropertyChanged(nameof(VfxFireDome));
        OnPropertyChanged(nameof(VfxShockwave));
        OnPropertyChanged(nameof(VfxSmokeTrails));
        OnPropertyChanged(nameof(VfxBonfire));
        OnPropertyChanged(nameof(VfxSummary));

        // Enemy-specific properties
        OnPropertyChanged(nameof(EnemyType));
        OnPropertyChanged(nameof(EnemyTypeName));
        OnPropertyChanged(nameof(EnemyCount));
        OnPropertyChanged(nameof(EnemyConstitution));
        OnPropertyChanged(nameof(EnemyMoveType));
        OnPropertyChanged(nameof(EnemyMoveName));
        OnPropertyChanged(nameof(EnemyAIType));
        OnPropertyChanged(nameof(EnemyAIName));

        // Adjust Enemy properties
        OnPropertyChanged(nameof(AdjustEnemyTarget));

        // Move Thing properties
        OnPropertyChanged(nameof(MoveThingTarget));

        // Create Target properties
        OnPropertyChanged(nameof(TargetMoveType));
        OnPropertyChanged(nameof(TargetMoveTypeName));
        OnPropertyChanged(nameof(CamTargetTypeIndex));
        OnPropertyChanged(nameof(CamTargetTypeName));
        OnPropertyChanged(nameof(TargetSpeed));
        OnPropertyChanged(nameof(TargetDelay));
        OnPropertyChanged(nameof(TargetCameraZoom));
        OnPropertyChanged(nameof(TargetCameraRotate));

        // Simple Waypoint properties
        OnPropertyChanged(nameof(SimpleWaypointDelay));
        OnPropertyChanged(nameof(SimpleWaypointDelaySeconds));

        // Conversation properties
        OnPropertyChanged(nameof(ConversePerson1));
        OnPropertyChanged(nameof(ConversePerson2));
        OnPropertyChanged(nameof(ConverseGrabCamera));

        // Message properties
        OnPropertyChanged(nameof(MessageTime));
        OnPropertyChanged(nameof(MessageWho));
        OnPropertyChanged(nameof(MessageSpeakerName));

        // Bonus Points properties
        OnPropertyChanged(nameof(BonusPoints));
        OnPropertyChanged(nameof(BonusType));
        OnPropertyChanged(nameof(BonusTypeName));
        OnPropertyChanged(nameof(BonusGenderFemale));
    }
}
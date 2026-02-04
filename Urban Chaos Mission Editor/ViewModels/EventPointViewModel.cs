using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Infrastructure;

namespace UrbanChaosMissionEditor.ViewModels
{
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

        /// <summary>
        /// Tooltip text for map hover
        /// </summary>
        public string TooltipText => $"{DisplayName}\n{Summary}\nPosition: {PositionString}";
    }
}

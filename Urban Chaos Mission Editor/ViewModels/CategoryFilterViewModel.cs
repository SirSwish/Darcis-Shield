using System.Windows.Media;
using UrbanChaosMissionEditor.Constants;
using UrbanChaosMissionEditor.Infrastructure;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// ViewModel for a category filter pill
/// </summary>
public class CategoryFilterViewModel : BaseViewModel
{
    private bool _isEnabled = true;
    private int _count;

    public CategoryFilterViewModel(WaypointCategory category)
    {
        Category = category;
        Name = category.ToString();
        Color = WaypointColors.GetCategoryColor(category);
        Brush = new SolidColorBrush(Color);
    }

    /// <summary>
    /// The category this filter represents
    /// </summary>
    public WaypointCategory Category { get; }

    /// <summary>
    /// Display name for the category
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Color for the pill
    /// </summary>
    public Color Color { get; }

    /// <summary>
    /// Brush for the pill background
    /// </summary>
    public SolidColorBrush Brush { get; }

    /// <summary>
    /// Whether this filter is currently enabled (showing items)
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(DisplayBrush));
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    /// <summary>
    /// Number of items in this category
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    /// <summary>
    /// Display text showing name and count
    /// </summary>
    public string DisplayText => $"{Name} ({Count})";

    /// <summary>
    /// Opacity based on enabled state
    /// </summary>
    public double Opacity => IsEnabled ? 1.0 : 0.4;

    /// <summary>
    /// Brush with adjusted opacity for display
    /// </summary>
    public SolidColorBrush DisplayBrush
    {
        get
        {
            var color = Color;
            if (!IsEnabled)
            {
                // Desaturate the color when disabled
                byte gray = (byte)((color.R + color.G + color.B) / 3);
                color = System.Windows.Media.Color.FromRgb(
                    (byte)((color.R + gray) / 2),
                    (byte)((color.G + gray) / 2),
                    (byte)((color.B + gray) / 2)
                );
            }
            return new SolidColorBrush(color);
        }
    }

    /// <summary>
    /// Toggle the enabled state
    /// </summary>
    public void Toggle()
    {
        IsEnabled = !IsEnabled;
    }
}
using UrbanChaosMissionEditor.Infrastructure;
using UrbanChaosMissionEditor.Models;

namespace UrbanChaosMissionEditor.ViewModels;

/// <summary>
/// ViewModel for editing mission properties (map name, light map name, etc.)
/// </summary>
public class MissionPropertiesViewModel : BaseViewModel
{
    private readonly Mission _mission;

    // Original values for reverting
    private readonly string _originalMapName;
    private readonly string _originalLightMapName;
    private readonly string _originalMissionName;
    private readonly string _originalBriefName;
    private readonly byte _originalCrimeRate;
    private readonly byte _originalCivsRate;
    private readonly byte _originalBoredomRate;
    private readonly byte _originalCarsRate;

    public MissionPropertiesViewModel(Mission mission)
    {
        _mission = mission ?? throw new ArgumentNullException(nameof(mission));

        // Store original values
        _originalMapName = mission.MapName;
        _originalLightMapName = mission.LightMapName;
        _originalMissionName = mission.MissionName;
        _originalBriefName = mission.BriefName;
        _originalCrimeRate = mission.CrimeRate;
        _originalCivsRate = mission.CivsRate;
        _originalBoredomRate = mission.BoredomRate;
        _originalCarsRate = mission.CarsRate;
    }

    public string HeaderText => "Mission Properties";

    /// <summary>
    /// Full map path as stored in UCM
    /// </summary>
    public string MapName
    {
        get => _mission.MapName;
        set
        {
            _mission.MapName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MapFileName));
        }
    }

    /// <summary>
    /// Just the filename portion of MapName (editable)
    /// </summary>
    public string MapFileName
    {
        get => GetFileName(_mission.MapName);
        set
        {
            // Preserve the path, just update the filename
            string path = GetDirectoryPath(_mission.MapName);
            _mission.MapName = string.IsNullOrEmpty(path) ? value : $"{path}\\{value}";
            OnPropertyChanged();
            OnPropertyChanged(nameof(MapName));
        }
    }

    /// <summary>
    /// Full light map path as stored in UCM
    /// </summary>
    public string LightMapName
    {
        get => _mission.LightMapName;
        set
        {
            _mission.LightMapName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LightMapFileName));
        }
    }

    /// <summary>
    /// Just the filename portion of LightMapName (editable)
    /// </summary>
    public string LightMapFileName
    {
        get => GetFileName(_mission.LightMapName);
        set
        {
            // Preserve the path, just update the filename
            string path = GetDirectoryPath(_mission.LightMapName);
            _mission.LightMapName = string.IsNullOrEmpty(path) ? value : $"{path}\\{value}";
            OnPropertyChanged();
            OnPropertyChanged(nameof(LightMapName));
        }
    }

    public string MissionName
    {
        get => _mission.MissionName;
        set
        {
            _mission.MissionName = value;
            OnPropertyChanged();
        }
    }

    public string BriefName
    {
        get => _mission.BriefName;
        set
        {
            _mission.BriefName = value;
            OnPropertyChanged();
        }
    }

    public byte CrimeRate
    {
        get => _mission.CrimeRate;
        set
        {
            _mission.CrimeRate = value;
            OnPropertyChanged();
        }
    }

    public byte CivsRate
    {
        get => _mission.CivsRate;
        set
        {
            _mission.CivsRate = value;
            OnPropertyChanged();
        }
    }

    public byte BoredomRate
    {
        get => _mission.BoredomRate;
        set
        {
            _mission.BoredomRate = value;
            OnPropertyChanged();
        }
    }

    public byte CarsRate
    {
        get => _mission.CarsRate;
        set
        {
            _mission.CarsRate = value;
            OnPropertyChanged();
        }
    }

    public uint Version => _mission.Version;
    public int EventPointCount => _mission.UsedEventPointCount;

    /// <summary>
    /// Revert all changes to original values
    /// </summary>
    public void RevertChanges()
    {
        _mission.MapName = _originalMapName;
        _mission.LightMapName = _originalLightMapName;
        _mission.MissionName = _originalMissionName;
        _mission.BriefName = _originalBriefName;
        _mission.CrimeRate = _originalCrimeRate;
        _mission.CivsRate = _originalCivsRate;
        _mission.BoredomRate = _originalBoredomRate;
        _mission.CarsRate = _originalCarsRate;
    }

    private static string GetFileName(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        int lastSlash = path.LastIndexOfAny(['\\', '/']);
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    private static string GetDirectoryPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        int lastSlash = path.LastIndexOfAny(['\\', '/']);
        return lastSlash >= 0 ? path[..lastSlash] : string.Empty;
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanChaosMissionEditor.Constants;

namespace UrbanChaosMissionEditor.Models
{
    /// <summary>
    /// Represents a complete UCM mission file
    /// </summary>
    public class Mission
    {
        public const int MaxEventPoints = 512;
        public const int CurrentVersion = 10;

        /// <summary>
        /// File version number
        /// </summary>
        public uint Version { get; set; }

        /// <summary>
        /// Mission flags
        /// </summary>
        public MissionFlags Flags { get; set; }

        // Path strings (260 chars each)

        /// <summary>
        /// Briefing file name
        /// </summary>
        public string BriefName { get; set; } = string.Empty;

        /// <summary>
        /// Light map file path (e.g., "Data\Lighting\gpost3.lgt")
        /// </summary>
        public string LightMapName { get; set; } = string.Empty;

        /// <summary>
        /// Map file path (e.g., "Data\gpost3.iam")
        /// </summary>
        public string MapName { get; set; } = string.Empty;

        /// <summary>
        /// Mission name (e.g., "wstores11")
        /// </summary>
        public string MissionName { get; set; } = string.Empty;

        /// <summary>
        /// Citizen speech text file path
        /// </summary>
        public string CitSezMapName { get; set; } = string.Empty;

        // Metadata

        /// <summary>
        /// Index into game_maps array
        /// </summary>
        public ushort MapIndex { get; set; }

        /// <summary>
        /// First free EventPoint index (linked list head)
        /// </summary>
        public ushort FreeEPoints { get; set; }

        /// <summary>
        /// First used EventPoint index (linked list head)
        /// </summary>
        public ushort UsedEPoints { get; set; }

        /// <summary>
        /// Crime rate value
        /// </summary>
        public byte CrimeRate { get; set; }

        /// <summary>
        /// Civilian spawn rate (default: 4)
        /// </summary>
        public byte CivsRate { get; set; } = 4;

        /// <summary>
        /// All EventPoints array
        /// </summary>
        public EventPoint[] EventPoints { get; set; } = new EventPoint[MaxEventPoints];

        /// <summary>
        /// AI skill levels per enemy type (254 bytes)
        /// </summary>
        public byte[] SkillLevels { get; set; } = new byte[254];

        /// <summary>
        /// Boredom rate (default: 4)
        /// </summary>
        public byte BoredomRate { get; set; } = 4;

        /// <summary>
        /// Car spawn rate
        /// </summary>
        public byte CarsRate { get; set; }

        /// <summary>
        /// Music world setting
        /// </summary>
        public byte MusicWorld { get; set; }

        /// <summary>
        /// Mission zones (128x128 grid of zone flags)
        /// </summary>
        public byte[,] MissionZones { get; set; } = new byte[128, 128];

        // Computed properties

        /// <summary>
        /// Get only the used EventPoints
        /// </summary>
        public IEnumerable<EventPoint> UsedEventPoints =>
            EventPoints.Where(ep => ep != null && ep.Used);

        /// <summary>
        /// Count of used EventPoints
        /// </summary>
        public int UsedEventPointCount =>
            EventPoints.Count(ep => ep != null && ep.Used);

        /// <summary>
        /// Get the map filename without path
        /// </summary>
        public string MapFileName
        {
            get
            {
                if (string.IsNullOrEmpty(MapName))
                    return string.Empty;

                int lastSlash = MapName.LastIndexOfAny(['\\', '/']);
                return lastSlash >= 0 ? MapName[(lastSlash + 1)..] : MapName;
            }
        }

        /// <summary>
        /// Get the light map filename without path
        /// </summary>
        public string LightMapFileName
        {
            get
            {
                if (string.IsNullOrEmpty(LightMapName))
                    return string.Empty;

                int lastSlash = LightMapName.LastIndexOfAny(['\\', '/']);
                return lastSlash >= 0 ? LightMapName[(lastSlash + 1)..] : LightMapName;
            }
        }

        /// <summary>
        /// Whether this mission has a light map
        /// </summary>
        public bool HasLightMap => !string.IsNullOrWhiteSpace(LightMapName);

        /// <summary>
        /// Whether this mission is marked as used
        /// </summary>
        public bool IsUsed => Flags.HasFlag(MissionFlags.Used);

        /// <summary>
        /// Get EventPoints by category
        /// </summary>
        public IEnumerable<EventPoint> GetEventPointsByCategory(WaypointCategory category)
        {
            return UsedEventPoints.Where(ep => ep.Category == category);
        }

        /// <summary>
        /// Get EventPoints by waypoint type
        /// </summary>
        public IEnumerable<EventPoint> GetEventPointsByType(WaypointType type)
        {
            return UsedEventPoints.Where(ep => ep.WaypointType == type);
        }

        /// <summary>
        /// Get EventPoints by group
        /// </summary>
        public IEnumerable<EventPoint> GetEventPointsByGroup(byte group)
        {
            return UsedEventPoints.Where(ep => ep.Group == group);
        }

        /// <summary>
        /// Get EventPoints by color
        /// </summary>
        public IEnumerable<EventPoint> GetEventPointsByColor(byte color)
        {
            return UsedEventPoints.Where(ep => ep.Colour == color);
        }

        /// <summary>
        /// Get category counts for statistics
        /// </summary>
        public Dictionary<WaypointCategory, int> GetCategoryCounts()
        {
            var counts = new Dictionary<WaypointCategory, int>();
            foreach (WaypointCategory category in Enum.GetValues<WaypointCategory>())
            {
                counts[category] = 0;
            }

            foreach (var ep in UsedEventPoints)
            {
                counts[ep.Category]++;
            }

            return counts;
        }

        /// <summary>
        /// Get zone flags at a specific grid position
        /// </summary>
        public ZoneFlags GetZoneFlags(int x, int z)
        {
            if (x < 0 || x >= 128 || z < 0 || z >= 128)
                return ZoneFlags.None;
            return (ZoneFlags)MissionZones[x, z];
        }

        /// <summary>
        /// Initialize EventPoints array with default instances
        /// </summary>
        public void InitializeEventPoints()
        {
            for (int i = 0; i < MaxEventPoints; i++)
            {
                EventPoints[i] = new EventPoint { Index = i };
            }
        }
    }
}

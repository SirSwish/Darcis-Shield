using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using UrbanChaosStoryboardEditor.Models;

namespace UrbanChaosStoryboardEditor.Services
{
    /// <summary>
    /// Manages loading, saving, and editing of .sty storyboard files.
    /// </summary>
    public sealed class StyDataService
    {
        public static StyDataService Instance { get; } = new StyDataService();

        // Use Windows-1252 encoding for legacy game files
        private static readonly Encoding FileEncoding = Encoding.GetEncoding(1252);

        private StyDataService()
        {
            // Register the encoding provider for Windows-1252 support in .NET Core
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Debug.WriteLine("[StyDataService] Singleton instance created");
        }

        public string? CurrentPath { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool HasChanges { get; private set; }

        public ObservableCollection<District> Districts { get; } = new();
        public ObservableCollection<MissionEntry> Missions { get; } = new();

        public event EventHandler? FileLoaded;
        public event EventHandler? FileCleared;
        public event EventHandler? DirtyStateChanged;

        public async Task LoadAsync(string path)
        {
            Debug.WriteLine($"[StyDataService.LoadAsync] Loading: {path}");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            // Read entire file content on background thread with correct encoding
            // Do NOT use ReadAllLines() as it splits on \r which breaks briefings
            string content = await Task.Run(() => File.ReadAllText(path, FileEncoding));

            // Split by \r\n only (not by \r alone) to get proper lines
            var lines = content.Split(new[] { "\r\n" }, StringSplitOptions.None);

            Debug.WriteLine($"[StyDataService.LoadAsync] Read {lines.Length} lines");

            // Parse on UI thread since we're modifying ObservableCollections
            Application.Current.Dispatcher.Invoke(() =>
            {
                Districts.Clear();
                Missions.Clear();
                ParseStyFile(lines);
            });

            CurrentPath = path;
            IsLoaded = true;
            HasChanges = false;

            FileLoaded?.Invoke(this, EventArgs.Empty);
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine($"[StyDataService.LoadAsync] Loaded {Districts.Count} districts, {Missions.Count} missions");
        }

        public async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPath))
                throw new InvalidOperationException("No current file path. Use SaveAsAsync first.");

            await SaveAsAsync(CurrentPath);
        }

        public async Task SaveAsAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is null/empty.", nameof(path));

            // Build content on UI thread (reads from ObservableCollections)
            string content = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                content = BuildStyContent();
            });

            // Write file on background thread with correct encoding
            await Task.Run(() => File.WriteAllText(path, content, FileEncoding));

            CurrentPath = path;
            HasChanges = false;

            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
            Debug.WriteLine($"[StyDataService.SaveAsAsync] Saved to: {path}");
        }

        public void New()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Districts.Clear();
                Missions.Clear();

                // Add two empty placeholder districts (required by game)
                Districts.Add(new District { DistrictId = 0, DistrictName = "", XPos = 0, YPos = 0 });
                Districts.Add(new District { DistrictId = 1, DistrictName = "", XPos = 0, YPos = 0 });
            });

            CurrentPath = null;
            IsLoaded = true;
            HasChanges = false;

            FileLoaded?.Invoke(this, EventArgs.Empty);
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);

            Debug.WriteLine("[StyDataService.New] Created new storyboard");
        }

        public void Clear()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Districts.Clear();
                Missions.Clear();
            });

            CurrentPath = null;
            IsLoaded = false;
            HasChanges = false;

            FileCleared?.Invoke(this, EventArgs.Empty);
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void MarkDirty()
        {
            if (HasChanges) return;
            HasChanges = true;
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ParseStyFile(string[] lines)
        {
            bool inDistricts = false;
            bool inMissions = true;  // Start in missions mode - missions are at the top with no header!
            int districtId = 0;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (string.IsNullOrEmpty(line)) continue;

                // Skip comments (both ; and // styles)
                if (line.StartsWith(";") || line.StartsWith("//")) continue;

                // Section headers
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var section = line.ToLowerInvariant();
                    Debug.WriteLine($"[StyDataService] Found section: {section}");

                    inDistricts = section == "[districts]";
                    inMissions = section == "[storyboard]" ||
                                 section == "[missions]" ||
                                 section == "[story]";
                    continue;
                }

                if (inDistricts)
                {
                    ParseDistrictLine(line, ref districtId);
                }
                else if (inMissions)
                {
                    ParseMissionLine(line);
                }
            }

            Debug.WriteLine($"[StyDataService] Parsing complete: {Districts.Count} districts, {Missions.Count} missions");
        }

        private void ParseDistrictLine(string line, ref int districtId)
        {
            // Format: DistrictName = X,Y
            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0) return;

            string name = line.Substring(0, eqIndex).Trim();
            string coordsPart = line.Substring(eqIndex + 1).Trim();
            var coords = coordsPart.Split(',');

            if (coords.Length >= 2 &&
                int.TryParse(coords[0].Trim(), out int x) &&
                int.TryParse(coords[1].Trim(), out int y))
            {
                Districts.Add(new District
                {
                    DistrictId = districtId++,
                    DistrictName = name,
                    XPos = x,
                    YPos = y
                });
                Debug.WriteLine($"[StyDataService] Added district {districtId - 1}: '{name}' at ({x},{y})");
            }
        }

        private void ParseMissionLine(string line)
        {
            // Format: ObjectID : GroupID : Parent : ParentIsGroup : Type : Flags : District : Filename : Title : Briefing

            Debug.WriteLine($"[StyDataService] Parsing mission line: {line.Substring(0, Math.Min(80, line.Length))}...");

            var parts = line.Split(':');

            Debug.WriteLine($"[StyDataService] Split into {parts.Length} parts");

            if (parts.Length < 10)
            {
                Debug.WriteLine($"[StyDataService] Not enough parts (need 10, got {parts.Length})");
                return;
            }

            try
            {
                // Join remaining parts for briefing (in case it contains colons)
                string briefing = string.Join(":", parts[9..]);

                // Convert \r to \r\n for proper display in UI TextBox
                briefing = briefing.Replace("\r", "\r\n");

                var mission = new MissionEntry
                {
                    // Trim numeric fields only
                    ObjectId = int.Parse(parts[0].Trim()),
                    GroupId = int.Parse(parts[1].Trim()),
                    Parent = int.Parse(parts[2].Trim()),
                    ParentIsGroup = int.Parse(parts[3].Trim()),
                    Type = int.Parse(parts[4].Trim()),
                    Flags = int.Parse(parts[5].Trim()),
                    District = int.Parse(parts[6].Trim()),
                    // Don't trim filename, name, or briefing - preserve spacing
                    MissionFile = parts[7].Trim(),  // Filename can be trimmed
                    MissionName = parts[8].TrimStart(),  // Only trim leading space, keep trailing
                    MissionBriefing = briefing.TrimStart()  // Only trim leading space
                };

                // Get audio file path from lookup
                mission.BriefingAudioFilePath = AudioFileLookup.GetFileNameById(mission.ObjectId);

                Missions.Add(mission);
                Debug.WriteLine($"[StyDataService] Added mission {mission.ObjectId}: '{mission.MissionName}'");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StyDataService] Failed to parse mission: {ex.Message}");
                Debug.WriteLine($"[StyDataService] Parts: [{string.Join("] [", parts.Take(10))}]");
            }
        }

        private string BuildStyContent()
        {
            var sb = new StringBuilder();

            // Header comments - match original format exactly
            sb.Append("//\r\n");
            sb.Append("// Urban Chaos Story Script\r\n");
            sb.Append("// Version 4\r\n");
            sb.Append($"// Exported from Darcis' Shield Mod Tool on {DateTime.Now:dd/MM/yy HH:mm:ss}\r\n");
            sb.Append("//\r\n");
            sb.Append("// Object ID : Group ID : Parent : Parent-is-group : Type : Flags : District : Filename : Title : Briefing\r\n");
            sb.Append("//\r\n");
            // NO empty line here - missions start immediately

            // Missions (no section header - they come first)
            // Use " : " spacing to match original format
            foreach (var m in Missions)
            {
                // Ensure briefing uses only \r for internal line breaks (not \r\n or \n)
                string briefing = m.MissionBriefing
                    .Replace("\r\n", "\r")
                    .Replace("\n", "\r");

                // Match original format with spaces around colons
                sb.Append($"{m.ObjectId} : {m.GroupId} : {m.Parent} : {m.ParentIsGroup} : {m.Type} : {m.Flags} : {m.District} : {m.MissionFile} : {m.MissionName} : {briefing}\r\n");
            }

            // Comment line before districts (matches original)
            sb.Append("//\r\n");

            // Districts section
            sb.Append("[districts]\r\n");
            foreach (var d in Districts)
            {
                sb.Append($"{d.DistrictName}={d.XPos},{d.YPos}\r\n");
            }

            return sb.ToString();
        }
    }
}
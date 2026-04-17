// /ViewModels/StyleEditorViewModel.cs
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using UrbanChaosEditor.Shared.Models.Styles;
using UrbanChaosStyleEditor.Models;
using UrbanChaosStyleEditor.Services;

namespace UrbanChaosStyleEditor.ViewModels
{
    public sealed class StyleEditorViewModel : INotifyPropertyChanged
    {
        private StyleProject _project = new();
        private TextureSlot? _selectedSlot;
        private StyleEntry? _selectedStyle;
        private DiscoveredWorld? _selectedWorld;
        private string _statusMessage = "Ready";

        public StyleProject Project
        {
            get => _project;
            set { _project = value; OnPropertyChanged(nameof(Project)); }
        }

        public TextureSlot? SelectedSlot
        {
            get => _selectedSlot;
            set { _selectedSlot = value; OnPropertyChanged(nameof(SelectedSlot)); }
        }

        public StyleEntry? SelectedStyle
        {
            get => _selectedStyle;
            set { _selectedStyle = value; OnPropertyChanged(nameof(SelectedStyle)); }
        }

        public DiscoveredWorld? SelectedWorld
        {
            get => _selectedWorld;
            set
            {
                if (_selectedWorld != value)
                {
                    _selectedWorld = value;
                    OnPropertyChanged(nameof(SelectedWorld));
                    if (value != null)
                        LoadWorld(value);
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public ObservableCollection<DiscoveredWorld> DiscoveredWorlds { get; } = new();

        public string CustomTexturesRoot =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustomTextures");

        public StyleEditorViewModel()
        {
            ScanCustomTextures();
        }

        public void ScanCustomTextures()
        {
            DiscoveredWorlds.Clear();

            if (!Directory.Exists(CustomTexturesRoot))
            {
                Directory.CreateDirectory(CustomTexturesRoot);
                StatusMessage = $"Created CustomTextures folder at {CustomTexturesRoot}";
                return;
            }

            foreach (var dir in Directory.GetDirectories(CustomTexturesRoot))
            {
                var dirName = Path.GetFileName(dir);
                var match = Regex.Match(dirName, @"^world(\d+)$", RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                int worldNum = int.Parse(match.Groups[1].Value);

                int texCount = Directory.GetFiles(dir, "tex*hi.png").Length
                             + Directory.GetFiles(dir, "tex*hi.bmp").Length
                             + Directory.GetFiles(dir, "tex*hi.tga").Length;
                bool hasTma = File.Exists(Path.Combine(dir, "style.tma"));
                bool hasSky = File.Exists(Path.Combine(dir, "sky.tga"))
                           || File.Exists(Path.Combine(dir, "sky.bmp"))
                           || File.Exists(Path.Combine(dir, "sky.png"));
                bool hasTexType = File.Exists(Path.Combine(dir, "textype.txt"));
                bool hasSoundFx = File.Exists(Path.Combine(dir, "soundfx.ini"));

                DiscoveredWorlds.Add(new DiscoveredWorld
                {
                    WorldNumber = worldNum,
                    FolderPath = dir,
                    TextureCount = texCount,
                    HasStyleTma = hasTma,
                    HasSky = hasSky,
                    HasTexType = hasTexType,
                    HasSoundFx = hasSoundFx
                });
            }

            StatusMessage = $"Found {DiscoveredWorlds.Count} custom world(s) in CustomTextures/";
        }

        public void LoadWorld(DiscoveredWorld world)
        {
            var project = new StyleProject
            {
                WorldNumber = world.WorldNumber,
                ProjectName = $"World {world.WorldNumber}"
            };

            // Load textures
            int loaded = 0;
            for (int i = 0; i < 256; i++)
            {
                var slot = project.Slots[i];
                string baseName = $"tex{i:D3}hi";

                // Try PNG first, then BMP, then TGA
                string? filePath = null;
                foreach (var ext in new[] { ".png", ".bmp", ".tga" })
                {
                    var candidate = Path.Combine(world.FolderPath, baseName + ext);
                    if (File.Exists(candidate))
                    {
                        filePath = candidate;
                        break;
                    }
                }

                if (filePath == null) continue;

                try
                {
                    var bmp = LoadAndResize(filePath, 64, 64);
                    slot.Image = bmp;
                    slot.SourceFilePath = filePath;
                    loaded++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StyleEditor] Failed to load {filePath}: {ex.Message}");
                }
            }

            // Load sky
            string skyTga = Path.Combine(world.FolderPath, "sky.tga");
            string skyBmp = Path.Combine(world.FolderPath, "sky.bmp");
            string skyPng = Path.Combine(world.FolderPath, "sky.png");
            string? skyPath = File.Exists(skyPng) ? skyPng
                            : File.Exists(skyTga) ? skyTga
                            : File.Exists(skyBmp) ? skyBmp
                            : null;

            if (skyPath != null)
            {
                try
                {
                    var sky = LoadAndResize(skyPath, 256, 256);
                    project.SkyImage = sky;
                    project.SkySourcePath = skyPath;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StyleEditor] Failed to load sky: {ex.Message}");
                }
            }

            // Load style.tma if present; otherwise seed with 199 blank default entries.
            string tmaPath = Path.Combine(world.FolderPath, "style.tma");

            if (File.Exists(tmaPath))
            {
                try
                {
                    var tma = TMAFile.ReadTMAFile(tmaPath);

                    // Skip raw row 0 (dummy) when building the editable project model.
                    for (int i = 1; i < tma.TextureStyles.Count; i++)
                    {
                        var style = tma.TextureStyles[i];

                        var entry = new StyleEntry
                        {
                            Index = project.Styles.Count,
                            Name = style.Name ?? ""
                        };

                        for (int j = 0; j < style.Entries.Count; j++)
                        {
                            var piece = style.Entries[j];
                            byte flag = (j < style.Flags.Count) ? (byte)style.Flags[j] : (byte)0x03;

                            entry.Pieces.Add(new StylePiece
                            {
                                Page = piece.Page,
                                Tx = piece.Tx,
                                Ty = piece.Ty,
                                Flip = piece.Flip,
                                Flag = flag
                            });
                        }

                        while (entry.Pieces.Count < 5)
                            entry.Pieces.Add(new StylePiece { Flag = 0x03 });

                        project.Styles.Add(entry);
                    }

                    // Ensure exactly 199 editable rows (pad if the file had fewer).
                    PadStylesTo199(project);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TMA] EXCEPTION reading style.tma: {ex.GetType().Name}: {ex.Message}");
                    PopulateDefaultStyles(project);
                }
            }
            else
            {
                PopulateDefaultStyles(project);
            }

            // Load textype.txt if present
            string texTypePath = Path.Combine(world.FolderPath, "textype.txt");
            if (File.Exists(texTypePath))
            {
                try
                {
                    TexTypeService.LoadIntoSlots(texTypePath, project.Slots);
                    Debug.WriteLine($"[StyleEditor] Loaded textype.txt from {texTypePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StyleEditor] Failed to load textype.txt: {ex.Message}");
                }
            }

            // Load soundfx.ini if present
            string soundFxPath = Path.Combine(world.FolderPath, "soundfx.ini");
            if (File.Exists(soundFxPath))
            {
                try
                {
                    var sfxData = SoundFxService.Load(soundFxPath);
                    SoundFxService.ApplyToSlots(sfxData, project.Slots);
                    project.SoundFxData = sfxData;
                    Debug.WriteLine($"[StyleEditor] Loaded soundfx.ini: {sfxData.Groups.Count} groups, {sfxData.TextureGroupMap.Count} mappings");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StyleEditor] Failed to load soundfx.ini: {ex.Message}");
                }
            }

            Project = project;
            SelectedSlot = null;
            SelectedStyle = null;
            StatusMessage = $"Loaded world {world.WorldNumber}: {loaded} textures, {project.Styles.Count} styles";
        }

        public bool NewTextureSet()
        {
            var inputDlg = new Views.NewWorldDialog { Owner = Application.Current.MainWindow };
            if (inputDlg.ShowDialog() != true)
                return false;

            int worldNum = inputDlg.WorldNumber;

            if (worldNum >= 1 && worldNum <= 20)
            {
                MessageBox.Show(
                    $"World numbers 1-20 are reserved by the game.\nPlease choose a number greater than 20.",
                    "Reserved World Number",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Check if already exists
            string worldPath = Path.Combine(CustomTexturesRoot, $"world{worldNum}");
            if (Directory.Exists(worldPath))
            {
                MessageBox.Show(
                    $"World {worldNum} already exists in CustomTextures.\nPlease choose a unique number or select the existing world from the sidebar.",
                    "World Already Exists",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Create the folder
            Directory.CreateDirectory(worldPath);

            var project = new StyleProject
            {
                WorldNumber = worldNum,
                ProjectName = $"World {worldNum}"
            };

            PopulateDefaultStyles(project);

            Project = project;
            SelectedSlot = null;
            SelectedStyle = null;

            // Refresh the sidebar
            ScanCustomTextures();

            // Select the new world
            foreach (var w in DiscoveredWorlds)
            {
                if (w.WorldNumber == worldNum)
                {
                    _selectedWorld = w;
                    OnPropertyChanged(nameof(SelectedWorld));
                    break;
                }
            }

            StatusMessage = $"Created new texture set: World {worldNum}";
            return true;
        }

        public void ImportTexture()
        {
            if (_selectedSlot == null)
            {
                StatusMessage = "Select a texture slot first.";
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = $"Import texture for slot {_selectedSlot.DisplayName}",
                Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg;*.tga|All Files|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var bmp = LoadAndResize(dlg.FileName, 64, 64);
                _selectedSlot.Image = bmp;
                _selectedSlot.SourceFilePath = dlg.FileName;
                StatusMessage = $"Imported {Path.GetFileName(dlg.FileName)} into {_selectedSlot.DisplayName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
        }

        public void ImportBatch()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import multiple textures to next available slots",
                Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg|All Files|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() != true)
                return;

            int imported = 0;
            int skipped = 0;

            foreach (var file in dlg.FileNames)
            {
                int slotIndex = FindFirstEmptySlot();
                if (slotIndex < 0)
                {
                    skipped = dlg.FileNames.Length - imported;
                    StatusMessage = $"All 256 slots are full. Imported {imported}, skipped {skipped}.";
                    MessageBox.Show(
                        $"All 256 texture slots are occupied.\n\nImported {imported} texture(s), {skipped} could not be added.\n\nClear some slots to make room.",
                        "No Empty Slots",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var bmp = LoadAndResize(file, 64, 64);
                    _project.Slots[slotIndex].Image = bmp;
                    _project.Slots[slotIndex].SourceFilePath = file;
                    imported++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StyleEditor] Failed to import {file}: {ex.Message}");
                }
            }

            StatusMessage = $"Imported {imported} texture(s).";
        }

        public void ClearSlot()
        {
            if (_selectedSlot == null) return;
            _selectedSlot.Image = null;
            _selectedSlot.SourceFilePath = null;
            StatusMessage = $"Cleared {_selectedSlot.DisplayName}";
        }

        public void ImportSky()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import sky image (256x256)",
                Filter = "Image Files|*.bmp;*.png;*.jpg;*.jpeg|All Files|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var bmp = LoadAndResize(dlg.FileName, 256, 256);
                _project.SkyImage = bmp;
                _project.SkySourcePath = dlg.FileName;
                StatusMessage = $"Sky imported from {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sky import failed: {ex.Message}";
            }
        }

        public void ClearSky()
        {
            _project.SkyImage = null;
            _project.SkySourcePath = null;
            StatusMessage = "Sky cleared.";
        }

        /// <summary>
        /// Fills a project with 199 blank default StyleEntry rows (all values 0, flags POLY_GT 0x03).
        /// This is the correct starting state for a new TMA — no default.tma file needed.
        /// </summary>
        private static void PopulateDefaultStyles(StyleProject project)
        {
            for (int i = project.Styles.Count; i < 199; i++)
            {
                var entry = new StyleEntry { Index = i };
                for (int j = 0; j < 5; j++)
                    entry.Pieces.Add(new StylePiece()); // all zeros, Flag defaults to 0x03
                project.Styles.Add(entry);
            }
        }

        /// <summary>
        /// Pads an existing style list up to 199 rows with blank default entries.
        /// </summary>
        private static void PadStylesTo199(StyleProject project)
        {
            for (int i = project.Styles.Count; i < 199; i++)
            {
                var entry = new StyleEntry { Index = i };
                for (int j = 0; j < 5; j++)
                    entry.Pieces.Add(new StylePiece());
                project.Styles.Add(entry);
            }
        }

        public void LoadTmaFromFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load TMA file",
                Filter = "TMA Style Files|*.tma|All Files|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var tma = UrbanChaosEditor.Shared.Models.Styles.TMAFile.ReadTMAFile(dlg.FileName);

                _project.Styles.Clear();

                // Skip row 0 (dummy) when building the editable model.
                for (int i = 1; i < tma.TextureStyles.Count; i++)
                {
                    var style = tma.TextureStyles[i];
                    var entry = new StyleEntry
                    {
                        Index = _project.Styles.Count,
                        Name = style.Name ?? ""
                    };

                    for (int j = 0; j < style.Entries.Count; j++)
                    {
                        var piece = style.Entries[j];
                        byte flag = (j < style.Flags.Count) ? (byte)style.Flags[j] : (byte)0x03;
                        entry.Pieces.Add(new StylePiece
                        {
                            Page = piece.Page,
                            Tx = piece.Tx,
                            Ty = piece.Ty,
                            Flip = piece.Flip,
                            Flag = flag
                        });
                    }

                    while (entry.Pieces.Count < 5)
                        entry.Pieces.Add(new StylePiece { Flag = 0x03 });

                    _project.Styles.Add(entry);
                }

                PadStylesTo199(_project);

                SelectedStyle = null;
                StatusMessage = $"Loaded {Path.GetFileName(dlg.FileName)}: {_project.Styles.Count} styles";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load TMA: {ex.Message}";
            }
        }

        private string? _lastExportRoot;

        public void ExportProject()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select the game's server\\textures folder (or any output root).\nA worldX subfolder will be created automatically.",
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrEmpty(_lastExportRoot) && Directory.Exists(_lastExportRoot))
                dlg.SelectedPath = _lastExportRoot;

            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            _lastExportRoot = dlg.SelectedPath;

            string outputFolder = Path.Combine(dlg.SelectedPath, $"world{_project.WorldNumber}");

            // Also export to CustomTextures so the Map Editor picks it up
            string customFolder = Path.Combine(CustomTexturesRoot, $"world{_project.WorldNumber}");

            StatusMessage = "Exporting...";

            var result = TextureExporter.Export(_project, outputFolder);
            if (!result.Success)
            {
                StatusMessage = $"Export failed: {result.Error}";
                return;
            }

            // Copy to CustomTextures as well if the target isn't already CustomTextures
            if (!outputFolder.Equals(customFolder, StringComparison.OrdinalIgnoreCase))
            {
                var customResult = TextureExporter.Export(_project, customFolder);
                if (!customResult.Success)
                    StatusMessage = $"Exported to game folder but CustomTextures copy failed: {customResult.Error}";
            }

            StatusMessage = $"Exported {result.TexturesExported} textures and {result.StylesExported} styles to {outputFolder}";
            ScanCustomTextures();
        }

        public void MoveSlot(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= 256 || toIndex < 0 || toIndex >= 256)
                return;
            if (fromIndex == toIndex)
                return;

            var fromSlot = _project.Slots[fromIndex];
            var toSlot = _project.Slots[toIndex];

            var tempImage = fromSlot.Image;
            var tempPath = fromSlot.SourceFilePath;

            fromSlot.Image = toSlot.Image;
            fromSlot.SourceFilePath = toSlot.SourceFilePath;

            toSlot.Image = tempImage;
            toSlot.SourceFilePath = tempPath;

            StatusMessage = $"Swapped tex{fromIndex:D3} and tex{toIndex:D3}";
        }

        private static BitmapSource LoadAndResize(string path, int width, int height)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = width;
            bmp.DecodePixelHeight = height;
            bmp.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static int ExtractSlotIndex(string filename)
        {
            var match = Regex.Match(filename, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
                return idx;
            return -1;
        }

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < 256; i++)
            {
                if (!_project.Slots[i].IsOccupied)
                    return i;
            }
            return -1;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class DiscoveredWorld : INotifyPropertyChanged
    {
        public int WorldNumber { get; init; }
        public string FolderPath { get; init; } = "";
        public int TextureCount { get; init; }
        public bool HasStyleTma { get; init; }
        public bool HasSky { get; init; }
        public bool HasTexType { get; init; }
        public bool HasSoundFx { get; init; }

        public string DisplayName => $"World {WorldNumber}";
        public string Summary => $"{TextureCount} textures{(HasStyleTma ? " + TMA" : "")}{(HasSky ? " + Sky" : "")}{(HasTexType ? " + TexType" : "")}{(HasSoundFx ? " + SFX" : "")}";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
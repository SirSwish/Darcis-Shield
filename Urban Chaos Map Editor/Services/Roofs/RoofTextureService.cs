// Services/Roofs/RoofTextureService.cs
using System.Diagnostics;
using System.IO;
using UrbanChaosEditor.Shared.Constants;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Models.Core;
using UrbanChaosMapEditor.Services.Buildings;
using UrbanChaosMapEditor.Services.Core;
using static UrbanChaosMapEditor.Services.Textures.TexturesAccessor;

namespace UrbanChaosMapEditor.Services.Roofs
{
    /// <summary>
    /// Manages the 128x128 UWORD warehouse roof texture map, loaded from / saved to a
    /// same-name .MAP file that sits alongside the .IAM.
    ///
    /// .MAP file layout:
    ///   [0..3]                           4-byte placeholder header
    ///   [4 .. 4 + 128*128*12 - 1]       128*128*12 bytes of cell data (ignored by editor)
    ///   [RoofTexOffset .. end]           128*128 x UWORD roof texture entries
    ///
    /// UWORD packing (PC engine layout, confirmed):
    ///   bits  0-9  = page/index  — textureByte | (groupTag &lt;&lt; 8)
    ///                              engine: num = val &amp; 0x3FF; page = num
    ///   bits 10-11 = rotation    (0-3)
    ///   bits 12-13 = flip        (0 — unused for now)
    ///   bits 14-15 = size        (0 — unused for now)
    ///
    /// Array indexing uses GAME coordinates:
    ///   game_x = 127 - UI_tx
    ///   game_z = 127 - UI_ty
    ///   RoofTex[game_x, game_z]
    /// </summary>
    public sealed class RoofTextureService
    {
        private static readonly Lazy<RoofTextureService> _lazy = new(() => new RoofTextureService());
        public static RoofTextureService Instance => _lazy.Value;

        // ── .MAP file layout constants ───────────────────────────────────────
        public const int MapHeaderSize = RoofFormatConstants.MapHeaderSize;
        public const int MapCellDataSize = RoofFormatConstants.MapCellDataSize;
        public const int RoofTexOffset = RoofFormatConstants.RoofTextureOffset;
        public const int RoofTexCount = RoofFormatConstants.RoofTextureCount;
        public const int RoofTexByteSize = RoofFormatConstants.RoofTextureByteSize;
        public const int TotalMapSize = RoofFormatConstants.TotalMapSize;

        // ── Internal state ───────────────────────────────────────────────────
        // Indexed [game_x, game_z]; game coords = (127 - UI_tx, 127 - UI_ty)
        private ushort[,] _roofTex = new ushort[128, 128];
        private HashSet<(int x, int z)>? _rf4OccupancyCache;
        private string? _loadedIamPath;

        public bool IsDirty { get; private set; }

        // ── Constructor / event wiring ───────────────────────────────────────
        private RoofTextureService()
        {
            MapDataService.Instance.MapLoaded += OnMapLoaded;
            MapDataService.Instance.MapCleared += OnMapCleared;
            MapDataService.Instance.MapSaved  += OnMapSaved;
            RoofsChangeBus.Instance.Changed   += OnRoofsChanged;
        }

        private void OnMapLoaded(object? sender, MapLoadedEventArgs e)
        {
            LoadFromMapFile(e.Path);
            _rf4OccupancyCache = null;
            RoofTexturesChangeBus.Instance.NotifyChanged();
        }

        private void OnMapCleared(object? sender, EventArgs e)
        {
            Reset();
            _rf4OccupancyCache = null;
            RoofTexturesChangeBus.Instance.NotifyChanged();
        }

        private void OnMapSaved(object? sender, MapSavedEventArgs e)
        {
            SaveToMapFile(e.Path);
        }

        private void OnRoofsChanged(object? sender, EventArgs e)
        {
            // RF4 data may have changed — invalidate the positional occupancy cache.
            _rf4OccupancyCache = null;
        }

        // ── Public read / write ──────────────────────────────────────────────

        /// <summary>Read roof texture at game coordinates (game_x, game_z).</summary>
        public ushort ReadEntry(int gameX, int gameZ)
        {
            if ((uint)gameX >= 128 || (uint)gameZ >= 128) return 0;
            return _roofTex[gameX, gameZ];
        }

        /// <summary>Write a single roof texture entry at game coordinates.</summary>
        public void WriteEntry(int gameX, int gameZ, ushort value)
        {
            if ((uint)gameX >= 128 || (uint)gameZ >= 128) return;
            _roofTex[gameX, gameZ] = value;
            IsDirty = true;
        }

        /// <summary>
        /// Write a roof texture to a rectangular region, given UI tile coordinates.
        /// Call NotifyChanged() after a batch write.
        /// </summary>
        public void WriteRegionUI(int uiMinTx, int uiMinTy, int uiMaxTx, int uiMaxTy, ushort value)
        {
            for (int ty = uiMinTy; ty <= uiMaxTy; ty++)
            {
                for (int tx = uiMinTx; tx <= uiMaxTx; tx++)
                {
                    int gx = 127 - tx;
                    int gz = 127 - ty;
                    if ((uint)gx < 128 && (uint)gz < 128)
                        _roofTex[gx, gz] = value;
                }
            }
            IsDirty = true;
        }

        /// <summary>Reset to a blank roof texture array (all zeroes).</summary>
        public void Reset()
        {
            _roofTex = new ushort[128, 128];
            IsDirty = false;
        }

        // ── RF4 occupancy ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the set of game-coordinate (x, z) cells covered by at least one
        /// warehouse RF4 tile.  Result is cached; invalidated on RoofsChangeBus or map reload.
        ///
        /// A warehouse RF4 is identified by TWO conditions (both must hold):
        ///   1. The PAP Hidden flag (bit 4) is set on the cell — the primary marker
        ///      for indoor/roof-covered tiles.
        ///   2. The walkable that owns the RF4 belongs to a Warehouse-type building
        ///      (BuildingType == 1).  This acts as a belt-and-suspenders guard.
        ///      If the RF4 cannot be traced to a building (e.g., unlinked data),
        ///      condition 1 alone is accepted so the cell is still included.
        /// </summary>
        public HashSet<(int x, int z)> GetRF4OccupiedCells()
        {
            if (_rf4OccupancyCache is not null)
                return _rf4OccupancyCache;

            _rf4OccupancyCache = new HashSet<(int, int)>();

            if (!MapDataService.Instance.IsLoaded) return _rf4OccupancyCache;

            if (!MapDataService.Instance.TryGetWalkables(out var walkables, out var rf4s))
                return _rf4OccupancyCache;

            // Build a 1-based RF4-index → BuildingType lookup from walkable ranges.
            var rf4ToBuildingType = BuildRF4ToBuildingType(walkables);

            var altAcc = new AltitudeAccessor(MapDataService.Instance);

            // Skip index 0 (1-based sentinel / dummy entry)
            for (int i = 1; i < rf4s.Length; i++)
            {
                var rf4 = rf4s[i];

                int gx = rf4.RX & 0x7F;  // game X = RX & 127
                int gz = rf4.RZ & 0x7F;  // game Z = RZ & 127 (≡ RZ-128 for valid entries)

                // Convert to UI tile coordinates for PAP flag read
                int tx = MapConstants.TilesPerSide - 1 - gx;
                int ty = MapConstants.TilesPerSide - 1 - gz;

                if ((uint)tx >= MapConstants.TilesPerSide || (uint)ty >= MapConstants.TilesPerSide)
                    continue;

                // Condition 1: PAP Hidden flag must be set on the cell
                if (!altAcc.HasFlags(tx, ty, PapFlags.Hidden))
                    continue;

                // Condition 2: building type must be Warehouse, if we can determine it
                if (rf4ToBuildingType.TryGetValue(i, out var bldType) &&
                    bldType != BuildingType.Warehouse)
                    continue;
                // If RF4 is not in the map (unlinked), Hidden flag alone is sufficient.

                _rf4OccupancyCache.Add((gx, gz));
            }

            return _rf4OccupancyCache;
        }

        /// <summary>True if the cell at game coordinates (gx, gz) is covered by a warehouse RF4.</summary>
        public bool IsCellCoveredByRF4(int gx, int gz)
            => GetRF4OccupiedCells().Contains((gx, gz));

        /// <summary>True if the cell at UI tile (tx, ty) is covered by a warehouse RF4.</summary>
        public bool IsUICellCoveredByRF4(int tx, int ty)
            => IsCellCoveredByRF4(MapConstants.TilesPerSide - 1 - tx, MapConstants.TilesPerSide - 1 - ty);

        public void InvalidateOccupancyCache() => _rf4OccupancyCache = null;

        // ── RF4 → Building-type lookup ────────────────────────────────────────

        /// <summary>
        /// Builds a map of (1-based RF4 index) → BuildingType by joining walkable
        /// RF4 ranges with their owning buildings.
        ///
        /// DWalkableRec.Building is a 1-based building ID.
        /// DWalkableRec.StartFace4..EndFace4 is the inclusive 1-based RF4 range for
        /// that walkable.
        /// </summary>
        private static Dictionary<int, BuildingType> BuildRF4ToBuildingType(DWalkableRec[] walkables)
        {
            var result = new Dictionary<int, BuildingType>();

            // We need the buildings array — read a snapshot.
            // This is done once per cache-build so the cost is acceptable.
            DBuildingRec[]? buildings = null;
            try
            {
                var snap = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
                buildings = snap.Buildings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RoofTex] BuildRF4ToBuildingType: could not read buildings — {ex.Message}");
            }

            if (buildings is null || buildings.Length == 0) return result;

            // Skip sentinel at index 0
            for (int wi = 1; wi < walkables.Length; wi++)
            {
                var w = walkables[wi];
                if (w.Building == 0) continue;

                // Try 1-based and 0-based lookups to be resilient to either convention.
                BuildingType? bldType = null;
                if (w.Building <= buildings.Length)
                    bldType = buildings[w.Building - 1].BuildingType;   // 1-based → 0-indexed
                else if (w.Building < buildings.Length)
                    bldType = buildings[w.Building].BuildingType;        // 0-based fallback

                if (bldType is null) continue;

                // Register every RF4 index in this walkable's range
                for (int fi = w.StartFace4; fi > 0 && fi <= w.EndFace4; fi++)
                    result[fi] = bldType.Value;
            }

            return result;
        }

        // ── UWORD encoding / decoding ────────────────────────────────────────

        /// <summary>
        /// Encodes a texture selection from the World/Shared/Prims picker into a .MAP UWORD.
        ///
        /// PC warehouse roof UWORD layout:
        ///   bits  0-9  = page/index  — packed as  textureByte | (groupTag &lt;&lt; 8)
        ///                              engine reads: num = val &amp; 0x3FF; page = num
        ///   bits 10-11 = rotation    (0-3)
        ///   bits 12-13 = flip        (0 — not used by editor for now)
        ///   bits 14-15 = size        (0 — not used by editor for now)
        ///
        /// The IAM texture picker already produces the correct page/index value on PC,
        /// so no remapping is needed.
        /// </summary>
        public static ushort EncodeFromTextureSelection(
            TextureGroup group,
            int texNumber,
            int rotationIndex)
        {
            byte textureByte = group switch
            {
                TextureGroup.World  => (byte)Math.Clamp(texNumber, 0, 255),
                TextureGroup.Shared => (byte)Math.Clamp(texNumber - 256, 0, 255),
                TextureGroup.Prims  => unchecked((byte)(sbyte)Math.Clamp(texNumber - 64, sbyte.MinValue, sbyte.MaxValue)),
                _                   => 0
            };

            int groupTag = group switch
            {
                TextureGroup.World  => 0,
                TextureGroup.Shared => 1,
                TextureGroup.Prims  => 2,
                _                   => 0
            };

            return (ushort)(((rotationIndex & 0x3) << 10) | ((groupTag & 0x3) << 8) | textureByte);
        }

        /// <summary>
        /// Decodes a .MAP UWORD back to group, logical texture number, and rotation.
        /// Mirrors <see cref="EncodeFromTextureSelection"/>.
        /// </summary>
        public static (TextureGroup group, int texNumber, int rotationIndex) DecodeEntry(ushort value)
        {
            byte textureByte   = (byte)(value & 0xFF);
            int  groupTag      = (value >> 8) & 0x3;
            int  rotationIndex = (value >> 10) & 0x3;

            var (group, texNumber) = groupTag switch
            {
                0 => (TextureGroup.World,  (int)textureByte),
                1 => (TextureGroup.Shared, (int)textureByte + 256),
                _ => (TextureGroup.Prims,  (int)(sbyte)textureByte + 64)
            };

            return (group, texNumber, rotationIndex);
        }

        // ── .MAP file I/O ────────────────────────────────────────────────────

        public void LoadFromMapFile(string iamPath)
        {
            Reset();
            _loadedIamPath = iamPath;

            string mapPath = Path.ChangeExtension(iamPath, ".map");
            if (!File.Exists(mapPath))
            {
                Debug.WriteLine($"[RoofTex] No .MAP at '{mapPath}'; initialising blank roof textures.");
                return;
            }

            try
            {
                byte[] data = File.ReadAllBytes(mapPath);
                int needed = RoofTexOffset + RoofTexByteSize;
                if (data.Length < needed)
                {
                    Debug.WriteLine($"[RoofTex] .MAP too short ({data.Length} bytes, need {needed}); using blank.");
                    return;
                }

                // Roof texture array stored x-major: RoofTex[x=0..127][z=0..127]
                int offset = RoofTexOffset;
                for (int x = 0; x < 128; x++)
                {
                    for (int z = 0; z < 128; z++)
                    {
                        _roofTex[x, z] = BitConverter.ToUInt16(data, offset);
                        offset += 2;
                    }
                }

                Debug.WriteLine($"[RoofTex] Loaded from '{mapPath}'.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RoofTex] Load failed: {ex.Message}. Using blank.");
                Reset();
            }
        }

        public void EnsureLoadedForCurrentMap()
        {
            if (!MapDataService.Instance.IsLoaded)
                return;

            string? path = MapDataService.Instance.CurrentPath; // use your actual path property name

            if (string.IsNullOrWhiteSpace(path))
                return;

            if (string.Equals(_loadedIamPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            LoadFromMapFile(path);
            _rf4OccupancyCache = null;
            RoofTexturesChangeBus.Instance.NotifyChanged();
        }

        public void SaveToMapFile(string iamPath)
        {
            string mapPath = Path.ChangeExtension(iamPath, ".map");

            try
            {
                byte[] data;

                if (File.Exists(mapPath))
                {
                    data = File.ReadAllBytes(mapPath);
                    if (data.Length < TotalMapSize)
                        data = BuildMinimalMapFile();
                }
                else
                {
                    data = BuildMinimalMapFile();
                }

                // Write roof textures x-major
                int offset = RoofTexOffset;
                for (int x = 0; x < 128; x++)
                {
                    for (int z = 0; z < 128; z++)
                    {
                        ushort entry = _roofTex[x, z];
                        data[offset + 0] = (byte)(entry & 0xFF);
                        data[offset + 1] = (byte)(entry >> 8);
                        offset += 2;
                    }
                }

                File.WriteAllBytes(mapPath, data);
                IsDirty = false;
                Debug.WriteLine($"[RoofTex] Saved to '{mapPath}'.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RoofTex] Save failed: {ex.Message}.");
            }
        }

        private static byte[] BuildMinimalMapFile()
        {
            // 4-byte header + 128*128*12 dummy cell data + roof tex region (all zeroed)
            return new byte[TotalMapSize];
        }
    }
}

// Services/Prims/PrimModelService.cs
// Singleton service that loads, caches, and provides 3D meshes for PRIM files.
// Reads PRIM and texture directories from the Prim Editor's PrimDirectoryService
// so configuration is shared across both apps.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Media3D;
using UrbanChaosPrimEditor.Models;
using UrbanChaosPrimEditor.Services;

namespace UrbanChaosMapEditor.Services.Prims
{
    public sealed class PrimModelService
    {
        private static readonly Lazy<PrimModelService> _lazy = new(() => new PrimModelService());
        public static PrimModelService Instance => _lazy.Value;

        private readonly Dictionary<int, PrmModel?> _modelCache = new();
        private readonly Dictionary<int, Model3DGroup?> _meshCache = new();
        private readonly PrmParserService _parser = new();

        private string? _lastSeenPrimDir;
        private string? _lastSeenTextureDir;
        private PrimTextureResolverService? _textureResolver;

        private PrimModelService()
        {
            // Ensure the Prim Editor settings are loaded; subscribe to changes
            // so caches are cleared when the user updates either directory in
            // the Prim Editor while the Map Editor is running.
            PrimDirectoryService.Instance.Load();
            PrimDirectoryService.Instance.DirectoryChanged += OnPrimDirectoryChanged;
            PrimDirectoryService.Instance.TextureDirectoryChanged += OnTextureDirectoryChanged;
            RefreshFromPrimEditor();
        }

        /// <summary>The currently-effective PRIM directory (inherited from Prim Editor).</summary>
        public string? PrimDirectory => PrimDirectoryService.Instance.PrmDirectory;

        /// <summary>The currently-effective texture directory (inherited from Prim Editor).</summary>
        public string? TextureDirectory => PrimDirectoryService.Instance.TextureDirectory;

        private void OnPrimDirectoryChanged(object? sender, EventArgs e)
        {
            string? current = PrimDirectoryService.Instance.PrmDirectory;
            if (current == _lastSeenPrimDir) return;
            _lastSeenPrimDir = current;
            ClearCache();
        }

        private void OnTextureDirectoryChanged(object? sender, EventArgs e)
        {
            string? current = PrimDirectoryService.Instance.TextureDirectory;
            if (current == _lastSeenTextureDir) return;
            _lastSeenTextureDir = current;
            _textureResolver = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
                ? new PrimTextureResolverService(current)
                : null;
            // Only mesh cache depends on textures; model cache is fine.
            _meshCache.Clear();
        }

        private void RefreshFromPrimEditor()
        {
            _lastSeenPrimDir = PrimDirectoryService.Instance.PrmDirectory;
            _lastSeenTextureDir = PrimDirectoryService.Instance.TextureDirectory;
            _textureResolver = !string.IsNullOrWhiteSpace(_lastSeenTextureDir) && Directory.Exists(_lastSeenTextureDir)
                ? new PrimTextureResolverService(_lastSeenTextureDir)
                : null;
        }

        /// <summary>Clears both model and mesh caches.</summary>
        public void ClearCache()
        {
            _modelCache.Clear();
            _meshCache.Clear();
        }

        /// <summary>
        /// Attempts to load a PRIM model by its number (1-255).
        /// Returns null if the file doesn't exist or can't be parsed.
        /// Results are cached.
        /// </summary>
        public PrmModel? GetModel(int primNumber)
        {
            if (primNumber < 0 || primNumber > 255)
                return null;

            if (_modelCache.TryGetValue(primNumber, out var cached))
                return cached;

            PrmModel? model = null;
            string? primDirectory = PrimDirectory;

            if (string.IsNullOrWhiteSpace(primDirectory))
            {
                Debug.WriteLine($"[PrimModelService] PRIM directory not configured (set it in the Prim Editor)");
            }
            else if (!Directory.Exists(primDirectory))
            {
                Debug.WriteLine($"[PrimModelService] PRIM directory does not exist: {primDirectory}");
            }
            else
            {
                // Urban Chaos uses two formats:
                // - NPRIM (newer): nprim###.prm
                // - PRIM (older): prim###.prm
                // Try NPRIM first, then fall back to PRIM
                string nprimPath = Path.Combine(primDirectory, $"nprim{primNumber:D3}.prm");
                string primPath = Path.Combine(primDirectory, $"prim{primNumber:D3}.prm");

                string? pathToLoad = null;
                if (File.Exists(nprimPath))
                    pathToLoad = nprimPath;
                else if (File.Exists(primPath))
                    pathToLoad = primPath;

                if (pathToLoad != null)
                {
                    try
                    {
                        model = _parser.Load(pathToLoad);
                        Debug.WriteLine($"[PrimModelService] Loaded PRIM {primNumber} from {pathToLoad} ({model.Points.Count} points, {model.Triangles.Count} tris, {model.Quadrangles.Count} quads)");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PrimModelService] Failed to parse {pathToLoad}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[PrimModelService] PRIM file not found. Tried: {nprimPath}, {primPath}");
                }
            }

            _modelCache[primNumber] = model;
            return model;
        }

        /// <summary>
        /// Gets a 3D mesh for the given prim number.
        /// Returns null if the model doesn't exist or can't be built.
        /// Meshes are cached and frozen for performance.
        /// </summary>
        public Model3DGroup? GetMesh(int primNumber)
        {
            if (primNumber < 0 || primNumber > 255)
                return null;

            if (_meshCache.TryGetValue(primNumber, out var cached))
                return cached;

            var model = GetModel(primNumber);
            if (model == null)
            {
                _meshCache[primNumber] = null;
                return null;
            }

            Model3DGroup? mesh = null;
            try
            {
                var builder = new PrmMeshBuilderService(_textureResolver);
                mesh = builder.Build(model);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PrimModelService] Failed to build mesh for PRIM {primNumber}: {ex.Message}");
            }

            _meshCache[primNumber] = mesh;
            return mesh;
        }

        /// <summary>Checks if a prim model is available (file exists and can be loaded).</summary>
        public bool IsAvailable(int primNumber) => GetModel(primNumber) != null;
    }
}

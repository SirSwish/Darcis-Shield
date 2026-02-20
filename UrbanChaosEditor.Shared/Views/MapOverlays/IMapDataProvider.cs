// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/IMapDataProvider.cs
// ============================================================
// Interface for map data access - implemented by each editor's data service

using System;
using System.Collections.Generic;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Provides read-only access to map data for overlay rendering.
/// Each editor implements this interface with their specific data service.
/// Uses callback subscriptions instead of events to avoid type signature issues.
/// </summary>
public interface IMapDataProvider
{
    /// <summary>Whether map data is currently loaded</summary>
    bool IsLoaded { get; }

    /// <summary>Get a copy of the raw map bytes</summary>
    byte[]? GetBytesCopy();

    /// <summary>Subscribe to map loaded events</summary>
    void SubscribeMapLoaded(Action callback);

    /// <summary>Subscribe to map cleared events</summary>
    void SubscribeMapCleared(Action callback);
}

/// <summary>
/// Extended interface for map data providers that support building region queries.
/// </summary>
public interface IBuildingDataProvider : IMapDataProvider
{
    /// <summary>Compute and cache the building region offsets</summary>
    void ComputeAndCacheBuildingRegion();

    /// <summary>Try to get the building region bounds</summary>
    bool TryGetBuildingRegion(out int start, out int length);
}

/// <summary>
/// Interface for texture data access.
/// </summary>
public interface ITextureDataProvider : IMapDataProvider
{
    /// <summary>Get texture key and rotation for a tile</summary>
    (string relativeKey, int rotationDeg) GetTileTextureKeyAndRotation(int tx, int ty);
}

/// <summary>
/// Interface for prim data access.
/// </summary>
public interface IPrimDataProvider
{
    /// <summary>Whether data is loaded</summary>
    bool IsLoaded { get; }

    /// <summary>Read all prims from the map data</summary>
    List<PrimDisplayInfo> ReadAllPrims();

    /// <summary>Subscribe to map loaded events</summary>
    void SubscribeMapLoaded(Action callback);

    /// <summary>Subscribe to map cleared events</summary>
    void SubscribeMapCleared(Action callback);
}

/// <summary>
/// Interface for light data access.
/// </summary>
public interface ILightDataProvider
{
    /// <summary>Whether light data is loaded</summary>
    bool IsLoaded { get; }

    /// <summary>Read all light entries</summary>
    List<LightDisplayInfo> ReadAllLights();

    /// <summary>Subscribe to lights loaded events</summary>
    void SubscribeLightsLoaded(Action callback);

    /// <summary>Subscribe to lights cleared events</summary>
    void SubscribeLightsCleared(Action callback);
}

// ============================================================
// DATA STRUCTURES
// ============================================================

/// <summary>
/// Display information for a single prim/object.
/// </summary>
public struct PrimDisplayInfo
{
    public int PixelX { get; set; }
    public int PixelZ { get; set; }
    public int Y { get; set; }  // Height for sorting
    public byte PrimNumber { get; set; }
    public byte Yaw { get; set; }
    public int Index { get; set; }  // Original index in the data array
}

/// <summary>
/// Display information for a single light.
/// </summary>
public struct LightDisplayInfo
{
    public int PixelX { get; set; }
    public int PixelZ { get; set; }
    public byte Range { get; set; }
    public sbyte Red { get; set; }
    public sbyte Green { get; set; }
    public sbyte Blue { get; set; }
    public int Index { get; set; }  // Original index
    public bool Used { get; set; }
}
// ============================================================
// UrbanChaosEditor.Shared/Views/MapOverlays/SharedBuildingLayer.cs
// ============================================================
// Shared building facet visualization layer

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using UrbanChaosEditor.Shared.Models;

namespace UrbanChaosEditor.Shared.Views.MapOverlays;

/// <summary>
/// Shared building layer that renders building facets as colored lines.
/// Can be used in read-only mode (Light/Mission Editor) or extended for editing (Map Editor).
/// </summary>
public class SharedBuildingLayer : MapOverlayBase
{
    #region Building Format Constants (V1)

    protected const int HeaderSize = 48;
    protected const int DBuildingSize = 24;
    protected const int AfterBuildingsPad = 14;
    protected const int DFacetSize = 26;

    #endregion

    #region Static Pens

    protected static readonly Pen PenWallGreen;
    protected static readonly Pen PenCableRed;
    protected static readonly Pen PenFenceYellow;
    protected static readonly Pen PenLadderOrange;
    protected static readonly Pen PenDoorPurple;
    protected static readonly Pen PenRoofBlue;
    protected static readonly Pen PenInsideGray;
    protected static readonly Pen PenDefault;

    static SharedBuildingLayer()
    {
        static Pen MakePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();
            return pen;
        }

        var fluoroGreen = new SolidColorBrush(Color.FromRgb(102, 255, 0));
        fluoroGreen.Freeze();

        PenWallGreen = MakePen(fluoroGreen, 6.0);
        PenCableRed = MakePen(Brushes.Red, 6.0);
        PenFenceYellow = MakePen(Brushes.Yellow, 5.0);
        PenLadderOrange = MakePen(Brushes.Orange, 8.0);
        PenDoorPurple = MakePen(Brushes.MediumPurple, 7.0);
        PenRoofBlue = MakePen(Brushes.DeepSkyBlue, 4.5);
        PenInsideGray = MakePen(Brushes.SlateGray, 4.5);
        PenDefault = MakePen(fluoroGreen, 4.5);
    }

    #endregion

    #region Cached Data

    protected byte[]? CachedBytes;
    protected int BuildingRegionStart = -1;
    protected int BuildingRegionLength = 0;
    protected int FacetsOffsetAbsolute = -1;
    protected int TotalBuildings = 0;
    protected int TotalFacets = 0;

    #endregion

    #region Data Provider

    private IBuildingDataProvider? _dataProvider;

    /// <summary>
    /// Set the data provider for this layer.
    /// Call this during initialization or when the data source changes.
    /// </summary>
    public void SetDataProvider(IBuildingDataProvider provider)
    {
        _dataProvider = provider;

        if (_dataProvider != null)
        {
            _dataProvider.SubscribeMapLoaded(OnMapLoaded);
            _dataProvider.SubscribeMapCleared(OnMapCleared);

            // Initial load if already loaded
            if (_dataProvider.IsLoaded)
            {
                SeedFromProvider();
                RefreshOnUiThread();
            }
        }
    }

    private void OnMapLoaded()
    {
        SeedFromProvider();
        RefreshOnUiThread();
    }

    private void OnMapCleared()
    {
        ClearCache();
        RefreshOnUiThread();
    }

    #endregion

    #region Cache Management

    protected virtual void ClearCache()
    {
        CachedBytes = null;
        BuildingRegionStart = -1;
        BuildingRegionLength = 0;
        FacetsOffsetAbsolute = -1;
        TotalBuildings = 0;
        TotalFacets = 0;
    }

    protected virtual void SeedFromProvider()
    {
        ClearCache();

        if (_dataProvider == null || !_dataProvider.IsLoaded)
        {
            Debug.WriteLine("[SharedBuildingLayer] Seed: no provider or map not loaded.");
            return;
        }

        _dataProvider.ComputeAndCacheBuildingRegion();
        if (!_dataProvider.TryGetBuildingRegion(out var start, out var length))
        {
            Debug.WriteLine("[SharedBuildingLayer] Seed: building region unavailable.");
            return;
        }

        BuildingRegionStart = start;
        BuildingRegionLength = length;
        CachedBytes = _dataProvider.GetBytesCopy();

        if (CachedBytes == null || CachedBytes.Length < BuildingRegionStart + HeaderSize)
        {
            Debug.WriteLine("[SharedBuildingLayer] Seed: block too short for header.");
            ClearCache();
            return;
        }

        int hdr = BuildingRegionStart;
        ushort nextBuildings = ReadU16(CachedBytes, hdr + 2);
        ushort nextFacets = ReadU16(CachedBytes, hdr + 4);
        TotalBuildings = Math.Max(0, nextBuildings - 1);
        TotalFacets = Math.Max(0, nextFacets - 1);

        FacetsOffsetAbsolute = hdr + HeaderSize + TotalBuildings * DBuildingSize + AfterBuildingsPad;

        long facetsEnd = (long)FacetsOffsetAbsolute + (long)TotalFacets * DFacetSize;
        if (FacetsOffsetAbsolute < BuildingRegionStart || facetsEnd > (BuildingRegionStart + BuildingRegionLength))
        {
            Debug.WriteLine("[SharedBuildingLayer] Seed: bad facet bounds.");
            ClearCache();
            return;
        }

        Debug.WriteLine($"[SharedBuildingLayer] Seed OK. buildings={TotalBuildings} facets={TotalFacets}");
    }

    #endregion

    #region Rendering

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (CachedBytes == null)
        {
            SeedFromProvider();
            if (CachedBytes == null) return;
        }

        if (FacetsOffsetAbsolute < 0 || TotalFacets <= 0) return;

        int drawn = 0;
        for (int i = 0; i < TotalFacets; i++)
        {
            int off = FacetsOffsetAbsolute + i * DFacetSize;
            if (off + DFacetSize > CachedBytes.Length) break;

            byte facetType = CachedBytes[off + 0];
            byte x0 = CachedBytes[off + 2];
            byte x1 = CachedBytes[off + 3];
            byte z0 = CachedBytes[off + 8];
            byte z1 = CachedBytes[off + 9];

            if ((x0 | x1 | z0 | z1) == 0) continue;

            // Convert to UI coordinates (origin flip)
            var p1 = new Point((128 - x0) * 64.0, (128 - z0) * 64.0);
            var p2 = new Point((128 - x1) * 64.0, (128 - z1) * 64.0);

            // Allow derived classes to customize rendering
            RenderFacet(dc, i, facetType, p1, p2);
            drawn++;
        }

        Debug.WriteLine($"[SharedBuildingLayer] Render drew {drawn} facet segments.");
    }

    /// <summary>
    /// Render a single facet. Override in derived classes to add selection highlighting, etc.
    /// </summary>
    protected virtual void RenderFacet(DrawingContext dc, int index, byte facetType, Point p1, Point p2)
    {
        dc.DrawLine(GetPenForFacet(facetType), p1, p2);
    }

    #endregion

    #region Utilities

    protected static ushort ReadU16(byte[] b, int off)
        => (ushort)(b[off + 0] | (b[off + 1] << 8));

    /// <summary>
    /// Get the appropriate pen for a facet type.
    /// </summary>
    public static Pen GetPenForFacet(byte facetType) => facetType switch
    {
        3 => PenWallGreen,           // Wall
        9 => PenCableRed,            // Cable
        10 or 11 or 13 => PenFenceYellow, // Fence variants
        12 => PenLadderOrange,       // Ladder
        18 or 19 or 21 => PenDoorPurple,  // Door variants
        2 or 4 => PenRoofBlue,       // Roof
        15 or 16 or 17 => PenInsideGray,  // Inside variants
        _ => PenDefault
    };

    #endregion
}
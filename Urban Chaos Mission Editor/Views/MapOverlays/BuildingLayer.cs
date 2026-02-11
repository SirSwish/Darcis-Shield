using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using UrbanChaosMissionEditor.Models;
using UrbanChaosMissionEditor.Services;

namespace UrbanChaosMissionEditor.Views.MapOverlays;

/// <summary>
/// Visualises building facets as colored lines (read-only display).
/// Uses the V1 building format from the Light Editor.
/// </summary>
public sealed class BuildingLayer : FrameworkElement
{
    // Layout constants (V1 format)
    private const int HeaderSize = 48;
    private const int DBuildingSize = 24;
    private const int AfterBuildingsPad = 14;
    private const int DFacetSize = 26;

    // Pens for different facet types
    private static readonly Pen PenWallGreen;
    private static readonly Pen PenCableRed;
    private static readonly Pen PenFenceYellow;
    private static readonly Pen PenLadderOrange;
    private static readonly Pen PenDoorPurple;
    private static readonly Pen PenRoofBlue;
    private static readonly Pen PenInsideGray;
    private static readonly Pen PenDefault;

    static BuildingLayer()
    {
        static Pen Make(Pen p)
        {
            p.StartLineCap = PenLineCap.Round;
            p.EndLineCap = PenLineCap.Round;
            p.LineJoin = PenLineJoin.Round;
            p.Freeze();
            return p;
        }

        var fluoroGreen = new SolidColorBrush(Color.FromRgb(102, 255, 0));
        fluoroGreen.Freeze();

        PenWallGreen = Make(new Pen(fluoroGreen, 6.0));
        PenCableRed = Make(new Pen(Brushes.Red, 6.0));
        PenFenceYellow = Make(new Pen(Brushes.Yellow, 5.0));
        PenLadderOrange = Make(new Pen(Brushes.Orange, 8.0));
        PenDoorPurple = Make(new Pen(Brushes.MediumPurple, 7.0));
        PenRoofBlue = Make(new Pen(Brushes.DeepSkyBlue, 4.5));
        PenInsideGray = Make(new Pen(Brushes.SlateGray, 4.5));
        PenDefault = Make(new Pen(fluoroGreen, 4.5));
    }

    // Cached data
    private byte[]? _cachedBytes;
    private int _bStart = -1;
    private int _bLen = 0;
    private int _facetsOffsetAbs = -1;
    private int _totalBuildings = 0;
    private int _totalFacets = 0;

    public BuildingLayer()
    {
        Width = MapConstants.MapPixelSize;
        Height = MapConstants.MapPixelSize;
        IsHitTestVisible = false;

        var svc = ReadOnlyMapDataService.Instance;
        svc.MapLoaded += (_, __) => { SeedFromService(); Dispatcher.Invoke(InvalidateVisual); };
        svc.MapCleared += (_, __) => { ClearCache(); Dispatcher.Invoke(InvalidateVisual); };
    }

    protected override Size MeasureOverride(Size availableSize)
        => new(MapConstants.MapPixelSize, MapConstants.MapPixelSize);

    public void Refresh()
    {
        SeedFromService();
        InvalidateVisual();
    }

    private void ClearCache()
    {
        _cachedBytes = null;
        _bStart = -1;
        _bLen = 0;
        _facetsOffsetAbs = -1;
        _totalBuildings = 0;
        _totalFacets = 0;
    }

    private void SeedFromService()
    {
        ClearCache();

        var svc = ReadOnlyMapDataService.Instance;
        if (!svc.IsLoaded)
        {
            Debug.WriteLine("[BuildingLayer] Seed: no map loaded.");
            return;
        }

        svc.ComputeAndCacheBuildingRegion();
        if (!svc.TryGetBuildingRegion(out _bStart, out _bLen))
        {
            Debug.WriteLine("[BuildingLayer] Seed: building region unavailable.");
            return;
        }

        _cachedBytes = svc.GetBytesCopy();

        int hdr = _bStart;
        if (_cachedBytes.Length < hdr + HeaderSize)
        {
            Debug.WriteLine("[BuildingLayer] Seed: block too short for header.");
            ClearCache();
            return;
        }

        ushort nextBuildings = ReadU16(_cachedBytes, hdr + 2);
        ushort nextFacets = ReadU16(_cachedBytes, hdr + 4);
        _totalBuildings = Math.Max(0, nextBuildings - 1);
        _totalFacets = Math.Max(0, nextFacets - 1);

        _facetsOffsetAbs = hdr + HeaderSize + _totalBuildings * DBuildingSize + AfterBuildingsPad;

        long facetsEnd = (long)_facetsOffsetAbs + (long)_totalFacets * DFacetSize;
        if (_facetsOffsetAbs < _bStart || facetsEnd > (_bStart + _bLen))
        {
            Debug.WriteLine($"[BuildingLayer] Seed: bad facet bounds.");
            ClearCache();
            return;
        }

        Debug.WriteLine($"[BuildingLayer] Seed OK. buildings={_totalBuildings} facets={_totalFacets}");
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_cachedBytes is null)
        {
            SeedFromService();
            if (_cachedBytes is null) return;
        }
        if (_facetsOffsetAbs < 0 || _totalFacets <= 0) return;

        int drawn = 0;
        for (int i = 0; i < _totalFacets; i++)
        {
            int off = _facetsOffsetAbs + i * DFacetSize;
            if (off + DFacetSize > _cachedBytes.Length) break;

            byte facetType = _cachedBytes[off + 0];
            byte x0 = _cachedBytes[off + 2];
            byte x1 = _cachedBytes[off + 3];
            byte z0 = _cachedBytes[off + 8];
            byte z1 = _cachedBytes[off + 9];
            if ((x0 | x1 | z0 | z1) == 0) continue;

            var p1 = new Point((128 - x0) * 64.0, (128 - z0) * 64.0);
            var p2 = new Point((128 - x1) * 64.0, (128 - z1) * 64.0);
            dc.DrawLine(GetPenForFacet(facetType), p1, p2);
            drawn++;
        }

        Debug.WriteLine($"[BuildingLayer] Render drew {drawn} facet segments.");
    }

    private static ushort ReadU16(byte[] b, int off)
        => (ushort)(b[off + 0] | (b[off + 1] << 8));

    private static Pen GetPenForFacet(byte type) => type switch
    {
        3 => PenWallGreen,
        9 => PenCableRed,
        10 or 11 or 13 => PenFenceYellow,
        12 => PenLadderOrange,
        18 or 19 or 21 => PenDoorPurple,
        2 or 4 => PenRoofBlue,
        15 or 16 or 17 => PenInsideGray,
        _ => PenDefault
    };
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using UrbanChaosMapEditor.Models.Buildings;
using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Buildings;

namespace UrbanChaosMapEditor.ViewModels.Buildings
{
    public sealed class BuildingBrowserViewModel
    {
        public ObservableCollection<BuildingVM> Buildings { get; } = new();

        // NEW: flat list of all cable facets extracted from BuildingArrays.Cables
        public ObservableCollection<CableFacetVM> Cables { get; } = new();

        public BuildingBrowserViewModel()
        {
            MapDataService.Instance.MapLoaded += (_, __) => Refresh();
            MapDataService.Instance.MapBytesReset += (_, __) => Refresh();
            MapDataService.Instance.MapCleared += (_, __) =>
            {
                Buildings.Clear();
                Cables.Clear();
            };
        }

        public void Refresh()
        {
            Buildings.Clear();
            Cables.Clear();

            var arrays = new BuildingsAccessor(MapDataService.Instance).ReadSnapshot();
            if (arrays is null) return;

            // guard if nothing at all
            if (arrays.Facets.Length == 0 &&
                arrays.Buildings.Length == 0 &&
                arrays.Cables.Length == 0)
            {
                return;
            }

            // ----- Buildings ? Storey/Facet tree (unchanged logic) -----
            // Facet ids in-file are 1-based; VM uses 0-based indices for arrays and shows 1-based IDs in UI.
            for (int b = 0; b < arrays.Buildings.Length; b++)
            {
                var br = arrays.Buildings[b];
                int start = Math.Max(1, (int)br.StartFacet); // ensure >=1
                int end = Math.Max(start, (int)br.EndFacet);

                // Clip to real facet count
                start = Math.Min(start, arrays.Facets.Length);
                end = Math.Min(end, arrays.Facets.Length);

                var facets = new List<(int id, DFacetRec f)>();
                for (int id1 = start; id1 < end; id1++)
                {
                    int idx0 = id1 - 1; // file 1-based ? array 0-based
                    facets.Add((id1, arrays.Facets[idx0]));
                }

                var bvm = new BuildingVM(b + 1, br.StartFacet, br.EndFacet, facets);
                Buildings.Add(bvm);
            }

            // ----- NEW: cables list -----
            // arrays.Cables is filled by BuildingsAccessor from DFacetRec entries of type FacetType.Cable
            foreach (var c in arrays.Cables)
            {
                Cables.Add(new CableFacetVM(c));
            }
        }
    }

    public sealed class BuildingVM
    {
        public int Id { get; }
        public int StartFacet1 { get; }  // 1-based from file
        public int EndFacet1 { get; }    // 1-based exclusive
        public int FacetCount { get; }
        public ObservableCollection<StoreyVM> Storeys { get; } = new();

        public BuildingVM(int id, int startFacet1, int endFacet1, IEnumerable<(int id1, DFacetRec f)> facets)
        {
            Id = id;
            StartFacet1 = startFacet1;
            EndFacet1 = endFacet1;

            // group by storey, then by type
            var byStorey = facets.GroupBy(t => t.f.Storey);

            foreach (var grp in byStorey.OrderBy(g => g.Key))
            {
                var svm = new StoreyVM((int)grp.Key);
                var byType = grp.GroupBy(t => t.f.Type);

                foreach (var tg in byType.OrderBy(g => g.Key))
                {
                    var gvm = new FacetTypeGroupVM(tg.Key);
                    foreach (var (id1, f) in tg)
                    {
                        gvm.Facets.Add(new FacetVM(id1, f));
                    }
                    svm.Groups.Add(gvm);
                }

                Storeys.Add(svm);
            }

            FacetCount = Storeys.SelectMany(s => s.Groups).Sum(g => g.Facets.Count);
        }
    }

    public sealed class StoreyVM
    {
        public int StoreyId { get; }
        public ObservableCollection<FacetTypeGroupVM> Groups { get; } = new();
        public int UsageCount => Groups.Sum(g => g.Facets.Count);
        public StoreyVM(int id) { StoreyId = id; }
    }

    public sealed class FacetTypeGroupVM
    {
        public FacetType Type { get; }
        public string TypeName => Type.ToString();
        public ObservableCollection<FacetVM> Facets { get; } = new();
        public int Count => Facets.Count;
        public FacetTypeGroupVM(FacetType type) { Type = type; }
    }

    public sealed class FacetVM
    {
        public int FacetId1 { get; }     // 1-based id in file
        public FacetType Type { get; }
        public string Coords => $"({X0},{Z0}) ? ({X1},{Z1})";
        public byte X0 { get; }
        public byte Z0 { get; }
        public byte X1 { get; }
        public byte Z1 { get; }
        public byte Height { get; }
        public byte FHeight { get; }
        public ushort StyleIndex { get; }
        public FacetFlags Flags { get; }

        public FacetVM(int id1, DFacetRec f)
        {
            FacetId1 = id1;
            Type = f.Type;
            X0 = f.X0;
            Z0 = f.Z0;
            X1 = f.X1;
            Z1 = f.Z1;
            Height = f.Height;
            FHeight = f.FHeight;
            StyleIndex = f.StyleIndex;
            Flags = f.Flags;
        }
    }

    // NEW: a simple VM to represent a single cable row in the cables list.
    public sealed class CableFacetVM
    {
        public int FacetId1 { get; }
        public int BuildingIndex { get; }

        // World endpoints
        public int WorldX1 { get; }
        public int WorldY1 { get; }
        public int WorldZ1 { get; }
        public int WorldX2 { get; }
        public int WorldY2 { get; }
        public int WorldZ2 { get; }

        public int SegmentCount { get; }

        public short SagBase { get; }
        public short SagAngleDelta1 { get; }
        public short SagAngleDelta2 { get; }

        public string Endpoints =>
            $"({WorldX1},{WorldY1},{WorldZ1}) ? ({WorldX2},{WorldY2},{WorldZ2})";

        public CableFacetVM(CableFacet c)
        {
            FacetId1 = c.FacetIndex;
            BuildingIndex = c.BuildingIndex;
            WorldX1 = c.WorldX1;
            WorldY1 = c.WorldY1;
            WorldZ1 = c.WorldZ1;
            WorldX2 = c.WorldX2;
            WorldY2 = c.WorldY2;
            WorldZ2 = c.WorldZ2;
            SegmentCount = c.SegmentCount;
            SagBase = c.SagBase;
            SagAngleDelta1 = c.SagAngleDelta1;
            SagAngleDelta2 = c.SagAngleDelta2;
        }
    }
}

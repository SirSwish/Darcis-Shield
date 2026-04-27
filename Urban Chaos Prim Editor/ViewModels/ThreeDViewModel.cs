// ViewModels/ThreeDViewModel.cs
// Owns the 3D scene shown in the viewport. The MainWindow's <ModelVisual3D>
// binds its Content to Scene. In editable mode the scene also contains
// per-point sphere markers; the view layer uses MarkerToPointId to
// hit-test back to point identifiers.

using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using UrbanChaosEditor.Shared.ViewModels;
using UrbanChaosPrimEditor.Models;
using UrbanChaosPrimEditor.Services;

namespace UrbanChaosPrimEditor.ViewModels
{
    public sealed class ThreeDViewModel : BaseViewModel
    {
        private readonly PrmMeshBuilderService _meshBuilder;

        private static readonly IReadOnlyDictionary<GeometryModel3D, int> EmptyMarkerToId =
            new Dictionary<GeometryModel3D, int>();
        private static readonly IReadOnlyDictionary<int, GeometryModel3D> EmptyIdToMarker =
            new Dictionary<int, GeometryModel3D>();

        public ThreeDViewModel(PrmMeshBuilderService meshBuilder)
        {
            _meshBuilder = meshBuilder;
            _scene = new Model3DGroup();
            _markerToPointId = EmptyMarkerToId;
            _pointIdToMarker = EmptyIdToMarker;
        }

        // ── Scene ────────────────────────────────────────────────────────────

        private Model3DGroup _scene;
        public Model3DGroup Scene
        {
            get => _scene;
            private set { _scene = value; RaisePropertyChanged(); }
        }

        // Half-diagonal of the model's bounding box — used by MainWindow to auto-fit the camera.
        private double _modelRadius = 200.0;
        public double ModelRadius
        {
            get => _modelRadius;
            private set { _modelRadius = value; RaisePropertyChanged(); }
        }

        // ── Marker lookups (for hit-testing) ─────────────────────────────────

        private IReadOnlyDictionary<GeometryModel3D, int> _markerToPointId;
        public IReadOnlyDictionary<GeometryModel3D, int> MarkerToPointId => _markerToPointId;

        private IReadOnlyDictionary<int, GeometryModel3D> _pointIdToMarker;
        public IReadOnlyDictionary<int, GeometryModel3D> PointIdToMarker => _pointIdToMarker;

        // ── Build / rebuild ──────────────────────────────────────────────────

        /// <summary>Initial load — also recomputes ModelRadius so the camera can refit.</summary>
        public void LoadModel(PrmModel model, int? selectedPointId, IReadOnlyCollection<int>? faceBuildIds, SelectedFaceHint? selectedFace = null)
        {
            PrmEditScene result = _meshBuilder.BuildEditable(model, selectedPointId, faceBuildIds, selectedFace);
            ApplyResult(result);

            Rect3D b = result.Group.Bounds;
            if (!b.IsEmpty)
            {
                double halfDiag = Math.Sqrt(b.SizeX * b.SizeX + b.SizeY * b.SizeY + b.SizeZ * b.SizeZ) / 2.0;
                ModelRadius = Math.Max(halfDiag, 50.0);
            }
            else
            {
                ModelRadius = 200.0;
            }
        }

        /// <summary>Rebuild after an edit — leaves ModelRadius (camera fit) alone.</summary>
        public void Rebuild(PrmModel model, int? selectedPointId, IReadOnlyCollection<int>? faceBuildIds, SelectedFaceHint? selectedFace = null)
        {
            PrmEditScene result = _meshBuilder.BuildEditable(model, selectedPointId, faceBuildIds, selectedFace);
            ApplyResult(result);
        }

        public void Clear()
        {
            Scene = new Model3DGroup();
            _markerToPointId = EmptyMarkerToId;
            _pointIdToMarker = EmptyIdToMarker;
            ModelRadius = 200.0;
        }

        private void ApplyResult(PrmEditScene result)
        {
            Scene = result.Group;
            _markerToPointId = result.MarkerToPointId;
            _pointIdToMarker = result.PointIdToMarker;
        }
    }
}

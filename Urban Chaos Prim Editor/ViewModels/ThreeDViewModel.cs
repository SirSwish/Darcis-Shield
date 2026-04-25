// ViewModels/ThreeDViewModel.cs
// Owns the 3D scene shown in the viewport. The MainWindow's <ModelVisual3D>
// binds its Content to Scene.

using System;
using System.Windows.Media.Media3D;
using UrbanChaosEditor.Shared.ViewModels;
using UrbanChaosPrimEditor.Models;
using UrbanChaosPrimEditor.Services;

namespace UrbanChaosPrimEditor.ViewModels
{
    public sealed class ThreeDViewModel : BaseViewModel
    {
        private readonly PrmMeshBuilderService _meshBuilder;

        public ThreeDViewModel(PrmMeshBuilderService meshBuilder)
        {
            _meshBuilder = meshBuilder;
            _scene = new Model3DGroup();
        }

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

        /// <summary>Build a fresh Model3DGroup for the given parsed PRM and assign to <see cref="Scene"/>.</summary>
        public void LoadModel(PrmModel model)
        {
            var group = _meshBuilder.Build(model);
            Scene = group;

            Rect3D b = group.Bounds;
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

        public void Clear()
        {
            Scene = new Model3DGroup();
            ModelRadius = 200.0;
        }
    }
}

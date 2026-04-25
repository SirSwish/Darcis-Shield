using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace UrbanChaosPrimEditor.Models
{
    public sealed class PrmRenderMesh
    {
        public int TextureId { get; init; }
        public MeshGeometry3D Mesh { get; init; } = new();
        public Material Material { get; init; } = new DiffuseMaterial(Brushes.LightGray);
    }
}

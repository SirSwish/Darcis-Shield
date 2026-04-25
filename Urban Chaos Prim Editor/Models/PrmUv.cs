using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanChaosPrimEditor.Models
{
    public readonly record struct PrmUv(double U, double V);

    public readonly record struct PrmTriangleUv(int TextureId, PrmUv A, PrmUv B, PrmUv C);

    public readonly record struct PrmQuadUv(int TextureId, PrmUv A, PrmUv B, PrmUv C, PrmUv D);
}

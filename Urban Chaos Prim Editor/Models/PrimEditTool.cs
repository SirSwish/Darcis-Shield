// Models/PrimEditTool.cs
// The active editing tool in the Prim Editor's 3D viewport.

namespace UrbanChaosPrimEditor.Models
{
    public enum PrimEditTool
    {
        /// <summary>Default tool — clicking a point selects it; no movement.</summary>
        Select,

        /// <summary>Click-and-drag a point to move it on the camera-facing plane.</summary>
        MovePoint,

        /// <summary>Clicking empty space adds a new point at the projected location.</summary>
        AddPoint,

        /// <summary>Click 3 existing points to create a new triangle face.</summary>
        NewTriangle,

        /// <summary>Click 4 existing points to create a new quadrangle face.</summary>
        NewQuad,

        /// <summary>Click a point to delete it (along with any face that references it).</summary>
        DeletePoint,
    }
}

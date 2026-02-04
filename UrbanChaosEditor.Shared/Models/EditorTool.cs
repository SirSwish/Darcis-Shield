// /Models/EditorTool.cs
namespace UrbanChaosMapEditor.Models
{
    public enum EditorTool
    {
        None,

        // Terrain Heights (vertex height offsets)
        RaiseHeight,
        LowerHeight,
        LevelHeight,
        FlattenHeight,
        DitchTemplate,

        // Cell Altitude (floor level)
        SetAltitude,       // Set cell altitude to target value
        SampleAltitude,    // Read cell altitude into target value
        ResetAltitude,     // Reset cell altitude to 0

        // Roof Building
        DetectRoof,        // Detect closed shapes near click point

        // Textures
        PaintTexture,

        // Future expansion
        PlacePrim,
        PlaceBuilding,
        PlaceLight
    }
}
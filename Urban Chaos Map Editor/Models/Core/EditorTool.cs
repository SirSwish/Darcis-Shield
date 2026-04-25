// /Models/EditorTool.cs
namespace UrbanChaosMapEditor.Models.Core
{
    public enum EditorTool
    {
        None,

        // Terrain Heights (vertex height offsets)
        RaiseHeight,
        AreaSetHeight,
        RandomizeHeightArea,   // drag rectangle to randomise terrain in area
        LowerHeight,
        LevelHeight,
        FlattenHeight,
        DitchTemplate,     // kept for backward-compat; StampHeight supersedes it
        StampHeight,       // apply currently-selected HeightStamp at click point

        // Cell Altitude (floor level)
        SetAltitude,       // Set cell altitude to target value
        SampleAltitude,    // Read cell altitude into target value
        ResetAltitude,     // Reset cell altitude to 0
        AreaSetPapFlags,

        // Roof Building
        DetectRoof,        // Detect closed shapes near click point

        // Textures
        PaintTexture,
        EyedropTexture,
        SelectTextureArea,  // drag to select a rect of cells; stays highlighted until cancelled
        PasteTexture,       // paste clipboard cells — click to place at cursor top-left

        // Roof textures (.MAP layer)
        PaintRoofTexture,   // paint into the 128x128 warehouse roof texture array

        // Future expansion
        PlacePrim,
        PlaceBuilding,
        MoveBuilding,    // Drag-place a captured building snapshot at a new position
    }
}
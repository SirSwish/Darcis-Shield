using UrbanChaosMapEditor.Services.Core;
using UrbanChaosMapEditor.Services.Heights;
using UrbanChaosMapEditor.Services.Roofs;
using System.Linq;

namespace UrbanChaosMapEditor.Services.Prims
{
    public static class FloorSnapService
    {
        /// <summary>
        /// Calculates the snapped Y value for a prim based on the terrain vertex heights
        /// and cell altitude at the prim's position. Returns the higher of the two sources
        /// multiplied by 8 (1 height unit = 8 Y pixels).
        /// Returns 0 if map data is not loaded.
        /// </summary>
        public static short CalculateSnapY(int mapWhoIndex, byte primX, byte primZ)
        {
            if (!MapDataService.Instance.IsLoaded)
                return 0;

            int gameCol = mapWhoIndex / 32;
            int gameRow = mapWhoIndex % 32;
            int uiCol   = 31 - gameCol;
            int uiRow   = 31 - gameRow;
            int tileX   = uiCol * 4 + (255 - primX) / 64;
            int tileZ   = uiRow * 4 + (255 - primZ) / 64;

            tileX = Math.Clamp(tileX, 0, 127);
            tileZ = Math.Clamp(tileZ, 0, 127);

            var altAcc = new AltitudeAccessor(MapDataService.Instance);
            var hgtAcc = new HeightsAccessor(MapDataService.Instance);

            int altEffective = altAcc.ReadAltRaw(tileX, tileZ) * (1 << AltitudeAccessor.PAP_ALT_SHIFT);

            int tx1 = Math.Clamp(tileX + 1, 0, 127);
            int tz1 = Math.Clamp(tileZ + 1, 0, 127);
            int v00 = hgtAcc.ReadHeight(tileX, tileZ);
            int v10 = hgtAcc.ReadHeight(tx1,   tileZ);
            int v01 = hgtAcc.ReadHeight(tileX, tz1);
            int v11 = hgtAcc.ReadHeight(tx1,   tz1);

            // For negative terrain: exclude zeros so altitude=0 (unset) doesn't win over
            // a negative vertex height. Among remaining values, Max gives the one closest to 0.
            int[] candidates = new[] { altEffective, v00, v10, v01, v11 };
            int[] nonZero = candidates.Where(v => v != 0).ToArray();
            int floorHeight = nonZero.Length > 0 ? nonZero.Max() : 0;

            return (short)(floorHeight * 8);
        }
    }
}

// /Services/ObjectSpace.cs
// File: Models/ObjectSpace.cs
using System;

namespace UrbanChaosMapEditor.Models
{
    public static class ObjectSpace
    {
        public const int CellsPerSide = 32;
        public const int PixelsPerCell = 256;

        // Game grid (row,col) has origin at bottom-right; UI has origin at top-left.
        public static void GameCellToUiRowCol(int gameRow, int gameCol, out int uiRow, out int uiCol)
        {
            uiRow = (CellsPerSide - 1) - gameRow;
            uiCol = (CellsPerSide - 1) - gameCol;
        }

        public static void GameIndexToUiRowCol(int mapWhoIndex, out int uiRow, out int uiCol)
        {
            // DISK: index is column-major -> col = index / 32, row = index % 32
            int gameCol = mapWhoIndex / CellsPerSide;
            int gameRow = mapWhoIndex % CellsPerSide;

            GameCellToUiRowCol(gameRow, gameCol, out uiRow, out uiCol);
        }

        // >>> The important bit: invert X and Z inside the 256×256 cell <<<
        public static void GamePrimToUiPixels(int mapWhoIndex, byte gameX, byte gameZ, out int uiPixelX, out int uiPixelZ)
        {
            GameIndexToUiRowCol(mapWhoIndex, out int uiRow, out int uiCol);
            uiPixelX = uiCol * PixelsPerCell + (255 - gameX);
            uiPixelZ = uiRow * PixelsPerCell + (255 - gameZ);
        }

        // Inverse mapping for drag: UI pixels -> game MapWho index + (X,Z) in 0..255 from bottom-right
        public static void UiPixelsToGamePrim(int uiPixelX, int uiPixelZ, out int mapWhoIndex, out byte gameX, out byte gameZ)
        {
            int uiCol = Math.Clamp(uiPixelX / PixelsPerCell, 0, CellsPerSide - 1);
            int uiRow = Math.Clamp(uiPixelZ / PixelsPerCell, 0, CellsPerSide - 1);

            int gameRow = (CellsPerSide - 1) - uiRow;
            int gameCol = (CellsPerSide - 1) - uiCol;
            mapWhoIndex = gameCol * CellsPerSide + gameRow;

            int uRelX = uiPixelX % PixelsPerCell;
            int uRelZ = uiPixelZ % PixelsPerCell;

            gameX = (byte)(255 - uRelX);
            gameZ = (byte)(255 - uRelZ);
        }
    }
}

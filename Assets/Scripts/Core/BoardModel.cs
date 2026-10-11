using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    /// <summary>Pure game-board state: an 8x8 grid of colour ids (Empty = -1).</summary>
    public sealed class BoardModel
    {
        public const int Size = 8;
        public const int Empty = -1;

        readonly int[,] cells = new int[Size, Size];

        public BoardModel() => Reset();

        public int this[int x, int y] => cells[x, y];

        public void Reset()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    cells[x, y] = Empty;
        }

        public static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        public bool IsFilled(int x, int y) => InBounds(x, y) && cells[x, y] != Empty;

        public void Set(int x, int y, int color) => cells[x, y] = color;

        public bool IsEmptyBoard()
        {
            foreach (var c in cells) if (c != Empty) return false;
            return true;
        }

        public bool CanPlace(PieceShape shape, int ox, int oy)
        {
            foreach (var c in shape.Cells)
            {
                int x = ox + c.x, y = oy + c.y;
                if (!InBounds(x, y) || cells[x, y] != Empty) return false;
            }
            return true;
        }

        public bool CanPlaceAnywhere(PieceShape shape)
        {
            for (int x = 0; x <= Size - shape.Width; x++)
                for (int y = 0; y <= Size - shape.Height; y++)
                    if (CanPlace(shape, x, y)) return true;
            return false;
        }

        public void Place(PieceShape shape, int ox, int oy, int color)
        {
            foreach (var c in shape.Cells) cells[ox + c.x, oy + c.y] = color;
        }

        /// <summary>Rows / columns that are completely filled. Optionally pretends a piece is placed first.</summary>
        public void FindFullLines(List<int> rows, List<int> cols, PieceShape shape = null, int ox = 0, int oy = 0)
        {
            rows.Clear();
            cols.Clear();
            for (int y = 0; y < Size; y++)
            {
                bool full = true;
                for (int x = 0; x < Size && full; x++) full = cells[x, y] != Empty || Covers(shape, ox, oy, x, y);
                if (full) rows.Add(y);
            }
            for (int x = 0; x < Size; x++)
            {
                bool full = true;
                for (int y = 0; y < Size && full; y++) full = cells[x, y] != Empty || Covers(shape, ox, oy, x, y);
                if (full) cols.Add(x);
            }
        }

        static bool Covers(PieceShape shape, int ox, int oy, int x, int y)
        {
            if (shape == null) return false;
            foreach (var c in shape.Cells)
                if (ox + c.x == x && oy + c.y == y) return true;
            return false;
        }

        /// <summary>Empties the given lines and returns every distinct cell that was cleared.</summary>
        public List<Vector2Int> ClearLines(List<int> rows, List<int> cols)
        {
            var cleared = new List<Vector2Int>();
            foreach (int y in rows)
                for (int x = 0; x < Size; x++)
                    if (cells[x, y] != Empty) { cleared.Add(new Vector2Int(x, y)); cells[x, y] = Empty; }
            foreach (int x in cols)
                for (int y = 0; y < Size; y++)
                    if (cells[x, y] != Empty) { cleared.Add(new Vector2Int(x, y)); cells[x, y] = Empty; }
            return cleared;
        }
    }
}

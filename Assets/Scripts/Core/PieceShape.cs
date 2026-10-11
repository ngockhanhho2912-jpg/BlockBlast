using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    /// <summary>Immutable polyomino. Cells use (x right, y down) with origin at top-left of the bounding box.</summary>
    public sealed class PieceShape
    {
        public readonly Vector2Int[] Cells;
        public readonly int Width;
        public readonly int Height;
        public readonly int Weight;

        public PieceShape(int weight, params string[] rows)
        {
            Weight = weight;
            Height = rows.Length;
            var cells = new List<Vector2Int>();
            for (int y = 0; y < rows.Length; y++)
            {
                Width = Mathf.Max(Width, rows[y].Length);
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == 'X') cells.Add(new Vector2Int(x, y));
            }
            Cells = cells.ToArray();
        }
    }

    public static class ShapeLibrary
    {
        public static readonly PieceShape Single = new PieceShape(2, "X");

        public static readonly PieceShape[] All =
        {
            Single,
            // lines
            new PieceShape(3, "XX"), new PieceShape(3, "X", "X"),
            new PieceShape(3, "XXX"), new PieceShape(3, "X", "X", "X"),
            new PieceShape(2, "XXXX"), new PieceShape(2, "X", "X", "X", "X"),
            new PieceShape(1, "XXXXX"), new PieceShape(1, "X", "X", "X", "X", "X"),
            // squares / rectangles
            new PieceShape(3, "XX", "XX"),
            new PieceShape(1, "XXX", "XXX", "XXX"),
            new PieceShape(1, "XX", "XX", "XX"), new PieceShape(1, "XXX", "XXX"),
            // small corners
            new PieceShape(2, "X.", "XX"), new PieceShape(2, ".X", "XX"),
            new PieceShape(2, "XX", "X."), new PieceShape(2, "XX", ".X"),
            // L / J
            new PieceShape(1, "X.", "X.", "XX"), new PieceShape(1, ".X", ".X", "XX"),
            new PieceShape(1, "XX", "X.", "X."), new PieceShape(1, "XX", ".X", ".X"),
            new PieceShape(1, "XXX", "X.."), new PieceShape(1, "XXX", "..X"),
            new PieceShape(1, "X..", "XXX"), new PieceShape(1, "..X", "XXX"),
            // T
            new PieceShape(1, "XXX", ".X."), new PieceShape(1, ".X.", "XXX"),
            new PieceShape(1, "X.", "XX", "X."), new PieceShape(1, ".X", "XX", ".X"),
            // S / Z
            new PieceShape(1, ".XX", "XX."), new PieceShape(1, "XX.", ".XX"),
            new PieceShape(1, "X.", "XX", ".X"), new PieceShape(1, ".X", "XX", "X."),
            // big corners
            new PieceShape(1, "X..", "X..", "XXX"), new PieceShape(1, "..X", "..X", "XXX"),
            new PieceShape(1, "XXX", "X..", "X.."), new PieceShape(1, "XXX", "..X", "..X"),
            // diagonals
            new PieceShape(1, "X.", ".X"), new PieceShape(1, ".X", "X."),
        };

        static int totalWeight;

        public static PieceShape Random(System.Random rng)
        {
            if (totalWeight == 0)
                foreach (var s in All) totalWeight += s.Weight;
            int r = rng.Next(totalWeight);
            foreach (var s in All)
            {
                if (r < s.Weight) return s;
                r -= s.Weight;
            }
            return All[0];
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessboardTactics.Core
{
    /// <summary>The grid, its impassable rocks and the units standing on it.</summary>
    public sealed class Board
    {
        readonly HashSet<Coord> rocks;
        readonly List<Unit> units = new List<Unit>();

        public Board(int width, int height, IEnumerable<Coord> rocks = null)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The board needs at least one tile.");
            }
            Width = width;
            Height = height;
            this.rocks = new HashSet<Coord>(rocks ?? Enumerable.Empty<Coord>());
        }

        public int Width { get; }
        public int Height { get; }

        public IReadOnlyCollection<Coord> Rocks => rocks;

        /// <summary>Living units, in the order they were added.</summary>
        public IEnumerable<Unit> Units => units.Where(u => u.IsAlive);

        public IEnumerable<Unit> UnitsOf(Team team) => Units.Where(u => u.Team == team);

        public bool InBounds(Coord c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

        public bool IsRock(Coord c) => rocks.Contains(c);

        /// <summary>On the board and not a rock.</summary>
        public bool IsWalkable(Coord c) => InBounds(c) && !IsRock(c);

        public Unit UnitAt(Coord c) => units.FirstOrDefault(u => u.IsAlive && u.Position == c);

        /// <summary>Walkable and empty: a unit may end its move here.</summary>
        public bool IsFree(Coord c) => IsWalkable(c) && UnitAt(c) == null;

        public Unit Find(int id) => units.FirstOrDefault(u => u.Id == id);

        public Unit Add(Team team, PieceType type, Coord position)
        {
            if (!IsFree(position))
            {
                throw new InvalidOperationException($"{position} is not free.");
            }
            var unit = new Unit(units.Count + 1, team, type, position);
            units.Add(unit);
            return unit;
        }

        public IEnumerable<Coord> AllTiles()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return new Coord(x, y);
                }
            }
        }
    }
}

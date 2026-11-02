using System.Collections.Generic;
using System.Linq;

namespace ChessboardTactics.Core
{
    /// <summary>Which tiles a unit can move to and which enemies it can strike.</summary>
    public static class Movement
    {
        static readonly Coord[] OrthogonalSteps = { new Coord(0, 1), new Coord(1, 0), new Coord(0, -1), new Coord(-1, 0) };
        static readonly Coord[] DiagonalSteps = { new Coord(1, 1), new Coord(1, -1), new Coord(-1, -1), new Coord(-1, 1) };
        static readonly Coord[] AllSteps = OrthogonalSteps.Concat(DiagonalSteps).ToArray();

        static readonly Coord[] KnightJumps =
        {
            new Coord(1, 2), new Coord(2, 1), new Coord(2, -1), new Coord(1, -2),
            new Coord(-1, -2), new Coord(-2, -1), new Coord(-2, 1), new Coord(-1, 2),
        };

        public static IReadOnlyList<Coord> Steps(Directions directions) =>
            directions == Directions.Orthogonal ? OrthogonalSteps : directions == Directions.Diagonal ? DiagonalSteps : AllSteps;

        /// <summary>Tiles the unit may end its move on, sorted rank by rank. Its own tile is not included.</summary>
        public static List<Coord> Destinations(Board board, Unit unit)
        {
            var result = new HashSet<Coord>();
            UnitStats stats = unit.Stats;
            switch (stats.MoveStyle)
            {
                case MoveStyle.Walk:
                    Walk(board, unit, stats, result);
                    break;
                case MoveStyle.Slide:
                    foreach (Coord step in Steps(stats.Directions))
                    {
                        for (int distance = 1; distance <= stats.MoveRange; distance++)
                        {
                            Coord tile = unit.Position + step * distance;
                            if (!board.IsFree(tile))
                            {
                                break;
                            }
                            result.Add(tile);
                        }
                    }
                    break;
                case MoveStyle.Jump:
                    foreach (Coord jump in KnightJumps)
                    {
                        Coord tile = unit.Position + jump;
                        if (board.IsFree(tile))
                        {
                            result.Add(tile);
                        }
                    }
                    break;
            }
            var sorted = result.ToList();
            sorted.Sort();
            return sorted;
        }

        /// <summary>Breadth-first walk: allies can be walked through but not stood on.</summary>
        static void Walk(Board board, Unit unit, UnitStats stats, HashSet<Coord> result)
        {
            var steps = Steps(stats.Directions);
            var visited = new HashSet<Coord> { unit.Position };
            var frontier = new List<Coord> { unit.Position };
            for (int distance = 1; distance <= stats.MoveRange && frontier.Count > 0; distance++)
            {
                var next = new List<Coord>();
                foreach (Coord from in frontier)
                {
                    foreach (Coord step in steps)
                    {
                        Coord tile = from + step;
                        if (!board.IsWalkable(tile) || !visited.Add(tile))
                        {
                            continue;
                        }
                        Unit occupant = board.UnitAt(tile);
                        if (occupant == null)
                        {
                            result.Add(tile);
                            next.Add(tile);
                        }
                        else if (occupant.Team == unit.Team)
                        {
                            next.Add(tile);
                        }
                    }
                }
                frontier = next;
            }
        }

        public static bool CanReach(Board board, Unit unit, Coord tile) => Destinations(board, unit).Contains(tile);

        /// <summary>Enemies the unit could strike if it stood on <paramref name="from"/>, sorted by position.</summary>
        public static List<Unit> TargetsFrom(Board board, Unit unit, Coord from)
        {
            UnitStats stats = unit.Stats;
            var targets = board.UnitsOf(unit.Team.Opponent())
                .Where(enemy => stats.InAttackRange(Coord.Chebyshev(from, enemy.Position)))
                .ToList();
            targets.Sort((a, b) => a.Position.CompareTo(b.Position));
            return targets;
        }

        public static List<Unit> Targets(Board board, Unit unit) => TargetsFrom(board, unit, unit.Position);

        /// <summary>Every tile the unit could strike from <paramref name="from"/>, whether or not an enemy stands there.</summary>
        public static IEnumerable<Coord> AttackArea(Board board, Unit unit, Coord from)
        {
            int reach = unit.Stats.MaxAttackRange;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var tile = new Coord(from.X + dx, from.Y + dy);
                    if (board.InBounds(tile) && unit.Stats.InAttackRange(Coord.Chebyshev(from, tile)))
                    {
                        yield return tile;
                    }
                }
            }
        }
    }
}

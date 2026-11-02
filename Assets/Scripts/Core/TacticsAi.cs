using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessboardTactics.Core
{
    /// <summary>One planned unit action: an optional move, then an optional attack (otherwise the unit waits).</summary>
    public sealed class AiAction
    {
        public AiAction(Unit unit, Coord destination, Unit target, int score)
        {
            Unit = unit;
            Destination = destination;
            Target = target;
            Score = score;
        }

        public Unit Unit { get; }
        /// <summary>Where the unit ends up; equal to its position when it stays.</summary>
        public Coord Destination { get; }
        /// <summary>The enemy to strike after moving, or null to wait.</summary>
        public Unit Target { get; }
        public int Score { get; }

        public bool Moves => Destination != Unit.Position;

        public override string ToString() => $"{Unit} -> {Destination}{(Target != null ? $" attacks {Target}" : " waits")} ({Score})";
    }

    /// <summary>
    /// A greedy one-ply opponent. For every unit that can still act it scores each reachable tile and
    /// each attack from it (damage, kills, the enemy king, counter-attacks, and how many enemies could
    /// strike that tile next turn), then plays the single best action. Ties go to the lowest unit id
    /// and the lowest tile, so the same position always produces the same move.
    /// </summary>
    public static class TacticsAi
    {
        /// <summary>The best action for the side whose turn it is, or null when no unit can act.</summary>
        public static AiAction ChooseNext(Match match)
        {
            if (match.IsOver)
            {
                return null;
            }
            Dictionary<Coord, int> danger = ThreatMap(match.Board, match.CurrentTeam.Opponent());
            AiAction best = null;
            foreach (Unit unit in match.PendingUnits().OrderBy(u => u.Id))
            {
                AiAction candidate = BestFor(match, unit, danger);
                if (best == null || candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>Carries out an action through the normal rules.</summary>
        public static (MoveResult Move, AttackResult Attack) Execute(Match match, AiAction action)
        {
            MoveResult move = action.Moves ? match.Move(action.Unit, action.Destination) : null;
            AttackResult attack = null;
            if (action.Target != null)
            {
                attack = match.Attack(action.Unit, action.Target);
            }
            else if (!action.Unit.HasActed)
            {
                match.Wait(action.Unit);
            }
            return (move, attack);
        }

        /// <summary>Plays every unit of the current side and ends the turn (used by tests and the demo mode).</summary>
        public static void PlayTurn(Match match)
        {
            AiAction action;
            while ((action = ChooseNext(match)) != null)
            {
                Execute(match, action);
            }
            if (!match.IsOver)
            {
                match.EndTurn();
            }
        }

        static AiAction BestFor(Match match, Unit unit, Dictionary<Coord, int> danger)
        {
            Board board = match.Board;
            var destinations = new List<Coord> { unit.Position };
            if (!unit.HasMoved)
            {
                destinations.AddRange(Movement.Destinations(board, unit));
            }
            List<Unit> enemies = board.UnitsOf(unit.Team.Opponent()).ToList();

            AiAction best = null;
            foreach (Coord tile in destinations)
            {
                int threat = danger.TryGetValue(tile, out int value) ? value : 0;
                foreach (Unit target in Movement.TargetsFrom(board, unit, tile))
                {
                    Consider(ref best, new AiAction(unit, tile, target, ScoreAttack(unit, tile, target, threat)));
                }
                if (!unit.HasMoved || tile == unit.Position)
                {
                    Consider(ref best, new AiAction(unit, tile, null, ScorePosition(board, unit, tile, threat, enemies)));
                }
            }
            return best;
        }

        static void Consider(ref AiAction best, AiAction candidate)
        {
            if (best == null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        static int ScoreAttack(Unit unit, Coord from, Unit target, int threat)
        {
            AttackForecast forecast = Combat.Forecast(unit, from, target);
            int score = 100 + forecast.Damage * 10;
            if (forecast.DefenderFalls)
            {
                score += 50 + Value(target.Type);
                if (target.Type == PieceType.King)
                {
                    score += 10000; // Winning the battle beats everything else.
                }
            }
            else if (target.Type == PieceType.King)
            {
                score += 30;
            }
            score -= forecast.CounterDamage * 7;
            if (forecast.AttackerFalls)
            {
                score -= 200 + Value(unit.Type); // Never trade a unit for a scratch.
            }
            score -= threat * (unit.Type == PieceType.King ? 8 : 1);
            return score;
        }

        static int ScorePosition(Board board, Unit unit, Coord tile, int threat, List<Unit> enemies)
        {
            if (enemies.Count == 0)
            {
                return 0;
            }
            int nearest = enemies.Min(e => Coord.Chebyshev(tile, e.Position));
            Unit king = enemies.FirstOrDefault(e => e.Type == PieceType.King);
            int toKing = king != null ? Coord.Chebyshev(tile, king.Position) : nearest;
            UnitStats stats = unit.Stats;

            if (unit.Type == PieceType.King)
            {
                // Stay out of reach and behind the lines.
                return -threat * 8 - Math.Max(0, 3 - nearest) * 5 - (tile == unit.Position ? 0 : 1);
            }

            int score;
            if (stats.IsRanged)
            {
                score = -Math.Abs(nearest - stats.MaxAttackRange) * 4 - threat * 2;
            }
            else
            {
                score = -nearest * 5 - toKing - threat;
            }
            if (threat >= unit.Health)
            {
                score -= 15;
            }
            if (unit.Type == PieceType.Pawn)
            {
                int rank = unit.PromotionRank(board.Height);
                int progress = board.Height - 1 - Math.Abs(rank - tile.Y);
                score += progress * 2 + (tile.Y == rank ? 40 : 0);
            }
            return score;
        }

        /// <summary>For every tile, the summed attack of the <paramref name="team"/> units that could strike it next turn.</summary>
        public static Dictionary<Coord, int> ThreatMap(Board board, Team team)
        {
            var danger = new Dictionary<Coord, int>();
            foreach (Unit unit in board.UnitsOf(team))
            {
                var reach = new HashSet<Coord>();
                foreach (Coord from in Movement.Destinations(board, unit).Append(unit.Position))
                {
                    reach.UnionWith(Movement.AttackArea(board, unit, from));
                }
                foreach (Coord tile in reach)
                {
                    danger[tile] = (danger.TryGetValue(tile, out int value) ? value : 0) + unit.Stats.Attack;
                }
            }
            return danger;
        }

        static int Value(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return 10;
                case PieceType.Knight: return 25;
                case PieceType.Bishop: return 25;
                case PieceType.Tower: return 30;
                case PieceType.WarDog: return 30;
                case PieceType.Queen: return 45;
                case PieceType.King: return 100;
                default: return 10;
            }
        }
    }
}

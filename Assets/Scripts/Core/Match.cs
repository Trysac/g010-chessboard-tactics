using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessboardTactics.Core
{
    public enum ActionError
    {
        None,
        MatchOver,
        NotYourTurn,
        UnitFallen,
        UnitDone,
        AlreadyMoved,
        Unreachable,
        NotAnEnemy,
        OutOfRange,
    }

    public sealed class MoveResult
    {
        internal MoveResult(Unit unit, Coord from, Coord to, bool promoted)
        {
            Unit = unit;
            From = from;
            To = to;
            Promoted = promoted;
        }

        public Unit Unit { get; }
        public Coord From { get; }
        public Coord To { get; }
        /// <summary>True when the move turned a pawn into a war dog.</summary>
        public bool Promoted { get; }
    }

    public sealed class AttackResult
    {
        internal AttackResult(Unit attacker, Unit defender, AttackForecast outcome)
        {
            Attacker = attacker;
            Defender = defender;
            Outcome = outcome;
        }

        public Unit Attacker { get; }
        public Unit Defender { get; }
        public AttackForecast Outcome { get; }
    }

    /// <summary>
    /// Turn structure and rules of one battle. On its turn a side may move each unit once and then
    /// attack once (or wait); attacking or waiting ends that unit's turn. The side that loses its
    /// king, or every unit, loses the battle.
    /// </summary>
    public sealed class Match
    {
        public Match(Board board, Team firstTeam = Team.Player)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            CurrentTeam = firstTeam;
            Round = 1;
        }

        public Board Board { get; }
        public Team CurrentTeam { get; private set; }
        /// <summary>Starts at 1 and grows each time both sides have played a turn.</summary>
        public int Round { get; private set; }
        public Team? Winner { get; private set; }
        public bool IsOver => Winner.HasValue;

        readonly Dictionary<Team, int> fallen = new Dictionary<Team, int> { [Team.Player] = 0, [Team.Enemy] = 0 };

        /// <summary>How many units of <paramref name="team"/> have fallen so far.</summary>
        public int Fallen(Team team) => fallen[team];

        /// <summary>A unit is done when it has attacked or waited, or has moved and has nobody left to strike.</summary>
        public bool IsDone(Unit unit) =>
            !unit.IsAlive || unit.HasActed || (unit.HasMoved && Movement.Targets(Board, unit).Count == 0);

        /// <summary>Units of the current side that can still do something this turn.</summary>
        public List<Unit> PendingUnits() => Board.UnitsOf(CurrentTeam).Where(u => !IsDone(u)).ToList();

        public ActionError CheckSelect(Unit unit)
        {
            if (IsOver) return ActionError.MatchOver;
            if (unit == null || !unit.IsAlive) return ActionError.UnitFallen;
            if (unit.Team != CurrentTeam) return ActionError.NotYourTurn;
            if (IsDone(unit)) return ActionError.UnitDone;
            return ActionError.None;
        }

        public ActionError CheckMove(Unit unit, Coord to)
        {
            ActionError error = CheckSelect(unit);
            if (error != ActionError.None) return error;
            if (unit.HasMoved) return ActionError.AlreadyMoved;
            if (!Movement.CanReach(Board, unit, to)) return ActionError.Unreachable;
            return ActionError.None;
        }

        public MoveResult Move(Unit unit, Coord to)
        {
            Require(CheckMove(unit, to));
            Coord from = unit.Position;
            unit.Position = to;
            unit.HasMoved = true;
            bool promoted = false;
            if (unit.Type == PieceType.Pawn && to.Y == unit.PromotionRank(Board.Height))
            {
                unit.Promote();
                promoted = true;
            }
            return new MoveResult(unit, from, to, promoted);
        }

        public ActionError CheckAttack(Unit attacker, Unit defender)
        {
            ActionError error = CheckSelect(attacker);
            if (error != ActionError.None) return error;
            if (defender == null || !defender.IsAlive || defender.Team == attacker.Team) return ActionError.NotAnEnemy;
            if (!attacker.Stats.InAttackRange(Coord.Chebyshev(attacker.Position, defender.Position))) return ActionError.OutOfRange;
            return ActionError.None;
        }

        public AttackResult Attack(Unit attacker, Unit defender)
        {
            Require(CheckAttack(attacker, defender));
            AttackForecast outcome = Combat.Resolve(attacker, defender);
            attacker.HasMoved = true;
            attacker.HasActed = true;
            if (outcome.DefenderFalls) Fall(defender);
            if (outcome.AttackerFalls) Fall(attacker);
            return new AttackResult(attacker, defender, outcome);
        }

        /// <summary>Ends the unit's turn without attacking.</summary>
        public void Wait(Unit unit)
        {
            ActionError error = CheckSelect(unit);
            if (error == ActionError.UnitDone && unit.IsAlive && !unit.HasActed)
            {
                error = ActionError.None; // Moved with nobody in range: waiting is still allowed.
            }
            Require(error);
            unit.HasMoved = true;
            unit.HasActed = true;
        }

        /// <summary>Hands the turn to the other side, whose units become ready again.</summary>
        public void EndTurn()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The battle is over.");
            }
            CurrentTeam = CurrentTeam.Opponent();
            if (CurrentTeam == Team.Player)
            {
                Round++;
            }
            foreach (Unit unit in Board.UnitsOf(CurrentTeam))
            {
                unit.Refresh();
            }
        }

        void Fall(Unit unit)
        {
            fallen[unit.Team]++;
            if (unit.Type == PieceType.King || !Board.UnitsOf(unit.Team).Any())
            {
                Winner = unit.Team.Opponent();
            }
        }

        static void Require(ActionError error)
        {
            if (error != ActionError.None)
            {
                throw new InvalidOperationException($"Action not allowed: {error}.");
            }
        }
    }
}

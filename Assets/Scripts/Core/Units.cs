using System;
using System.Collections.Generic;

namespace ChessboardTactics.Core
{
    public enum Team
    {
        Player,
        Enemy,
    }

    /// <summary>Unit types. The order matches the original enum so serialized WarriorData values keep their meaning.</summary>
    public enum PieceType
    {
        Pawn,
        Bishop,
        Tower,
        Knight,
        King,
        Queen,
        WarDog, // A pawn that reached the far side of the board.
    }

    public enum MoveStyle
    {
        /// <summary>Steps tile by tile up to the range; may pass allies, never enemies or rocks.</summary>
        Walk,
        /// <summary>Moves in a straight line up to the range and stops before anything in the way.</summary>
        Slide,
        /// <summary>Leaps in an L like a chess knight, over anything.</summary>
        Jump,
    }

    public enum Directions
    {
        Orthogonal,
        Diagonal,
        All,
    }

    /// <summary>The rules of one unit type.</summary>
    public sealed class UnitStats
    {
        public UnitStats(PieceType type, string name, string role, int maxHealth, int attack,
            MoveStyle moveStyle, Directions directions, int moveRange, int minAttackRange, int maxAttackRange)
        {
            Type = type;
            Name = name;
            Role = role;
            MaxHealth = maxHealth;
            Attack = attack;
            MoveStyle = moveStyle;
            Directions = directions;
            MoveRange = moveRange;
            MinAttackRange = minAttackRange;
            MaxAttackRange = maxAttackRange;
        }

        public PieceType Type { get; }
        public string Name { get; }
        public string Role { get; }
        public int MaxHealth { get; }
        public int Attack { get; }
        public MoveStyle MoveStyle { get; }
        public Directions Directions { get; }
        public int MoveRange { get; }
        public int MinAttackRange { get; }
        public int MaxAttackRange { get; }

        public bool IsRanged => MinAttackRange > 1;

        /// <summary>Damage dealt back when this unit survives an attack from inside its range.</summary>
        public int CounterAttack => Math.Max(1, Attack / 2);

        public bool InAttackRange(int distance) => distance >= MinAttackRange && distance <= MaxAttackRange;
    }

    /// <summary>The shipped tuning of every unit type. Attack ranges are measured in king moves (Chebyshev distance).</summary>
    public static class UnitCatalog
    {
        /// <summary>Health a pawn recovers when it is promoted.</summary>
        public const int PromotionHeal = 4;

        static readonly Dictionary<PieceType, UnitStats> Stats = new Dictionary<PieceType, UnitStats>
        {
            [PieceType.Pawn] = new UnitStats(PieceType.Pawn, "Pawn", "Foot soldier. Reach the far rank to become a War Dog.",
                8, 3, MoveStyle.Walk, Directions.Orthogonal, 2, 1, 1),
            [PieceType.Knight] = new UnitStats(PieceType.Knight, "Knight", "Leaps in an L over anything in the way.",
                10, 4, MoveStyle.Jump, Directions.All, 1, 1, 1),
            [PieceType.Bishop] = new UnitStats(PieceType.Bishop, "Bishop", "Casts bolts 2-3 tiles away but cannot strike adjacent foes.",
                6, 4, MoveStyle.Slide, Directions.Diagonal, 3, 2, 3),
            [PieceType.Tower] = new UnitStats(PieceType.Tower, "Tower", "Slow, sturdy guardian that charges in straight lines.",
                14, 4, MoveStyle.Slide, Directions.Orthogonal, 3, 1, 1),
            [PieceType.Queen] = new UnitStats(PieceType.Queen, "Queen", "Strongest piece: moves any way and hits up to 2 tiles away.",
                12, 5, MoveStyle.Slide, Directions.All, 3, 1, 2),
            [PieceType.King] = new UnitStats(PieceType.King, "King", "Lose him and the battle is lost.",
                16, 3, MoveStyle.Walk, Directions.All, 1, 1, 1),
            [PieceType.WarDog] = new UnitStats(PieceType.WarDog, "War Dog", "A promoted pawn: fast and fierce.",
                10, 5, MoveStyle.Walk, Directions.Orthogonal, 3, 1, 1),
        };

        public static UnitStats Get(PieceType type) => Stats[type];

        public static IEnumerable<UnitStats> All => Stats.Values;
    }

    public sealed class Unit
    {
        public Unit(int id, Team team, PieceType type, Coord position)
        {
            Id = id;
            Team = team;
            Type = type;
            Position = position;
            Health = Stats.MaxHealth;
        }

        public int Id { get; }
        public Team Team { get; }
        public PieceType Type { get; private set; }
        public UnitStats Stats => UnitCatalog.Get(Type);
        public Coord Position { get; internal set; }
        public int Health { get; private set; }
        public bool HasMoved { get; internal set; }
        public bool HasActed { get; internal set; }
        public bool IsAlive => Health > 0;

        /// <summary>The rank a pawn of this team must reach to be promoted.</summary>
        public int PromotionRank(int boardHeight) => Team == Team.Player ? boardHeight - 1 : 0;

        internal void TakeDamage(int amount) => Health = Math.Max(0, Health - amount);

        internal void Promote()
        {
            Type = PieceType.WarDog;
            Health = Math.Min(Stats.MaxHealth, Health + UnitCatalog.PromotionHeal);
        }

        internal void Refresh()
        {
            HasMoved = false;
            HasActed = false;
        }

        public override string ToString() => $"{Team} {Stats.Name} #{Id} at {Position} ({Health} HP)";
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team team) => team == Team.Player ? Team.Enemy : Team.Player;
    }
}

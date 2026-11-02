using System;

namespace ChessboardTactics.Core
{
    /// <summary>A board square. X is the file (column), Y is the rank (row); (0, 0) is the player's left corner.</summary>
    public readonly struct Coord : IEquatable<Coord>, IComparable<Coord>
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Coord operator +(Coord a, Coord b) => new Coord(a.X + b.X, a.Y + b.Y);

        public static Coord operator -(Coord a, Coord b) => new Coord(a.X - b.X, a.Y - b.Y);

        public static Coord operator *(Coord a, int scale) => new Coord(a.X * scale, a.Y * scale);

        public static bool operator ==(Coord a, Coord b) => a.Equals(b);

        public static bool operator !=(Coord a, Coord b) => !a.Equals(b);

        /// <summary>King-move distance: diagonal neighbours are one step away.</summary>
        public static int Chebyshev(Coord a, Coord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        public static int Manhattan(Coord a, Coord b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        public bool Equals(Coord other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Coord other && Equals(other);

        public override int GetHashCode() => X * 397 ^ Y;

        /// <summary>Row-major order (rank first), used to keep every search deterministic.</summary>
        public int CompareTo(Coord other) => Y != other.Y ? Y.CompareTo(other.Y) : X.CompareTo(other.X);

        public override string ToString() => $"{(char)('a' + X)}{Y + 1}";
    }
}

namespace ChessboardTactics.Core
{
    /// <summary>The shipped battle: an 8x8 chessboard with four rocks and two mirrored armies of ten.</summary>
    public static class Battlefield
    {
        public const int Size = 8;

        /// <summary>Rocks placed point-symmetrically so neither side gets better cover.</summary>
        public static readonly Coord[] Rocks = { new Coord(1, 3), new Coord(6, 4), new Coord(5, 3), new Coord(2, 4) };

        /// <summary>The player's army; the enemy mirrors it on the far ranks.</summary>
        public static readonly (PieceType Type, Coord Position)[] Army =
        {
            (PieceType.Knight, new Coord(1, 0)),
            (PieceType.Bishop, new Coord(2, 0)),
            (PieceType.King, new Coord(3, 0)),
            (PieceType.Queen, new Coord(4, 0)),
            (PieceType.Tower, new Coord(5, 0)),
            (PieceType.Knight, new Coord(6, 0)),
            (PieceType.Pawn, new Coord(1, 1)),
            (PieceType.Pawn, new Coord(3, 1)),
            (PieceType.Pawn, new Coord(4, 1)),
            (PieceType.Pawn, new Coord(6, 1)),
        };

        public static Board CreateBoard()
        {
            var board = new Board(Size, Size, Rocks);
            foreach (var (type, position) in Army)
            {
                board.Add(Team.Player, type, position);
            }
            foreach (var (type, position) in Army)
            {
                board.Add(Team.Enemy, type, new Coord(position.X, Size - 1 - position.Y));
            }
            return board;
        }

        public static Match CreateMatch() => new Match(CreateBoard(), Team.Player);
    }
}

using System.Linq;
using NUnit.Framework;

namespace ChessboardTactics.Core.Tests
{
    public class MovementTests
    {
        static Coord C(int x, int y) => new Coord(x, y);

        [Test]
        public void PawnWalksUpToTwoOrthogonalSteps()
        {
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 3));

            var tiles = Movement.Destinations(board, pawn);

            // Every tile two orthogonal steps away, including the corners reached by turning.
            Assert.That(tiles, Has.Count.EqualTo(12));
            Assert.That(tiles, Has.Member(C(3, 5)).And.Member(C(3, 1)).And.Member(C(5, 3)).And.Member(C(4, 4)));
            Assert.That(tiles, Has.No.Member(C(3, 6)).And.Not.Member(C(3, 3)));
        }

        [Test]
        public void PawnCannotEndItsMoveOnAnOccupiedTile()
        {
            // The original BoardTile check only compared distances, so a pawn could be moved onto
            // the tile of another piece.
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 3));
            board.Add(Team.Player, PieceType.Tower, C(3, 4));
            board.Add(Team.Enemy, PieceType.Pawn, C(4, 3));

            var tiles = Movement.Destinations(board, pawn);

            Assert.That(tiles, Has.No.Member(C(3, 4)));
            Assert.That(tiles, Has.No.Member(C(4, 3)));
        }

        [Test]
        public void WalkersPassThroughAlliesButNotEnemiesOrRocks()
        {
            var board = new Board(8, 8, new[] { C(2, 3) });
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 3));
            board.Add(Team.Player, PieceType.Tower, C(3, 4));
            board.Add(Team.Enemy, PieceType.Pawn, C(4, 3));

            var tiles = Movement.Destinations(board, pawn);

            Assert.That(tiles, Has.Member(C(3, 5)), "through the allied tower");
            Assert.That(tiles, Has.No.Member(C(5, 3)), "the enemy pawn blocks the way");
            Assert.That(tiles, Has.No.Member(C(1, 3)).And.Not.Member(C(2, 3)), "the rock blocks the way");
        }

        [Test]
        public void TowerSlidesInStraightLinesAndStopsBeforeBlockers()
        {
            var board = new Board(8, 8, new[] { C(5, 2) });
            Unit tower = board.Add(Team.Player, PieceType.Tower, C(2, 2));
            board.Add(Team.Player, PieceType.Pawn, C(2, 4));

            var tiles = Movement.Destinations(board, tower);

            Assert.That(tiles, Is.EquivalentTo(new[]
            {
                C(2, 3),                     // up, stopped by the pawn on (2, 4)
                C(3, 2), C(4, 2),            // right, stopped by the rock on (5, 2)
                C(2, 1), C(2, 0),            // down to the edge
                C(1, 2), C(0, 2),            // left to the edge
            }));
        }

        [Test]
        public void BishopSlidesOnlyDiagonallyUpToThreeTiles()
        {
            var board = new Board(8, 8);
            Unit bishop = board.Add(Team.Player, PieceType.Bishop, C(3, 3));

            var tiles = Movement.Destinations(board, bishop);

            Assert.That(tiles.All(t => t.X != 3 && t.Y != 3), Is.True);
            Assert.That(tiles, Has.Member(C(6, 6)).And.Member(C(0, 0)).And.Not.Member(C(7, 7)));
            Assert.That(tiles, Has.Count.EqualTo(12));
        }

        [Test]
        public void KnightLeapsOverPiecesButNeedsAFreeLandingTile()
        {
            var board = new Board(8, 8, new[] { C(5, 4) });
            Unit knight = board.Add(Team.Player, PieceType.Knight, C(3, 3));
            board.Add(Team.Player, PieceType.Pawn, C(3, 4));
            board.Add(Team.Player, PieceType.Pawn, C(4, 4));
            board.Add(Team.Enemy, PieceType.Pawn, C(4, 5));

            var tiles = Movement.Destinations(board, knight);

            Assert.That(tiles, Is.EquivalentTo(new[] { C(2, 5), C(5, 2), C(4, 1), C(2, 1), C(1, 2), C(1, 4) }));
        }

        [Test]
        public void KingStepsOneTileInEveryDirection()
        {
            var board = new Board(8, 8);
            Unit king = board.Add(Team.Player, PieceType.King, C(0, 0));

            Assert.That(Movement.Destinations(board, king), Is.EquivalentTo(new[] { C(1, 0), C(0, 1), C(1, 1) }));
        }

        [Test]
        public void TargetsAreEnemiesInsideTheAttackRange()
        {
            var board = new Board(8, 8);
            Unit bishop = board.Add(Team.Player, PieceType.Bishop, C(3, 3));
            board.Add(Team.Enemy, PieceType.Pawn, C(4, 4));  // adjacent: too close for a bishop
            Unit far = board.Add(Team.Enemy, PieceType.Pawn, C(6, 3));
            board.Add(Team.Enemy, PieceType.Pawn, C(7, 3));  // four away: too far
            board.Add(Team.Player, PieceType.Pawn, C(3, 5)); // an ally is never a target

            Assert.That(Movement.Targets(board, bishop), Is.EqualTo(new[] { far }));
        }
    }
}

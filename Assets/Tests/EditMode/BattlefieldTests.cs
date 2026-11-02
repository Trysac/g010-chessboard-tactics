using System.Linq;
using NUnit.Framework;

namespace ChessboardTactics.Core.Tests
{
    public class BattlefieldTests
    {
        [Test]
        public void TheShippedBattleIsAMirroredEightByEightBoard()
        {
            Board board = Battlefield.CreateBoard();

            Assert.That(board.Width, Is.EqualTo(8));
            Assert.That(board.Height, Is.EqualTo(8));
            Assert.That(board.UnitsOf(Team.Player).Count(), Is.EqualTo(10));
            Assert.That(board.UnitsOf(Team.Enemy).Count(), Is.EqualTo(10));
            Assert.That(board.UnitsOf(Team.Player).Count(u => u.Type == PieceType.King), Is.EqualTo(1));
            foreach (Unit unit in board.UnitsOf(Team.Player))
            {
                Unit mirror = board.UnitAt(new Coord(unit.Position.X, 7 - unit.Position.Y));
                Assert.That(mirror.Team, Is.EqualTo(Team.Enemy));
                Assert.That(mirror.Type, Is.EqualTo(unit.Type));
            }
            foreach (Coord rock in board.Rocks)
            {
                Assert.That(board.IsRock(new Coord(7 - rock.X, 7 - rock.Y)), Is.True, "rocks are point-symmetric");
            }
        }

        [Test]
        public void EveryUnitTypeHasSensibleStats()
        {
            foreach (UnitStats stats in UnitCatalog.All)
            {
                Assert.That(stats.MaxHealth, Is.Positive, stats.Name);
                Assert.That(stats.Attack, Is.Positive, stats.Name);
                Assert.That(stats.MoveRange, Is.Positive, stats.Name);
                Assert.That(stats.MinAttackRange, Is.InRange(1, stats.MaxAttackRange), stats.Name);
                Assert.That(stats.Role, Is.Not.Empty, stats.Name);
            }
            Assert.That(UnitCatalog.All.Count(), Is.EqualTo(System.Enum.GetValues(typeof(PieceType)).Length));
        }
    }
}

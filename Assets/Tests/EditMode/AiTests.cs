using System.Linq;
using NUnit.Framework;

namespace ChessboardTactics.Core.Tests
{
    public class TacticsAiTests
    {
        static Coord C(int x, int y) => new Coord(x, y);

        [Test]
        public void TheAiFinishesOffAWoundedUnitBeforeHittingAHealthyOne()
        {
            var board = new Board(8, 8);
            Unit wounded = board.Add(Team.Player, PieceType.Pawn, C(2, 4));
            Unit healthy = board.Add(Team.Player, PieceType.Tower, C(4, 4));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit queen = board.Add(Team.Enemy, PieceType.Queen, C(3, 5));
            board.Add(Team.Enemy, PieceType.Knight, C(7, 7));
            board.Add(Team.Enemy, PieceType.King, C(7, 0));
            var match = new Match(board, Team.Enemy);
            match.Attack(queen, wounded); // the pawn drops to 3
            match.EndTurn();
            match.EndTurn();

            AiAction action = TacticsAi.ChooseNext(match);

            Assert.That(action.Target, Is.EqualTo(wounded));
            Assert.That(wounded.Health, Is.LessThanOrEqualTo(queen.Stats.Attack));
            Assert.That(healthy.IsAlive, Is.True);
        }

        [Test]
        public void TheAiTakesTheEnemyKingWhenItCan()
        {
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(1, 1));
            Unit king = board.Add(Team.Player, PieceType.King, C(5, 5));
            Unit queen = board.Add(Team.Enemy, PieceType.Queen, C(5, 7));
            board.Add(Team.Enemy, PieceType.King, C(0, 7));
            var match = new Match(board, Team.Enemy);
            // Bring the king down to one blow.
            for (int i = 0; i < 3; i++)
            {
                match.Attack(queen, king);
                match.EndTurn();
                match.EndTurn();
            }

            AiAction action = TacticsAi.ChooseNext(match);
            TacticsAi.Execute(match, action);

            Assert.That(action.Target, Is.EqualTo(king));
            Assert.That(match.Winner, Is.EqualTo(Team.Enemy));
            Assert.That(pawn.IsAlive, Is.True);
        }

        [Test]
        public void TheAiDoesNotThrowAWoundedUnitAtACounterAttackThatKillsIt()
        {
            var board = new Board(8, 8);
            Unit tower = board.Add(Team.Player, PieceType.Tower, C(3, 3));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit pawn = board.Add(Team.Enemy, PieceType.Pawn, C(3, 4));
            board.Add(Team.Enemy, PieceType.King, C(7, 7));
            var match = new Match(board);
            match.Attack(tower, pawn); // 4 damage, pawn at 4
            match.EndTurn();
            match.Attack(pawn, tower); // counter 2, pawn at 2
            match.EndTurn();
            match.Wait(tower);
            match.EndTurn();

            AiAction action = TacticsAi.ChooseNext(match);

            Assert.That(Combat.Forecast(pawn, tower).AttackerFalls, Is.True);
            Assert.That(action.Unit == pawn && action.Target == tower, Is.False);
        }

        [Test]
        public void TheAiAdvancesWhenNobodyIsInReach()
        {
            Match match = Battlefield.CreateMatch();
            match.EndTurn();
            int before = Distance(match);

            TacticsAi.PlayTurn(match);

            Assert.That(match.CurrentTeam, Is.EqualTo(Team.Player));
            Assert.That(Distance(match), Is.LessThan(before));
        }

        [Test]
        public void TheAiKeepsItsKingOutOfReach()
        {
            var board = new Board(8, 8);
            board.Add(Team.Player, PieceType.Queen, C(1, 1));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit king = board.Add(Team.Enemy, PieceType.King, C(6, 6));
            var match = new Match(board, Team.Enemy);

            AiAction action = TacticsAi.ChooseNext(match);
            var danger = TacticsAi.ThreatMap(board, Team.Player);

            Assert.That(action.Unit, Is.EqualTo(king));
            Assert.That(danger.ContainsKey(action.Destination), Is.False, $"{action.Destination} is threatened");
        }

        [Test]
        public void TheAiIsDeterministic()
        {
            Match first = Battlefield.CreateMatch();
            Match second = Battlefield.CreateMatch();
            for (int turn = 0; turn < 12; turn++)
            {
                TacticsAi.PlayTurn(first);
                TacticsAi.PlayTurn(second);
            }

            Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
        }

        [Test]
        public void TwoAiArmiesFinishTheShippedBattle()
        {
            Match match = Battlefield.CreateMatch();

            while (!match.IsOver && match.Round <= 60)
            {
                TacticsAi.PlayTurn(match);
            }

            Assert.That(match.IsOver, Is.True, $"still fighting after round {match.Round}");
        }

        static int Distance(Match match)
        {
            var players = match.Board.UnitsOf(Team.Player).ToList();
            return match.Board.UnitsOf(Team.Enemy).Sum(e => players.Min(p => Coord.Chebyshev(p.Position, e.Position)));
        }

        static string Snapshot(Match match) =>
            string.Join(";", match.Board.Units.Select(u => $"{u.Id}:{u.Type}:{u.Position}:{u.Health}"));
    }
}

using System;
using NUnit.Framework;

namespace ChessboardTactics.Core.Tests
{
    public class CombatTests
    {
        static Coord C(int x, int y) => new Coord(x, y);

        [Test]
        public void AttackerDealsItsAttackAndASurvivingDefenderStrikesBack()
        {
            var board = new Board(8, 8);
            Unit knight = board.Add(Team.Player, PieceType.Knight, C(3, 3));
            Unit tower = board.Add(Team.Enemy, PieceType.Tower, C(3, 4));
            var match = new Match(board);

            AttackResult result = match.Attack(knight, tower);

            int knightAttack = UnitCatalog.Get(PieceType.Knight).Attack;
            int counter = UnitCatalog.Get(PieceType.Tower).CounterAttack;
            Assert.That(result.Outcome.Damage, Is.EqualTo(knightAttack));
            Assert.That(tower.Health, Is.EqualTo(UnitCatalog.Get(PieceType.Tower).MaxHealth - knightAttack));
            Assert.That(result.Outcome.CounterDamage, Is.EqualTo(counter));
            Assert.That(knight.Health, Is.EqualTo(UnitCatalog.Get(PieceType.Knight).MaxHealth - counter));
            Assert.That(counter, Is.EqualTo(Math.Max(1, UnitCatalog.Get(PieceType.Tower).Attack / 2)));
        }

        [Test]
        public void RangedAttacksCannotBeAnsweredByMeleeUnits()
        {
            var board = new Board(8, 8);
            Unit bishop = board.Add(Team.Player, PieceType.Bishop, C(3, 3));
            Unit pawn = board.Add(Team.Enemy, PieceType.Pawn, C(5, 5));
            var match = new Match(board);

            AttackResult result = match.Attack(bishop, pawn);

            Assert.That(result.Outcome.CounterDamage, Is.Zero);
            Assert.That(bishop.Health, Is.EqualTo(bishop.Stats.MaxHealth));
        }

        [Test]
        public void ADefenderThatFallsDoesNotStrikeBackAndLeavesTheBoard()
        {
            var board = new Board(8, 8);
            Unit queen = board.Add(Team.Player, PieceType.Queen, C(3, 3));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit bishop = board.Add(Team.Enemy, PieceType.Bishop, C(3, 4));
            board.Add(Team.Enemy, PieceType.King, C(7, 7));
            var match = new Match(board);
            match.Attack(queen, bishop); // 5 damage
            match.EndTurn();
            match.EndTurn();

            AttackResult result = match.Attack(queen, bishop);

            Assert.That(result.Outcome.DefenderFalls, Is.True);
            Assert.That(result.Outcome.CounterDamage, Is.Zero);
            Assert.That(board.UnitAt(C(3, 4)), Is.Null);
            Assert.That(match.Fallen(Team.Enemy), Is.EqualTo(1));
            Assert.That(match.IsOver, Is.False);
        }

        [Test]
        public void ACounterAttackCanDefeatTheAttacker()
        {
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 3));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit tower = board.Add(Team.Enemy, PieceType.Tower, C(3, 4));
            Unit enemyPawn = board.Add(Team.Enemy, PieceType.Pawn, C(2, 3));
            board.Add(Team.Enemy, PieceType.King, C(7, 7));
            var match = new Match(board, Team.Enemy);
            match.Attack(tower, pawn);
            match.Attack(enemyPawn, pawn);
            match.EndTurn();
            Assert.That(pawn.Health, Is.EqualTo(1));

            AttackForecast forecast = Combat.Forecast(pawn, tower);
            AttackResult result = match.Attack(pawn, tower);

            Assert.That(forecast.AttackerFalls, Is.True);
            Assert.That(result.Outcome.AttackerFalls, Is.True);
            Assert.That(pawn.IsAlive, Is.False);
            Assert.That(tower.IsAlive, Is.True);
            Assert.That(match.Fallen(Team.Player), Is.EqualTo(1));
            Assert.That(match.IsOver, Is.False, "the player's king still stands");
        }

        [Test]
        public void DefeatingTheKingWinsTheBattle()
        {
            var board = new Board(8, 8);
            Unit queen = board.Add(Team.Player, PieceType.Queen, C(3, 3));
            board.Add(Team.Player, PieceType.King, C(0, 0));
            Unit king = board.Add(Team.Enemy, PieceType.King, C(4, 4));
            board.Add(Team.Enemy, PieceType.Tower, C(7, 7));
            var match = new Match(board);

            for (int round = 0; king.IsAlive; round++)
            {
                Assert.That(round, Is.LessThan(10));
                match.Attack(queen, king);
                if (king.IsAlive)
                {
                    match.EndTurn();
                    match.EndTurn();
                }
            }

            Assert.That(match.IsOver, Is.True);
            Assert.That(match.Winner, Is.EqualTo(Team.Player));
            Assert.Throws<InvalidOperationException>(() => match.EndTurn());
            Assert.That(match.CheckSelect(queen), Is.EqualTo(ActionError.MatchOver));
        }

        [Test]
        public void DefeatingEveryEnemyUnitWinsTheBattle()
        {
            var board = new Board(8, 8);
            Unit queen = board.Add(Team.Player, PieceType.Queen, C(3, 3));
            Unit pawn = board.Add(Team.Enemy, PieceType.Pawn, C(3, 5));
            var match = new Match(board);
            match.Attack(queen, pawn);
            match.EndTurn();
            match.EndTurn();

            match.Attack(queen, pawn);

            Assert.That(match.Winner, Is.EqualTo(Team.Player));
        }
    }

    public class MatchTests
    {
        static Coord C(int x, int y) => new Coord(x, y);

        [Test]
        public void EachUnitMovesOnceAndThenAttacksOrWaits()
        {
            Match match = Battlefield.CreateMatch();
            Unit pawn = match.Board.UnitAt(C(3, 1));

            match.Move(pawn, C(3, 3));

            Assert.That(pawn.Position, Is.EqualTo(C(3, 3)));
            Assert.That(match.CheckMove(pawn, C(3, 4)), Is.EqualTo(ActionError.UnitDone), "moved with nobody in range");
            match.Wait(pawn);
            Assert.That(pawn.HasActed, Is.True);
            Assert.That(match.PendingUnits(), Has.No.Member(pawn));
        }

        [Test]
        public void AUnitThatMovedNextToAnEnemyCanStillAttackButNotMoveAgain()
        {
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 3));
            board.Add(Team.Enemy, PieceType.Pawn, C(3, 6));
            var match = new Match(board);

            match.Move(pawn, C(3, 5));

            Assert.That(match.IsDone(pawn), Is.False);
            Assert.That(match.CheckMove(pawn, C(2, 5)), Is.EqualTo(ActionError.AlreadyMoved));
            Assert.That(match.CheckAttack(pawn, board.UnitAt(C(3, 6))), Is.EqualTo(ActionError.None));
        }

        [Test]
        public void OnlyTheSideWhoseTurnItIsCanAct()
        {
            Match match = Battlefield.CreateMatch();
            Unit enemyPawn = match.Board.UnitAt(C(3, 6));

            Assert.That(match.CheckMove(enemyPawn, C(3, 5)), Is.EqualTo(ActionError.NotYourTurn));
            Assert.Throws<InvalidOperationException>(() => match.Move(enemyPawn, C(3, 5)));
        }

        [Test]
        public void IllegalMovesAndAttacksAreRejected()
        {
            Match match = Battlefield.CreateMatch();
            Unit pawn = match.Board.UnitAt(C(3, 1));
            Unit enemyKing = match.Board.UnitAt(C(3, 7));

            Assert.That(match.CheckMove(pawn, C(3, 4)), Is.EqualTo(ActionError.Unreachable));
            Assert.That(match.CheckMove(pawn, C(4, 1)), Is.EqualTo(ActionError.Unreachable), "occupied by the queen's pawn");
            Assert.That(match.CheckAttack(pawn, enemyKing), Is.EqualTo(ActionError.OutOfRange));
            Assert.That(match.CheckAttack(pawn, match.Board.UnitAt(C(4, 1))), Is.EqualTo(ActionError.NotAnEnemy));
        }

        [Test]
        public void EndingTheTurnRefreshesTheOtherSideAndCountsRounds()
        {
            Match match = Battlefield.CreateMatch();
            Unit pawn = match.Board.UnitAt(C(3, 1));
            match.Move(pawn, C(3, 3));
            match.Wait(pawn);

            match.EndTurn();
            Assert.That(match.CurrentTeam, Is.EqualTo(Team.Enemy));
            Assert.That(match.Round, Is.EqualTo(1));
            Assert.That(match.PendingUnits(), Has.Count.EqualTo(10));

            match.EndTurn();
            Assert.That(match.CurrentTeam, Is.EqualTo(Team.Player));
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(pawn.HasMoved, Is.False);
            Assert.That(match.PendingUnits(), Has.Member(pawn));
        }

        [Test]
        public void APawnReachingTheFarRankBecomesAWarDog()
        {
            var board = new Board(8, 8);
            Unit pawn = board.Add(Team.Player, PieceType.Pawn, C(3, 5));
            Unit enemyPawn = board.Add(Team.Enemy, PieceType.Pawn, C(0, 2));
            var match = new Match(board);

            MoveResult move = match.Move(pawn, C(3, 7));

            Assert.That(move.Promoted, Is.True);
            Assert.That(pawn.Type, Is.EqualTo(PieceType.WarDog));
            Assert.That(pawn.Health, Is.EqualTo(Math.Min(UnitCatalog.Get(PieceType.WarDog).MaxHealth,
                UnitCatalog.Get(PieceType.Pawn).MaxHealth + UnitCatalog.PromotionHeal)));

            match.Wait(pawn);
            match.EndTurn();
            Assert.That(match.Move(enemyPawn, C(0, 1)).Promoted, Is.False);
            match.Wait(enemyPawn);
            match.EndTurn();
            match.EndTurn();
            Assert.That(match.Move(enemyPawn, C(0, 0)).Promoted, Is.True, "enemy pawns promote on rank 1");
        }
    }
}

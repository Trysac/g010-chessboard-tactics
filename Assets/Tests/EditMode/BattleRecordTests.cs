using NUnit.Framework;

namespace ChessboardTactics.Core.Tests
{
    public class BattleRecordTests
    {
        [Test]
        public void VictoriesDefeatsAndTheFastestVictoryAreSaved()
        {
            var store = new MemoryRecordStore(wins: 1, losses: 2, bestRounds: 12);
            var record = new BattleRecord(store);

            Assert.That(record.Record(won: true, rounds: 14), Is.False, "slower than the best");
            Assert.That(record.Record(won: true, rounds: 9), Is.True);
            Assert.That(record.Record(won: false, rounds: 5), Is.False);

            Assert.That((record.Wins, record.Losses, record.BestRounds), Is.EqualTo((3, 3, 9)));
            Assert.That(store.Load(), Is.EqualTo((3, 3, 9)));
            Assert.That(new BattleRecord(store).BestRounds, Is.EqualTo(9));
        }

        [Test]
        public void TheFirstVictoryIsAlwaysTheFastest()
        {
            var record = new BattleRecord(new MemoryRecordStore());

            Assert.That(record.Record(won: false, rounds: 3), Is.False);
            Assert.That(record.Record(won: true, rounds: 30), Is.True);
            Assert.That(record.BestRounds, Is.EqualTo(30));
        }
    }
}

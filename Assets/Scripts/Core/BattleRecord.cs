namespace ChessboardTactics.Core
{
    /// <summary>Where the battle record is kept between sessions.</summary>
    public interface IRecordStore
    {
        (int Wins, int Losses, int BestRounds) Load();

        void Save(int wins, int losses, int bestRounds);
    }

    public sealed class MemoryRecordStore : IRecordStore
    {
        (int, int, int) saved;

        public MemoryRecordStore(int wins = 0, int losses = 0, int bestRounds = 0) => saved = (wins, losses, bestRounds);

        public (int Wins, int Losses, int BestRounds) Load() => saved;

        public void Save(int wins, int losses, int bestRounds) => saved = (wins, losses, bestRounds);
    }

    /// <summary>Victories, defeats and the fastest victory (fewest rounds) across battles.</summary>
    public sealed class BattleRecord
    {
        readonly IRecordStore store;

        public BattleRecord(IRecordStore store)
        {
            this.store = store;
            (Wins, Losses, BestRounds) = store.Load();
        }

        public int Wins { get; private set; }
        public int Losses { get; private set; }
        /// <summary>Rounds of the fastest victory, or 0 before the first one.</summary>
        public int BestRounds { get; private set; }

        /// <summary>Records a finished battle and returns true when it was a new fastest victory.</summary>
        public bool Record(bool won, int rounds)
        {
            bool fastest = false;
            if (won)
            {
                Wins++;
                if (BestRounds == 0 || rounds < BestRounds)
                {
                    BestRounds = rounds;
                    fastest = true;
                }
            }
            else
            {
                Losses++;
            }
            store.Save(Wins, Losses, BestRounds);
            return fastest;
        }
    }
}

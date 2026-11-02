using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    public sealed class PlayerPrefsRecordStore : IRecordStore
    {
        const string Prefix = "chessboard.record.";

        public (int Wins, int Losses, int BestRounds) Load() =>
            (PlayerPrefs.GetInt(Prefix + "wins", 0), PlayerPrefs.GetInt(Prefix + "losses", 0), PlayerPrefs.GetInt(Prefix + "best", 0));

        public void Save(int wins, int losses, int bestRounds)
        {
            PlayerPrefs.SetInt(Prefix + "wins", wins);
            PlayerPrefs.SetInt(Prefix + "losses", losses);
            PlayerPrefs.SetInt(Prefix + "best", bestRounds);
            PlayerPrefs.Save();
        }
    }
}

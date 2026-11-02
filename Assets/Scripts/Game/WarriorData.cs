using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>
    /// Presentation data of one unit type: the names each army gives it and its flavour text.
    /// The rules (health, attack, movement, range) come from <see cref="UnitCatalog"/>, so the
    /// tests cover the exact values the game ships with.
    /// </summary>
    [CreateAssetMenu(fileName = "New_Warrior_Data", menuName = "Chessboard Tactics/Warrior Data")]
    public sealed class WarriorData : ScriptableObject
    {
        [SerializeField] PieceType characterType;
        [SerializeField] string characterName;
        [SerializeField] string enemyName;
        [SerializeField, TextArea] string characterDescription;

        public PieceType CharacterType => characterType;
        public UnitStats Stats => UnitCatalog.Get(characterType);
        public string CharacterDescription => characterDescription;

        public string NameFor(Team team) => team == Team.Player ? characterName : enemyName;

        public void Configure(PieceType type, string playerName, string hordeName, string description)
        {
            characterType = type;
            characterName = playerName;
            enemyName = hordeName;
            characterDescription = description;
        }
    }
}

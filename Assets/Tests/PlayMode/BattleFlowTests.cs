using System.Collections;
using System.Linq;
using ChessboardTactics.Core;
using ChessboardTactics.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ChessboardTactics.Tests
{
    /// <summary>
    /// Battle flow in the real scene: a finished battle must not show its result
    /// screen over a battle or menu opened during the finish delay.
    /// </summary>
    public class BattleFlowTests
    {
        GameDirector director;

        IEnumerator Load()
        {
            SceneManager.LoadScene("Battle");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            Assert.That(director, Is.Not.Null);
            director.UseMemoryRecord();
            Assert.That(director.Phase, Is.EqualTo(GamePhase.Title));
            director.StartBattle();
            yield return null;
            Assert.That(director.Phase, Is.EqualTo(GamePhase.PlayerTurn));
        }

        /// Plays with the player AI against a passive enemy until the next player attack would end the battle,
        /// then performs that attack through the director's click handling.
        IEnumerator WinThroughClicks()
        {
            Match m = director.Match;
            AiAction final = null;
            for (int guard = 0; guard < 2000 && final == null && !m.IsOver; guard++)
            {
                if (m.CurrentTeam == Team.Enemy)
                {
                    foreach (Unit u in m.PendingUnits()) m.Wait(u);
                    m.EndTurn();
                    continue;
                }
                AiAction a = TacticsAi.ChooseNext(m);
                if (a == null) { m.EndTurn(); continue; }
                if (a.Target != null)
                {
                    AttackForecast f = Combat.Forecast(a.Unit, a.Destination, a.Target);
                    if (f.DefenderFalls && (a.Target.Type == PieceType.King || m.Board.UnitsOf(Team.Enemy).Count() == 1))
                    {
                        final = a;
                        break;
                    }
                }
                TacticsAi.Execute(m, a);
            }
            if (final == null) Assert.Inconclusive("no winning blow found");
            director.Simulate(0);
            yield return null;
            director.ClickTile(final.Unit.Position);
            yield return null;
            if (final.Moves)
            {
                director.ClickTile(final.Destination);
                float t0 = Time.realtimeSinceStartup;
                yield return null;
                while (director.Busy && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            }
            director.ClickTile(final.Target.Position);
            float t1 = Time.realtimeSinceStartup;
            while (!m.IsOver && Time.realtimeSinceStartup - t1 < 10f) yield return null;
            while (director.Busy && Time.realtimeSinceStartup - t1 < 10f) yield return null;
            Assert.That(m.Winner, Is.EqualTo(Team.Player));
        }

        [UnityTest]
        public IEnumerator WinningBlow_ShowsTheResultScreen()
        {
            yield return Load();
            yield return WinThroughClicks();
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(director.Phase, Is.EqualTo(GamePhase.GameOver));
        }

        [UnityTest]
        public IEnumerator RestartDuringTheFinishDelay_StartsACleanBattle()
        {
            yield return Load();
            yield return WinThroughClicks();
            director.TogglePause();
            Assert.That(director.Phase, Is.EqualTo(GamePhase.Paused));
            director.StartBattle(); // what "Restart battle" in the pause menu calls
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(director.Phase, Is.EqualTo(GamePhase.PlayerTurn), "result screen of the previous battle popped over the new one");
        }

        [UnityTest]
        public IEnumerator MainMenuDuringTheFinishDelay_StaysOnTheTitle()
        {
            yield return Load();
            yield return WinThroughClicks();
            director.TogglePause();
            director.ToTitle(); // what "Main menu" in the pause menu calls
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(director.Phase, Is.EqualTo(GamePhase.Title), "result screen of the previous battle popped over the title");
        }
    }
}

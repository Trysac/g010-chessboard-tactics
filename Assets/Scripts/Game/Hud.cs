using System;
using System.Collections.Generic;
using ChessboardTactics.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessboardTactics.Game
{
    public enum HudScreen
    {
        Title,
        Battle,
        Paused,
        GameOver,
    }

    /// <summary>
    /// Binds the UI Toolkit HUD (Hud.uxml). Buttons never take keyboard focus: the director
    /// handles keys itself, so a key still held from the battle cannot press a menu button.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        const float FloaterLifetime = 1.1f;

        sealed class Floater
        {
            public Label Label;
            public Vector2 Position;
            public float Age;
        }

        VisualElement root;
        VisualElement floaters;
        VisualElement topBar;
        Label round;
        Label armies;
        Label turn;
        Button endTurnButton;
        VisualElement unitCard;
        Label unitName;
        Label unitTeam;
        VisualElement unitHpFill;
        Label unitHp;
        Label unitAttack;
        Label unitMove;
        Label unitRange;
        Label unitRole;
        Label unitLore;
        Label unitStatus;
        Button waitButton;
        VisualElement forecast;
        Label forecastTitle;
        Label forecastDeal;
        Label forecastCounter;
        Label forecastResult;
        Label hint;
        Label banner;
        VisualElement title;
        VisualElement rules;
        Label record;
        VisualElement pause;
        Label muteStatus;
        VisualElement gameOver;
        Label resultTitle;
        Label resultSubtitle;
        Label resultRounds;
        Label resultDefeated;
        Label resultLost;
        Label resultRecord;
        Label resultBest;
        IVisualElementScheduledItem bannerHide;
        readonly List<Floater> active = new List<Floater>();

        public event Action PlayClicked;
        public event Action EndTurnClicked;
        public event Action WaitClicked;
        public event Action PauseClicked;
        public event Action ResumeClicked;
        public event Action RestartClicked;
        public event Action TitleClicked;
        public event Action ButtonPressed;

        public bool RulesOpen => !rules.ClassListContains("hidden");

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            floaters = root.Q("floaters");
            topBar = root.Q("top-bar");
            round = root.Q<Label>("round");
            armies = root.Q<Label>("armies");
            turn = root.Q<Label>("turn");
            endTurnButton = root.Q<Button>("end-turn-button");
            unitCard = root.Q("unit-card");
            unitName = root.Q<Label>("unit-name");
            unitTeam = root.Q<Label>("unit-team");
            unitHpFill = root.Q("unit-hp-fill");
            unitHp = root.Q<Label>("unit-hp");
            unitAttack = root.Q<Label>("unit-attack");
            unitMove = root.Q<Label>("unit-move");
            unitRange = root.Q<Label>("unit-range");
            unitRole = root.Q<Label>("unit-role");
            unitLore = root.Q<Label>("unit-lore");
            unitStatus = root.Q<Label>("unit-status");
            waitButton = root.Q<Button>("wait-button");
            forecast = root.Q("forecast");
            forecastTitle = root.Q<Label>("forecast-title");
            forecastDeal = root.Q<Label>("forecast-deal");
            forecastCounter = root.Q<Label>("forecast-counter");
            forecastResult = root.Q<Label>("forecast-result");
            hint = root.Q<Label>("hint");
            banner = root.Q<Label>("banner");
            title = root.Q("title");
            rules = root.Q("rules");
            record = root.Q<Label>("record");
            pause = root.Q("pause");
            muteStatus = root.Q<Label>("mute-status");
            gameOver = root.Q("game-over");
            resultTitle = root.Q<Label>("result-title");
            resultSubtitle = root.Q<Label>("result-subtitle");
            resultRounds = root.Q<Label>("result-rounds");
            resultDefeated = root.Q<Label>("result-defeated");
            resultLost = root.Q<Label>("result-lost");
            resultRecord = root.Q<Label>("result-record");
            resultBest = root.Q<Label>("result-best");

            Bind("play-button", () => PlayClicked?.Invoke());
            Bind("rules-button", () => ShowRules(true));
            Bind("rules-close-button", () => ShowRules(false));
            Bind("end-turn-button", () => EndTurnClicked?.Invoke());
            Bind("wait-button", () => WaitClicked?.Invoke());
            Bind("pause-button", () => PauseClicked?.Invoke());
            Bind("resume-button", () => ResumeClicked?.Invoke());
            Bind("restart-button", () => RestartClicked?.Invoke());
            Bind("again-button", () => RestartClicked?.Invoke());
            Bind("pause-title-button", () => TitleClicked?.Invoke());
            Bind("over-title-button", () => TitleClicked?.Invoke());
            HideUnit();
            HideForecast();
        }

        void Bind(string name, Action action)
        {
            Button button = root.Q<Button>(name);
            button.focusable = false;
            button.clicked += () =>
            {
                ButtonPressed?.Invoke();
                action();
            };
        }

        public void ShowScreen(HudScreen screen)
        {
            bool battle = screen == HudScreen.Battle || screen == HudScreen.Paused;
            SetVisible(title, screen == HudScreen.Title);
            SetVisible(pause, screen == HudScreen.Paused);
            SetVisible(gameOver, screen == HudScreen.GameOver);
            SetVisible(topBar, battle);
            SetVisible(hint, screen == HudScreen.Battle);
            if (screen != HudScreen.Battle)
            {
                HideUnit();
                HideForecast();
            }
            if (screen == HudScreen.Title)
            {
                ShowRules(false);
            }
        }

        public void ShowRules(bool open) => SetVisible(rules, open);

        public void SetTurn(Team team, int roundNumber, bool canEndTurn)
        {
            round.text = $"ROUND {roundNumber}";
            turn.text = team == Team.Player ? "YOUR TURN" : "ENEMY TURN";
            turn.EnableInClassList("turn-chip--player", team == Team.Player);
            turn.EnableInClassList("turn-chip--enemy", team == Team.Enemy);
            SetVisible(endTurnButton, canEndTurn);
        }

        public void SetArmies(int player, int enemy) => armies.text = $"{player} vs {enemy}";

        public void SetHint(string text) => hint.text = text;

        public void SetMuted(bool muted) => muteStatus.text = muted ? "Sound off  [M]" : "Sound on  [M]";

        public void SetRecord(BattleRecord battles)
        {
            record.text = battles.Wins + battles.Losses == 0
                ? "No battles fought yet"
                : $"Victories {battles.Wins}  ·  Defeats {battles.Losses}" +
                  (battles.BestRounds > 0 ? $"  ·  Fastest win {battles.BestRounds} rounds" : string.Empty);
        }

        public void ShowUnit(Unit unit, GameArt art, string status, bool canWait)
        {
            UnitStats stats = unit.Stats;
            WarriorData warrior = art.Warrior(unit.Type);
            unitName.text = art.NameOf(unit);
            bool player = unit.Team == Team.Player;
            unitTeam.text = player ? "ORDER" : "HORDE";
            unitTeam.EnableInClassList("tag--player", player);
            unitTeam.EnableInClassList("tag--enemy", !player);
            unitHpFill.style.width = Length.Percent(100f * unit.Health / stats.MaxHealth);
            unitHpFill.EnableInClassList("hp__fill--enemy", !player);
            unitHp.text = $"{unit.Health} / {stats.MaxHealth}";
            unitAttack.text = stats.Attack.ToString();
            unitMove.text = MoveText(stats);
            unitRange.text = stats.MinAttackRange == stats.MaxAttackRange
                ? stats.MaxAttackRange.ToString()
                : $"{stats.MinAttackRange}-{stats.MaxAttackRange}";
            unitRole.text = stats.Role;
            unitLore.text = warrior != null ? warrior.CharacterDescription : string.Empty;
            SetVisible(unitLore, !string.IsNullOrEmpty(unitLore.text));
            unitStatus.text = status;
            SetVisible(unitStatus, !string.IsNullOrEmpty(status));
            SetVisible(waitButton, canWait);
            unitCard.RemoveFromClassList("card--off");
        }

        public void HideUnit() => unitCard.AddToClassList("card--off");

        static string MoveText(UnitStats stats)
        {
            switch (stats.MoveStyle)
            {
                case MoveStyle.Jump: return "L-leap";
                case MoveStyle.Slide:
                    string way = stats.Directions == Directions.Diagonal ? "diag." : stats.Directions == Directions.Orthogonal ? "line" : "any";
                    return $"{stats.MoveRange} {way}";
                default:
                    return stats.Directions == Directions.All ? $"{stats.MoveRange} any" : $"{stats.MoveRange} steps";
            }
        }

        public void ShowForecast(string attacker, string defender, AttackForecast outcome, Unit defenderUnit, Unit attackerUnit)
        {
            forecastTitle.text = $"Attack {defender}";
            forecastDeal.text = $"Deals {outcome.Damage}  ->  {defenderUnit.Health - outcome.Damage} / {defenderUnit.Stats.MaxHealth} HP";
            forecastCounter.text = outcome.CounterDamage > 0
                ? $"Counter {outcome.CounterDamage}  ->  {attacker} at {attackerUnit.Health - outcome.CounterDamage} HP"
                : "No counter-attack";
            if (outcome.DefenderFalls)
            {
                forecastResult.text = defenderUnit.Type == PieceType.King ? "DEFEATS THE KING - VICTORY" : "DEFEATS IT";
            }
            else
            {
                forecastResult.text = outcome.AttackerFalls ? $"YOUR {attacker.ToUpperInvariant()} FALLS" : string.Empty;
            }
            forecastResult.EnableInClassList("forecast__result--bad", outcome.AttackerFalls);
            SetVisible(forecastResult, !string.IsNullOrEmpty(forecastResult.text));
            forecast.RemoveFromClassList("card--off");
        }

        public void HideForecast() => forecast.AddToClassList("card--off");

        /// <summary>Shows a big centred message for a moment.</summary>
        public void Banner(string text, string style)
        {
            banner.text = text;
            banner.EnableInClassList("banner--enemy", style == "enemy");
            banner.EnableInClassList("banner--gold", style == "gold");
            banner.AddToClassList("banner--show");
            bannerHide?.Pause();
            bannerHide = banner.schedule.Execute(() => banner.RemoveFromClassList("banner--show"));
            bannerHide.ExecuteLater(1100);
        }

        /// <summary>A number or word that rises from a point of the board and fades.</summary>
        public void Float(Vector3 world, string text, string style)
        {
            Camera view = Camera.main;
            if (root?.panel == null || view == null)
            {
                return;
            }
            Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, view);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("floater");
            if (!string.IsNullOrEmpty(style))
            {
                label.AddToClassList("floater--" + style);
            }
            floaters.Add(label);
            active.Add(new Floater { Label = label, Position = position });
            Place(active[active.Count - 1]);
        }

        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Floater floater = active[i];
                floater.Age += Time.unscaledDeltaTime;
                if (floater.Age >= FloaterLifetime)
                {
                    floater.Label.RemoveFromHierarchy();
                    active.RemoveAt(i);
                    continue;
                }
                Place(floater);
            }
        }

        static void Place(Floater floater)
        {
            float t = floater.Age / FloaterLifetime;
            floater.Label.style.left = floater.Position.x;
            floater.Label.style.top = floater.Position.y - 50f * Mathf.Sqrt(t);
            floater.Label.style.opacity = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            float pop = t < 0.12f ? 1f + (0.12f - t) * 4f : 1f;
            floater.Label.style.scale = new Scale(new Vector3(pop, pop, 1f));
        }

        public void ShowResult(bool won, Match match, BattleRecord battles, bool fastest)
        {
            resultTitle.text = won ? "VICTORY" : "DEFEAT";
            resultTitle.EnableInClassList("title--defeat", !won);
            bool kingFell = won
                ? !HasKing(match, Team.Enemy)
                : !HasKing(match, Team.Player);
            resultSubtitle.text = won
                ? (kingFell ? "The Horde's king has fallen." : "The Horde has been wiped out.")
                : (kingFell ? "Your king has fallen." : "Your army has been wiped out.");
            resultRounds.text = match.Round.ToString();
            resultDefeated.text = match.Fallen(Team.Enemy).ToString();
            resultLost.text = match.Fallen(Team.Player).ToString();
            resultRecord.text = $"{battles.Wins} won / {battles.Losses} lost";
            SetVisible(resultBest, won && fastest);
        }

        static bool HasKing(Match match, Team team)
        {
            foreach (Unit unit in match.Board.UnitsOf(team))
            {
                if (unit.Type == PieceType.King)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Is a screen position (pixels, origin bottom-left) over an interactive part of the HUD?</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            if (root?.panel == null)
            {
                return false;
            }
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            VisualElement picked = root.panel.Pick(panelPosition);
            return picked != null && picked != root;
        }

        static void SetVisible(VisualElement element, bool visible) => element.EnableInClassList("hidden", !visible);
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChessboardTactics.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChessboardTactics.Game
{
    public enum GamePhase
    {
        Title,
        PlayerTurn,
        EnemyTurn,
        Paused,
        GameOver,
    }

    /// <summary>
    /// Runs the battle: reads mouse, keyboard and gamepad input, applies it to the core
    /// <see cref="Match"/>, plays the AI's turn and animates every result on the board.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        const float AiStepDelay = 0.35f;
        const float MenuKeyDelay = 0.8f;

        [SerializeField] GameArt art;
        [SerializeField] Hud hud;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] BoardView boardView;

        readonly Dictionary<int, UnitView> views = new Dictionary<int, UnitView>();
        AudioDirector audioDirector;
        BattleRecord record;
        Transform unitRoot;
        Unit selected;
        Coord? hovered;
        Coord cursor = new Coord(3, 1);
        bool keyboardCursor;
        Vector2 lastMouse;
        float stickRepeat;
        float phaseTime;
        GamePhase pausedFrom;
        Coroutine turnRoutine;
        int busy;
        bool marksDirty = true;
        // Clicks come through actions, which see every event, so a press and release that land
        // in the same frame (fast taps, slow WebGL frames) still count.
        InputAction leftClick;
        InputAction rightClick;
        int leftClicks;
        int rightClicks;

        public Match Match { get; private set; }
        public Hud Hud => hud;
        public GamePhase Phase { get; private set; } = GamePhase.Title;
        public bool Busy => busy > 0;

        /// <summary>Screenshot scenarios pin the hovered tile so it does not follow the real mouse.</summary>
        public Coord? ForcedHover { get; set; }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Shapes.Init(art);
            audioDirector = gameObject.AddComponent<AudioDirector>();
            audioDirector.Init();
            record = new BattleRecord(new PlayerPrefsRecordStore());
            unitRoot = new GameObject("Units").transform;

            hud.PlayClicked += StartBattle;
            hud.RestartClicked += StartBattle;
            hud.TitleClicked += ToTitle;
            hud.EndTurnClicked += EndPlayerTurn;
            hud.WaitClicked += WaitSelected;
            hud.PauseClicked += TogglePause;
            hud.ResumeClicked += TogglePause;
            hud.ButtonPressed += () => audioDirector.Play(art.Click, 0.7f);
        }

        void Start()
        {
            ToTitle();
        }

        void OnEnable()
        {
            leftClick = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");
            leftClick.performed += _ => leftClicks++;
            leftClick.Enable();
            rightClick = new InputAction("Cancel click", InputActionType.Button, "<Mouse>/rightButton");
            rightClick.performed += _ => rightClicks++;
            rightClick.Enable();
        }

        void OnDisable()
        {
            leftClick?.Dispose();
            rightClick?.Dispose();
        }

        // ---- Flow ----

        public void ToTitle()
        {
            StopTurn();
            Time.timeScale = 1f;
            CreateMatch();
            SetPhase(GamePhase.Title);
            hud.ShowScreen(HudScreen.Title);
            hud.SetRecord(record);
            hud.SetMuted(audioDirector.Muted);
            cameraRig.Drift = 1f;
            audioDirector.PlayMusic(art.CampMusic);
            audioDirector.SetDucked(false);
        }

        public void StartBattle()
        {
            StopTurn();
            Time.timeScale = 1f;
            CreateMatch();
            cameraRig.Drift = 0f;
            hud.ShowScreen(HudScreen.Battle);
            audioDirector.PlayMusic(art.BattleMusic);
            audioDirector.SetDucked(false);
            BeginPlayerTurn();
        }

        void CreateMatch()
        {
            Match = Battlefield.CreateMatch();
            selected = null;
            ForcedHover = null;
            boardView.Build(Match.Board, art);
            foreach (UnitView view in views.Values)
            {
                Destroy(view.gameObject);
            }
            views.Clear();
            foreach (Unit unit in Match.Board.Units)
            {
                views[unit.Id] = UnitView.Create(unitRoot, unit, art);
            }
            RefreshUnits();
            marksDirty = true;
        }

        void SetPhase(GamePhase phase)
        {
            Phase = phase;
            phaseTime = Time.unscaledTime;
            marksDirty = true;
        }

        // Every caller replaces the match, so nothing the director started may
        // keep running: that includes the delayed result screen of a finished
        // battle and the fall animations of defeated units.
        void StopTurn()
        {
            StopAllCoroutines();
            turnRoutine = null;
            StopAllCoroutinesOnViews();
            busy = 0;
        }

        void StopAllCoroutinesOnViews()
        {
            foreach (UnitView view in views.Values)
            {
                view.StopAllCoroutines();
            }
        }

        void BeginPlayerTurn()
        {
            SetPhase(GamePhase.PlayerTurn);
            selected = null;
            UpdateTopBar();
            hud.Banner("YOUR TURN", "player");
            audioDirector.Play(art.TurnPlayer, 0.8f, 0f);
            RefreshUnits();
            Unit first = Match.PendingUnits().FirstOrDefault();
            if (first != null && keyboardCursor)
            {
                cursor = first.Position;
            }
        }

        public void EndPlayerTurn()
        {
            if (Phase != GamePhase.PlayerTurn || Busy || Match.IsOver)
            {
                return;
            }
            Deselect();
            Match.EndTurn();
            turnRoutine = StartCoroutine(EnemyTurn());
        }

        IEnumerator EnemyTurn()
        {
            SetPhase(GamePhase.EnemyTurn);
            UpdateTopBar();
            RefreshUnits();
            hud.Banner("ENEMY TURN", "enemy");
            audioDirector.Play(art.TurnEnemy, 0.8f, 0f);
            yield return new WaitForSeconds(0.9f);

            AiAction action;
            while (!Match.IsOver && (action = TacticsAi.ChooseNext(Match)) != null)
            {
                Unit unit = action.Unit;
                selected = unit;
                marksDirty = true;
                yield return new WaitForSeconds(AiStepDelay);
                if (action.Moves)
                {
                    yield return MoveRoutine(unit, action.Destination);
                }
                if (action.Target != null)
                {
                    boardView.ClearMarks();
                    boardView.Mark(new[] { action.Target.Position }, TileMark.Attack);
                    yield return new WaitForSeconds(0.2f);
                    yield return AttackRoutine(unit, action.Target);
                }
                else if (!unit.HasActed)
                {
                    Match.Wait(unit);
                }
                selected = null;
                RefreshUnits();
                yield return new WaitForSeconds(AiStepDelay * 0.6f);
            }
            turnRoutine = null;
            if (Match.IsOver)
            {
                yield break;
            }
            Match.EndTurn();
            BeginPlayerTurn();
        }

        public void TogglePause()
        {
            if (Phase == GamePhase.Paused)
            {
                Time.timeScale = 1f;
                SetPhase(pausedFrom);
                hud.ShowScreen(HudScreen.Battle);
                audioDirector.SetDucked(false);
                UpdateTopBar();
            }
            else if (Phase == GamePhase.PlayerTurn || Phase == GamePhase.EnemyTurn)
            {
                pausedFrom = Phase;
                Time.timeScale = 0f;
                SetPhase(GamePhase.Paused);
                hud.ShowScreen(HudScreen.Paused);
                hud.SetMuted(audioDirector.Muted);
                audioDirector.SetDucked(true);
            }
        }

        IEnumerator FinishBattle()
        {
            boardView.ClearMarks();
            boardView.SetSelection(null);
            bool won = Match.Winner == Team.Player;
            bool fastest = record.Record(won, Match.Round);
            hud.Banner(won ? "VICTORY" : "DEFEAT", won ? "gold" : "enemy");
            audioDirector.Play(won ? art.Victory : art.Defeat, 1f, 0f);
            audioDirector.SetDucked(true);
            yield return new WaitForSeconds(1.4f);
            ShowGameOver(won, fastest);
        }

        void ShowGameOver(bool won, bool fastest)
        {
            SetPhase(GamePhase.GameOver);
            hud.ShowScreen(HudScreen.GameOver);
            hud.ShowResult(won, Match, record, fastest);
            hud.SetRecord(record);
        }

        // ---- Player actions ----

        void Select(Unit unit)
        {
            if (Match.CheckSelect(unit) != ActionError.None)
            {
                audioDirector.Play(art.Invalid, 0.6f);
                return;
            }
            selected = unit;
            audioDirector.Play(art.Select, 0.7f);
            marksDirty = true;
        }

        void Deselect()
        {
            selected = null;
            marksDirty = true;
        }

        public void WaitSelected()
        {
            if (Phase != GamePhase.PlayerTurn || Busy || selected == null)
            {
                return;
            }
            Match.Wait(selected);
            audioDirector.Play(art.Click, 0.6f);
            Deselect();
            AfterPlayerAction();
        }

        void Confirm(Coord tile)
        {
            if (Phase != GamePhase.PlayerTurn || Busy)
            {
                return;
            }
            Unit clicked = Match.Board.UnitAt(tile);
            if (selected != null)
            {
                if (clicked == selected)
                {
                    if (!selected.HasMoved)
                    {
                        Deselect();
                    }
                    return;
                }
                if (clicked != null && clicked.Team != selected.Team)
                {
                    if (Match.CheckAttack(selected, clicked) == ActionError.None)
                    {
                        turnRoutine = StartCoroutine(PlayerAttack(selected, clicked));
                    }
                    else
                    {
                        audioDirector.Play(art.Invalid, 0.6f);
                    }
                    return;
                }
                if (clicked == null && Match.CheckMove(selected, tile) == ActionError.None)
                {
                    turnRoutine = StartCoroutine(PlayerMove(selected, tile));
                    return;
                }
                if (clicked != null && clicked.Team == Team.Player && !selected.HasMoved && !Match.IsDone(clicked))
                {
                    Select(clicked);
                    return;
                }
                if (!selected.HasMoved)
                {
                    Deselect();
                }
                else
                {
                    audioDirector.Play(art.Invalid, 0.6f);
                }
                return;
            }
            if (clicked != null && clicked.Team == Team.Player)
            {
                Select(clicked);
            }
        }

        void Cancel()
        {
            if (Phase != GamePhase.PlayerTurn || Busy)
            {
                return;
            }
            if (selected == null)
            {
                return;
            }
            if (selected.HasMoved)
            {
                WaitSelected();
            }
            else
            {
                Deselect();
            }
        }

        void SelectNextReady()
        {
            if (Phase != GamePhase.PlayerTurn || Busy || (selected != null && selected.HasMoved))
            {
                return;
            }
            List<Unit> pending = Match.PendingUnits();
            if (pending.Count == 0)
            {
                return;
            }
            int index = selected == null ? 0 : (pending.IndexOf(selected) + 1) % pending.Count;
            Select(pending[index]);
            cursor = pending[index].Position;
            keyboardCursor = true;
        }

        IEnumerator PlayerMove(Unit unit, Coord tile)
        {
            yield return MoveRoutine(unit, tile);
            turnRoutine = null;
            if (Match.IsDone(unit))
            {
                Deselect();
            }
            AfterPlayerAction();
        }

        IEnumerator PlayerAttack(Unit attacker, Unit defender)
        {
            yield return AttackRoutine(attacker, defender);
            turnRoutine = null;
            Deselect();
            AfterPlayerAction();
        }

        void AfterPlayerAction()
        {
            RefreshUnits();
            marksDirty = true;
            if (Match.IsOver || Phase != GamePhase.PlayerTurn)
            {
                return;
            }
            if (Match.PendingUnits().Count == 0)
            {
                turnRoutine = StartCoroutine(AutoEndTurn());
            }
        }

        IEnumerator AutoEndTurn()
        {
            busy++;
            yield return new WaitForSeconds(0.5f);
            busy--;
            turnRoutine = null;
            EndPlayerTurn();
        }

        // ---- Shared animated actions ----

        IEnumerator MoveRoutine(Unit unit, Coord tile)
        {
            busy++;
            MoveResult result = Match.Move(unit, tile);
            UnitView view = views[unit.Id];
            boardView.ClearMarks();
            audioDirector.Play(art.Move, 0.8f);
            yield return view.MoveTo(tile);
            if (result.Promoted)
            {
                audioDirector.Play(art.Promote, 0.9f, 0f);
                Effects.Burst(view.transform.position + Vector3.up * 0.5f, art.ParticleAdditive,
                    new Color(1f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.2f), 40, 3f, 0.22f, 0.8f, -0.2f);
                hud.Float(view.transform.position + Vector3.up * 1.4f, "WAR DOG!", "gold");
                yield return view.Promote();
            }
            busy--;
            RefreshUnits();
            marksDirty = true;
        }

        IEnumerator AttackRoutine(Unit attacker, Unit defender)
        {
            busy++;
            UnitView attackerView = views[attacker.Id];
            UnitView defenderView = views[defender.Id];
            AttackResult result = null;
            yield return Strike(attackerView, defenderView, () => result = Match.Attack(attacker, defender),
                () => result.Outcome.Damage, () => result.Outcome.DefenderFalls);

            if (result.Outcome.CounterDamage > 0)
            {
                yield return new WaitForSeconds(0.15f);
                yield return Strike(defenderView, attackerView, null, () => result.Outcome.CounterDamage,
                    () => result.Outcome.AttackerFalls);
            }

            var falls = new List<UnitView>();
            if (result.Outcome.DefenderFalls) falls.Add(defenderView);
            if (result.Outcome.AttackerFalls) falls.Add(attackerView);
            foreach (UnitView fallen in falls)
            {
                audioDirector.Play(art.UnitDown, 0.9f);
                Effects.Burst(fallen.transform.position + Vector3.up * 0.3f, art.ParticleAlpha,
                    new Color(0.55f, 0.5f, 0.45f, 0.8f), new Color(0.3f, 0.28f, 0.26f, 0.7f), 26, 1.6f, 0.4f, 0.9f, -0.1f);
                StartCoroutine(fallen.Fall());
            }
            if (falls.Count > 0)
            {
                cameraRig.Shake(0.45f);
                yield return new WaitForSeconds(0.75f);
            }
            busy--;
            RefreshUnits();
            UpdateTopBar();
            if (Match.IsOver)
            {
                StopTurnRoutineForFinish();
            }
        }

        void StopTurnRoutineForFinish()
        {
            if (turnRoutine != null)
            {
                StopCoroutine(turnRoutine);
                turnRoutine = null;
            }
            busy = 0;
            StartCoroutine(FinishBattle());
        }

        IEnumerator Strike(UnitView from, UnitView to, System.Action apply, System.Func<int> damage, System.Func<bool> falls)
        {
            Vector3 target = to.transform.position;
            bool ranged = from.Unit.Stats.IsRanged;
            void Impact()
            {
                apply?.Invoke();
                to.Hit();
                int amount = damage();
                hud.Float(to.transform.position + Vector3.up * (to.Height + 0.1f), $"-{amount}", "damage");
                Color spark = from.Unit.Team == Team.Player ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.55f, 0.35f);
                Effects.Burst(to.transform.position + Vector3.up * (to.Height * 0.6f), art.ParticleAdditive,
                    spark, Color.white, 22, 3.2f, 0.16f, 0.45f, 0.4f);
                cameraRig.Shake(falls() ? 0.35f : 0.22f);
                audioDirector.Play(ranged ? art.Bolt : art.Slash, 0.9f);
            }

            if (ranged)
            {
                StartCoroutine(from.Strike(target, null));
                yield return Projectile(from, to);
                Impact();
                yield return new WaitForSeconds(0.25f);
            }
            else
            {
                yield return from.Strike(target, Impact);
                yield return new WaitForSeconds(0.1f);
            }
        }

        IEnumerator Projectile(UnitView from, UnitView to)
        {
            GameObject bolt = Shapes.Create(PrimitiveType.Sphere);
            bolt.transform.localScale = Vector3.one * 0.18f;
            bolt.GetComponent<Renderer>().sharedMaterial = from.Unit.Team == Team.Player ? art.PlayerAccent : art.EnemyAccent;
            Vector3 start = from.transform.position + Vector3.up * from.Height;
            Vector3 end = to.transform.position + Vector3.up * (to.Height * 0.6f);
            const float duration = 0.3f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                bolt.transform.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.6f;
                yield return null;
            }
            Destroy(bolt);
        }

        // ---- Per frame ----

        void Update()
        {
            ReadInput();
            UpdateHover();
            if (marksDirty)
            {
                marksDirty = false;
                RefreshMarks();
            }
        }

        void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool clicked = leftClicks > 0;
            bool rightClicked = rightClicks > 0;
            leftClicks = 0;
            rightClicks = 0;
            bool confirm = Pressed(keyboard?.spaceKey) || Pressed(keyboard?.enterKey) || Pressed(keyboard?.numpadEnterKey) || Pressed(pad?.buttonSouth);
            bool back = Pressed(keyboard?.escapeKey) || Pressed(pad?.startButton);
            bool cancel = Pressed(keyboard?.backspaceKey) || Pressed(pad?.buttonEast) || rightClicked;

            if (Pressed(keyboard?.mKey))
            {
                audioDirector.ToggleMute();
                hud.SetMuted(audioDirector.Muted);
            }

            switch (Phase)
            {
                case GamePhase.Title:
                    if (hud.RulesOpen && (back || confirm))
                    {
                        hud.ShowRules(false);
                    }
                    else if (confirm && Time.unscaledTime - phaseTime > 0.2f)
                    {
                        StartBattle();
                    }
                    return;
                case GamePhase.Paused:
                    if (back || cancel)
                    {
                        TogglePause();
                    }
                    return;
                case GamePhase.GameOver:
                    if (Time.unscaledTime - phaseTime < MenuKeyDelay)
                    {
                        return;
                    }
                    if (confirm)
                    {
                        StartBattle();
                    }
                    else if (back)
                    {
                        ToTitle();
                    }
                    return;
                case GamePhase.EnemyTurn:
                    if (back)
                    {
                        TogglePause();
                    }
                    return;
            }

            // Player turn.
            if (back)
            {
                if (selected != null)
                {
                    Cancel();
                }
                else
                {
                    TogglePause();
                }
                return;
            }
            if (cancel)
            {
                Cancel();
            }
            if (Pressed(keyboard?.eKey) || Pressed(pad?.buttonNorth))
            {
                EndPlayerTurn();
            }
            if (Pressed(keyboard?.xKey) || Pressed(pad?.buttonWest))
            {
                WaitSelected();
            }
            if (Pressed(keyboard?.tabKey) || Pressed(pad?.rightShoulder))
            {
                SelectNextReady();
            }

            Vector2Int step = Vector2Int.zero;
            if (Pressed(keyboard?.upArrowKey) || Pressed(keyboard?.wKey)) step.y++;
            if (Pressed(keyboard?.downArrowKey) || Pressed(keyboard?.sKey)) step.y--;
            if (Pressed(keyboard?.rightArrowKey) || Pressed(keyboard?.dKey)) step.x++;
            if (Pressed(keyboard?.leftArrowKey) || Pressed(keyboard?.aKey)) step.x--;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                if (stick.magnitude > 0.5f)
                {
                    stickRepeat -= Time.unscaledDeltaTime;
                    if (stickRepeat <= 0f)
                    {
                        stickRepeat = 0.18f;
                        if (Mathf.Abs(stick.x) > 0.5f) step.x += stick.x > 0f ? 1 : -1;
                        if (Mathf.Abs(stick.y) > 0.5f) step.y += stick.y > 0f ? 1 : -1;
                    }
                }
                else
                {
                    stickRepeat = 0f;
                }
            }
            if (step != Vector2Int.zero)
            {
                keyboardCursor = true;
                cursor = new Coord(Mathf.Clamp(cursor.X + step.x, 0, 7), Mathf.Clamp(cursor.Y + step.y, 0, 7));
            }

            if (mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                if ((position - lastMouse).sqrMagnitude > 4f)
                {
                    keyboardCursor = false;
                }
                lastMouse = position;
                if (clicked && !hud.IsOverUi(position))
                {
                    keyboardCursor = false;
                    hovered = TileUnderPointer(position);
                    marksDirty = true;
                    if (hovered.HasValue)
                    {
                        Confirm(hovered.Value);
                    }
                }
            }
            if (confirm && hovered.HasValue)
            {
                Confirm(hovered.Value);
            }
        }

        static bool Pressed(UnityEngine.InputSystem.Controls.ButtonControl button) => button != null && button.wasPressedThisFrame;

        void UpdateHover()
        {
            Coord? tile = null;
            if (ForcedHover.HasValue)
            {
                tile = ForcedHover;
            }
            else if (Phase == GamePhase.PlayerTurn)
            {
                if (keyboardCursor)
                {
                    tile = cursor;
                }
                else if (Mouse.current != null)
                {
                    Vector2 position = Mouse.current.position.ReadValue();
                    if (!hud.IsOverUi(position))
                    {
                        tile = TileUnderPointer(position);
                    }
                }
            }
            if (tile != hovered)
            {
                hovered = tile;
                marksDirty = true;
            }
        }

        Coord? TileUnderPointer(Vector2 screen)
        {
            Camera view = Camera.main;
            if (view == null)
            {
                return null;
            }
            Ray ray = view.ScreenPointToRay(screen);
            UnitView best = null;
            float bestDistance = float.MaxValue;
            foreach (UnitView unitView in views.Values)
            {
                float? distance = unitView.Pick(ray);
                if (distance.HasValue && distance.Value < bestDistance)
                {
                    best = unitView;
                    bestDistance = distance.Value;
                }
            }
            if (best != null)
            {
                return best.Unit.Position;
            }
            if (Mathf.Abs(ray.direction.y) < 1e-4f)
            {
                return null;
            }
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f)
            {
                return null;
            }
            Coord tile = BoardView.TileAt(ray.origin + ray.direction * t);
            return Match.Board.InBounds(tile) ? tile : (Coord?)null;
        }

        // ---- Presentation ----

        void RefreshUnits()
        {
            if (Match == null)
            {
                return;
            }
            bool battle = Phase == GamePhase.PlayerTurn || Phase == GamePhase.EnemyTurn;
            foreach (UnitView view in views.Values)
            {
                Unit unit = view.Unit;
                bool current = battle && unit.Team == Match.CurrentTeam;
                view.SetReady(!current || !Match.IsDone(unit), current);
            }
            hud.SetArmies(Match.Board.UnitsOf(Team.Player).Count(), Match.Board.UnitsOf(Team.Enemy).Count());
        }

        void UpdateTopBar() => hud.SetTurn(Match.CurrentTeam, Match.Round, Phase == GamePhase.PlayerTurn);

        void RefreshMarks()
        {
            if (Match == null)
            {
                return;
            }
            boardView.ClearMarks();
            foreach (UnitView view in views.Values)
            {
                view.SetSelected(view.Unit == selected);
            }
            boardView.SetSelection(selected?.Position);
            boardView.SetHover(Phase == GamePhase.PlayerTurn ? hovered : null);
            hud.HideForecast();

            if (Phase != GamePhase.PlayerTurn && Phase != GamePhase.EnemyTurn)
            {
                hud.HideUnit();
                return;
            }

            Board board = Match.Board;
            Unit hoveredUnit = hovered.HasValue ? board.UnitAt(hovered.Value) : null;

            if (selected != null)
            {
                if (!selected.HasMoved)
                {
                    boardView.Mark(Movement.Destinations(board, selected), TileMark.Move);
                }
                Coord from = selected.Position;
                if (Phase == GamePhase.PlayerTurn && hovered.HasValue && !selected.HasMoved && Movement.CanReach(board, selected, hovered.Value))
                {
                    from = hovered.Value; // preview the targets from the tile under the cursor
                }
                boardView.Mark(Movement.TargetsFrom(board, selected, from).Select(u => u.Position), TileMark.Attack);

                if (Phase == GamePhase.PlayerTurn && hoveredUnit != null && hoveredUnit.Team != selected.Team
                    && Match.CheckAttack(selected, hoveredUnit) == ActionError.None)
                {
                    hud.ShowForecast(art.NameOf(selected), art.NameOf(hoveredUnit), Combat.Forecast(selected, hoveredUnit),
                        hoveredUnit, selected);
                }
            }
            else if (hoveredUnit != null && hoveredUnit.Team == Team.Enemy && Phase == GamePhase.PlayerTurn)
            {
                // Show where this enemy could strike next turn.
                var reach = new HashSet<Coord>();
                foreach (Coord tile in Movement.Destinations(board, hoveredUnit).Append(hoveredUnit.Position))
                {
                    reach.UnionWith(Movement.AttackArea(board, hoveredUnit, tile));
                }
                boardView.Mark(reach, TileMark.Threat);
            }
            else if (hoveredUnit != null && hoveredUnit.Team == Team.Player && Phase == GamePhase.PlayerTurn && !Match.IsDone(hoveredUnit))
            {
                boardView.Mark(Movement.Destinations(board, hoveredUnit), TileMark.Move);
            }

            Unit shown = Phase == GamePhase.EnemyTurn ? selected : selected ?? hoveredUnit;
            if (shown != null)
            {
                hud.ShowUnit(shown, art, StatusOf(shown), Phase == GamePhase.PlayerTurn && shown == selected);
            }
            else
            {
                hud.HideUnit();
            }
            hud.SetHint(HintText());
        }

        string StatusOf(Unit unit)
        {
            if (unit.Team != Match.CurrentTeam)
            {
                return unit.Team == Team.Enemy ? "Orange tiles: where it can strike" : string.Empty;
            }
            if (Phase == GamePhase.EnemyTurn)
            {
                return "Acting";
            }
            if (Match.IsDone(unit))
            {
                return "Done this turn";
            }
            if (unit.HasMoved)
            {
                return "Moved - attack or wait";
            }
            return "Ready";
        }

        string HintText()
        {
            if (Phase == GamePhase.EnemyTurn)
            {
                return "The Horde is moving...";
            }
            if (selected == null)
            {
                return Match.PendingUnits().Count > 0
                    ? "Select a unit with a coloured disc  ·  Tab: next unit  ·  E: end turn"
                    : "All units have acted  ·  E: end turn";
            }
            if (selected.HasMoved)
            {
                return "Click a red enemy to attack  ·  X or right-click: wait";
            }
            return "Blue: move  ·  Red: attack  ·  Right-click: cancel";
        }

        // ---- Screenshot support ----

        /// <summary>Plays <paramref name="rounds"/> rounds instantly with the AI on both sides and snaps the pieces into place.</summary>
        public void Simulate(int rounds)
        {
            for (int i = 0; i < rounds * 2 && !Match.IsOver; i++)
            {
                TacticsAi.PlayTurn(Match);
            }
            foreach (UnitView view in views.Values)
            {
                view.StopAllCoroutines();
                view.transform.position = BoardView.TileCenter(view.Unit.Position);
                view.transform.localScale = Vector3.one;
                view.RefreshModel();
                view.gameObject.SetActive(view.Unit.IsAlive);
            }
            if (Match.CurrentTeam != Team.Player && !Match.IsOver)
            {
                TacticsAi.PlayTurn(Match);
            }
            selected = null;
            RefreshUnits();
            UpdateTopBar();
            marksDirty = true;
        }

        public void SelectForScreenshot(Unit unit)
        {
            selected = unit;
            marksDirty = true;
        }

        /// <summary>Keeps screenshot runs from touching the saved battle record.</summary>
        public void UseMemoryRecord() => record = new BattleRecord(new MemoryRecordStore());

        /// <summary>Plays the battle out with the AI on both sides and shows the real result screen.</summary>
        public void FinishForScreenshot()
        {
            StopTurn();
            while (!Match.IsOver && Match.Round <= 80)
            {
                TacticsAi.PlayTurn(Match);
            }
            Simulate(0);
            if (Match.IsOver)
            {
                bool won = Match.Winner == Team.Player;
                ShowGameOver(won, record.Record(won, Match.Round));
            }
        }

        public UnitView ViewOf(Unit unit) => views[unit.Id];

        /// <summary>Acts as if the player clicked <paramref name="tile"/> (used by the screenshot smoke run).</summary>
        public void ClickTile(Coord tile) => Confirm(tile);
    }
}

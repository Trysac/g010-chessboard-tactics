using System;
using System.Collections;
using System.IO;
using System.Linq;
using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>
    /// Opt-in capture of README screenshots from a built player:
    /// <c>ChessboardTactics.exe -screenshot &lt;folder&gt; -scenario title,select,forecast,enemy,gameover</c>.
    /// Without the -screenshot argument nothing is created, so normal runs and WebGL builds
    /// (which have no command line) are unaffected. Scenarios run in the order given.
    /// The same hook runs inside the editor (ProjectBuilder.CaptureScreenshots), which renders
    /// the camera and the HUD off screen and blends them, for machines that cannot run the player.
    /// Adding <c>-frames N [-fps F]</c> records N numbered frames per scenario at a fixed timestep
    /// (into &lt;folder&gt;/&lt;scenario&gt;/) instead of one still, e.g. <c>-scenario gameplay -frames 90 -fps 15</c>.
    /// </summary>
    public sealed class ScreenshotScenarios : MonoBehaviour
    {
        const string DefaultScenarios = "title,rules,select,forecast,enemy,gameover";

        const int Width = 1280;
        const int Height = 720;

        string folder;
        string[] scenarios;
        int frames;
        int fps;
        RenderTexture sceneTarget;
        RenderTexture uiTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            string target = Argument("-screenshot");
            if (string.IsNullOrEmpty(target))
            {
                return;
            }
            var host = new GameObject(nameof(ScreenshotScenarios)).AddComponent<ScreenshotScenarios>();
            host.folder = target;
            host.scenarios = (Argument("-scenario") ?? DefaultScenarios).Split(',');
            host.frames = int.TryParse(Argument("-frames"), out int count) ? count : 0;
            host.fps = int.TryParse(Argument("-fps"), out int rate) ? rate : 15;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            var director = FindAnyObjectByType<GameDirector>();
            director.UseMemoryRecord();
            if (Application.isEditor)
            {
                RenderOffscreen(director);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (string raw in scenarios)
            {
                string scenario = raw.Trim();
                if (frames > 0)
                {
                    yield return Record(director, scenario);
                    continue;
                }
                yield return Stage(director, scenario);
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(folder, scenario + ".png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log($"Saved screenshot {scenario}.png");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Stages <paramref name="scenario"/>, then saves <see cref="frames"/> frames while game time
        /// advances exactly 1/<see cref="fps"/> s per frame, however long each capture takes.
        /// </summary>
        IEnumerator Record(GameDirector director, string scenario)
        {
            string target = Path.Combine(folder, scenario);
            Directory.CreateDirectory(target);
            yield return Stage(director, scenario);
            if (scenario == "gameplay")
            {
                StartCoroutine(PlayTurn(director));
            }
            Time.captureFramerate = fps;
            for (int i = 0; i < frames; i++)
            {
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(target, $"frame_{i:0000}.png"), shot.EncodeToPNG());
                Destroy(shot);
            }
            Time.captureFramerate = 0;
            Debug.Log($"Saved {frames} frames of {scenario} at {fps} fps.");
        }

        /// <summary>One real player action (select, move, attack) followed by the enemy's animated reply.</summary>
        static IEnumerator PlayTurn(GameDirector director)
        {
            Match match = director.Match;
            Unit attacker = null;
            Coord? destination = null;
            Unit target = null;
            foreach (Unit unit in match.PendingUnits())
            {
                foreach (Coord tile in Movement.Destinations(match.Board, unit))
                {
                    target = Movement.TargetsFrom(match.Board, unit, tile).FirstOrDefault();
                    if (target != null)
                    {
                        attacker = unit;
                        destination = tile;
                        break;
                    }
                }
                if (attacker != null)
                {
                    break;
                }
            }
            if (attacker == null)
            {
                director.EndPlayerTurn();
                yield break;
            }
            yield return new WaitForSeconds(0.4f);
            director.ClickTile(attacker.Position);
            yield return new WaitForSeconds(0.9f);
            director.ClickTile(destination.Value);
            yield return null;
            while (director.Busy)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            director.ClickTile(target.Position);
            yield return null;
            while (director.Busy)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            director.EndPlayerTurn();
        }

        /// <summary>Points the camera and the HUD panel at render textures of a fixed size.</summary>
        void RenderOffscreen(GameDirector director)
        {
            sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Camera.main.targetTexture = sceneTarget;
            var document = director.Hud.GetComponent<UnityEngine.UIElements.UIDocument>();
            UnityEngine.UIElements.PanelSettings panel = Instantiate(document.panelSettings);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            document.panelSettings = panel;
        }

        /// <summary>Blends the HUD over the 3D view, like the screen would show them.</summary>
        Texture2D CaptureOffscreen()
        {
            Color[] scene = Read(sceneTarget);
            Color[] ui = Read(uiTarget);
            for (int i = 0; i < scene.Length; i++)
            {
                Color over = ui[i];
                Color result = Color.Lerp(scene[i], new Color(over.r, over.g, over.b), over.a);
                result.a = 1f;
                scene[i] = result;
            }
            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels(scene);
            shot.Apply();
            return shot;
        }

        static Color[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Destroy(texture);
            return pixels;
        }

        static IEnumerator Stage(GameDirector director, string scenario)
        {
            director.ForcedHover = null;
            switch (scenario)
            {
                case "title":
                    director.ToTitle();
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                case "rules":
                    director.ToTitle();
                    director.Hud.ShowRules(true);
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case "select":
                    yield return Battle(director, 2);
                    Unit unit = director.Match.PendingUnits().OrderByDescending(u => Movement.Destinations(director.Match.Board, u).Count).First();
                    director.SelectForScreenshot(unit);
                    director.ForcedHover = Movement.Destinations(director.Match.Board, unit).OrderByDescending(t => t.Y).First();
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case "forecast":
                    yield return Battle(director, 4);
                    StageAttack(director);
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                case "enemy":
                    yield return Battle(director, 3);
                    director.EndPlayerTurn();
                    yield return new WaitForSecondsRealtime(2.2f);
                    break;
                case "play":
                    // Smoke run of the real animated flow: select, move, end the turn, let the AI play.
                    director.StartBattle();
                    yield return new WaitForSecondsRealtime(1.2f);
                    director.ClickTile(new Coord(3, 1));
                    director.ClickTile(new Coord(3, 3));
                    yield return new WaitForSecondsRealtime(1f);
                    director.ClickTile(new Coord(1, 0));
                    director.ClickTile(new Coord(2, 2));
                    yield return new WaitForSecondsRealtime(1f);
                    director.EndPlayerTurn();
                    float waited = 0f;
                    while (director.Phase != GamePhase.PlayerTurn && waited < 40f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Debug.Log($"Smoke run: enemy turn finished={director.Phase == GamePhase.PlayerTurn} after {waited:0.0}s, round {director.Match.Round}, " +
                        $"pawn at {director.Match.Board.UnitAt(new Coord(3, 3))?.Type}, knight at {director.Match.Board.UnitAt(new Coord(2, 2))?.Type}.");
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                case "gameplay":
                    yield return Battle(director, 3);
                    break;
                case "gameover":
                    yield return Battle(director, 0);
                    director.FinishForScreenshot();
                    yield return new WaitForSecondsRealtime(0.8f);
                    break;
                default:
                    Debug.LogWarning($"Unknown screenshot scenario '{scenario}'.");
                    break;
            }
        }

        static IEnumerator Battle(GameDirector director, int rounds)
        {
            director.StartBattle();
            director.Simulate(rounds);
            yield return new WaitForSecondsRealtime(1.6f); // let the turn banner fade
        }

        /// <summary>Finds a player unit that can attack (moving it first if needed), selects it and hovers its target.</summary>
        static void StageAttack(GameDirector director)
        {
            Match match = director.Match;
            foreach (Unit unit in match.PendingUnits())
            {
                Unit target = Movement.Targets(match.Board, unit).FirstOrDefault();
                if (target == null)
                {
                    foreach (Coord tile in Movement.Destinations(match.Board, unit))
                    {
                        if (Movement.TargetsFrom(match.Board, unit, tile).Count > 0)
                        {
                            match.Move(unit, tile);
                            director.ViewOf(unit).transform.position = BoardView.TileCenter(tile);
                            target = Movement.Targets(match.Board, unit).First();
                            break;
                        }
                    }
                }
                if (target != null)
                {
                    director.SelectForScreenshot(unit);
                    director.ForcedHover = target.Position;
                    return;
                }
            }
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

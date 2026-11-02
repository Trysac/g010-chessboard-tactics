using System;
using System.IO;
using ChessboardTactics.Core;
using ChessboardTactics.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ChessboardTactics.EditorTools
{
    /// <summary>
    /// Generates the materials, unit data, art asset, scene and player settings from code, and
    /// builds the players, so every binary asset can be recreated from text. Batch mode:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod ChessboardTactics.EditorTools.ProjectBuilder.SetUpProject
    /// Unity -batchmode -quit -projectPath . -executeMethod ChessboardTactics.EditorTools.ProjectBuilder.BuildWindows -buildOutput &lt;folder&gt;
    /// Unity -batchmode -quit -projectPath . -executeMethod ChessboardTactics.EditorTools.ProjectBuilder.BuildWebGL -buildOutput &lt;folder&gt;
    /// </code>
    /// </summary>
    public static class ProjectBuilder
    {
        const string ScenePath = "Assets/Scenes/Battle.unity";
        const string ArtFolder = "Assets/Art";
        const string MaterialFolder = "Assets/Art/Materials";
        const string ArtPath = "Assets/Art/GameArt.asset";
        const string SoftDotPath = "Assets/Art/SoftDot.png";
        const string IconPath = "Assets/Art/AppIcon.png";
        const string WarriorFolder = "Assets/Data/Warriors";
        const string AudioFolder = "Assets/Audio";
        const string LayoutPath = "Assets/UI/Hud.uxml";
        const string ThemePath = "Assets/UI/HudTheme.tss";
        const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";

        static readonly Color Background = Hex("#11151e");

        [MenuItem("Chessboard Tactics/Regenerate Art, Scene and Player Settings")]
        public static void SetUpProject()
        {
            GameArt art = CreateArt();
            PanelSettings panel = CreatePanelSettings();
            CreateScene(art, panel);
            ConfigurePlayer(CreateIcon());
            AssetDatabase.SaveAssets();
            Debug.Log("Chessboard Tactics project set up.");
        }

        /// <summary>
        /// Enters play mode and lets ScreenshotScenarios capture the README screenshots, then exits.
        /// Run in batch mode without -quit: <c>-executeMethod ...CaptureScreenshots -screenshot &lt;folder&gt;</c>.
        /// </summary>
        public static void CaptureScreenshots()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "ChessboardTactics.exe");

        public static void BuildWebGL() => Build(BuildTarget.WebGL, string.Empty);

        // ---- Art ----

        static GameArt CreateArt()
        {
            Directory.CreateDirectory(MaterialFolder);
            var art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameArt>();
                AssetDatabase.CreateAsset(art, ArtPath);
            }

            art.LightTile = Lit("LightTile", "#e6d3ab", 0.35f);
            art.DarkTile = Lit("DarkTile", "#8c5d3b", 0.35f);
            art.Frame = Lit("Frame", "#3a2417", 0.45f);
            art.Trim = Lit("Trim", "#c99b4f", 0.6f, metallic: 0.6f);
            art.Table = Lit("Table", "#1b2130", 0.05f);
            art.Rock = Lit("Rock", "#8f8b85", 0.1f);
            art.RockDark = Lit("RockDark", "#6b6762", 0.08f);

            art.MoveHighlight = Overlay("MoveHighlight", new Color(0.25f, 0.6f, 1f, 0.6f));
            art.AttackHighlight = Overlay("AttackHighlight", new Color(1f, 0.25f, 0.2f, 0.65f));
            art.ThreatHighlight = Overlay("ThreatHighlight", new Color(1f, 0.55f, 0.15f, 0.38f));
            art.SelectHighlight = Overlay("SelectHighlight", new Color(1f, 0.85f, 0.3f, 0.75f));
            art.HoverHighlight = Overlay("HoverHighlight", new Color(1f, 1f, 1f, 0.3f));

            art.PlayerBody = Lit("PlayerBody", "#f1ead8", 0.5f, emission: "#000000");
            art.PlayerAccent = Lit("PlayerAccent", "#3d86ec", 0.55f, emission: "#000000");
            art.EnemyBody = Lit("EnemyBody", "#4b3c37", 0.5f, emission: "#000000");
            art.EnemyAccent = Lit("EnemyAccent", "#de4436", 0.55f, emission: "#000000");
            art.Gold = Lit("Gold", "#e4b44c", 0.7f, metallic: 0.7f, emission: "#000000");
            art.BarBack = Unlit("BarBack", "#121318");
            art.BarFill = Unlit("BarFill", "#ffffff");

            Texture2D dot = CreateSoftDot();
            art.ParticleAdditive = Particle("ParticleAdditive", dot, additive: true);
            art.ParticleAlpha = Particle("ParticleAlpha", dot, additive: false);

            art.Warriors = CreateWarriors();

            art.BattleMusic = Clip("battle_theme", music: true);
            art.CampMusic = Clip("camp_theme", music: true);
            art.Select = Clip("select");
            art.Move = Clip("move");
            art.Slash = Clip("slash");
            art.Bolt = Clip("bolt");
            art.UnitDown = Clip("unit_down");
            art.Promote = Clip("promote");
            art.TurnPlayer = Clip("turn_player");
            art.TurnEnemy = Clip("turn_enemy");
            art.Victory = Clip("victory");
            art.Defeat = Clip("defeat");
            art.Click = Clip("click");
            art.Invalid = Clip("invalid");

            EditorUtility.SetDirty(art);
            return art;
        }

        static WarriorData[] CreateWarriors()
        {
            Directory.CreateDirectory(WarriorFolder);
            return new[]
            {
                Warrior(PieceType.Pawn, "Pawn", "Orc Pawn", "Low range soldier. Cheap, stubborn and always one march away from glory."),
                Warrior(PieceType.Knight, "Knight", "Wolf Rider", "Circles the line and strikes from angles nobody guards."),
                Warrior(PieceType.Bishop, "Bishop", "Shaman", "Fragile up close; keep it behind the front and let it shoot."),
                Warrior(PieceType.Tower, "Tower Guard", "Troll", "Shrugs off blows that would fell anyone else."),
                Warrior(PieceType.Queen, "Queen", "Warlord", "The deadliest piece on the board. Losing her hurts."),
                Warrior(PieceType.King, "King", "Orc Chieftain", "The battle ends the moment he falls."),
                Warrior(PieceType.WarDog, "War Dog", "Warg", "What a pawn becomes after crossing the whole board."),
            };
        }

        static WarriorData Warrior(PieceType type, string playerName, string enemyName, string lore)
        {
            string path = $"{WarriorFolder}/{type}.asset";
            var data = AssetDatabase.LoadAssetAtPath<WarriorData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WarriorData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.Configure(type, playerName, enemyName, lore);
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>A Standard material; emission is enabled (black) on pieces so hits can flash them.</summary>
        static Material Lit(string name, string color, float smoothness, float metallic = 0f, string emission = null)
        {
            Material material = MaterialAt(name, Shader.Find("Standard"));
            material.color = Hex(color);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission != null)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", Hex(emission));
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Unlit(string name, string color)
        {
            Material material = MaterialAt(name, Shader.Find("Unlit/Color"));
            material.color = Hex(color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A transparent, unlit tint for tile highlights.</summary>
        static Material Overlay(string name, Color color)
        {
            Material material = MaterialAt(name, Shader.Find("Sprites/Default"));
            material.color = color;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Particle(string name, Texture2D texture, bool additive)
        {
            Material material = MaterialAt(name, Shader.Find("Legacy Shaders/Particles/" + (additive ? "Additive" : "Alpha Blended")));
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialAt(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        /// <summary>A small white disc with a soft edge for every particle effect.</summary>
        static Texture2D CreateSoftDot()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha * (3f - 2f * alpha)));
                }
            }
            texture.Apply();
            File.WriteAllBytes(SoftDotPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SoftDotPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftDotPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        }

        static AudioClip Clip(string name, bool music = false)
        {
            string path = $"{AudioFolder}/{name}.ogg";
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Missing {path}: run python tools/audio/build.py first.");
                return null;
            }
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---- UI ----

        static PanelSettings CreatePanelSettings()
        {
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ---- Scene ----

        static void CreateScene(GameArt art, PanelSettings panel)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 60f;
            cameraObject.AddComponent<AudioListener>();
            CameraRig rig = cameraObject.AddComponent<CameraRig>();

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("#ffe9c9");
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var fillObject = new GameObject("Fill light");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = Hex("#7fa4ff");
            fill.intensity = 0.25f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#8f9bb8");
            RenderSettings.ambientEquatorColor = Hex("#6d6a6a");
            RenderSettings.ambientGroundColor = Hex("#3a3029");
            RenderSettings.fog = false;

            var boardObject = new GameObject("Board");
            BoardView board = boardObject.AddComponent<BoardView>();

            var ui = new GameObject("HUD");
            var document = ui.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            Hud hud = ui.AddComponent<Hud>();

            var game = new GameObject("Game");
            var director = game.AddComponent<GameDirector>();
            var serialized = new SerializedObject(director);
            serialized.FindProperty("art").objectReferenceValue = art;
            serialized.FindProperty("hud").objectReferenceValue = hud;
            serialized.FindProperty("cameraRig").objectReferenceValue = rig;
            serialized.FindProperty("boardView").objectReferenceValue = board;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer(Texture2D icon)
        {
            PlayerSettings.companyName = "Trysac";
            PlayerSettings.productName = "Chessboard Tactics";
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var standaloneIcons = new Texture2D[sizes.Length];
            Array.Fill(standaloneIcons, icon);
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standaloneIcons, IconKind.Any);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            // Static hosts such as GitHub Pages do not send Content-Encoding for .br/.gz files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
        }

        // ---- Icon: a brass king over a slate tile with a wooden checkerboard ----

        static Texture2D CreateIcon()
        {
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = IconPixel(x + 0.5f - size / 2f, y + 0.5f - size / 2f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        static Color IconPixel(float x, float y)
        {
            const float aa = 1.5f;
            Color color = Hex("#1b2130");
            color.a = Coverage(-RoundedBox(x, y, 470f, 470f, 180f), aa);

            // A 4x4 checkerboard inset in the tile.
            float board = RoundedBox(x, y, 390f, 390f, 110f);
            bool dark = ((Mathf.FloorToInt((x + 400f) / 200f) + Mathf.FloorToInt((y + 400f) / 200f)) & 1) == 0;
            color = Over(color, Hex(dark ? "#8c5d3b" : "#e6d3ab"), Coverage(-board, aa));
            color = Over(color, Hex("#c99b4f"), Coverage(10f - Mathf.Abs(board), aa));

            // The king: base, tapered body, collar, head and cross.
            float king = RoundedBox(x, y + 300f, 210f, 42f, 30f);
            king = Mathf.Min(king, ConvexPolygon(x, y, new Vector2(-165f, -265f), new Vector2(165f, -265f),
                new Vector2(85f, 130f), new Vector2(-85f, 130f)));
            king = Mathf.Min(king, RoundedBox(x, y - 150f, 135f, 28f, 20f));
            king = Mathf.Min(king, Length(x, y - 240f) - 82f);
            king = Mathf.Min(king, RoundedBox(x, y - 375f, 24f, 64f, 8f));
            king = Mathf.Min(king, RoundedBox(x, y - 385f, 66f, 22f, 8f));
            color = Over(color, Hex("#10131b"), Coverage(-(king - 20f), aa));
            color = Over(color, Hex("#e4b44c"), Coverage(-king, aa));
            color = Over(color, Hex("#f7dc94"), Coverage(-(king + 4f), aa) * Mathf.Clamp01((y + 80f) / 600f) * 0.55f);
            return color;
        }

        static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float RoundedBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(x) - (halfWidth - radius);
            float qy = Mathf.Abs(y) - (halfHeight - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>Signed distance to a convex polygon given counter-clockwise (negative inside).</summary>
        static float ConvexPolygon(float x, float y, params Vector2[] points)
        {
            var p = new Vector2(x, y);
            float distance = float.MaxValue;
            bool inside = true;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Length];
                distance = Mathf.Min(distance, Segment(p, a, b));
                inside &= Cross(b - a, p - a) >= 0f;
            }
            return inside ? -distance : distance;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float Coverage(float inside, float width) => Mathf.Clamp01(inside / width + 0.5f);

        static Color Over(Color below, Color above, float amount)
        {
            Color mixed = Color.Lerp(below, above, amount);
            mixed.a = below.a;
            return mixed;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;

        // ---- Builds ----

        static void Build(BuildTarget target, string fileName)
        {
            string folder = Argument("-buildOutput");
            if (string.IsNullOrEmpty(folder))
            {
                throw new ArgumentException("Pass -buildOutput <folder> outside the repository.");
            }
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = target,
                locationPathName = string.IsNullOrEmpty(fileName) ? folder : Path.Combine(folder, fileName),
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Build {target}: {summary.result}, {summary.totalSize / (1024 * 1024f):0.0} MB, {summary.totalErrors} errors.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
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

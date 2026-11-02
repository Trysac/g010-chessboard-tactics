using System.Collections;
using System.Collections.Generic;
using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>The 3D piece of one unit: model, ready marker, health bar and its animations.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        static readonly Color PlayerBar = new Color(0.36f, 0.85f, 0.45f);
        static readonly Color EnemyBar = new Color(0.95f, 0.33f, 0.28f);

        GameArt art;
        Transform model;
        Transform marker;
        Transform barRoot;
        Transform barFill;
        Renderer barFillRenderer;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Color> baseColors = new List<Color>();
        MaterialPropertyBlock block;
        float displayedHealth;
        float flash;
        bool dimmed;
        bool selected;

        public Unit Unit { get; private set; }
        public float Height => PieceModel.Height(Unit.Type);
        public bool Busy { get; private set; }

        public static UnitView Create(Transform parent, Unit unit, GameArt art)
        {
            var host = new GameObject($"{unit.Team} {unit.Type} #{unit.Id}");
            host.transform.SetParent(parent, false);
            host.transform.position = BoardView.TileCenter(unit.Position);
            UnitView view = host.AddComponent<UnitView>();
            view.Unit = unit;
            view.art = art;
            view.block = new MaterialPropertyBlock();
            view.BuildMarker();
            view.BuildModel();
            view.BuildBar();
            view.displayedHealth = unit.Health;
            return view;
        }

        void BuildModel()
        {
            if (model != null)
            {
                Destroy(model.gameObject);
            }
            bool player = Unit.Team == Team.Player;
            model = PieceModel.Build(transform, Unit.Type, player ? art.PlayerBody : art.EnemyBody,
                player ? art.PlayerAccent : art.EnemyAccent, art.Gold).transform;
            model.localRotation = Quaternion.Euler(0f, player ? 0f : 180f, 0f);
            renderers.Clear();
            baseColors.Clear();
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>())
            {
                renderers.Add(part);
                baseColors.Add(part.sharedMaterial.color);
            }
            ApplyTint();
        }

        void BuildMarker()
        {
            GameObject disc = Shapes.Create(PrimitiveType.Cylinder);
            disc.name = "Ready marker";
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            disc.transform.localScale = new Vector3(0.86f, 0.006f, 0.86f);
            disc.GetComponent<Renderer>().sharedMaterial = Unit.Team == Team.Player ? art.PlayerAccent : art.EnemyAccent;
            marker = disc.transform;
        }

        void BuildBar()
        {
            barRoot = new GameObject("Health bar").transform;
            barRoot.SetParent(transform, false);
            Quad(barRoot, "Back", art.BarBack, new Vector3(0.66f, 0.12f, 1f), 0f);
            var fillPivot = new GameObject("Fill").transform;
            fillPivot.SetParent(barRoot, false);
            fillPivot.localPosition = new Vector3(-0.3f, 0f, -0.002f);
            Transform fill = Quad(fillPivot, "Bar", art.BarFill, new Vector3(0.6f, 0.07f, 1f), 0f);
            fill.localPosition = new Vector3(0.3f, 0f, 0f);
            barFill = fillPivot;
            barFillRenderer = fill.GetComponent<Renderer>();
            var barBlock = new MaterialPropertyBlock();
            barBlock.SetColor(ColorId, Unit.Team == Team.Player ? PlayerBar : EnemyBar);
            barFillRenderer.SetPropertyBlock(barBlock);
        }

        static Transform Quad(Transform parent, string name, Material material, Vector3 scale, float z)
        {
            GameObject quad = Shapes.Create(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);
            quad.transform.localScale = scale;
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return quad.transform;
        }

        /// <summary>Shows whether the unit can still act this turn (marker on, full colour).</summary>
        public void SetReady(bool ready, bool showMarker)
        {
            marker.gameObject.SetActive(ready && showMarker && Unit.IsAlive);
            dimmed = !ready && showMarker;
            ApplyTint();
        }

        public void SetSelected(bool value) => selected = value;

        public void RefreshModel()
        {
            BuildModel();
        }

        void LateUpdate()
        {
            Camera view = Camera.main;
            if (view != null)
            {
                barRoot.rotation = view.transform.rotation;
            }
            barRoot.localPosition = new Vector3(0f, Height + 0.22f, 0f);
            displayedHealth = Mathf.MoveTowards(displayedHealth, Unit.Health, Time.deltaTime * 12f);
            barFill.localScale = new Vector3(Mathf.Clamp01(displayedHealth / Unit.Stats.MaxHealth), 1f, 1f);
            barRoot.gameObject.SetActive(Unit.IsAlive || displayedHealth > 0.01f);

            if (!Busy)
            {
                float bob = selected ? 0.06f + Mathf.Sin(Time.time * 6f) * 0.04f : 0f;
                model.localPosition = Vector3.Lerp(model.localPosition, new Vector3(0f, bob, 0f), Time.deltaTime * 14f);
            }
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime * 4f);
                ApplyTint();
            }
        }

        void ApplyTint()
        {
            if (block == null)
            {
                return;
            }
            Color emission = Color.white * (flash * 0.9f);
            for (int i = 0; i < renderers.Count; i++)
            {
                Color color = baseColors[i];
                if (dimmed)
                {
                    color = Color.Lerp(color, new Color(0.3f, 0.3f, 0.32f), 0.45f);
                }
                block.SetColor(ColorId, color);
                block.SetColor(EmissionId, emission);
                renderers[i].SetPropertyBlock(block);
            }
        }

        // ---- Animations ----

        public IEnumerator MoveTo(Coord tile)
        {
            Busy = true;
            Vector3 start = transform.position;
            Vector3 end = BoardView.TileCenter(tile);
            float distance = Vector3.Distance(start, end);
            bool leap = Unit.Stats.MoveStyle == MoveStyle.Jump;
            float duration = 0.22f + distance * 0.07f;
            Face(end);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float lift = leap ? Mathf.Sin(t * Mathf.PI) * 0.9f : Mathf.Abs(Mathf.Sin(t * Mathf.PI * Mathf.Max(1f, distance))) * 0.1f;
                transform.position = Vector3.Lerp(start, end, eased) + Vector3.up * lift;
                yield return null;
            }
            transform.position = end;
            Busy = false;
        }

        /// <summary>A quick lunge toward the target; <paramref name="onImpact"/> runs at the moment of the hit.</summary>
        public IEnumerator Strike(Vector3 target, System.Action onImpact)
        {
            Busy = true;
            Face(target);
            Vector3 home = transform.position;
            Vector3 toward = Vector3.ProjectOnPlane(target - home, Vector3.up).normalized;
            bool ranged = Unit.Stats.IsRanged;
            float reach = ranged ? -0.12f : 0.38f;
            const float duration = 0.18f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = home + toward * (reach * Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * (ranged ? t * 0.12f : 0f);
                yield return null;
            }
            onImpact?.Invoke();
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                transform.position = Vector3.Lerp(home + toward * reach, home, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.position = home;
            Busy = false;
        }

        public void Hit()
        {
            flash = 1f;
            StartCoroutine(Shudder());
        }

        IEnumerator Shudder()
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                model.localPosition = new Vector3(Mathf.Sin(t * 90f) * 0.05f * (1f - t * 4f), 0f, 0f);
                yield return null;
            }
        }

        public IEnumerator Fall()
        {
            Busy = true;
            marker.gameObject.SetActive(false);
            Vector3 start = transform.position;
            Quaternion tilt = Quaternion.Euler(Random.Range(-60f, 60f), 0f, Random.Range(50f, 80f) * (Random.value < 0.5f ? -1f : 1f));
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
            {
                model.localRotation = Quaternion.Slerp(model.localRotation, tilt, t * 0.3f);
                transform.position = start + Vector3.down * (t * t * 1.1f);
                transform.localScale = Vector3.one * (1f - t * 0.6f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        public IEnumerator Promote()
        {
            Busy = true;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                transform.localScale = Vector3.one * (1f - t * 0.7f);
                yield return null;
            }
            BuildModel();
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                transform.localScale = Vector3.one * (0.3f + 0.7f * t + Mathf.Sin(t * Mathf.PI) * 0.25f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            flash = 1f;
            Busy = false;
        }

        void Face(Vector3 target)
        {
            Vector3 direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, Unit.Team == Team.Player ? 0f : 180f, 0f);
            }
        }

        /// <summary>Distance along the ray to this piece's upright cylinder, or null when the ray misses it.</summary>
        public float? Pick(Ray ray)
        {
            if (!Unit.IsAlive || !gameObject.activeSelf)
            {
                return null;
            }
            const float radius = 0.34f;
            Vector3 center = transform.position;
            // Closest approach between the ray and the vertical axis, solved in the horizontal plane.
            var origin = new Vector2(ray.origin.x - center.x, ray.origin.z - center.z);
            var direction = new Vector2(ray.direction.x, ray.direction.z);
            float a = Vector2.Dot(direction, direction);
            if (a < 1e-6f)
            {
                return null;
            }
            float b = 2f * Vector2.Dot(origin, direction);
            float c = Vector2.Dot(origin, origin) - radius * radius;
            float disc = b * b - 4f * a * c;
            if (disc < 0f)
            {
                return null;
            }
            float root = Mathf.Sqrt(disc);
            foreach (float distance in new[] { (-b - root) / (2f * a), (-b + root) / (2f * a) })
            {
                float y = ray.origin.y + ray.direction.y * distance - center.y;
                if (distance > 0f && y >= 0f && y <= Height)
                {
                    return distance;
                }
            }
            return null;
        }
    }
}

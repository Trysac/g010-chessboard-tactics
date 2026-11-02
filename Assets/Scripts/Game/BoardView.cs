using System.Collections.Generic;
using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    public enum TileMark
    {
        None,
        Move,
        Attack,
        Threat,
    }

    /// <summary>The 3D chessboard: tiles, frame, rocks and the coloured tile highlights.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float HighlightHeight = 0.011f;

        GameArt art;
        Board board;
        Renderer[,] marks;
        Transform hover;
        Transform selection;

        public static Vector3 TileCenter(Coord c) => new Vector3(c.X - 3.5f, 0f, c.Y - 3.5f);

        /// <summary>The tile under a world point on the board plane, if any.</summary>
        public static Coord TileAt(Vector3 point) => new Coord(Mathf.FloorToInt(point.x + 4f), Mathf.FloorToInt(point.z + 4f));

        public void Build(Board model, GameArt gameArt)
        {
            board = model;
            art = gameArt;
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            Block("Table", new Vector3(0f, -0.62f, 0f), new Vector3(40f, 1f, 40f), art.Table, castShadows: false);
            Block("Frame", new Vector3(0f, -0.21f, 0f), new Vector3(8.9f, 0.32f, 8.9f), art.Frame);
            Block("Trim", new Vector3(0f, -0.17f, 0f), new Vector3(8.24f, 0.3f, 8.24f), art.Trim);

            marks = new Renderer[board.Width, board.Height];
            foreach (Coord tile in board.AllTiles())
            {
                bool dark = (tile.X + tile.Y) % 2 == 0;
                Block($"Tile {tile}", TileCenter(tile) + Vector3.down * 0.1f, new Vector3(1f, 0.2f, 1f),
                    dark ? art.DarkTile : art.LightTile, castShadows: false);
                Renderer mark = FlatQuad($"Mark {tile}", tile, 0.94f, HighlightHeight);
                mark.enabled = false;
                marks[tile.X, tile.Y] = mark;
                if (board.IsRock(tile))
                {
                    BuildRock(tile);
                }
            }

            selection = FlatQuad("Selection", new Coord(0, 0), 0.98f, HighlightHeight + 0.002f).transform;
            selection.GetComponent<Renderer>().sharedMaterial = art.SelectHighlight;
            selection.gameObject.SetActive(false);
            hover = FlatQuad("Hover", new Coord(0, 0), 1f, HighlightHeight + 0.004f).transform;
            hover.GetComponent<Renderer>().sharedMaterial = art.HoverHighlight;
            hover.gameObject.SetActive(false);
        }

        public void ClearMarks()
        {
            foreach (Renderer mark in marks)
            {
                mark.enabled = false;
            }
        }

        public void Mark(IEnumerable<Coord> tiles, TileMark kind)
        {
            Material material = kind == TileMark.Move ? art.MoveHighlight
                : kind == TileMark.Attack ? art.AttackHighlight
                : art.ThreatHighlight;
            foreach (Coord tile in tiles)
            {
                if (!board.InBounds(tile))
                {
                    continue;
                }
                Renderer mark = marks[tile.X, tile.Y];
                mark.enabled = kind != TileMark.None;
                mark.sharedMaterial = material;
            }
        }

        public void SetHover(Coord? tile)
        {
            hover.gameObject.SetActive(tile.HasValue && board.InBounds(tile.Value));
            if (tile.HasValue)
            {
                hover.position = TileCenter(tile.Value) + Vector3.up * (HighlightHeight + 0.004f);
            }
        }

        public void SetSelection(Coord? tile)
        {
            selection.gameObject.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                selection.position = TileCenter(tile.Value) + Vector3.up * (HighlightHeight + 0.002f);
            }
        }

        void BuildRock(Coord tile)
        {
            var random = new System.Random(tile.X * 31 + tile.Y * 7);
            Vector3 center = TileCenter(tile);
            for (int i = 0; i < 4; i++)
            {
                float size = i == 0 ? 0.62f : 0.28f + (float)random.NextDouble() * 0.18f;
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                Vector3 offset = i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.28f;
                Transform rock = Block("Rock", center + offset + Vector3.up * size * 0.32f,
                    new Vector3(size, size * 0.75f, size * 0.9f), i % 2 == 0 ? art.Rock : art.RockDark);
                rock.rotation = Quaternion.Euler((float)random.NextDouble() * 30f - 15f, (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 30f - 15f);
            }
        }

        Transform Block(string name, Vector3 position, Vector3 scale, Material material, bool castShadows = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(block.GetComponent<Collider>());
            block.name = name;
            block.transform.SetParent(transform, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!castShadows)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return block.transform;
        }

        Renderer FlatQuad(string name, Coord tile, float size, float height)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(transform, false);
            quad.transform.position = TileCenter(tile) + Vector3.up * height;
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}

using ChessboardTactics.Core;
using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>Builds a stylised chess piece for every unit type out of Unity's primitive meshes.</summary>
    public static class PieceModel
    {
        /// <summary>Height of the piece's top, used for picking and to place the health bar.</summary>
        public static float Height(PieceType type)
        {
            switch (type)
            {
                case PieceType.King: return 1.42f;
                case PieceType.Queen: return 1.25f;
                case PieceType.Bishop: return 1.22f;
                case PieceType.Knight: return 1.08f;
                case PieceType.Tower: return 1.0f;
                default: return 0.86f;
            }
        }

        /// <summary>Creates the model as a child of <paramref name="parent"/>, facing +z.</summary>
        public static GameObject Build(Transform parent, PieceType type, Material body, Material accent, Material gold)
        {
            var root = new GameObject(type.ToString());
            root.transform.SetParent(parent, false);
            Transform t = root.transform;

            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.68f, 0.06f, 0.68f), body);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.135f, 0f), new Vector3(0.56f, 0.025f, 0.56f), accent);

            switch (type)
            {
                case PieceType.Pawn:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0f), new Vector3(0.34f, 0.17f, 0.34f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.42f, 0.03f, 0.42f), accent);
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0f), Vector3.one * 0.32f, body);
                    break;
                case PieceType.Tower:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.46f, 0f), new Vector3(0.5f, 0.32f, 0.5f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(0.53f, 0.035f, 0.53f), accent);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.82f, 0f), new Vector3(0.6f, 0.05f, 0.6f), body);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI / 2f + Mathf.PI / 4f;
                        Part(t, PrimitiveType.Cube, new Vector3(Mathf.Cos(angle) * 0.2f, 0.93f, Mathf.Sin(angle) * 0.2f),
                            new Vector3(0.15f, 0.14f, 0.15f), body, Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f));
                    }
                    break;
                case PieceType.Bishop:
                    Part(t, PrimitiveType.Capsule, new Vector3(0f, 0.48f, 0f), new Vector3(0.34f, 0.34f, 0.34f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.78f, 0f), new Vector3(0.36f, 0.03f, 0.36f), accent);
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.96f, 0f), new Vector3(0.28f, 0.4f, 0.28f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 1.0f, 0.1f), new Vector3(0.04f, 0.22f, 0.12f), accent,
                        Quaternion.Euler(-20f, 0f, 0f));
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.09f, gold);
                    break;
                case PieceType.Knight:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.42f, 0.16f, 0.42f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.62f, -0.02f), new Vector3(0.26f, 0.5f, 0.3f), body,
                        Quaternion.Euler(-14f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.88f, 0.12f), new Vector3(0.24f, 0.22f, 0.48f), body,
                        Quaternion.Euler(18f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.74f, -0.16f), new Vector3(0.08f, 0.5f, 0.12f), accent,
                        Quaternion.Euler(-14f, 0f, 0f));
                    Part(t, PrimitiveType.Cube, new Vector3(0.07f, 1.04f, 0.02f), new Vector3(0.05f, 0.12f, 0.05f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(-0.07f, 1.04f, 0.02f), new Vector3(0.05f, 0.12f, 0.05f), body);
                    Part(t, PrimitiveType.Sphere, new Vector3(0.1f, 0.93f, 0.18f), Vector3.one * 0.05f, gold);
                    Part(t, PrimitiveType.Sphere, new Vector3(-0.1f, 0.93f, 0.18f), Vector3.one * 0.05f, gold);
                    break;
                case PieceType.Queen:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.44f, 0f), new Vector3(0.44f, 0.3f, 0.44f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.76f, 0f), new Vector3(0.48f, 0.03f, 0.48f), accent);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.32f, 0.12f, 0.32f), body);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        Part(t, PrimitiveType.Sphere, new Vector3(Mathf.Cos(angle) * 0.17f, 1.06f, Mathf.Sin(angle) * 0.17f),
                            Vector3.one * 0.1f, gold);
                    }
                    Part(t, PrimitiveType.Sphere, new Vector3(0f, 1.14f, 0f), Vector3.one * 0.16f, accent);
                    break;
                case PieceType.King:
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f), new Vector3(0.46f, 0.34f, 0.46f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.82f, 0f), new Vector3(0.5f, 0.03f, 0.5f), accent);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.1f, 0.34f), body);
                    Part(t, PrimitiveType.Cylinder, new Vector3(0f, 1.07f, 0f), new Vector3(0.38f, 0.04f, 0.38f), gold);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 1.24f, 0f), new Vector3(0.08f, 0.3f, 0.08f), gold);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 1.28f, 0f), new Vector3(0.24f, 0.08f, 0.08f), gold);
                    break;
                case PieceType.WarDog:
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.42f, -0.04f), new Vector3(0.3f, 0.26f, 0.52f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.26f), new Vector3(0.26f, 0.24f, 0.26f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.43f), new Vector3(0.15f, 0.12f, 0.14f), body);
                    Part(t, PrimitiveType.Cube, new Vector3(0.08f, 0.76f, 0.22f), new Vector3(0.07f, 0.12f, 0.05f), accent);
                    Part(t, PrimitiveType.Cube, new Vector3(-0.08f, 0.76f, 0.22f), new Vector3(0.07f, 0.12f, 0.05f), accent);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.1f), new Vector3(0.32f, 0.06f, 0.1f), accent);
                    Part(t, PrimitiveType.Cube, new Vector3(0f, 0.6f, -0.32f), new Vector3(0.06f, 0.2f, 0.06f), accent,
                        Quaternion.Euler(-35f, 0f, 0f));
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i % 2 == 0 ? 0.1f : -0.1f;
                        float z = i < 2 ? 0.14f : -0.2f;
                        Part(t, PrimitiveType.Cube, new Vector3(x, 0.24f, z), new Vector3(0.08f, 0.2f, 0.08f), body);
                    }
                    Part(t, PrimitiveType.Sphere, new Vector3(0.07f, 0.66f, 0.39f), Vector3.one * 0.05f, gold);
                    Part(t, PrimitiveType.Sphere, new Vector3(-0.07f, 0.66f, 0.39f), Vector3.one * 0.05f, gold);
                    break;
            }
            return root;
        }

        static void Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material,
            Quaternion? rotation = null)
        {
            GameObject part = Shapes.Create(shape);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}

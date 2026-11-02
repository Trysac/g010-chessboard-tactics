using UnityEngine;

namespace ChessboardTactics.Game
{
    /// <summary>
    /// Creates mesh objects from Unity's built-in shapes without colliders. GameObject.CreatePrimitive
    /// would add physics colliders, which the game never uses (picking is done with plain maths).
    /// </summary>
    public static class Shapes
    {
        static GameArt art;

        public static void Init(GameArt gameArt) => art = gameArt;

        public static GameObject Create(PrimitiveType type)
        {
            var shape = new GameObject(type.ToString());
            shape.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
            shape.AddComponent<MeshRenderer>();
            return shape;
        }

        static Mesh MeshOf(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Sphere: return art.SphereMesh;
                case PrimitiveType.Cylinder: return art.CylinderMesh;
                case PrimitiveType.Capsule: return art.CapsuleMesh;
                case PrimitiveType.Quad: return art.QuadMesh;
                default: return art.CubeMesh;
            }
        }
    }
}

using SlimeNull.DockovParty.Networking.Protocol;
using UnityEngine;

namespace SlimeNull.DockovParty.Game
{
    internal static class NetworkConversion
    {
        public static VectorState ToNetwork(this Vector3 value)
        {
            return new VectorState(value.x, value.y, value.z);
        }

        public static Vector3 ToUnity(this VectorState value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        public static QuaternionState ToNetwork(this Quaternion value)
        {
            return new QuaternionState(value.x, value.y, value.z, value.w);
        }

        public static Quaternion ToUnity(this QuaternionState value)
        {
            return new Quaternion(value.X, value.Y, value.Z, value.W);
        }

        public static TransformState ToNetwork(this Transform value, Quaternion rotation)
        {
            return new TransformState
            {
                Position = value.position.ToNetwork(),
                Rotation = rotation.ToNetwork(),
            };
        }
    }
}

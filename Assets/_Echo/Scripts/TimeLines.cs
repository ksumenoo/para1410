using UnityEngine;

namespace Echo
{
    // Две копии одной карты: линия 1 (прошлое/настоящее) и линия 2 (будущее).
    // "То же место" на другой линии = та же локальная позиция относительно корня линии.
    public class TimeLines : MonoBehaviour
    {
        public static TimeLines I { get; private set; }

        public Transform line1Root;
        public Transform line2Root;
        public Vector3 startLocal = new Vector3(0f, 0.1f, 0f);

        void Awake() => I = this;

        public Transform Root(int line) => line == 1 ? line1Root : line2Root;
        public Vector3 ToLocal(Vector3 world, int line) => world - Root(line).position;
        public Vector3 ToWorld(Vector3 local, int line) => Root(line).position + local;

        public int LineOf(Vector3 world) =>
            Mathf.Abs(world.x - line1Root.position.x) <= Mathf.Abs(world.x - line2Root.position.x) ? 1 : 2;

        public static string LineName(int line) => line == 1 ? "Линия 1 · прошлое" : "Линия 2 · будущее";
    }
}

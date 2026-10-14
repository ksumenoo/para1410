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

        // Линия 2 отзеркалена по X (сборщик отражает её геометрию): Герой 2 идёт по экрану справа налево.
        // Локальная позиция одинакова на обеих линиях, мировая на линии 2 = корень + (-x, y, z).
        public bool line2Mirrored;

        public Transform Root(int line) => line == 1 ? line1Root : line2Root;
        bool Mirrored(int line) => line == 2 && line2Mirrored;

        public Vector3 ToLocal(Vector3 world, int line)
        {
            var v = world - Root(line).position;
            if (Mirrored(line)) v.x = -v.x;
            return v;
        }

        public Vector3 ToWorld(Vector3 local, int line)
        {
            if (Mirrored(line)) local.x = -local.x;
            return Root(line).position + local;
        }

        // Направление «вперёд по уровню» в мире для линии (для подсказок и стартового поворота).
        public float ForwardSign(int line) => Mirrored(line) ? -1f : 1f;

        public int LineOf(Vector3 world)
        {
            float mid = (line1Root.position.x + line2Root.position.x) * 0.5f;
            return world.x < mid ? 1 : 2;
        }

        public static string LineName(int line) => line == 1 ? "Линия 1 · прошлое" : "Линия 2 · будущее";
    }
}

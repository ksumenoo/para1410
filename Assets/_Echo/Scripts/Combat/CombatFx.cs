using System.Collections.Generic;
using UnityEngine;

namespace Echo.Combat
{
    // Локальные эффекты боя (у каждого клиента свои): трассеры, пули, «звёздочки» оглушения.
    public static class CombatFx
    {
        static Material _fbBullet, _fbTracerInverse, _fbTracerNormal, _fbStun;
        static readonly List<GameObject> Stuns = new List<GameObject>();

        static Material Pick(Material assigned, ref Material fallback, Color color)
        {
            if (assigned != null) return assigned;
            if (fallback == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                fallback = new Material(shader) { color = color };
                if (fallback.HasProperty("_BaseColor")) fallback.SetColor("_BaseColor", color);
            }
            return fallback;
        }

        static CombatDirector D => CombatDirector.I;
        public static Material BulletMat => Pick(D ? D.bulletMaterial : null, ref _fbBullet, new Color(0.75f, 0.45f, 1f));
        public static Material TracerMat(bool inverse) => inverse
            ? Pick(D ? D.tracerInverseMaterial : null, ref _fbTracerInverse, new Color(0.7f, 0.5f, 1f))
            : Pick(D ? D.tracerNormalMaterial : null, ref _fbTracerNormal, new Color(1f, 0.85f, 0.4f));
        public static Material StunMat => Pick(D ? D.stunMaterial : null, ref _fbStun, new Color(1f, 0.9f, 0.2f));

        public static void Tracer(Vector3 from, Vector3 to, bool inverse)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.07f;
            lr.endWidth = 0.03f;
            lr.sharedMaterial = TracerMat(inverse);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FxLifetime>().until = Time.time + 0.09f;
        }

        public static GameObject Bullet(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "InverseBullet";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.2f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = BulletMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.14f;
            trail.endWidth = 0f;
            trail.sharedMaterial = TracerMat(true);
            trail.emitting = false; // пока застряла — без шлейфа
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static void Stun(Transform target, float seconds)
        {
            if (target == null) return;
            var root = new GameObject("StunFx");
            root.transform.SetParent(target, false);
            root.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            for (int i = 0; i < 3; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(root.transform, false);
                float a = i * Mathf.PI * 2f / 3f;
                s.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.4f;
                s.transform.localScale = Vector3.one * 0.16f;
                var r = s.GetComponent<Renderer>();
                r.sharedMaterial = StunMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var life = root.AddComponent<FxLifetime>();
            life.until = Time.time + seconds;
            life.spin = 300f;
            Stuns.RemoveAll(g => g == null);
            Stuns.Add(root);
        }

        public static void ClearStuns()
        {
            foreach (var g in Stuns) if (g != null) Object.Destroy(g);
            Stuns.Clear();
        }
    }

    // Уничтожает объект эффекта по таймеру, может крутить его.
    public class FxLifetime : MonoBehaviour
    {
        public float until;
        public float spin;

        void Update()
        {
            if (spin != 0f) transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
            if (Time.time >= until) Destroy(gameObject);
        }
    }
}

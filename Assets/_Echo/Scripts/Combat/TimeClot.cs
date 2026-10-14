using UnityEngine;

namespace Echo.Combat
{
    // Сгусток времени: дрожащий полупрозрачный силуэт. Замечает героя своей линии и медленно плывёт к нему
    // (аномалия — стены ему не помеха). Касание = удар. Рассеян пулей — собирается снова через 4 с.
    public class TimeClot : Enemy
    {
        [Header("Сгусток")]
        public float sightRange = 9f;
        public float loseRange = 14f;
        public float speed = 1.2f;

        [Header("Вид (заглушка)")]
        public Transform visual;
        public Renderer[] renderers;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        float _form = 1f;
        float _jitterTimer;
        Vector3 _offset;
        Vector3 _visualBase;
        Color[] _baseColors;
        MaterialPropertyBlock _mpb;
        float _seed;

        protected override string DamageMessage => "Сгусток времени обжёг тебя!";

        void Awake()
        {
            if (visual) _visualBase = visual.localPosition;
            _seed = Random.value * 100f;
            _mpb = new MaterialPropertyBlock();
            _baseColors = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < _baseColors.Length; i++)
            {
                var m = renderers[i] ? renderers[i].sharedMaterial : null;
                _baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        // ---------- Сервер ----------

        protected override void ServerTick(float dt)
        {
            var t = Target;
            if (t != null && (!t.IsSpawned || t.Line.Value != _line || t.IsHiding ||
                              Hero.FlatDistance(t.transform.position, transform.position) > loseRange))
                t = null;
            if (t == null) t = FindVisibleHero(sightRange);
            Target = t;

            Vector3 goal = t != null ? t.transform.position : _spawnPos;
            var to = goal - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.2f) return;
            float spd = t != null ? speed : speed * 0.5f;
            transform.position += to / dist * Mathf.Min(spd * dt, dist);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 180f * dt);
        }

        // ---------- Вид (все клиенты) ----------

        void LateUpdate()
        {
            if (!IsSpawned || visual == null) return;
            float dt = Time.deltaTime;
            bool dead = Dead.Value;
            _form = Mathf.MoveTowards(_form, dead ? 0f : 1f, dt * (dead ? 4f : 1f / Mathf.Max(0.1f, riseSeconds)));

            bool show = _form > 0.02f;
            if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
            if (!show) return;

            _jitterTimer -= dt;
            if (_jitterTimer <= 0f)
            {
                _jitterTimer = 0.04f;
                _offset = Random.insideUnitSphere * 0.08f;
            }
            float t = Time.time + _seed;
            visual.localPosition = _visualBase + _offset + Vector3.up * Mathf.Sin(t * 2f) * 0.1f;
            float pulse = 1f + 0.06f * Mathf.Sin(t * 23f);
            visual.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, _form) * pulse;

            if (renderers == null) return;
            float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(t * 6f, _seed);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                var c = _baseColors[i];
                c.a *= flicker * _form;
                renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(_mpb);
            }
        }
    }
}

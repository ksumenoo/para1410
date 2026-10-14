using UnityEngine;

namespace Echo.Combat
{
    // Сгусток времени: дрожащий полупрозрачный силуэт. Замечает героя своей линии и медленно плывёт к нему
    // (аномалия — стены ему не помеха). Касание = удар. Рассеян пулей — собирается снова через 4 с.
    public class TimeClot : Enemy
    {
        [System.NonSerialized] public float sightRange = 9f;
        [System.NonSerialized] public float loseRange = 14f;
        [System.NonSerialized] public float speed = 1.2f;

        [Header("Вид (заглушка-капсула)")]
        public Transform visual;
        public Renderer[] renderers;

        [Header("Спрайт художника (если есть — вместо капсулы)")]
        public SpriteRenderer spriteRenderer;  // дочерний у visual: дрожит и рассеивается вместе с ним
        public Sprite sprite;                  // enm_timeclot: лицом вправо, низ у нижнего края
        public float cameraPitch = 50f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        float _form = 1f;
        float _calm;
        float _jitterTimer;
        Vector3 _offset;
        Vector3 _visualBase;
        Color[] _baseColors;
        MaterialPropertyBlock _mpb;
        float _seed;
        bool _useSprite;

        protected override string DamageMessage => "Сгусток времени обжёг тебя!";

        void Awake()
        {
            touchRadius = 0.9f;
            if (visual) _visualBase = visual.localPosition;
            _seed = Random.value * 100f;
            _useSprite = spriteRenderer != null && sprite != null;
            if (spriteRenderer)
            {
                spriteRenderer.enabled = _useSprite;
                if (_useSprite) spriteRenderer.sprite = sprite;
            }
            if (_useSprite && renderers != null)
                foreach (var r in renderers) if (r) r.enabled = false;
            if (_useSprite && visual != null)
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            _mpb = new MaterialPropertyBlock();
            _baseColors = new Color[renderers != null ? renderers.Length : 0];
            for (int i = 0; i < _baseColors.Length; i++)
            {
                var m = renderers[i] ? renderers[i].sharedMaterial : null;
                _baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
        }

        // ---------- Сервер ----------

        public override void ServerOnSwapped()
        {
            Target = null;
            _calm = SwapCalmSeconds;
        }

        protected override void ServerTick(float dt)
        {
            if (_calm > 0f) { _calm -= dt; Target = null; }
            var t = Target;
            if (t != null && (!t.IsSpawned || t.Line.Value != _line || t.IsHiding ||
                              Hero.FlatDistance(t.transform.position, transform.position) > loseRange))
                t = null;
            if (t == null && _calm <= 0f) t = FindVisibleHero(sightRange);
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

            float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(t * 6f, _seed);
            if (_useSprite)
            {
                var st = spriteRenderer.transform;
                st.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
                float fx = transform.forward.x;
                if (Mathf.Abs(fx) > 0.1f)
                {
                    var sc = st.localScale;
                    sc.x = Mathf.Abs(sc.x) * (fx < 0f ? -1f : 1f);
                    st.localScale = sc;
                }
                spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.45f + 0.5f * flicker) * _form);
                return;
            }
            if (renderers == null) return;
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

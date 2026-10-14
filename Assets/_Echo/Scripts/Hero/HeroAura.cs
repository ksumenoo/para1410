using UnityEngine;

// Призматическая аура Героя 2: ореол за телом и радужная кромка по контуру,
// мерцает и «барахлит» — части ореола на мгновение сдвигаются.
public class HeroAura : MonoBehaviour
{
    public bool auraOn;
    public SpriteRenderer[] glows;
    public SpriteRenderer[] rims;

    [Header("Барахлит")]
    public float glitchShift = 0.05f;      // насколько сдвигается часть ореола (в юнитах)
    public float glitchPart = 0.35f;       // доля частей, которые сдвигаются за раз
    public Vector2 calmTime = new Vector2(0.15f, 0.6f);
    public Vector2 glitchTime = new Vector2(0.04f, 0.1f);

    [Header("Мерцание")]
    public float pulseSpeed = 3f;
    public float hueSpeed = 0.15f;

    Vector3[] basePos;
    float nextSwitch;
    bool glitching;

    void Awake() { Cache(); SetAura(auraOn); }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this) SetAura(auraOn); };
    }
#endif

    void Cache()
    {
        if (glows == null) return;
        basePos = new Vector3[glows.Length];
        for (int i = 0; i < glows.Length; i++) if (glows[i]) basePos[i] = glows[i].transform.localPosition;
    }

    public void SetAura(bool on)
    {
        auraOn = on;
        if (glows != null) foreach (var g in glows) if (g) g.enabled = on;
        if (rims != null) foreach (var r in rims) if (r) r.enabled = on;
    }

    void Update()
    {
        if (!auraOn || glows == null) return;
        if (basePos == null || basePos.Length != glows.Length) Cache();

        float a = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
        for (int i = 0; i < glows.Length; i++)
        {
            if (!glows[i]) continue;
            var tint = Color.HSVToRGB(Mathf.Repeat(Time.time * hueSpeed + i * 0.07f, 1f), 0.25f, 1f);
            tint.a = a;
            glows[i].color = tint;
        }
        if (rims != null)
            for (int i = 0; i < rims.Length; i++)
                if (rims[i]) rims[i].color = Color.HSVToRGB(Mathf.Repeat(Time.time * hueSpeed + 0.5f + i * 0.07f, 1f), 0.2f, 1f);

        if (Time.time < nextSwitch) return;
        glitching = !glitching;
        for (int i = 0; i < glows.Length; i++)
        {
            if (!glows[i]) continue;
            bool move = glitching && Random.value < glitchPart;
            glows[i].transform.localPosition = basePos[i] + (move ? Vector3.right * Random.Range(-glitchShift, glitchShift) : Vector3.zero);
        }
        var range = glitching ? glitchTime : calmTime;
        nextSwitch = Time.time + Random.Range(range.x, range.y);
    }
}

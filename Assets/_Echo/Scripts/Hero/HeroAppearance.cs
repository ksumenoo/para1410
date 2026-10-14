using UnityEngine;

// Внешность героя: причёска, глаза, мимика, цвета кожи, волос и комбинезона.
// Индексы с нуля. По сети достаточно синхронизировать шесть чисел и вызвать Set(...).
public class HeroAppearance : MonoBehaviour
{
    [Header("Выбор")]
    public int hairIndex;
    public int eyesIndex;
    public int faceIndex;
    public int skinIndex;
    public int hairColorIndex;
    public int suitIndex;

    [Header("Варианты (заполняет сборщик)")]
    public Sprite[] hairSprites;
    public Sprite[] hairGlowSprites;
    public Sprite[] hairRimSprites;
    public Sprite[] eyeSprites;
    public Sprite[] faceSprites;
    public Sprite[] torsoSprites;
    public Color[] skinColors;
    public Color[] hairColors;
    public Color[] suitColors;
    public Color[] suitShades;

    [Header("Части (заполняет сборщик)")]
    public SpriteRenderer hair;
    public SpriteRenderer hairGlow;
    public SpriteRenderer hairRim;
    public SpriteRenderer eyes;
    public SpriteRenderer face;
    public SpriteRenderer torso;
    public SpriteRenderer[] skin;
    public SpriteRenderer[] skinShade;
    public SpriteRenderer[] suit;
    public SpriteRenderer[] suitShade;
    public GameObject gun;

    const float ShadeK = 0.82f;

    void Start() { Apply(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this && hairSprites != null && hairSprites.Length > 0) Apply(); };
    }
#endif

    public void Set(int hairIdx, int eyesIdx, int faceIdx, int skinIdx, int hairColorIdx, int suitIdx)
    {
        hairIndex = hairIdx; eyesIndex = eyesIdx; faceIndex = faceIdx;
        skinIndex = skinIdx; hairColorIndex = hairColorIdx; suitIndex = suitIdx;
        Apply();
    }

    public void Apply()
    {
        SetSprite(hair, hairSprites, hairIndex);
        SetSprite(hairGlow, hairGlowSprites, hairIndex);
        SetSprite(hairRim, hairRimSprites, hairIndex);
        SetSprite(eyes, eyeSprites, eyesIndex);
        SetSprite(face, faceSprites, faceIndex);
        SetSprite(torso, torsoSprites, suitIndex);

        Color s = Pick(skinColors, skinIndex);
        Color ss = new Color(s.r * ShadeK, s.g * ShadeK, s.b * ShadeK, 1f);
        Paint(skin, s);
        Paint(skinShade, ss);
        if (hair) hair.color = Pick(hairColors, hairColorIndex);
        Paint(suit, Pick(suitColors, suitIndex));
        Paint(suitShade, Pick(suitShades, suitIndex));
    }

    public void SetGunVisible(bool visible)
    {
        if (gun) gun.SetActive(visible);
    }

    static void SetSprite(SpriteRenderer r, Sprite[] list, int i)
    {
        if (r && list != null && list.Length > 0) r.sprite = list[Mathf.Clamp(i, 0, list.Length - 1)];
    }

    static Color Pick(Color[] list, int i)
    {
        return (list != null && list.Length > 0) ? list[Mathf.Clamp(i, 0, list.Length - 1)] : Color.white;
    }

    static void Paint(SpriteRenderer[] list, Color c)
    {
        if (list == null) return;
        foreach (var r in list) if (r) r.color = c;
    }
}

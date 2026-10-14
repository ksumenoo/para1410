using Unity.Netcode;
using UnityEngine;

namespace Echo.Combat
{
    // Временный интерфейс боя (IMGUI): сердечки и патроны под инвентарём каждой половины, красная вспышка при ударе.
    // Потом Чат 4 переделает — данные брать из HeroHealth.Hp и HeroGun.
    [AddToGameSystems]
    public class CombatHud : MonoBehaviour
    {
        Texture2D _heart;
        GUIStyle _label;

        void OnGUI()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !(nm.IsServer || nm.IsClient) || SplitCameras.I == null) return;
            if (_heart == null) _heart = MakeHeart(32);
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
                _label.normal.textColor = Color.white;
            }

            float s = NetworkBootstrap.GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            float h = Screen.height / s;
            DrawHalf(SplitCameras.I.TopHero, 0f, w, h / 2f);
            DrawHalf(SplitCameras.I.BottomHero, h / 2f, w, h / 2f);
        }

        void DrawHalf(Hero hero, float y0, float w, float hh)
        {
            if (hero == null || !hero.IsSpawned) return;
            var old = GUI.color;

            var hp = hero.GetComponent<HeroHealth>();
            if (hp != null)
            {
                if (Time.time < hp.FlashUntil)
                {
                    float f = (hp.FlashUntil - Time.time) / HeroHealth.FlashSeconds;
                    GUI.color = new Color(1f, 0.05f, 0.05f, 0.4f * f);
                    GUI.DrawTexture(new Rect(0f, y0, w, hh), Texture2D.whiteTexture);
                }
                for (int i = 0; i < HeroHealth.MaxHp; i++)
                {
                    GUI.color = i < hp.Hp.Value ? new Color(1f, 0.2f, 0.25f) : new Color(0.2f, 0.2f, 0.2f, 0.7f);
                    GUI.DrawTexture(new Rect(12f + i * 30f, y0 + 92f, 26f, 26f), _heart);
                }
            }

            var gun = hero.GetComponent<HeroGun>();
            if (gun != null && gun.HasPistol)
            {
                float x = 12f + HeroHealth.MaxHp * 30f + 18f;
                if (gun.IsInverse)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y0 + 92f, 70f, 26f), "Обойма", _label);
                    for (int i = 0; i < HeroGun.ClipSize; i++)
                    {
                        GUI.color = i < gun.InverseRounds.Value ? new Color(0.75f, 0.5f, 1f) : new Color(0.25f, 0.2f, 0.3f, 0.7f);
                        GUI.DrawTexture(new Rect(x + 72f + i * 14f, y0 + 97f, 10f, 16f), Texture2D.whiteTexture);
                    }
                }
                else
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x, y0 + 92f, 260f, 26f),
                        "Патроны: " + gun.LoadedRounds.Value + " / " + gun.AmmoPacks * HeroGun.ShotsPerPack, _label);
                }
            }
            GUI.color = old;
        }

        static Texture2D MakeHeart(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // (x² + y² − 1)³ − x²·y³ ≤ 0 — сердечко
                    float fx = (x + 0.5f) / size * 2.6f - 1.3f;
                    float fy = (y + 0.5f) / size * 2.6f - 1.25f;
                    float a = fx * fx + fy * fy - 1f;
                    bool inside = a * a * a - fx * fx * fy * fy * fy <= 0f;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

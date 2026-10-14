using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Levels
{
    public enum Zone { None, Public, Staff, Closed }

    // Строительные помощники для одной линии: стены с проёмами, полы, мебель, метки, материалы.
    // Все боксы собираются с мешем, у которого UV считаются в метрах, поэтому текстуры друга
    // (Assets/Art/Textures/tex_<набор>_<floor|wall>_L1|L2.png) ложатся бесшовно и без растяжения.
    public class LevelKit
    {
        public const float T = 0.4f;   // толщина стен
        public const float H = 3f;     // высота стен
        const string MatDir = "Assets/_Echo/Levels/Materials";
        const string TexDir = "Assets/Art/Textures";

        public readonly Transform Root;
        public readonly int Line;
        public bool L1 => Line == 1;

        Transform _grp;
        readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static Mesh _cubeSrc;

        public LevelKit(Transform root, int line)
        {
            Root = root;
            Line = line;
            EnsureFolder("Assets/_Echo/Levels");
            EnsureFolder(MatDir);
        }

        public Color C(Color l1, Color l2) => L1 ? l1 : l2;
        public static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        public void Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            _grp = go.transform;
        }

        // ---------- Материалы ----------

        Material LoadOrCreate(string name)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        // Матовая поверхность; если лежит текстура набора для этой линии — берём её.
        public Material Surface(string key, Color l1, Color l2, string texSet = null, string texKind = null, float smooth = 0.1f)
        {
            if (_mats.TryGetValue(key, out var cached)) return cached;
            var m = LoadOrCreate("L" + Line + "_" + key);
            var c = C(l1, l2);
            Texture2D tex = null;
            if (texSet != null)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/tex_" + texSet + "_" + texKind + "_L" + Line + ".png");
            if (tex != null) c = Color.white;
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        public bool HasTexture(string set, string kind) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/tex_" + set + "_" + kind + "_L" + Line + ".png") != null;

        // Светящаяся поверхность.
        public Material Glow(string key, Color l1, Color l2, float intensity = 1.5f)
        {
            if (_mats.TryGetValue(key, out var cached)) return cached;
            var m = LoadOrCreate("L" + Line + "_" + key);
            var c = C(l1, l2);
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        public Material Transparent(string key, Color l1, Color l2, bool emissive = true)
        {
            if (_mats.TryGetValue(key, out var cached)) return cached;
            var m = LoadOrCreate("L" + Line + "_" + key);
            var c = C(l1, l2);
            m.SetColor("_BaseColor", c);
            m.color = c;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(c.r, c.g, c.b) * 1.2f);
            }
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        // Полосы зон: на линии 2 выцветшие.
        public Material Stripe(Zone zone)
        {
            switch (zone)
            {
                case Zone.Public: return Surface("stripe_blue", Rgb(0.2f, 0.62f, 0.95f), Rgb(0.28f, 0.38f, 0.42f), "zone", "blue");
                case Zone.Staff: return Surface("stripe_yellow", Rgb(0.96f, 0.8f, 0.15f), Rgb(0.5f, 0.46f, 0.25f), "zone", "yellow");
                default: return Surface("stripe_red", Rgb(0.85f, 0.1f, 0.1f), Rgb(0.4f, 0.2f, 0.18f), "zone", "red");
            }
        }

        // ---------- Геометрия ----------

        static Mesh BoxMesh(Vector3 size, Vector3 center, Vector2 tile)
        {
            if (_cubeSrc == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cubeSrc = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
            }
            var v = _cubeSrc.vertices;
            var n = _cubeSrc.normals;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = Vector3.Scale(v[i], size);
                var w = p + center;
                var nn = n[i];
                Vector2 c;
                if (Mathf.Abs(nn.y) > 0.5f) c = new Vector2(w.x, w.z);
                else if (Mathf.Abs(nn.x) > 0.5f) c = new Vector2(w.z, w.y);
                else c = new Vector2(w.x, w.y);
                uv[i] = new Vector2(c.x / tile.x, c.y / tile.y);
                v[i] = p;
            }
            var m = new Mesh { name = "LevelBox", vertices = v, normals = n, triangles = _cubeSrc.triangles, uv = uv };
            m.RecalculateBounds();
            return m;
        }

        public GameObject Box(string name, Vector3 center, Vector3 size, Material mat,
            bool collider = true, float yaw = 0f, int layer = 0, Vector2? tile = null, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot ?? Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, center, tile ?? new Vector2(3f, 3f));
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            if (layer != 0) go.layer = layer;
            return go;
        }

        // Вертикальный цилиндр: center — середина по высоте.
        public GameObject Cyl(string name, Vector3 center, float radius, float height, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public GameObject Ball(string name, Vector3 center, float radius, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = center;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public void Floor(float x0, float x1, float z0, float z1, Material mat)
        {
            Box("Floor", new Vector3((x0 + x1) * 0.5f, -0.1f, (z0 + z1) * 0.5f),
                new Vector3(x1 - x0 + 0.2f, 0.2f, z1 - z0 + 0.2f), mat);
        }

        static List<Vector2> SortedGaps(Vector2[] gaps)
        {
            var list = new List<Vector2>(gaps);
            list.Sort((a, b) => a.x.CompareTo(b.x));
            return list;
        }

        // Стена вдоль X на z. gaps: (центр по X, ширина). inside: в какую сторону по Z внутренность (+1/-1/0) — туда идёт полоса зоны.
        public void WallX(float z, float x0, float x1, Material wall, Zone zone, int inside, params Vector2[] gaps)
        {
            float cursor = x0 - T * 0.5f;
            foreach (var g in SortedGaps(gaps))
            {
                WallXSeg(z, cursor, g.x - g.y * 0.5f, wall, zone, inside);
                cursor = g.x + g.y * 0.5f;
            }
            WallXSeg(z, cursor, x1 + T * 0.5f, wall, zone, inside);
        }

        void WallXSeg(float z, float a, float b, Material wall, Zone zone, int inside)
        {
            float len = b - a;
            if (len < 0.05f) return;
            float mid = (a + b) * 0.5f;
            Box("Wall", new Vector3(mid, H * 0.5f, z), new Vector3(len, H, T), wall);
            if (zone != Zone.None && inside != 0) StripeBox(new Vector3(mid, 1.2f, z + inside * (T * 0.5f + 0.02f)), len, true, zone);
        }

        // Стена вдоль Z на x. gaps: (центр по Z, ширина). inside: сторона по X.
        public void WallZ(float x, float z0, float z1, Material wall, Zone zone, int inside, params Vector2[] gaps)
        {
            float cursor = z0 - T * 0.5f;
            foreach (var g in SortedGaps(gaps))
            {
                WallZSeg(x, cursor, g.x - g.y * 0.5f, wall, zone, inside);
                cursor = g.x + g.y * 0.5f;
            }
            WallZSeg(x, cursor, z1 + T * 0.5f, wall, zone, inside);
        }

        void WallZSeg(float x, float a, float b, Material wall, Zone zone, int inside)
        {
            float len = b - a;
            if (len < 0.05f) return;
            float mid = (a + b) * 0.5f;
            Box("Wall", new Vector3(x, H * 0.5f, mid), new Vector3(T, H, len), wall);
            if (zone != Zone.None && inside != 0) StripeBox(new Vector3(x + inside * (T * 0.5f + 0.02f), 1.2f, mid), len, false, zone);
        }

        void StripeBox(Vector3 center, float len, bool alongX, Zone zone)
        {
            var size = alongX ? new Vector3(len, 0.25f, 0.04f) : new Vector3(0.04f, 0.25f, len);
            Box("Stripe", center, size, Stripe(zone), false, 0f, 0, new Vector2(2f, 0.25f));
            if (zone == Zone.Closed)
            {
                var white = Surface("stripe_white", Rgb(0.96f, 0.96f, 0.96f), Rgb(0.6f, 0.6f, 0.58f));
                var s2 = alongX ? new Vector3(len, 0.07f, 0.045f) : new Vector3(0.045f, 0.07f, len);
                Box("StripeWhite", center + Vector3.up * 0.2f, s2, white, false);
            }
        }

        // Стеклянная стена (слой 2 Ignore Raycast, с коллайдером). Края проёмов — металлические стойки.
        public void GlassX(float z, float x0, float x1, Material glass, Material frame, params Vector2[] gaps)
        {
            float cursor = x0;
            Post(new Vector3(x0, H * 0.5f, z), frame);
            foreach (var g in SortedGaps(gaps))
            {
                GlassSeg(z, cursor, g.x - g.y * 0.5f, glass);
                Post(new Vector3(g.x - g.y * 0.5f, H * 0.5f, z), frame);
                Post(new Vector3(g.x + g.y * 0.5f, H * 0.5f, z), frame);
                cursor = g.x + g.y * 0.5f;
            }
            GlassSeg(z, cursor, x1, glass);
            Post(new Vector3(x1, H * 0.5f, z), frame);
        }

        void GlassSeg(float z, float a, float b, Material glass)
        {
            if (b - a < 0.05f) return;
            Box("Glass", new Vector3((a + b) * 0.5f, H * 0.5f, z), new Vector3(b - a, H, 0.15f), glass, true, 0f, 2);
        }

        void Post(Vector3 c, Material frame) => Box("Post", c, new Vector3(0.3f, H, 0.3f), frame);

        // ---------- Мебель и метки ----------

        public void Locker(Vector3 cabinetCenter, float yaw, Material mat)
        {
            Box("Locker", cabinetCenter + Vector3.up * 1.1f, new Vector3(1f, 2.2f, 0.7f), mat, true, yaw);
            LockerSpotAt(cabinetCenter + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.85f);
        }

        public void LockerSpotAt(Vector3 pos)
        {
            var spot = new GameObject("LockerSpot");
            spot.transform.SetParent(_grp, false);
            spot.transform.localPosition = new Vector3(pos.x, 0f, pos.z);
            spot.AddComponent<LockerSpot>();
        }

        public void PlaceItem(ItemType type, int keyId, Vector3 pos)
        {
            var go = new GameObject("Item_" + type + (keyId != 0 ? "_" + keyId : ""));
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            var im = go.AddComponent<ItemMarker>();
            im.type = type;
            im.keyId = keyId;
        }

        // yaw 90 — для стены вдоль Z (проём вдоль оси Z).
        public void PlaceDoor(int doorId, int keyId, Vector3 pos, float yaw = 90f)
        {
            var go = new GameObject("DoorMarker_" + doorId);
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var d = go.AddComponent<DoorMarker>();
            d.doorId = doorId;
            d.keyId = keyId;
        }

        // Охрана и военные живут только на линии 1; на линии 2 вызов ничего не делает.
        public void PlaceGuard(bool soldier, Vector3 start, params Vector3[] patrol)
        {
            if (!L1) return;
            var go = new GameObject(soldier ? "Soldier" : "Guard");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = start;
            go.AddComponent<GuardMarker>().soldier = soldier;
            for (int i = 0; i < patrol.Length; i++)
            {
                var p = new GameObject("P" + (i + 1));
                p.transform.SetParent(go.transform, false);
                p.transform.position = Root.position + patrol[i];
            }
        }

        // Чекпоинты только на линии 1.
        public void PlaceCheckpoint(Vector3 pos)
        {
            if (!L1) return;
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.AddComponent<CheckpointMarker>();
        }

        public void PlaceReactor(Vector3 pos)
        {
            var go = new GameObject("ReactorMarker");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.AddComponent<ReactorMarker>();
        }

        public void PlaceMachine(Vector3 pos, float radius)
        {
            var go = new GameObject("MachineMarker");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.AddComponent<MachineMarker>().radius = radius;
        }

        // Мутанты и сгустки (метки модуля боя) — только на линии 2.
        public void PlaceMutant(Vector3 pos)
        {
            if (L1) return;
            var go = new GameObject("MutantMarker");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.AddComponent<Echo.Combat.MutantMarker>();
        }

        public void PlaceClot(Vector3 pos)
        {
            if (L1) return;
            var go = new GameObject("TimeClotMarker");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            go.AddComponent<Echo.Combat.TimeClotMarker>();
        }

        // Табличка на русском: лежит на полу, читается сверху.
        public void Sign(string text, Vector3 pos, float size = 0.6f, float yaw = 0f)
        {
            var go = new GameObject("Sign_" + text);
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = new Vector3(pos.x, 0.05f, pos.z);
            go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 100;
            tm.characterSize = 0.1f * size;
            tm.color = C(Rgb(0.1f, 0.12f, 0.2f), Rgb(0.65f, 1f, 0.75f));
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
        }

        // Зелёное свечение будущего: точечный свет (только линия 2).
        public void GlowLight(Vector3 pos, float intensity = 3f, float range = 11f)
        {
            if (L1) return;
            var go = new GameObject("GreenGlow");
            go.transform.SetParent(_grp, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.3f, 1f, 0.55f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}

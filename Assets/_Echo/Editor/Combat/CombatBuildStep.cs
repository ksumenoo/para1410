using Echo.EditorTools;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Echo.Combat.EditorBuild
{
    // Шаг сборки сцены для боя: материалы, префабы мутанта и сгустка, CombatDirector, тестовые враги.
    public static class CombatBuildStep
    {
        [EchoBuildStep(order: 20)]
        public static void Build(EchoBuildContext ctx)
        {
            string matDir = ctx.Root + "/Materials/Combat";
            string prefabDir = ctx.Root + "/Prefabs/Combat";
            Folder(matDir);
            Folder(prefabDir);

            var mutantMat = Mat(matDir, "Mutant", new Color(0.42f, 0.52f, 0.34f));
            var mutantAngry = Mat(matDir, "Mutant_Angry", new Color(0.62f, 0.34f, 0.26f));
            var eyes = Mat(matDir, "Mutant_Eyes", new Color(1f, 0.15f, 0.1f), unlit: true);
            var clotCore = Mat(matDir, "TimeClot_Core", new Color(0.6f, 0.4f, 1f, 0.5f), transparent: true);
            var clotShell = Mat(matDir, "TimeClot_Shell", new Color(0.4f, 0.85f, 1f, 0.2f), transparent: true);
            var clotHeart = Mat(matDir, "TimeClot_Heart", new Color(0.9f, 0.8f, 1f), unlit: true);
            var bullet = Mat(matDir, "Bullet_Inverse", new Color(0.8f, 0.55f, 1f), unlit: true);
            var tracerInv = Mat(matDir, "Tracer_Inverse", new Color(0.7f, 0.5f, 1f), unlit: true);
            var tracerNorm = Mat(matDir, "Tracer_Normal", new Color(1f, 0.85f, 0.4f), unlit: true);
            var stun = Mat(matDir, "Stun", new Color(1f, 0.9f, 0.2f), unlit: true);

            var mutant = BuildMutant(prefabDir, mutantMat, mutantAngry, eyes);
            var clot = BuildClot(prefabDir, clotCore, clotShell, clotHeart);
            ctx.RegisterNetworkPrefab(mutant);
            ctx.RegisterNetworkPrefab(clot);

            var director = ctx.GameSystems.GetComponent<CombatDirector>();
            if (director == null) director = ctx.GameSystems.AddComponent<CombatDirector>();
            director.mutantPrefab = mutant.GetComponent<NetworkObject>();
            director.timeClotPrefab = clot.GetComponent<NetworkObject>();
            director.bulletMaterial = bullet;
            director.tracerInverseMaterial = tracerInv;
            director.tracerNormalMaterial = tracerNorm;
            director.stunMaterial = stun;
            if (ctx.GameSystems.GetComponent<CombatHud>() == null) ctx.GameSystems.AddComponent<CombatHud>();

            // Пока модуль уровней не расставил своих врагов — тестовые на линии 2.
            bool hasMarkers = Object.FindObjectsByType<MutantMarker>(FindObjectsSortMode.None).Length > 0 ||
                              Object.FindObjectsByType<TimeClotMarker>(FindObjectsSortMode.None).Length > 0;
            if (!hasMarkers && ctx.Line2 != null)
            {
                Marker<MutantMarker>(ctx.Line2, "Mutant_Test_1", new Vector3(11f, 0f, -3f), 200f);
                Marker<MutantMarker>(ctx.Line2, "Mutant_Test_2", new Vector3(16f, 0f, 3.5f), 250f);
                Marker<MutantMarker>(ctx.Line2, "Mutant_Test_3", new Vector3(34f, 0f, -2.5f), 270f);
                Marker<TimeClotMarker>(ctx.Line2, "TimeClot_Test_1", new Vector3(44f, 0f, 2f), 270f);
            }
        }

        // ---------- Префабы ----------

        static GameObject BuildMutant(string dir, Material body, Material angry, Material eyes)
        {
            var go = new GameObject("Mutant");
            go.AddComponent<NetworkObject>();
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.9f;
            agent.speed = 1.3f;
            agent.angularSpeed = 720f;
            agent.acceleration = 16f;
            agent.stoppingDistance = 0.3f;
            agent.enabled = false; // включает сервер в Enemy.ServerInit
            NoScaleTransform(go);
            var mutant = go.AddComponent<Mutant>();

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(go.transform, false);
            var torso = Prim(PrimitiveType.Capsule, "Body", pivot, new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.95f, 0.75f), body);
            Prim(PrimitiveType.Cube, "Arm_L", pivot, new Vector3(-0.32f, 1.3f, 0.4f), new Vector3(0.16f, 0.16f, 0.75f), body);
            Prim(PrimitiveType.Cube, "Arm_R", pivot, new Vector3(0.32f, 1.25f, 0.45f), new Vector3(0.16f, 0.16f, 0.8f), body);
            Prim(PrimitiveType.Sphere, "Eye_L", pivot, new Vector3(-0.14f, 1.6f, 0.33f), Vector3.one * 0.12f, eyes);
            Prim(PrimitiveType.Sphere, "Eye_R", pivot, new Vector3(0.14f, 1.6f, 0.33f), Vector3.one * 0.12f, eyes);

            mutant.pivot = pivot;
            mutant.bodyRenderer = torso.GetComponent<Renderer>();
            mutant.calmMaterial = body;
            mutant.angryMaterial = angry;

            // Спрайты художника (Assets/Art/Enemies/enm_mutant.png, enm_mutant_dead.png), если уже есть
            mutant.aliveSprite = LoadEnemySprite("enm_mutant");
            mutant.deadSprite = LoadEnemySprite("enm_mutant_dead");
            if (mutant.aliveSprite != null)
                mutant.spriteRenderer = SpriteChild(go.transform, mutant.aliveSprite);
            return Save(go, dir + "/Mutant.prefab");
        }

        static GameObject BuildClot(string dir, Material core, Material shell, Material heart)
        {
            var go = new GameObject("TimeClot");
            go.AddComponent<NetworkObject>();
            NoScaleTransform(go);
            var clot = go.AddComponent<TimeClot>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var c = Prim(PrimitiveType.Capsule, "Core", visual, new Vector3(0f, 1f, 0f), new Vector3(0.7f, 0.95f, 0.7f), core);
            var s = Prim(PrimitiveType.Capsule, "Shell", visual, new Vector3(0f, 1f, 0f), new Vector3(1.05f, 1.2f, 1.05f), shell);
            Prim(PrimitiveType.Sphere, "Heart", visual, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.25f, heart);

            clot.visual = visual;
            clot.renderers = new[] { c.GetComponent<Renderer>(), s.GetComponent<Renderer>() };

            // Спрайт художника (Assets/Art/Enemies/enm_timeclot.png), если уже есть
            clot.sprite = LoadEnemySprite("enm_timeclot");
            if (clot.sprite != null)
                clot.spriteRenderer = SpriteChild(visual, clot.sprite);
            return Save(go, dir + "/TimeClot.prefab");
        }

        static SpriteRenderer SpriteChild(Transform parent, Sprite sprite)
        {
            var sp = new GameObject("Sprite");
            sp.transform.SetParent(parent, false);
            sp.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
            var sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            return sr;
        }

        // Как у охраны в ядре: спрайт 256×256, ноги у нижнего края → пивот снизу по центру, PPU 110 (рост ≈ 2,3 м).
        static Sprite LoadEnemySprite(string name)
        {
            string path = "Assets/Art/Enemies/" + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            if (ti.textureType != TextureImporterType.Sprite || ti.spritePivot != new Vector2(0.5f, 0f) ||
                !Mathf.Approximately(ti.spritePixelsPerUnit, 110f))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 110f;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteAlignment = (int)SpriteAlignment.Custom;
                st.spritePivot = new Vector2(0.5f, 0f);
                ti.SetTextureSettings(st);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void NoScaleTransform(GameObject go)
        {
            var nt = go.AddComponent<NetworkTransform>(); // серверный по умолчанию
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
        }

        static void Marker<T>(Transform root, string name, Vector3 local, float yaw) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<T>();
        }

        // ---------- Утилиты ----------

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // враги без коллайдеров: попадания считает HeroGun
            return go;
        }

        static GameObject Save(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static Material Mat(string dir, string name, Color color, bool transparent = false, bool unlit = false)
        {
            string path = dir + "/" + name + ".mat";
            var shaderName = unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit";
            var shader = Shader.Find(shaderName) ?? Shader.Find(unlit ? "Unlit/Color" : "Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 1.2f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}

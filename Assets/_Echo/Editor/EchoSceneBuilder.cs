using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Echo.EditorTools
{
    // Echo → Собрать сцену прототипа: строит Assets/_Echo/Scenes/Game.unity с нуля.
    public static class EchoSceneBuilder
    {
        const string Root = "Assets/_Echo";
        public const string ScenePath = Root + "/Scenes/Game.unity";
        static readonly Vector3 Line2Offset = new Vector3(1000f, 0f, 0f);

        class Mats
        {
            public Material floor1, wall1, floor2, wall2, rubble;
            public Material hero1, hero2, aura, door, reactor, machine;
            public Material key, barrel, backpack, ammo, pistol;
            public Material guard, soldier, alertYellow, alertRed, coneCalm, coneAlert, locker, glass;
        }

        [MenuItem("Echo/Собрать сцену прототипа")]
        public static void Build()
        {
            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Echo", "Пересобрать сцену Game? Ручные правки в ней пропадут.", "Пересобрать", "Отмена"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildSilently();
        }

        // Без диалогов (для пульта EchoRemote). Несохранённые правки открытой сцены сохраняются.
        public static void BuildSilently()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (sc.isDirty && !string.IsNullOrEmpty(sc.path) && sc.path != ScenePath) EditorSceneManager.SaveScene(sc);
            }

            Folder(Root + "/Materials");
            Folder(Root + "/Prefabs");
            Folder(Root + "/Scenes");

            var m = CreateMaterials();
            var heroPrefab = BuildHeroPrefab(m);
            var itemPrefab = BuildItemPrefab(m);
            var doorPrefab = BuildDoorPrefab(m);
            var guardPrefab = BuildGuardPrefab(m);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Свет
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            // Сеть
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = utp;
            nm.NetworkConfig.PlayerPrefab = null;

            // Линии времени: уровень от модуля уровней, если он есть, иначе тестовый коридор.
            var line1 = new GameObject("Line1_Past").transform;
            var line2 = new GameObject("Line2_Future").transform;
            line2.position = Line2Offset;

            // Камеры
            var top = MakeCamera("TopCamera", new Rect(0f, 0.5f, 1f, 0.5f), true);
            var bottom = MakeCamera("BottomCamera", new Rect(0f, 0f, 1f, 0.5f), false);

            // Системы
            var game = new GameObject("Game");
            var lines = game.AddComponent<TimeLines>();
            lines.line1Root = line1;
            lines.line2Root = line2;
            var director = game.AddComponent<GameDirector>();
            director.heroPrefab = heroPrefab.GetComponent<NetworkObject>();
            director.itemPrefab = itemPrefab.GetComponent<NetworkObject>();
            director.doorPrefab = doorPrefab.GetComponent<NetworkObject>();
            director.guardPrefab = guardPrefab.GetComponent<NetworkObject>();
            var split = game.AddComponent<SplitCameras>();
            split.top = top;
            split.bottom = bottom;
            game.AddComponent<Hud>();
            game.AddComponent<NetworkBootstrap>();
            foreach (var t in TypeCache.GetTypesWithAttribute<AddToGameSystemsAttribute>())
                if (typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && game.GetComponent(t) == null) game.AddComponent(t);

            var ctx = new EchoBuildContext { Line1 = line1, Line2 = line2, GameSystems = game, Root = Root };
            var levelBuilders = TypeCache.GetMethodsWithAttribute<EchoLevelBuilderAttribute>();
            if (levelBuilders.Count > 0)
            {
                if (levelBuilders.Count > 1) Debug.LogWarning("[Echo] Найдено несколько EchoLevelBuilder, беру первый: " + levelBuilders[0].DeclaringType);
                levelBuilders[0].Invoke(null, new object[] { ctx, line1, 1 });
                levelBuilders[0].Invoke(null, new object[] { ctx, line2, 2 });
            }
            else
            {
                BuildTestLine(line1, m.floor1, m.wall1, m, false);
                BuildTestLine(line2, m.floor2, m.wall2, m, true);
                BuildTestContent(line1, line2);
            }

            foreach (var step in TypeCache.GetMethodsWithAttribute<EchoBuildStepAttribute>()
                         .OrderBy(mi => ((EchoBuildStepAttribute)mi.GetCustomAttributes(typeof(EchoBuildStepAttribute), false)[0]).Order))
                step.Invoke(null, new object[] { ctx });

            // Линия 2 — зеркальная: Герой 2 идёт по экрану справа налево.
            // Отражаем раскладку (позиции и повороты), а не масштаб: тексты остаются читаемыми, коллайдеры — с положительным размером.
            MirrorX(line2);
            lines.line2Mirrored = true;

            // Навигация для охраны и мутантов (печётся на сервере при старте)
            foreach (var root in new[] { line1, line2 })
            {
                var surface = root.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            }

            var all = new List<GameObject> { heroPrefab, itemPrefab, doorPrefab, guardPrefab };
            all.AddRange(ctx.NetworkPrefabs);
            var prefabList = BuildPrefabList(all.ToArray());
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Echo] Сцена собрана: " + ScenePath + ". Нажми Play и выбери «Один ПК».");
        }

        static void MirrorX(Transform root)
        {
            foreach (Transform child in root)
            {
                var p = child.localPosition;
                child.localPosition = new Vector3(-p.x, p.y, p.z);
                var q = child.localRotation;
                child.localRotation = new Quaternion(q.x, -q.y, -q.z, q.w);
                MirrorX(child);
            }
        }

        static void BuildTestContent(Transform line1, Transform line2)
        {
            Marker(line1, ItemType.Key, 1, new Vector3(8f, 0f, 4f));
            Marker(line1, ItemType.Barrel, 0, new Vector3(4f, 0f, -3f));
            Marker(line1, ItemType.Backpack, 0, new Vector3(14f, 0f, -4f));
            Marker(line1, ItemType.Ammo, 0, new Vector3(30f, 0f, 4f));
            Marker(line1, ItemType.Key, 2, new Vector3(44f, 0f, -4f));
            Marker(line2, ItemType.Pistol, 0, new Vector3(2f, 0f, 3f));
            GuardAt(line1, false, new Vector3(16f, 0f, -3f), new Vector3(16f, 0f, 3f), new Vector3(6f, 0f, 3f));
            GuardAt(line1, true, new Vector3(34f, 0f, 4f), new Vector3(46f, 0f, 4f), new Vector3(46f, 0f, -4f), new Vector3(34f, 0f, -4f));
            CheckpointAt(line1, new Vector3(22f, 0f, 0f));
            CheckpointAt(line1, new Vector3(37f, 0f, 0f));
            CheckpointAt(line1, new Vector3(52f, 0f, 0f));
        }

        // ---------- Уровень ----------

        static void BuildTestLine(Transform root, Material floor, Material wall, Mats m, bool future)
        {
            Box("Floor", root, new Vector3(30f, -0.1f, 0f), new Vector3(72f, 0.2f, 12f), floor);
            Box("Wall_N", root, new Vector3(30f, 1.5f, 6f), new Vector3(72f, 3f, 0.4f), wall);
            Box("Wall_S", root, new Vector3(30f, 1.5f, -6f), new Vector3(72f, 3f, 0.4f), wall);
            Box("Wall_W", root, new Vector3(-6f, 1.5f, 0f), new Vector3(0.4f, 3f, 12f), wall);
            Box("Wall_E", root, new Vector3(66f, 1.5f, 0f), new Vector3(0.4f, 3f, 12f), wall);

            CrossWall(root, 20f, wall);
            CrossWall(root, 50f, wall);
            DoorMarkerAt(root, 1, 1, new Vector3(20f, 0f, 0f));
            DoorMarkerAt(root, 2, 2, new Vector3(50f, 0f, 0f));

            var reactor = Prim(PrimitiveType.Cylinder, "Reactor", root, new Vector3(40f, 0.6f, 0f), new Vector3(2.4f, 0.6f, 2.4f), m.reactor, true);
            reactor.AddComponent<ReactorMarker>();
            var machine = Prim(PrimitiveType.Cube, "Machine", root, new Vector3(62f, 1f, 0f), new Vector3(2f, 2f, 2f), m.machine, true);
            machine.AddComponent<MachineMarker>();

            // Шкафчики и стеклянная перегородка (одинаковые на обеих линиях)
            LockerAt(root, new Vector3(12f, 0f, 5.4f), 180f, m);
            LockerAt(root, new Vector3(26f, 0f, -5.4f), 0f, m);
            LockerAt(root, new Vector3(31f, 0f, 5.4f), 180f, m);
            var glass = Box("Glass", root, new Vector3(28f, 1.5f, 1.5f), new Vector3(4f, 3f, 0.15f), m.glass);
            glass.layer = 2; // Ignore Raycast: охрана видит сквозь стекло

            if (future)
            {
                Box("Rubble_1", root, new Vector3(10f, 0.5f, 2f), new Vector3(1.6f, 1f, 1.6f), m.rubble);
                Box("Rubble_2", root, new Vector3(30f, 0.4f, -3f), new Vector3(2.2f, 0.8f, 1.2f), m.rubble);
                Box("Rubble_3", root, new Vector3(46f, 0.6f, 3f), new Vector3(1.2f, 1.2f, 2f), m.rubble);
            }
            
        }

        static void CrossWall(Transform root, float x, Material wall)
        {
            Box("CrossWall_" + x + "_S", root, new Vector3(x, 1.5f, -3.75f), new Vector3(0.4f, 3f, 4.5f), wall);
            Box("CrossWall_" + x + "_N", root, new Vector3(x, 1.5f, 3.75f), new Vector3(0.4f, 3f, 4.5f), wall);
        }

        static void DoorMarkerAt(Transform root, int doorId, int keyId, Vector3 local)
        {
            var go = new GameObject("DoorMarker_" + doorId);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var d = go.AddComponent<DoorMarker>();
            d.doorId = doorId;
            d.keyId = keyId;
        }

        static void Marker(Transform root, ItemType type, int keyId, Vector3 local)
        {
            var go = new GameObject("Item_" + type + (keyId != 0 ? "_" + keyId : ""));
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var im = go.AddComponent<ItemMarker>();
            im.type = type;
            im.keyId = keyId;
        }

        static Camera MakeCamera(string name, Rect rect, bool main)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.rect = rect;
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            go.transform.SetPositionAndRotation(new Vector3(0f, 12f, -10f), Quaternion.Euler(50f, 0f, 0f));
            if (main)
            {
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }
            return cam;
        }

        static void GuardAt(Transform root, bool soldier, Vector3 start, params Vector3[] patrol)
        {
            var go = new GameObject(soldier ? "Soldier" : "Guard");
            go.transform.SetParent(root, false);
            go.transform.localPosition = start;
            var gm = go.AddComponent<GuardMarker>();
            gm.soldier = soldier;
            for (int i = 0; i < patrol.Length; i++)
            {
                var p = new GameObject("P" + (i + 1));
                p.transform.SetParent(go.transform, false);
                p.transform.position = root.position + patrol[i];
            }
        }

        static void CheckpointAt(Transform root, Vector3 local)
        {
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            go.AddComponent<CheckpointMarker>();
        }

        static void LockerAt(Transform root, Vector3 local, float yaw, Mats m)
        {
            var go = Prim(PrimitiveType.Cube, "Locker", root, local + new Vector3(0f, 1.1f, 0f), new Vector3(1f, 2.2f, 0.7f), m.locker, true);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var spot = new GameObject("LockerSpot");
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = local + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.8f;
            spot.AddComponent<LockerSpot>();
        }

        // ---------- Префабы ----------

        static GameObject BuildGuardPrefab(Mats m)
        {
            var go = new GameObject("Guard");
            go.AddComponent<NetworkObject>();
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.speed = 2f;
            agent.angularSpeed = 540f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.2f;
            agent.enabled = false; // включает сервер в Guard.ServerInit
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            var guard = go.AddComponent<Guard>();

            var body = Prim(PrimitiveType.Capsule, "Body", go.transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), m.guard);
            Prim(PrimitiveType.Cube, "Nose", body.transform, new Vector3(0f, 0.3f, 0.5f), new Vector3(0.3f, 0.15f, 0.3f), m.machine);
            var icon = Prim(PrimitiveType.Sphere, "Alert", go.transform, new Vector3(0f, 2.5f, 0f), Vector3.one * 0.45f, m.alertYellow);
            var cone = new GameObject("VisionCone");
            cone.transform.SetParent(go.transform, false);
            var mf = cone.AddComponent<MeshFilter>();
            var mr = cone.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.coneCalm;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            guard.bodyRenderer = body.GetComponent<Renderer>();
            // Спрайты художника (Assets/Art/Enemies/enm_guard.png, enm_soldier.png), если уже есть
            guard.guardSprite = LoadEnemySprite("enm_guard");
            guard.soldierSprite = LoadEnemySprite("enm_soldier");
            if (guard.guardSprite != null || guard.soldierSprite != null)
            {
                var sp = new GameObject("Sprite");
                sp.transform.SetParent(go.transform, false);
                var sr = sp.AddComponent<SpriteRenderer>();
                sr.sprite = guard.guardSprite != null ? guard.guardSprite : guard.soldierSprite;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                guard.spriteRenderer = sr;
            }
            guard.guardMaterial = m.guard;
            guard.soldierMaterial = m.soldier;
            guard.alertIcon = icon.GetComponent<Renderer>();
            guard.suspiciousMaterial = m.alertYellow;
            guard.chaseMaterial = m.alertRed;
            guard.coneFilter = mf;
            guard.coneRenderer = mr;
            guard.coneCalmMaterial = m.coneCalm;
            guard.coneAlertMaterial = m.coneAlert;
            return SavePrefab(go, "Guard");
        }

        static GameObject BuildHeroPrefab(Mats m)
        {
            var go = new GameObject("Hero");
            var no = go.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = true;
            var cc = go.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 2f;
            cc.radius = 0.4f;
            var nt = go.AddComponent<OwnerNetworkTransform>();
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            var hero = go.AddComponent<Hero>();
            foreach (var t in TypeCache.GetTypesWithAttribute<AddToHeroPrefabAttribute>())
                if (typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && go.GetComponent(t) == null) go.AddComponent(t);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            hero.visualRoot = visual;
            hero.hero1Material = m.hero1;
            hero.hero2Material = m.hero2;

            var rig = LoadOrBuildRig();
            if (rig != null)
            {
                // Скелетный герой от художника: билборд к камере, без капсулы.
                var bb = new GameObject("Billboard");
                bb.transform.SetParent(visual.transform, false);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(rig, bb.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                hero.billboard = bb.transform;
                hero.rigAnimator = inst.GetComponent<Animator>();
                hero.rigAura = inst.GetComponent<HeroAura>();
                hero.rigAppearance = inst.GetComponent<HeroAppearance>();
            }
            else
            {
                var body = Prim(PrimitiveType.Capsule, "Body", visual.transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), m.hero1);
                Prim(PrimitiveType.Cube, "Nose", body.transform, new Vector3(0f, 0.3f, 0.5f), new Vector3(0.3f, 0.15f, 0.3f), m.machine);
                var aura = Prim(PrimitiveType.Sphere, "Aura", visual.transform, new Vector3(0f, 1f, 0f), new Vector3(1.6f, 2.4f, 1.6f), m.aura);
                hero.bodyRenderer = body.GetComponent<Renderer>();
                hero.aura = aura;
            }
            return SavePrefab(go, "Hero");
        }

        // Спрайт врага 256×256, ноги у нижнего края → пивот снизу по центру, рост ≈ 2,3 м.
        static Sprite LoadEnemySprite(string name)
        {
            string path = "Assets/Art/Enemies/" + name + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            if (ti.textureType != TextureImporterType.Sprite || ti.spritePivot != new Vector2(0.5f, 0f))
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

        const string RigPrefabPath = "Assets/Art/Characters/Hero/Generated/Hero.prefab";
        const string RigJsonPath = "Assets/Art/Characters/Hero/Rig/hero_rig.json";

        static GameObject LoadOrBuildRig()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rig != null || AssetDatabase.LoadAssetAtPath<TextAsset>(RigJsonPath) == null) return rig;
            HeroRigBuilder.Build();
            // Сборщик художника оставляет экземпляр в открытой сцене — он нам не нужен.
            if (Selection.activeGameObject != null && Selection.activeGameObject.name == "Hero" &&
                Selection.activeGameObject.GetComponent<HeroAppearance>() != null)
                Object.DestroyImmediate(Selection.activeGameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        }

        static GameObject BuildItemPrefab(Mats m)
        {
            var go = new GameObject("Item");
            go.AddComponent<NetworkObject>();
            var item = go.AddComponent<Item>();
            var key = Prim(PrimitiveType.Cube, "Key", go.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.15f, 0.25f), m.key);
            var barrel = Prim(PrimitiveType.Cylinder, "Barrel", go.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.8f, 0.55f, 0.8f), m.barrel);
            var backpack = Prim(PrimitiveType.Cube, "Backpack", go.transform, new Vector3(0f, 0.35f, 0f), new Vector3(0.6f, 0.7f, 0.4f), m.backpack);
            var ammo = Prim(PrimitiveType.Cube, "Ammo", go.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.35f, 0.3f, 0.35f), m.ammo);
            var pistol = Prim(PrimitiveType.Cube, "Pistol", go.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.2f, 0.2f), m.pistol);
            var aura = Prim(PrimitiveType.Sphere, "Aura", go.transform, new Vector3(0f, 0.4f, 0f), Vector3.one * 1.3f, m.aura);
            item.visuals = new[] { key, barrel, backpack, ammo, pistol };
            item.aura = aura;
            return SavePrefab(go, "Item");
        }

        static GameObject BuildDoorPrefab(Mats m)
        {
            var go = new GameObject("Door");
            go.AddComponent<NetworkObject>();
            var door = go.AddComponent<Door>();
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(3f, 3f, 0.4f);
            var panel = Prim(PrimitiveType.Cube, "Panel", go.transform, new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 0.4f), m.door);
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.5f, 0f);
            obstacle.size = new Vector3(3f, 3f, 0.4f);
            obstacle.carving = true;
            door.closedVisual = panel;
            door.blocker = box;
            door.navBlocker = obstacle;
            return SavePrefab(go, "Door");
        }

        static NetworkPrefabsList BuildPrefabList(params GameObject[] prefabs)
        {
            string path = Root + "/Prefabs/EchoNetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, path);
            }
            foreach (var p in prefabs)
                if (!list.Contains(p)) list.Add(new NetworkPrefab { Prefab = p });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            return list;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ---------- Материалы и примитивы ----------

        static Mats CreateMaterials()
        {
            return new Mats
            {
                floor1 = Mat("Floor_Past", new Color(0.82f, 0.74f, 0.6f)),
                wall1 = Mat("Wall_Past", new Color(0.93f, 0.9f, 0.84f)),
                floor2 = Mat("Floor_Future", new Color(0.18f, 0.22f, 0.2f)),
                wall2 = Mat("Wall_Future", new Color(0.26f, 0.28f, 0.27f)),
                rubble = Mat("Rubble", new Color(0.35f, 0.33f, 0.3f)),
                hero1 = Mat("Hero1", new Color(0.25f, 0.5f, 0.95f)),
                hero2 = Mat("Hero2", new Color(0.95f, 0.55f, 0.2f)),
                aura = Mat("Aura", new Color(0.6f, 0.3f, 1f, 0.25f), true),
                door = Mat("Door", new Color(0.6f, 0.2f, 0.15f)),
                reactor = Mat("Reactor", new Color(0.2f, 0.85f, 0.75f)),
                machine = Mat("Machine", new Color(0.15f, 0.15f, 0.18f)),
                key = Mat("Item_Key", new Color(1f, 0.85f, 0.2f)),
                barrel = Mat("Item_Barrel", new Color(0.5f, 0.3f, 0.15f)),
                backpack = Mat("Item_Backpack", new Color(0.3f, 0.6f, 0.25f)),
                ammo = Mat("Item_Ammo", new Color(0.6f, 0.6f, 0.6f)),
                pistol = Mat("Item_Pistol", new Color(0.1f, 0.1f, 0.12f)),
                guard = Mat("Guard", new Color(0.2f, 0.25f, 0.45f)),
                soldier = Mat("Soldier", new Color(0.35f, 0.4f, 0.2f)),
                alertYellow = Mat("Alert_Suspicious", new Color(1f, 0.85f, 0.1f)),
                alertRed = Mat("Alert_Chase", new Color(1f, 0.15f, 0.1f)),
                coneCalm = Mat("Cone_Calm", new Color(1f, 0.95f, 0.4f, 0.18f), true),
                coneAlert = Mat("Cone_Alert", new Color(1f, 0.2f, 0.1f, 0.25f), true),
                locker = Mat("Locker", new Color(0.45f, 0.55f, 0.6f)),
                glass = Mat("Glass", new Color(0.6f, 0.85f, 1f, 0.25f), true),
            };
        }

        static Material Mat(string name, Color color, bool transparent = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
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
                mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 1.5f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat) =>
            Prim(PrimitiveType.Cube, name, parent, localPos, scale, mat, true);

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

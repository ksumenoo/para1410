using System.Collections.Generic;
using System.IO;
using System.Linq;
using Echo.Audio;
using Echo.Ending;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Echo.UI
{
    // Редакторская часть интерфейса, звука и концовок (Чат 4):
    //  • шаг сборки сцены: сетевые префабы EndingState и AppearanceState + ссылки в EndingSpawner;
    //  • «Echo → Интерфейс → Обновить арт и звук»: заполняет EchoUiAssets и EchoAudioLibrary
    //    из Assets/Art/{Endings,Items,UI,Audio}. Запускается и сам, когда в эти папки кладут файлы.
    public static class UiBuildStep
    {
        const string ResourcesDir = "Assets/_Echo/UI/Resources";

        [Echo.EditorTools.EchoBuildStep(order: 30)]
        public static void Build(Echo.EditorTools.EchoBuildContext ctx)
        {
            string prefabDir = ctx.Root + "/Prefabs/UI";
            EnsureFolder(prefabDir);

            var ending = SaveNetworkPrefab<EndingState>("EndingState", prefabDir);
            var appearance = SaveNetworkPrefab<AppearanceState>("AppearanceState", prefabDir);
            ctx.RegisterNetworkPrefab(ending);
            ctx.RegisterNetworkPrefab(appearance);

            var spawner = ctx.GameSystems.GetComponent<EndingSpawner>();
            if (spawner == null) spawner = ctx.GameSystems.AddComponent<EndingSpawner>();
            spawner.endingPrefab = ending.GetComponent<NetworkObject>();
            spawner.appearancePrefab = appearance.GetComponent<NetworkObject>();
            EditorUtility.SetDirty(spawner);

            RefreshAssets();
        }

        static GameObject SaveNetworkPrefab<T>(string name, string dir) where T : Component
        {
            var go = new GameObject(name);
            go.AddComponent<NetworkObject>();
            go.AddComponent<T>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- Арт и звук ----------

        [MenuItem("Echo/Интерфейс/Обновить арт и звук")]
        public static void RefreshAssets()
        {
            EnsureFolder(ResourcesDir);

            var ui = LoadOrCreate<EchoUiAssets>(ResourcesDir + "/" + EchoUiAssets.ResourceName + ".asset");
            ui.endLoop = FindSprite("Assets/Art/Endings/end_loop");
            ui.endOneBody = FindSprite("Assets/Art/Endings/end_onebody");
            ui.slot = FindSprite("Assets/Art/UI/ui_inventory_slot");
            ui.radioTalk = FindSprite("Assets/Art/UI/ui_radio_talk");
            ui.radioMuted = FindSprite("Assets/Art/UI/ui_radio_mute", "Assets/Art/UI/ui_radio_muted");
            ui.itemIcons = new[]
            {
                FindSprite("Assets/Art/Items/itm_keycard", "Assets/Art/Items/itm_key"),
                FindSprite("Assets/Art/Items/itm_barrel"),
                FindSprite("Assets/Art/Items/itm_backpack"),
                FindSprite("Assets/Art/Items/itm_ammo"),
                FindSprite("Assets/Art/Items/itm_pistol"),
            };
            EditorUtility.SetDirty(ui);

            var lib = LoadOrCreate<EchoAudioLibrary>(ResourcesDir + "/" + EchoAudioLibrary.ResourceName + ".asset");
            var clips = FindClips("Assets/Art/Audio");
            lib.musicPast = Pick(clips, new[] { "music", "mus" }, new[] { "past", "l1" });
            lib.musicFuture = Pick(clips, new[] { "music", "mus" }, new[] { "future", "l2" });
            lib.steps = clips.Where(c => Has(c, "step")).ToArray();
            lib.pickup = Pick(clips, "pickup", "take", "grab");
            lib.drop = Pick(clips, "drop", "put");
            lib.door = Pick(clips, "door");
            lib.alarm = Pick(clips, "alarm", "alert");
            lib.shotBack = Pick(clips, "shot_back", "shotback", "reverse", "return");
            lib.shot = clips.FirstOrDefault(c => (Has(c, "shot") || Has(c, "shoot") || Has(c, "gun")) && c != lib.shotBack);
            lib.hit = Pick(clips, "hit", "hurt");
            lib.reactorHum = Pick(clips, "reactor", "hum");
            lib.click = Pick(clips, "click", "ui_");
            lib.radioOn = Pick(clips, "radio_on", "radioon");
            lib.radioOff = Pick(clips, "radio_off", "radiooff");
            lib.radioNoise = Pick(clips, "radio_noise", "static", "noise");
            lib.ending = Pick(clips, "ending", "end_");
            EditorUtility.SetDirty(lib);

            AssetDatabase.SaveAssets();
        }

        // Само обновляет ссылки, когда художник или звукорежиссёр добавил файлы.
        class ArtWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                bool relevant = imported.Concat(deleted).Concat(moved).Any(p =>
                    p.StartsWith("Assets/Art/Audio/") || p.StartsWith("Assets/Art/Items/") ||
                    p.StartsWith("Assets/Art/UI/") || p.StartsWith("Assets/Art/Endings/"));
                if (relevant) EditorApplication.delayCall += RefreshAssets;
            }
        }

        // ---------- Помощники ----------

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // basePath без расширения; пробует png и jpg, при необходимости переключает импорт на Sprite.
        static Sprite FindSprite(params string[] basePaths)
        {
            foreach (var basePath in basePaths)
            {
                foreach (var ext in new[] { ".png", ".jpg" })
                {
                    string path = basePath + ext;
                    if (!File.Exists(path)) continue;
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                    }
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null) return sprite;
                }
            }
            return null;
        }

        static List<AudioClip> FindClips(string folder)
        {
            var list = new List<AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder)) return list;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) list.Add(clip);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        static bool Has(AudioClip clip, string word) => clip.name.ToLowerInvariant().Contains(word);

        // Первый клип, в имени которого есть любое из слов.
        static AudioClip Pick(List<AudioClip> clips, params string[] words) =>
            clips.FirstOrDefault(c => words.Any(w => Has(c, w)));

        // Первый клип, в имени которого есть любое слово из первой группы и любое из второй.
        static AudioClip Pick(List<AudioClip> clips, string[] groupA, string[] groupB) =>
            clips.FirstOrDefault(c => groupA.Any(w => Has(c, w)) && groupB.Any(w => Has(c, w)));

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

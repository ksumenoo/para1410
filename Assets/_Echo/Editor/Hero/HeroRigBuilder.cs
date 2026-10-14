#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// «Наоборот получилось»: собирает скелетного героя из частей, делает анимации и контроллер.
// Меню: Tools → Наоборот → Собрать героя
public static class HeroRigBuilder
{
    const string ArtDir = "Assets/Art/Characters/Hero/Rig";
    const string OutDir = "Assets/Art/Characters/Hero/Generated";

    [Serializable] public class RigSprite { public string file; public float pivotX; public float pivotY; }
    [Serializable] public class RigBone { public string name; public string parent; public float x; public float y; public string sprite; public int order; public string tint; public bool glow; }
    [Serializable] public class RigKey { public string bone; public float[] values; }
    [Serializable] public class RigClip { public string name; public float fps; public bool loop; public RigKey[] keys; }
    [Serializable] public class RigData
    {
        public float ppu;
        public RigSprite[] sprites;
        public RigBone[] bones;
        public RigClip[] clips;
        public string[] hair, eyes, faces, torsos;
        public string[] suitColors, suitShades, skinColors, hairColors;
    }

    [MenuItem("Tools/Наоборот/Собрать героя")]
    public static void Build()
    {
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>(ArtDir + "/hero_rig.json");
        if (json == null) { Debug.LogError("Не найден " + ArtDir + "/hero_rig.json"); return; }
        var data = JsonUtility.FromJson<RigData>(json.text);

        ImportSprites(data);
        EnsureFolder(OutDir);

        float k = 1f / data.ppu;
        var root = new GameObject("Hero");
        root.AddComponent<SortingGroup>();
        var app = root.AddComponent<HeroAppearance>();
        var aura = root.AddComponent<HeroAura>();

        var map = new Dictionary<string, Transform> { { "Root", root.transform } };
        var skin = new List<SpriteRenderer>();
        var skinShade = new List<SpriteRenderer>();
        var suit = new List<SpriteRenderer>();
        var suitShade = new List<SpriteRenderer>();
        var glows = new List<SpriteRenderer>();
        var rims = new List<SpriteRenderer>();

        foreach (var b in data.bones)
        {
            var go = new GameObject(b.name);
            go.transform.SetParent(map[b.parent], false);
            go.transform.localPosition = new Vector3(b.x * k, b.y * k, 0f);
            map[b.name] = go.transform;
            if (string.IsNullOrEmpty(b.sprite)) continue;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(b.sprite);
            sr.sortingOrder = b.order * 2;

            switch (b.tint)
            {
                case "skin": skin.Add(sr); break;
                case "skinShade": skinShade.Add(sr); break;
                case "suit": suit.Add(sr); break;
                case "suitShade": suitShade.Add(sr); break;
            }
            if (b.name == "Hair") app.hair = sr;
            if (b.name == "Eyes") app.eyes = sr;
            if (b.name == "Face") app.face = sr;
            if (b.name == "Torso") app.torso = sr;

            if (b.glow)
            {
                string baseName = b.name == "Torso" ? "hero_torso" : b.sprite;
                var g = MakeChild(go.transform, "AuraGlow", baseName + "_glow", -1);
                var r = MakeChild(go.transform, "AuraRim", baseName + "_rim", b.order * 2 + 1);
                glows.Add(g);
                rims.Add(r);
                if (b.name == "Hair") { app.hairGlow = g; app.hairRim = r; }
            }
        }

        app.skin = skin.ToArray();
        app.skinShade = skinShade.ToArray();
        app.suit = suit.ToArray();
        app.suitShade = suitShade.ToArray();
        app.hairSprites = data.hair.Select(LoadSprite).ToArray();
        app.hairGlowSprites = data.hair.Select(n => LoadSprite(n + "_glow")).ToArray();
        app.hairRimSprites = data.hair.Select(n => LoadSprite(n + "_rim")).ToArray();
        app.eyeSprites = data.eyes.Select(LoadSprite).ToArray();
        app.faceSprites = data.faces.Select(LoadSprite).ToArray();
        app.torsoSprites = data.torsos.Select(LoadSprite).ToArray();
        app.skinColors = data.skinColors.Select(ParseColor).ToArray();
        app.hairColors = data.hairColors.Select(ParseColor).ToArray();
        app.suitColors = data.suitColors.Select(ParseColor).ToArray();
        app.suitShades = data.suitShades.Select(ParseColor).ToArray();
        app.gun = map["Gun"].gameObject;
        app.Apply();
        app.SetGunVisible(false);

        aura.glows = glows.ToArray();
        aura.rims = rims.ToArray();
        aura.SetAura(false);

        // анимации
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var c in data.clips)
        {
            var clip = BuildClip(c, map, root.transform, k);
            string path = OutDir + "/hero_" + c.name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            clips[c.name] = clip;
        }

        var ctrl = BuildController(clips);
        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;

        string prefabPath = OutDir + "/Hero.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("Герой собран: " + prefabPath);
    }

    static AnimationClip BuildClip(RigClip c, Dictionary<string, Transform> map, Transform root, float k)
    {
        var clip = new AnimationClip { name = "hero_" + c.name, frameRate = 60f };
        foreach (var key in c.keys)
        {
            int n = key.values.Length;
            int count = c.loop ? n + 1 : n;

            if (key.bone == "Flash@on")
            {
                var curve = new AnimationCurve();
                for (int i = 0; i < count; i++) curve.AddKey(new Keyframe(i / c.fps, key.values[i % n]));
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                var binding = EditorCurveBinding.FloatCurve(PathOf(map["Flash"], root), typeof(SpriteRenderer), "m_Enabled");
                AnimationUtility.SetEditorCurve(clip, binding, curve);
                continue;
            }

            if (key.bone.StartsWith("Hips@"))
            {
                string hips = PathOf(map["Hips"], root);
                var curve = new AnimationCurve();
                for (int i = 0; i < count; i++) curve.AddKey(i / c.fps, key.values[i % n] * k);
                Smooth(curve);
                clip.SetCurve(hips, typeof(Transform), key.bone.EndsWith("x") ? "localPosition.x" : "localPosition.y", curve);
                if (key.bone.EndsWith("x"))
                    clip.SetCurve(hips, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, (count - 1) / c.fps, 0f));
                continue;
            }

            if (!map.ContainsKey(key.bone)) continue;
            string p = PathOf(map[key.bone], root);
            var qx = new AnimationCurve(); var qy = new AnimationCurve();
            var qz = new AnimationCurve(); var qw = new AnimationCurve();
            for (int i = 0; i < count; i++)
            {
                float t = i / c.fps;
                var q = Quaternion.Euler(0f, 0f, key.values[i % n]);
                qx.AddKey(t, q.x); qy.AddKey(t, q.y); qz.AddKey(t, q.z); qw.AddKey(t, q.w);
            }
            Smooth(qz); Smooth(qw);
            clip.SetCurve(p, typeof(Transform), "localRotation.x", qx);
            clip.SetCurve(p, typeof(Transform), "localRotation.y", qy);
            clip.SetCurve(p, typeof(Transform), "localRotation.z", qz);
            clip.SetCurve(p, typeof(Transform), "localRotation.w", qw);
        }
        clip.EnsureQuaternionContinuity();
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = c.loop;
        AnimationUtility.SetAnimationClipSettings(clip, st);
        return clip;
    }

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        string path = OutDir + "/Hero.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = AddState(sm, clips, "idle", 300, 0);
        var walk = AddState(sm, clips, "walk", 600, 0);
        var crouch = AddState(sm, clips, "crouch_idle", 300, 150);
        var sneak = AddState(sm, clips, "sneak", 600, 150);
        var shoot = AddState(sm, clips, "shoot", 300, -150);
        var interact = AddState(sm, clips, "interact", 600, -150);
        sm.defaultState = idle;

        Go(idle, walk).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        Go(walk, idle).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        Go(idle, crouch).AddCondition(AnimatorConditionMode.If, 0f, "Crouch");
        Go(crouch, idle).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouch");
        Go(walk, sneak).AddCondition(AnimatorConditionMode.If, 0f, "Crouch");
        Go(sneak, walk).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouch");
        Go(crouch, sneak).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        Go(sneak, crouch).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var toShoot = sm.AddAnyStateTransition(shoot);
        toShoot.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
        toShoot.duration = 0.03f; toShoot.canTransitionToSelf = true;
        var toInteract = sm.AddAnyStateTransition(interact);
        toInteract.AddCondition(AnimatorConditionMode.If, 0f, "Interact");
        toInteract.duration = 0.08f; toInteract.canTransitionToSelf = false;

        foreach (var s in new[] { shoot, interact })
        {
            var back = s.AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f;
        }
        return ctrl;
    }

    static AnimatorState AddState(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string name, float x, float y)
    {
        var st = sm.AddState(name, new Vector3(x, y, 0f));
        st.motion = clips[name];
        return st;
    }

    static AnimatorStateTransition Go(AnimatorState a, AnimatorState b)
    {
        var t = a.AddTransition(b);
        t.hasExitTime = false;
        t.duration = 0.15f;
        return t;
    }

    static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
    }

    static SpriteRenderer MakeChild(Transform parent, string name, string sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LoadSprite(sprite);
        sr.sortingOrder = order;
        return sr;
    }

    static void ImportSprites(RigData data)
    {
        foreach (var s in data.sprites)
        {
            string path = ArtDir + "/" + s.file + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("Нет файла " + path); continue; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = data.ppu;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteAlignment = (int)SpriteAlignment.Custom;
            st.spritePivot = new Vector2(s.pivotX, s.pivotY);
            st.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
        }
    }

    static Sprite LoadSprite(string file)
    {
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + file + ".png");
        if (sp == null) Debug.LogWarning("Не найден спрайт " + file);
        return sp;
    }

    static Color ParseColor(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }

    static string PathOf(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif

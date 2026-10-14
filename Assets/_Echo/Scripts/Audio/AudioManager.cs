using System.Collections.Generic;
using Echo.Ending;
using UnityEngine;

namespace Echo.Audio
{
    public enum Sfx { Step, Pickup, Drop, Door, Alarm, Shot, ShotBack, Hit, Click, RadioOn, RadioOff, RadioNoise, Ending }

    // Звук игры: музыка по линиям (кроссфейд при сопряжении) и эффекты. Ничего не требует от ядра:
    // события ловит сам, опрашивая состояние героев, предметов, дверей и охраны.
    [AddToGameSystems]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        const string VolumeKey = "echo.volume";
        const float MusicLevel = 0.45f;
        const float CrossfadeSeconds = 1.5f;

        public static float MasterVolume
        {
            get => AudioListener.volume;
            set
            {
                AudioListener.volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            }
        }

        AudioSource _past, _future, _reactor, _sfx;
        float _pastVol, _futureVol;
        float _alarmUntil;
        int _lastChoice;

        class StepInfo { public Vector3 Pos; public float Timer; public bool Init; }
        readonly Dictionary<ulong, StepInfo> _steps = new Dictionary<ulong, StepInfo>();
        readonly Dictionary<ulong, ulong> _carriers = new Dictionary<ulong, ulong>();
        readonly Dictionary<ulong, bool> _doors = new Dictionary<ulong, bool>();
        readonly Dictionary<ulong, int> _guards = new Dictionary<ulong, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (I != null || FindFirstObjectByType<AudioManager>() != null) return;
            new GameObject("EchoAudio").AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;
            try { AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f); } catch { }
            _past = MakeSource("MusicPast", true);
            _future = MakeSource("MusicFuture", true);
            _reactor = MakeSource("ReactorHum", true);
            _sfx = MakeSource("Sfx", false);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        // ---------- Публичное API (можно вызывать из других модулей) ----------

        public static void Play(Sfx sfx, float volume = 1f)
        {
            if (I != null) I.PlayInternal(sfx, volume);
        }

        void PlayInternal(Sfx sfx, float volume)
        {
            var lib = EchoAudioLibrary.I;
            if (lib == null || _sfx == null) return;
            AudioClip clip = null;
            switch (sfx)
            {
                case Sfx.Step: if (lib.steps != null && lib.steps.Length > 0) clip = lib.steps[Random.Range(0, lib.steps.Length)]; break;
                case Sfx.Pickup: clip = lib.pickup; break;
                case Sfx.Drop: clip = lib.drop; break;
                case Sfx.Door: clip = lib.door; break;
                case Sfx.Alarm: clip = lib.alarm; break;
                case Sfx.Shot: clip = lib.shot; break;
                case Sfx.ShotBack: clip = lib.shotBack; break;
                case Sfx.Hit: clip = lib.hit; break;
                case Sfx.Click: clip = lib.click; break;
                case Sfx.RadioOn: clip = lib.radioOn; break;
                case Sfx.RadioOff: clip = lib.radioOff; break;
                case Sfx.RadioNoise: clip = lib.radioNoise; break;
                case Sfx.Ending: clip = lib.ending; break;
            }
            if (clip != null) _sfx.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ---------- Цикл ----------

        void Update()
        {
            var lib = EchoAudioLibrary.I;
            var cams = SplitCameras.I;
            var top = cams != null ? cams.TopHero : null;
            if (top != null && !top.IsSpawned) top = null;

            UpdateMusic(lib, top);
            if (top == null) return;

            UpdateSteps(top);
            UpdateItems(top);
            UpdateDoors(top);
            UpdateGuards(top);
            UpdateReactor(lib);
            UpdateEnding();
        }

        void UpdateMusic(EchoAudioLibrary lib, Hero top)
        {
            float step = Time.unscaledDeltaTime / CrossfadeSeconds;
            float wantPast = top != null && top.Line.Value == 1 ? 1f : 0f;
            float wantFuture = top != null && top.Line.Value == 2 ? 1f : 0f;
            _pastVol = Mathf.MoveTowards(_pastVol, wantPast, step);
            _futureVol = Mathf.MoveTowards(_futureVol, wantFuture, step);
            Drive(_past, lib != null ? lib.musicPast : null, _pastVol * MusicLevel);
            Drive(_future, lib != null ? lib.musicFuture : null, _futureVol * MusicLevel);
        }

        static void Drive(AudioSource src, AudioClip clip, float volume)
        {
            if (src.clip != clip) { src.Stop(); src.clip = clip; }
            if (clip == null) { src.volume = 0f; return; }
            src.volume = volume;
            if (volume > 0.001f && !src.isPlaying) src.Play();
        }

        void UpdateSteps(Hero top)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            foreach (var h in Hero.All)
            {
                if (!h.IsSpawned) continue;
                if (!_steps.TryGetValue(h.NetworkObjectId, out var info)) _steps[h.NetworkObjectId] = info = new StepInfo();
                var pos = h.transform.position;
                float speed = info.Init ? Hero.FlatDistance(pos, info.Pos) / dt : 0f;
                info.Pos = pos;
                info.Init = true;
                if (speed > 20f || speed < 0.6f || h.IsHiding) { info.Timer = 0f; continue; }
                info.Timer += dt;
                float interval = h.IsSneaking ? 0.62f : 0.38f;
                if (info.Timer < interval) continue;
                info.Timer = 0f;
                float vol = (h == top ? 0.5f : 0.18f) * (h.IsSneaking ? 0.5f : 1f);
                PlayInternal(Sfx.Step, vol);
            }
        }

        void UpdateItems(Hero top)
        {
            foreach (var it in Item.All)
            {
                if (!it.IsSpawned) continue;
                ulong id = it.NetworkObjectId;
                ulong carrier = it.Carrier.Value;
                if (!_carriers.TryGetValue(id, out ulong last)) { _carriers[id] = carrier; continue; }
                if (last == carrier) continue;
                _carriers[id] = carrier;
                ulong who = carrier != 0 ? carrier : last;
                float vol = top.NetworkObjectId == who ? 0.8f : 0.3f;
                PlayInternal(carrier != 0 ? Sfx.Pickup : Sfx.Drop, vol);
            }
        }

        void UpdateDoors(Hero top)
        {
            foreach (var d in Door.All)
            {
                if (!d.IsSpawned) continue;
                ulong id = d.NetworkObjectId;
                bool open = d.Open.Value;
                if (!_doors.TryGetValue(id, out bool last)) { _doors[id] = open; continue; }
                if (last == open) continue;
                _doors[id] = open;
                if (open) PlayInternal(Sfx.Door, d.Line.Value == top.Line.Value ? 0.9f : 0.35f);
            }
        }

        void UpdateGuards(Hero top)
        {
            foreach (var g in Guard.All)
            {
                if (!g.IsSpawned) continue;
                ulong id = g.NetworkObjectId;
                int state = g.State.Value;
                if (!_guards.TryGetValue(id, out int last)) { _guards[id] = state; continue; }
                if (last == state) continue;
                _guards[id] = state;
                if (state != (int)GuardState.Chase || Time.time < _alarmUntil) continue;
                _alarmUntil = Time.time + 1.5f;
                int line = TimeLines.I != null ? TimeLines.I.LineOf(g.transform.position) : 1;
                PlayInternal(Sfx.Alarm, line == top.Line.Value ? 0.9f : 0.25f);
            }
        }

        void UpdateReactor(EchoAudioLibrary lib)
        {
            float progress = 0f;
            foreach (var h in Hero.All)
                if (h.IsSpawned && h.ReactorProgress.Value > progress) progress = h.ReactorProgress.Value;
            Drive(_reactor, lib != null ? lib.reactorHum : null, progress * 0.8f);
            if (progress <= 0.001f && _reactor.isPlaying) _reactor.Stop();
        }

        void UpdateEnding()
        {
            int choice = EndingState.I != null ? EndingState.I.Choice.Value : 0;
            if (choice != _lastChoice)
            {
                _lastChoice = choice;
                if (choice != EndingState.None) PlayInternal(Sfx.Ending, 1f);
            }
        }
    }
}

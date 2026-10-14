using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Echo
{
    // Верхняя половина экрана — свой герой, нижняя — напарник. Экраны не меняются.
    public class SplitCameras : MonoBehaviour
    {
        public static SplitCameras I { get; private set; }

        public Camera top;
        public Camera bottom;
        public float distance = 16f;
        public float pitch = 50f;
        public float smooth = 8f;

        public Hero TopHero { get; private set; }
        public Hero BottomHero { get; private set; }

        void Awake() => I = this;

        public static Hero MyHero()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || Hero.All.Count == 0) return null;
            if (GameSession.SinglePc) return Hero.Get(1);
            if (nm.IsServer) return Hero.Get(GameSession.HostHeroChoice);
            var owned = Hero.All.FirstOrDefault(h => h.IsOwner);
            return owned != null ? owned : Hero.Get(1);
        }

        void LateUpdate()
        {
            TopHero = MyHero();
            BottomHero = TopHero != null ? Hero.All.FirstOrDefault(h => h != TopHero) : null;
            Follow(top, TopHero, 1);
            Follow(bottom, BottomHero, 2);
        }

        void Follow(Camera cam, Hero hero, int idleLine)
        {
            if (cam == null || TimeLines.I == null) return;
            Vector3 target = hero != null
                ? hero.transform.position + Vector3.up
                : TimeLines.I.ToWorld(new Vector3(30f, 0f, 0f), idleLine);
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            var want = target - rot * Vector3.forward * distance;
            bool snap = (cam.transform.position - want).sqrMagnitude > 400f;
            var pos = snap ? want : Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            cam.transform.SetPositionAndRotation(pos, rot);
        }
    }
}

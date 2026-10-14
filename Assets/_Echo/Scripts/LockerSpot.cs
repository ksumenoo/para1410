using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Echo
{
    // Шкафчик: E рядом — спрятаться, E ещё раз — выйти. Это часть статичной сцены,
    // поэтому индекс в отсортированном списке одинаков на всех машинах.
    public class LockerSpot : MonoBehaviour
    {
        static readonly List<LockerSpot> Registered = new List<LockerSpot>();
        static List<LockerSpot> _sorted;

        void OnEnable() { Registered.Add(this); _sorted = null; }
        void OnDisable() { Registered.Remove(this); _sorted = null; }

        public static IReadOnlyList<LockerSpot> All
        {
            get
            {
                if (_sorted == null)
                    _sorted = Registered.OrderBy(l => l.transform.position.x)
                                        .ThenBy(l => l.transform.position.z).ToList();
                return _sorted;
            }
        }

        public static int IndexOf(LockerSpot l)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++) if (all[i] == l) return i;
            return -1;
        }
    }
}

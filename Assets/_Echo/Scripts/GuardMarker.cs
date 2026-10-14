using UnityEngine;

namespace Echo
{
    // Охранник или военный. Точки патруля — дочерние объекты метки.
    public class GuardMarker : MonoBehaviour
    {
        public bool soldier;

        void OnDrawGizmos()
        {
            Gizmos.color = soldier ? new Color(0.5f, 0.6f, 0.2f) : Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.8f, 2f, 0.8f));
            Vector3 prev = transform.position;
            foreach (Transform p in transform) { Gizmos.DrawLine(prev, p.position); prev = p.position; }
        }
    }
}

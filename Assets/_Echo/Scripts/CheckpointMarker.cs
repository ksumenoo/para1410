using UnityEngine;

namespace Echo
{
    // Точка возрождения. Ставится на линии 1, работает для обеих линий (та же локальная позиция).
    public class CheckpointMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
        }
    }
}

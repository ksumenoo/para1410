using UnityEngine;

namespace Echo.Combat
{
    // Метка сгустка времени. Линия определяется по позиции.
    public class TimeClotMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.65f, 0.45f, 1f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.6f);
        }
    }
}

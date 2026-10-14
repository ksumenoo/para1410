using UnityEngine;

namespace Echo.Combat
{
    // Метка мутанта. Ставится модулем уровней (обычно на линии 2). Линия определяется по позиции.
    public class MutantMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.9f, 0.3f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }
    }
}

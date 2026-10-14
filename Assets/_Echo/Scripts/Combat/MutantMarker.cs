using UnityEngine;

namespace Echo.Combat
{
    // Метка мутанта. Ставится модулем уровней (обычно на линии 2). Линия — по позиции, взгляд — по повороту Y.
    public class MutantMarker : MonoBehaviour
    {
        [Tooltip("«Мёртвый до нас»: лежит трупом с пулей. Когда Герой 2 с пистолетом подходит ближе wakeRadius, " +
                 "пуля вылетает из трупа в его обойму (+1), мутант встаёт и нападает.")]
        public bool startDead;

        void OnDrawGizmos()
        {
            Gizmos.color = startDead ? new Color(0.6f, 0.4f, 1f) : new Color(0.5f, 0.9f, 0.3f);
            var p = transform.position + Vector3.up * (startDead ? 0.3f : 1f);
            Gizmos.DrawWireSphere(p, 0.5f);
            Gizmos.DrawLine(p, p + transform.forward);
            if (startDead) Gizmos.DrawWireSphere(transform.position, 5f);
        }
    }
}

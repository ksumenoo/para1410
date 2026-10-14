using UnityEngine;

namespace Echo
{
    // Аппарат (финал): у начала трапа Герой 2 выбирает концовку. Ставит модуль уровней,
    // логику концовок ведёт модуль интерфейса/концовок.
    public class MachineMarker : MonoBehaviour
    {
        public float radius = 3f;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

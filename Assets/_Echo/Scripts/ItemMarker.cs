using UnityEngine;

namespace Echo
{
    // Метка предмета. Сервер при старте создаёт по ней сетевой предмет.
    public class ItemMarker : MonoBehaviour
    {
        public ItemType type;
        public int keyId;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.3f, Vector3.one * 0.6f);
        }
    }
}

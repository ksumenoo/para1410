using UnityEngine;

namespace Echo
{
    // Метка двери. Дверь с тем же doorId на линии 2 открывается, когда её открыли на линии 1.
    public class DoorMarker : MonoBehaviour
    {
        public int doorId = 1;
        public int keyId = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 1.5f, new Vector3(3f, 3f, 0.4f));
        }
    }
}

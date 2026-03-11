using UnityEngine;

public class LockCameraPosition : MonoBehaviour
{
    private float startX;
    private float startZ;
    private bool captured = false;

    void LateUpdate()
    {
        // Wait one frame for OVR tracking to initialize floor height
        if (!captured)
        {
            startX = transform.position.x;
            startZ = transform.position.z;
            captured = true;
            return;
        }

        // Only lock XZ position (horizontal movement from thumbstick)
        // Let Y stay free so OVR floor-level tracking works
        Vector3 pos = transform.position;
        pos.x = startX;
        pos.z = startZ;
        transform.position = pos;
    }
}

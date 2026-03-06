using UnityEngine;

public class CursedPainting : MonoBehaviour
{
    [Header("Painting Textures (drag in order)")]
    public Texture2D[] variants;         // Normal, Eyes closed, Cracked, Bleeding

    [Header("Gaze Detection")]
    public float lookAngleThreshold = 25f;
    public float cooldown = 1f;          // Minimum seconds between swaps

    private int currentIndex = 0;
    private bool playerIsLooking = false;
    private bool hasLookedOnce = false;
    private float lastSwapTime = -10f;
    private Renderer paintingRenderer;
    private Transform playerCamera;

    void Start()
    {
        paintingRenderer = GetComponent<Renderer>();

        if (variants != null && variants.Length > 0)
            paintingRenderer.material.mainTexture = variants[0];
    }

    void Update()
    {
        if (playerCamera == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
                playerCamera = rig.centerEyeAnchor;
            return;
        }

        Vector3 dirToPainting = (transform.position - playerCamera.position).normalized;
        float angle = Vector3.Angle(playerCamera.forward, dirToPainting);
        bool isLooking = angle < lookAngleThreshold;

        if (isLooking && !playerIsLooking)
        {
            // First time looking — just register it, don't swap
            if (!hasLookedOnce)
            {
                hasLookedOnce = true;
            }
            else if (Time.time - lastSwapTime > cooldown)
            {
                // Looked away and back — swap texture
                currentIndex = (currentIndex + 1) % variants.Length;
                paintingRenderer.material.mainTexture = variants[currentIndex];
                lastSwapTime = Time.time;
            }
        }

        playerIsLooking = isLooking;
    }
}

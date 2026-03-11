using UnityEngine;
using System.Collections;

public class CandleController : MonoBehaviour
{
    [Header("Fire Effect")]
    public GameObject candleFire;            // CandleFire parent GameObject

    [Header("Skull")]
    public GameObject skull;
    public float skullFadeInDuration = 2f;

    [Header("Appear With Skull")]
    public GameObject pentagram;
    public GameObject rope;

    [Header("Cross Ritual")]
    public CrossRitual crossRitual;

    [Header("Candle Buildup (B Button)")]
    public float buildupDuration = 3f;       // Time to reach max while holding B
    public float startEmission = 10f;         // Starting emission rate
    public float maxEmission = 80f;           // Max emission rate at full buildup
    public float startSizeMultiplier = 1f;    // Starting particle size
    public float maxSizeMultiplier = 3f;      // Max particle size at full buildup

    // State
    private bool isActivated = false;
    private bool isBuildingUp = false;
    private bool skullAppearing = false;
    private bool skullFullyVisible = false;
    private float buildupProgress = 0f;
    private float skullFadeTimer = 0f;

    private ParticleSystem[] fireParticles;
    private float[] originalStartSizes;
    private Renderer[] skullRenderers;

    void Start()
    {
        if (candleFire != null)
            candleFire.SetActive(false);

        if (skull != null)
            skull.SetActive(false);

        if (pentagram != null)
            pentagram.SetActive(false);

        if (rope != null)
            rope.SetActive(false);
    }

    void Update()
    {
        if (!isActivated) return;

        // B button hold — only works after cross ritual is complete
        if (!skullAppearing && !skullFullyVisible)
        {
            // Block B button until cross ritual is done
            if (crossRitual != null && !crossRitual.IsComplete())
                return;

            if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            {
                isBuildingUp = true;
                buildupProgress += Time.deltaTime / buildupDuration;
                buildupProgress = Mathf.Clamp01(buildupProgress);

                float currentRate = Mathf.Lerp(startEmission, maxEmission, buildupProgress);
                float currentSize = Mathf.Lerp(startSizeMultiplier, maxSizeMultiplier, buildupProgress);
                SetFireEmissionRate(currentRate);
                SetFireSize(currentSize);

                if (buildupProgress >= 1f)
                {
                    isBuildingUp = false;
                    StartSkullFadeIn();
                }
            }
            else if (isBuildingUp)
            {
                isBuildingUp = false;
            }
        }

        if (skullAppearing)
        {
            skullFadeTimer += Time.deltaTime;
            float fadeProgress = Mathf.Clamp01(skullFadeTimer / skullFadeInDuration);

            SetSkullAlpha(fadeProgress);

            if (fadeProgress >= 1f)
            {
                skullAppearing = false;
                skullFullyVisible = true;
                SetSkullAlpha(1f);
            }
        }
    }

    void SetFireEmissionRate(float rate)
    {
        if (fireParticles == null) return;
        foreach (ParticleSystem ps in fireParticles)
        {
            var emission = ps.emission;
            if (emission.enabled)
                emission.rateOverTime = rate;
        }
    }

    void SetFireSize(float multiplier)
    {
        if (fireParticles == null || originalStartSizes == null) return;
        for (int i = 0; i < fireParticles.Length; i++)
        {
            var main = fireParticles[i].main;
            main.startSizeMultiplier = originalStartSizes[i] * multiplier;
        }
    }

    void StartSkullFadeIn()
    {
        skullAppearing = true;
        skullFadeTimer = 0f;

        if (skull != null)
        {
            skull.SetActive(true);

            // Show pentagram and rope when skull appears
            if (pentagram != null)
                pentagram.SetActive(true);
            if (rope != null)
                rope.SetActive(true);

            skullRenderers = skull.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in skullRenderers)
            {
                foreach (Material mat in r.materials)
                {
                    SetMaterialTransparent(mat);
                    Color c = mat.color;
                    c.a = 0f;
                    mat.color = c;
                }
            }
        }
    }

    void SetSkullAlpha(float alpha)
    {
        if (skullRenderers == null) return;

        foreach (Renderer r in skullRenderers)
        {
            foreach (Material mat in r.materials)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;

                if (alpha >= 1f)
                    SetMaterialOpaque(mat);
            }
        }
    }

    void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    void SetMaterialOpaque(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.renderQueue = 2000;
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    Texture2D CreateSoftCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha; // Softer falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    // Called by CabinetController when cabinet hits the table
    public void Flare()
    {
        isActivated = true;

        // Notify cross ritual that cabinet hit the table
        if (crossRitual != null)
            crossRitual.OnCabinetHit();

        if (candleFire != null)
        {
            candleFire.SetActive(true);

            // Vefects shader doesn't work on Quest GPU — replace with URP additive particle material
            Material fireMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            fireMat.SetTexture("_BaseMap", CreateSoftCircleTexture(64));
            fireMat.SetColor("_BaseColor", new Color(1f, 0.6f, 0.1f, 0.8f));
            // Additive blending for glowing fire
            fireMat.SetFloat("_Surface", 1f);
            fireMat.SetFloat("_Blend", 1f); // Additive
            fireMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            fireMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            fireMat.SetFloat("_ZWrite", 0f);
            fireMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fireMat.EnableKeyword("_BLENDMODE_ADD");
            fireMat.renderQueue = 3000;

            foreach (ParticleSystemRenderer psr in candleFire.GetComponentsInChildren<ParticleSystemRenderer>())
            {
                psr.material = fireMat;
                psr.enabled = true;
            }

            fireParticles = candleFire.GetComponentsInChildren<ParticleSystem>();

            // Cache original start sizes before we modify them
            originalStartSizes = new float[fireParticles.Length];
            for (int i = 0; i < fireParticles.Length; i++)
                originalStartSizes[i] = fireParticles[i].main.startSize.constant;

            SetFireEmissionRate(startEmission);
        }
    }
}

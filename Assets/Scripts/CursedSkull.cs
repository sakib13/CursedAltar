using UnityEngine;

public class CursedSkull : MonoBehaviour
{
    [Header("Table Reference")]
    public Transform table;
    public float placementDistance = 1.0f;

    [Header("Pentagram Candles")]
    public GameObject pentagram;
    public float candleLightDistance = 0.5f;

    [Header("Hanging Girl Swing")]
    public GameObject rope;
    public float swingSpeed = 1f;
    public float swingAngle = 5f;

    [Header("Audio (SoundManager names)")]
    public string takeMeWithYouSound = "takeMeWithYou";
    public string returnMeSound = "returnMe";
    public string leaveSound = "leave";

    [Header("Grab Settings")]
    public float grabDistance = 0.3f;

    [Header("Stare Contest")]
    public float stareRampDuration = 5f;
    public Color glowColor = new Color(0.8f, 0.05f, 0.02f);
    public float maxGlowIntensity = 8f;
    public float gazeAngleThreshold = 30f; // Degrees — look away beyond this resets the contest

    [Header("Blackout")]
    public float blackoutDuration = 5f;

    [Header("End Sequence")]
    public float buzzerLoopDuration = 10f;
    public ESP32Connection esp32;

    private enum State { WaitingForGrab, LightingCandles, ReturnToTable, StareContest, Blackout, End }
    private State currentState = State.WaitingForGrab;

    private float whisperTimer = 0f;
    private float whisperInterval = 5f;

    // Grab
    private bool isGrabbed = false;
    private Transform attachedHand;
    private OVRCameraRig ovrRig;

    // Pentagram candles — use particle system positions for distance (that's where the flame visually is)
    private ParticleSystem[] candleParticleSystems;
    private bool[] candleLit;
    private int totalCandles = 0;
    private int litCount = 0;

    // Grace timers — prevent instant triggering when entering a state
    private float stateTimer = 0f;
    private float lightingGrace = 2f;   // seconds before candle checks begin after grab
    private float returnGrace = 5f;     // seconds before table check begins after all candles lit

    // Hanging girl swing — hinge-based pendulum
    private float swingTimer = 0f;
    private GameObject ropeHinge;

    // Stare contest
    private float stareTimer = 0f;
    private Renderer[] skullRenderers;

    // Blackout
    private MeshRenderer blackoutRenderer;
    private float blackoutTimer = 0f;

    // End
    private float endTimer = 0f;
    private bool leaveWhisperPlayed = false;

    // Whisper audio
    private AudioSource whisperSource;

    void Awake()
    {
        // Disable ALL colliders and physics IMMEDIATELY on SetActive(true)
        // Awake runs before any physics step — prevents even 1 frame of collision
        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void Start()
    {
        // Dedicated whisper AudioSource
        whisperSource = gameObject.AddComponent<AudioSource>();
        whisperSource.spatialBlend = 0f;
        whisperSource.playOnAwake = false;

        // Find pentagram candle particle systems
        if (pentagram != null)
        {
            Transform[] allChildren = pentagram.GetComponentsInChildren<Transform>(true);
            var psList = new System.Collections.Generic.List<ParticleSystem>();

            foreach (Transform t in allChildren)
            {
                if (t.name.StartsWith("Candle"))
                {
                    ParticleSystem ps = t.GetComponentInChildren<ParticleSystem>(true);
                    if (ps != null)
                    {
                        psList.Add(ps);
                        ps.gameObject.SetActive(false);
                    }

                    // Destroy Point Light child — point lights near surfaces cause rendering glitches on Quest GPU
                    // Must destroy (not disable) because pentagram.SetActive(true) re-enables all children
                    Light pointLight = t.GetComponentInChildren<Light>(true);
                    if (pointLight != null)
                        Destroy(pointLight.gameObject);
                }
            }

            candleParticleSystems = psList.ToArray();
            totalCandles = candleParticleSystems.Length;
            candleLit = new bool[totalCandles];

            // Disable ALL colliders on pentagram — BoxColliders on candles interfere with Quest VR camera
            foreach (Collider col in pentagram.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            Debug.Log("[CursedSkull] Found " + totalCandles + " pentagram candles");
        }

        // Disable all colliders on rope/hanginggirl too
        if (rope != null)
        {
            foreach (Collider col in rope.GetComponentsInChildren<Collider>(true))
                Destroy(col);
        }

        // Create ceiling hinge for pendulum swing — same technique as DoorScare
        if (rope != null)
        {
            // Hinge at the rope's current position (ceiling attachment point)
            ropeHinge = new GameObject("RopeHinge");
            ropeHinge.transform.position = rope.transform.position;
            ropeHinge.transform.rotation = rope.transform.rotation;

            // Parent rope under the hinge so it swings from the ceiling
            rope.transform.SetParent(ropeHinge.transform);
        }

        skullRenderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        if (ovrRig == null)
            ovrRig = FindObjectOfType<OVRCameraRig>();

        UpdateHangingGirlSwing();

        switch (currentState)
        {
            case State.WaitingForGrab:
                UpdateWaitingForGrab();
                break;
            case State.LightingCandles:
                UpdateLightingCandles();
                break;
            case State.ReturnToTable:
                UpdateReturnToTable();
                break;
            case State.StareContest:
                UpdateStareContest();
                break;
            case State.Blackout:
                UpdateBlackout();
                break;
            case State.End:
                UpdateEnd();
                break;
        }

        if (isGrabbed && attachedHand != null)
        {
            transform.position = attachedHand.position;
            transform.rotation = attachedHand.rotation;
        }
    }

    // --- WaitingForGrab ---
    void UpdateWaitingForGrab()
    {
        whisperTimer += Time.deltaTime;
        if (whisperTimer >= whisperInterval)
        {
            whisperTimer = 0f;
            PlayWhisper(takeMeWithYouSound);
        }

        if (ovrRig == null) return;

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) &&
            IsHandNear(ovrRig.rightHandAnchor))
        {
            GrabSkull(ovrRig.rightHandAnchor);
            return;
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) &&
            IsHandNear(ovrRig.leftHandAnchor))
        {
            GrabSkull(ovrRig.leftHandAnchor);
        }
    }

    bool IsHandNear(Transform hand)
    {
        if (hand == null) return false;
        return Vector3.Distance(hand.position, transform.position) < grabDistance;
    }

    void GrabSkull(Transform hand)
    {
        isGrabbed = true;
        attachedHand = hand;
        whisperTimer = 0f;
        stateTimer = 0f;

        // Disable LODGroup to prevent LOD switching near camera causing frame spikes
        LODGroup lod = GetComponent<LODGroup>();
        if (lod != null) lod.enabled = false;

        Debug.Log("[CursedSkull] Grabbed! Candles to light: " + totalCandles);

        currentState = totalCandles > 0 ? State.LightingCandles : State.ReturnToTable;
    }

    // --- LightingCandles ---
    void UpdateLightingCandles()
    {
        if (candleParticleSystems == null) return;

        // Grace period — don't check candles for first 2 seconds after grab
        // This prevents instant lighting when skull/pentagram are close together
        stateTimer += Time.deltaTime;
        if (stateTimer < lightingGrace) return;

        for (int i = 0; i < totalCandles; i++)
        {
            if (candleLit[i]) continue;

            // Use the particle system's parent transform position (candle position)
            float dist = Vector3.Distance(transform.position, candleParticleSystems[i].transform.parent.position);
            if (dist < candleLightDistance)
            {
                candleLit[i] = true;
                litCount++;

                candleParticleSystems[i].gameObject.SetActive(true);
                candleParticleSystems[i].Play();

                Debug.Log("[CursedSkull] Lit " + litCount + "/" + totalCandles + " (dist=" + dist.ToString("F2") + ")");
            }
        }

        if (litCount >= totalCandles)
        {
            Debug.Log("[CursedSkull] All candles lit! -> ReturnToTable");
            currentState = State.ReturnToTable;
            whisperTimer = 0f;
            stateTimer = 0f;
        }
    }

    // --- ReturnToTable ---
    void UpdateReturnToTable()
    {
        stateTimer += Time.deltaTime;

        whisperTimer += Time.deltaTime;
        if (whisperTimer >= whisperInterval)
        {
            whisperTimer = 0f;
            PlayWhisper(returnMeSound);
        }

        // Grace period — don't check table for first 5 seconds
        // Gives player time to hear whisper and walk back
        if (stateTimer < returnGrace) return;

        if (table == null) return;

        // Use XZ distance only — table origin is at its base (Y=-0.826), not the surface
        Vector3 skullPos = transform.position;
        Vector3 tablePos = table.position;
        float xzDist = Vector2.Distance(
            new Vector2(skullPos.x, skullPos.z),
            new Vector2(tablePos.x, tablePos.z));

        if (xzDist < placementDistance)
        {
            Debug.Log("[CursedSkull] Near table (xzDist=" + xzDist.ToString("F2") + ") -> placing skull");
            PlaceSkullOnTable();
        }
    }

    void PlaceSkullOnTable()
    {
        isGrabbed = false;
        attachedHand = null;

        if (table != null)
        {
            // Table origin is at base (Y=-0.826). Skull's original Y was 0.2 (on the surface).
            // Place skull back at its original height above the table
            Vector3 pos = new Vector3(table.position.x, 0.2f, table.position.z);
            transform.position = pos;
        }

        Debug.Log("[CursedSkull] Placed on table -> StareContest");
        currentState = State.StareContest;
        stareTimer = 0f;
    }

    // --- StareContest ---
    void UpdateStareContest()
    {
        // Check if player is looking at the skull
        if (ovrRig != null)
        {
            Transform head = ovrRig.centerEyeAnchor;
            Vector3 dirToSkull = (transform.position - head.position).normalized;
            float angle = Vector3.Angle(head.forward, dirToSkull);

            if (angle > gazeAngleThreshold)
            {
                // Player looked away — reset the contest
                stareTimer = 0f;
                SetSkullGlow(0f);
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                return;
            }
        }

        stareTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(stareTimer / stareRampDuration);

        SetSkullGlow(progress);

        // Pulsing vibration — oscillates between low and high to feel more intense
        // Pulse speed increases with progress (slow throb → frantic buzz)
        float pulseSpeed = Mathf.Lerp(4f, 14f, progress);
        float pulse = (Mathf.Sin(stareTimer * pulseSpeed) + 1f) * 0.5f; // 0 to 1
        float lowAmp = Mathf.Lerp(0.3f, 0.6f, progress);
        float highAmp = 1f;
        float vibration = Mathf.Lerp(lowAmp, highAmp, pulse);
        OVRInput.SetControllerVibration(1f, vibration, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(1f, vibration, OVRInput.Controller.LTouch);

        if (progress >= 1f)
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);

            CreateBlackoutQuad();
            currentState = State.Blackout;
            blackoutTimer = 0f;
        }
    }

    void SetSkullGlow(float intensity)
    {
        if (skullRenderers == null) return;
        for (int i = 0; i < skullRenderers.Length; i++)
        {
            Material mat = skullRenderers[i].material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", glowColor * intensity * maxGlowIntensity);
        }
    }

    // --- Blackout ---
    void UpdateBlackout()
    {
        blackoutTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(blackoutTimer / blackoutDuration);

        if (blackoutRenderer != null)
            blackoutRenderer.material.SetColor("_BaseColor", new Color(0f, 0f, 0f, progress));

        if (progress >= 1f)
        {
            if (esp32 != null)
                esp32.SendBuzzerOn();

            currentState = State.End;
            endTimer = 0f;
            leaveWhisperPlayed = false;
        }
    }

    void CreateBlackoutQuad()
    {
        if (ovrRig == null) return;

        Transform head = ovrRig.centerEyeAnchor;

        GameObject quadGO = new GameObject("BlackoutQuad");
        quadGO.transform.SetParent(head, false);
        quadGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        quadGO.transform.localRotation = Quaternion.identity;
        quadGO.transform.localScale = new Vector3(2f, 2f, 1f);

        MeshFilter mf = quadGO.AddComponent<MeshFilter>();
        mf.mesh = CreateQuadMesh();

        MeshRenderer mr = quadGO.AddComponent<MeshRenderer>();
        Material fadeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        fadeMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
        fadeMat.SetFloat("_Surface", 1f);
        fadeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fadeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fadeMat.SetFloat("_ZWrite", 0f);
        fadeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fadeMat.renderQueue = 4000;
        mr.material = fadeMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        blackoutRenderer = mr;
    }

    Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        return mesh;
    }

    // --- End ---
    void UpdateEnd()
    {
        endTimer += Time.deltaTime;

        if (!leaveWhisperPlayed && endTimer >= 2f)
        {
            leaveWhisperPlayed = true;
            PlayWhisper(leaveSound);
        }

        if (endTimer >= buzzerLoopDuration && esp32 != null)
        {
            esp32.SendBuzzerOff();
            esp32 = null;
        }
    }

    // --- Hanging Girl Swing ---
    void UpdateHangingGirlSwing()
    {
        if (ropeHinge == null || rope == null || !rope.activeInHierarchy) return;

        swingTimer += Time.deltaTime * swingSpeed;
        float angle = Mathf.Sin(swingTimer) * swingAngle;

        // Rotate the hinge on Z-axis — rope and girl swing as pendulum from ceiling pivot
        ropeHinge.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // --- Audio ---
    void PlayWhisper(string soundName)
    {
        if (whisperSource != null && whisperSource.isPlaying) return;
        if (SoundManager.Instance == null) return;

        SoundManager.Sound s = System.Array.Find(SoundManager.Instance.sounds, x => x.name == soundName);
        if (s == null || s.clip == null) return;

        whisperSource.clip = s.clip;
        whisperSource.volume = s.volume;
        whisperSource.Play();
    }
}

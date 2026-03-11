using UnityEngine;
using UnityEngine.Video;

public class CursedSkull : MonoBehaviour
{
    [Header("Table Reference")]
    public Transform table;
    public float placementDistance = 1.0f;

    [Header("Pentagram Candles")]
    public GameObject pentagram;
    public Transform pentagramCenter; // Empty GameObject at exact visual center of pentagram
    public float candleLightDistance = 0.5f;

    [Header("Hanging Girl Swing")]
    public GameObject rope;
    public float swingSpeed = 1f;
    public float swingAngle = 5f;
    public float fastSwingSpeed = 4f;
    public float fastSwingAngle = 20f;

    [Header("Audio (SoundManager names)")]
    public string takeMeWithYouSound = "takeMeWithYou";
    public string returnMeSound = "returnMe";
    public string leaveSound = "leave";
    public string pianostingerSound = "pianostinger";
    public string rockingchairSound = "rockingchair";
    public string barndoorSound = "barndoor";

    [Header("Grab Settings")]
    public float grabDistance = 0.3f;

    [Header("Stare Contest")]
    public float stareRampDuration = 5f;
    public Color glowColor = new Color(0.8f, 0.05f, 0.02f);
    public float maxGlowIntensity = 8f;
    public float gazeAngleThreshold = 10f;
    public float stareMaxDistance = 1.5f;

    [Header("Blackout")]
    public float blackoutDuration = 5f;

    [Header("Door Reference")]
    public DoorScare door;
    public float doorApproachDistance = 1.5f;

    [Header("Video")]
    public VideoClip theRingVideo;

    [Header("End Sequence")]
    public float buzzerLoopDuration = 10f;
    public ESP32Connection esp32;

    [Header("Jump Scare References")]
    public LanternController lanternController;
    public RockingChairController rockingChair;

    private enum State { WaitingForGrab, LightingCandles, ReturnToTable, PreStare, StareContest, DarknessPhase, ChaosPhase, Blackout, VideoPlayback, End }
    private State currentState = State.WaitingForGrab;

    private float whisperTimer = 0f;
    private float whisperInterval = 5f;

    // Grab
    private bool isGrabbed = false;
    private Transform attachedHand;
    private OVRCameraRig ovrRig;

    // Pentagram candles
    private ParticleSystem[] candleParticleSystems;
    private bool[] candleLit;
    private int totalCandles = 0;
    private int litCount = 0;

    // Grace timers
    private float stateTimer = 0f;
    private float lightingGrace = 2f;
    private float returnGrace = 5f;

    // Hanging girl swing — hinge-based pendulum
    private float swingTimer = 0f;
    private GameObject ropeHinge;
    private Quaternion hingeBaseRotation;
    private float currentSwingSpeed;
    private float currentSwingAngle;

    // PreStare
    private float preStareTimer = 0f;
    private bool preStareRattleStarted = false;
    private bool preStareReady = false;

    // Stare contest
    private float stareTimer = 0f;
    private Renderer[] skullRenderers;
    private bool doorSlammedDuringStare = false;

    // Darkness phase
    private Vector3 savedRopeHingePos;
    private Quaternion savedRopeHingeRot;
    private float hingeToRopeOffset; // Y distance from hinge to rope origin
    private float darknessGirlGazeAngle = 60f;

    // Chaos phase
    private float chaosTimer = 0f;
    private float chaosDuration = 12f;
    private float savedLanternIntensity;

    // Blackout
    private MeshRenderer blackoutRenderer;
    private GameObject blackoutQuadGO;
    private float blackoutTimer = 0f;

    // Video
    private VideoPlayer videoPlayer;
    private bool videoStarted = false;
    private bool videoFinished = false;
    private RenderTexture videoRT;

    // End
    private float endTimer = 0f;
    private bool leaveWhisperPlayed = false;
    private bool buzzerStarted = false;

    // Whisper audio
    private AudioSource whisperSource;

    void Awake()
    {
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

                    Light pointLight = t.GetComponentInChildren<Light>(true);
                    if (pointLight != null)
                        Destroy(pointLight.gameObject);
                }
            }

            candleParticleSystems = psList.ToArray();
            totalCandles = candleParticleSystems.Length;
            candleLit = new bool[totalCandles];

            foreach (Collider col in pentagram.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            Debug.Log("[CursedSkull] Found " + totalCandles + " pentagram candles");
        }

        if (rope != null)
        {
            foreach (Collider col in rope.GetComponentsInChildren<Collider>(true))
                Destroy(col);
        }

        // Create ceiling hinge at the topmost point of the rope mesh
        if (rope != null)
        {
            Renderer[] ropeRenderers = rope.GetComponentsInChildren<Renderer>(true);
            float maxY = rope.transform.position.y;
            float minY = rope.transform.position.y;
            foreach (Renderer r in ropeRenderers)
            {
                if (r.bounds.max.y > maxY)
                    maxY = r.bounds.max.y;
                if (r.bounds.min.y < minY)
                    minY = r.bounds.min.y;
            }

            ropeHinge = new GameObject("RopeHinge");
            ropeHinge.transform.position = new Vector3(rope.transform.position.x, maxY, rope.transform.position.z);
            ropeHinge.transform.rotation = rope.transform.rotation;
            rope.transform.SetParent(ropeHinge.transform);
            hingeBaseRotation = ropeHinge.transform.rotation;

            // Save full height from hinge (top) to girl's feet (bottom) for repositioning later
            hingeToRopeOffset = maxY - minY;
            Debug.Log("[CursedSkull] Rope hinge at Y=" + maxY + " (feet minY=" + minY + ", fullHeight=" + hingeToRopeOffset + ")");
        }

        currentSwingSpeed = swingSpeed;
        currentSwingAngle = swingAngle;
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
            case State.PreStare:
                UpdatePreStare();
                break;
            case State.StareContest:
                UpdateStareContest();
                break;
            case State.DarknessPhase:
                UpdateDarknessPhase();
                break;
            case State.ChaosPhase:
                UpdateChaosPhase();
                break;
            case State.Blackout:
                UpdateBlackout();
                break;
            case State.VideoPlayback:
                UpdateVideoPlayback();
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

        LODGroup lod = GetComponent<LODGroup>();
        if (lod != null) lod.enabled = false;

        Debug.Log("[CursedSkull] Grabbed! Candles to light: " + totalCandles);

        currentState = totalCandles > 0 ? State.LightingCandles : State.ReturnToTable;
    }

    // --- LightingCandles ---
    void UpdateLightingCandles()
    {
        if (candleParticleSystems == null) return;

        stateTimer += Time.deltaTime;
        if (stateTimer < lightingGrace) return;

        for (int i = 0; i < totalCandles; i++)
        {
            if (candleLit[i]) continue;

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

        if (stateTimer < returnGrace) return;

        if (table == null) return;

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
            Vector3 pos = new Vector3(table.position.x, 0.2f, table.position.z);
            transform.position = pos;
        }

        Debug.Log("[CursedSkull] Placed on table -> PreStare (3s delay)");
        currentState = State.PreStare;
        preStareTimer = 0f;
        preStareRattleStarted = false;
        preStareReady = false;
    }

    // --- PreStare ---
    void UpdatePreStare()
    {
        preStareTimer += Time.deltaTime;

        if (!preStareRattleStarted && preStareTimer >= 3f)
        {
            preStareRattleStarted = true;
            currentSwingSpeed = fastSwingSpeed;
            currentSwingAngle = fastSwingAngle;

            if (door != null)
                door.StartRattle();

            Debug.Log("[CursedSkull] PreStare: girl sped up, door rattling");
        }

        if (!preStareReady && preStareTimer >= 5f)
        {
            preStareReady = true;
            Debug.Log("[CursedSkull] PreStare -> StareContest ready");
            currentState = State.StareContest;
            stareTimer = 0f;
            doorSlammedDuringStare = false;
        }
    }

    // --- StareContest ---
    void UpdateStareContest()
    {
        // Check door approach
        if (door != null && ovrRig != null)
        {
            float distToDoor = Vector3.Distance(ovrRig.centerEyeAnchor.position, door.transform.position);

            if (distToDoor < doorApproachDistance && door.IsRattling)
            {
                door.SlamFromRattle();
                doorSlammedDuringStare = true;
                Debug.Log("[CursedSkull] Player approached door -> slam!");
            }

            if (doorSlammedDuringStare && door.IsClosed)
            {
                Vector3 playerPos = ovrRig.centerEyeAnchor.position;
                Vector3 tablePos = table.position;
                float distToTable = Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.z),
                    new Vector2(tablePos.x, tablePos.z));
                if (distToTable < stareMaxDistance)
                {
                    door.StartRattle();
                    doorSlammedDuringStare = false;
                    Debug.Log("[CursedSkull] Player back at table -> door rattles again");
                }
            }
        }

        // Check gaze
        if (ovrRig != null)
        {
            Transform head = ovrRig.centerEyeAnchor;
            Vector3 dirToSkull = (transform.position - head.position).normalized;
            float angle = Vector3.Angle(head.forward, dirToSkull);
            float distToSkull = Vector3.Distance(head.position, transform.position);

            if (angle > gazeAngleThreshold || distToSkull > stareMaxDistance)
            {
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

        // Pulsing vibration
        float pulseSpeed = Mathf.Lerp(4f, 14f, progress);
        float pulse = (Mathf.Sin(stareTimer * pulseSpeed) + 1f) * 0.5f;
        float lowAmp = Mathf.Lerp(0.3f, 0.6f, progress);
        float highAmp = 1f;
        float vibration = Mathf.Lerp(lowAmp, highAmp, pulse);
        OVRInput.SetControllerVibration(1f, vibration, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(1f, vibration, OVRInput.Controller.LTouch);

        // At 80% → instant darkness jump scare
        if (progress >= 0.8f)
        {
            EnterDarknessPhase();
            return;
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

    // --- DarknessPhase: instant blackout, reposition girl, wait for gaze ---
    void EnterDarknessPhase()
    {
        currentState = State.DarknessPhase;

        // Stop all vibration
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);

        // Reset skull glow
        SetSkullGlow(0f);

        // Stop door rattle
        if (door != null)
            door.StopRattle();

        // Stop all sounds
        if (whisperSource != null && whisperSource.isPlaying)
            whisperSource.Stop();
        if (SoundManager.Instance != null)
            SoundManager.Instance.SetMusicVolume(0f);
        // Stop any other playing audio sources in the scene
        AudioSource[] allAudio = FindObjectsOfType<AudioSource>();
        foreach (AudioSource src in allAudio)
        {
            if (src.isPlaying)
                src.Stop();
        }

        // Turn off lantern — total darkness
        if (lanternController != null)
        {
            savedLanternIntensity = lanternController.normalIntensity;
            lanternController.ForceLightOff();
        }

        // Hide any visible text (e.g. "Hold your Breath" from CrossRitual)
        TMPro.TextMeshPro[] allTexts = FindObjectsOfType<TMPro.TextMeshPro>();
        foreach (TMPro.TextMeshPro tmp in allTexts)
            tmp.gameObject.SetActive(false);

        // Freeze hanging girl — no swing at all
        girlFrozen = true;
        currentSwingSpeed = 0f;
        currentSwingAngle = 0f;

        // Reposition hanging girl to pentagram center
        if (ropeHinge != null && pentagramCenter != null)
        {
            savedRopeHingePos = ropeHinge.transform.position;
            savedRopeHingeRot = ropeHinge.transform.rotation;

            Vector3 center = pentagramCenter.position;
            // Place hinge above pentagram so girl's feet are at floor level
            // hingeToRopeOffset = full height from hinge top to girl's feet
            float hingeY = center.y + hingeToRopeOffset;
            ropeHinge.transform.position = new Vector3(center.x, hingeY, center.z);
            ropeHinge.transform.rotation = Quaternion.identity; // Stand straight, no swing
            Debug.Log("[CursedSkull] Girl repositioned: hinge Y=" + hingeY + ", feet at Y=" + center.y);
        }

        Debug.Log("[CursedSkull] DarknessPhase: everything dark, girl at pentagram, waiting for gaze");
    }

    void UpdateDarknessPhase()
    {
        if (ovrRig == null || rope == null) return;

        // Check if player gazes at the hanging girl position
        Transform head = ovrRig.centerEyeAnchor;
        Vector3 girlPos = rope.transform.position;
        Vector3 dirToGirl = (girlPos - head.position).normalized;
        float angle = Vector3.Angle(head.forward, dirToGirl);

        if (angle < darknessGirlGazeAngle)
        {
            EnterChaosPhase();
        }
    }

    // --- ChaosPhase: everything erupts for 5 seconds ---
    void EnterChaosPhase()
    {
        currentState = State.ChaosPhase;
        chaosTimer = 0f;

        // Light up the room via lantern (brighter for scare impact)
        if (lanternController != null)
        {
            lanternController.normalIntensity = savedLanternIntensity * 2f;
            lanternController.ForceLightOn();
        }

        // Play pianostinger once
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(pianostingerSound);

        // Force rocking chair to rock immediately (handles its own rockingchair + girllaugh audio)
        if (rockingChair != null)
            rockingChair.ForceRock();

        // Activate door rattling with glow
        if (door != null)
            door.StartRattle();

        // Activate pentagram with all candle particle systems
        if (pentagram != null)
        {
            pentagram.SetActive(true);
            if (candleParticleSystems != null)
            {
                foreach (ParticleSystem ps in candleParticleSystems)
                {
                    if (ps != null)
                    {
                        ps.gameObject.SetActive(true);
                        ps.Play();
                    }
                }
            }
        }

        // Resume girl swinging fast
        girlFrozen = false;
        currentSwingSpeed = fastSwingSpeed;
        currentSwingAngle = fastSwingAngle;

        Debug.Log("[CursedSkull] ChaosPhase: room erupts for 5 seconds");
    }

    void UpdateChaosPhase()
    {
        chaosTimer += Time.deltaTime;

        if (chaosTimer >= chaosDuration)
        {
            // Stop all chaos audio (rocking chair, door rattle, etc.)
            AudioSource[] allAudio = FindObjectsOfType<AudioSource>();
            foreach (AudioSource src in allAudio)
            {
                if (src.isPlaying)
                    src.Stop();
            }

            // Stop door rattle
            if (door != null)
                door.StopRattle();

            // Stop girl swing
            currentSwingSpeed = 0f;
            currentSwingAngle = 0f;

            // Create blackout quad and fade to black
            CreateBlackoutQuad();
            currentState = State.Blackout;
            blackoutTimer = 0f;

            Debug.Log("[CursedSkull] ChaosPhase done -> Blackout");
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
            DestroySceneObjects();

            Debug.Log("[CursedSkull] Blackout complete -> VideoPlayback");
            currentState = State.VideoPlayback;
            videoStarted = false;
        }
    }

    void CreateBlackoutQuad()
    {
        if (ovrRig == null) return;

        Transform head = ovrRig.centerEyeAnchor;

        blackoutQuadGO = new GameObject("BlackoutQuad");
        blackoutQuadGO.transform.SetParent(head, false);
        blackoutQuadGO.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        blackoutQuadGO.transform.localRotation = Quaternion.identity;
        blackoutQuadGO.transform.localScale = new Vector3(2f, 2f, 1f);

        MeshFilter mf = blackoutQuadGO.AddComponent<MeshFilter>();
        mf.mesh = CreateQuadMesh();

        MeshRenderer mr = blackoutQuadGO.AddComponent<MeshRenderer>();
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
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
        return mesh;
    }

    // --- VideoPlayback ---
    void UpdateVideoPlayback()
    {
        if (!videoStarted)
        {
            videoStarted = true;
            videoFinished = false;
            StartVideo();
        }

        if (videoFinished)
        {
            Debug.Log("[CursedSkull] Video finished -> End");

            if (videoPlayer != null)
                Destroy(videoPlayer.gameObject);

            if (videoRT != null)
            {
                videoRT.Release();
                videoRT = null;
            }

            currentState = State.End;
            endTimer = 0f;
            leaveWhisperPlayed = false;
            buzzerStarted = false;
        }
    }

    void StartVideo()
    {
        if (theRingVideo == null || blackoutQuadGO == null)
        {
            Debug.LogWarning("[CursedSkull] No video clip or blackout quad — skipping video");
            currentState = State.End;
            endTimer = 0f;
            leaveWhisperPlayed = false;
            buzzerStarted = false;

            if (esp32 != null)
                esp32.SendBuzzerOn();

            return;
        }

        // Keep blackout quad as solid black background
        Material bgMat = blackoutRenderer.material;
        bgMat.SetColor("_BaseColor", Color.black);
        bgMat.SetFloat("_Surface", 0f);
        bgMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        bgMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        bgMat.SetFloat("_ZWrite", 0f);
        bgMat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        bgMat.renderQueue = 4000;

        // Create a separate video quad in front of the blackout
        GameObject videoQuadGO = new GameObject("VideoQuad");
        videoQuadGO.transform.SetParent(blackoutQuadGO.transform.parent, false);
        videoQuadGO.transform.localPosition = new Vector3(0f, 0f, 2.0f);
        videoQuadGO.transform.localRotation = Quaternion.identity;
        videoQuadGO.transform.localScale = new Vector3(1.6f, 0.9f, 1f);

        MeshFilter vMF = videoQuadGO.AddComponent<MeshFilter>();
        vMF.mesh = CreateQuadMesh();

        // Create RenderTexture for video
        videoRT = new RenderTexture(1280, 720, 0);
        videoRT.Create();

        // Video material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", videoRT);
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.renderQueue = 4001;

        MeshRenderer vMR = videoQuadGO.AddComponent<MeshRenderer>();
        vMR.material = mat;
        vMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        videoPlayer = videoQuadGO.AddComponent<VideoPlayer>();
        videoPlayer.clip = theRingVideo;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRT;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        videoPlayer.isLooping = false;
        videoPlayer.playOnAwake = false;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();

        Debug.Log("[CursedSkull] Playing The Ring video");
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        videoFinished = true;
        Debug.Log("[CursedSkull] Video loopPointReached");
    }

    // --- End ---
    void UpdateEnd()
    {
        endTimer += Time.deltaTime;

        if (!leaveWhisperPlayed && endTimer >= 1f)
        {
            leaveWhisperPlayed = true;
            PlayWhisper(leaveSound);
        }

        if (!buzzerStarted && endTimer >= 2f)
        {
            buzzerStarted = true;
            if (esp32 != null)
                esp32.SendBuzzerOn();
        }

        if (endTimer >= buzzerLoopDuration && esp32 != null)
        {
            esp32.SendBuzzerOff();
            esp32 = null;
        }
    }

    // --- Hanging Girl Swing ---
    private bool girlFrozen = false;

    void UpdateHangingGirlSwing()
    {
        if (ropeHinge == null || rope == null || !rope.activeInHierarchy) return;

        // Girl is frozen (standing still) — don't touch rotation at all
        if (girlFrozen) return;

        swingTimer += Time.deltaTime * currentSwingSpeed;
        float angle = Mathf.Sin(swingTimer) * currentSwingAngle;

        ropeHinge.transform.rotation = hingeBaseRotation * Quaternion.Euler(angle, 0f, 0f);
    }

    // --- Cleanup ---
    void DestroySceneObjects()
    {
        GameObject[] allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in allRoots)
        {
            if (root.GetComponentInChildren<OVRCameraRig>() != null)
                continue;

            if (root.GetComponentInChildren<SoundManager>() != null)
                continue;

            if (root == transform.root.gameObject)
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                    r.enabled = false;
                continue;
            }

            Destroy(root);
        }

        if (door != null)
            door.StopRattle();

        door = null;
        rope = null;
        ropeHinge = null;
        pentagram = null;

        Debug.Log("[CursedSkull] Destroyed all scene objects for end sequence");
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

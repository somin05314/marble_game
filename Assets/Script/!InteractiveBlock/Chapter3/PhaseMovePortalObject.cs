using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class PhaseMovePortalObject : MonoBehaviour, IPoResettable
{
    [Header("Sensor")]
    [SerializeField] LaserSensor2D sensor;

    [Header("Move")]
    [SerializeField] float moveDistance = 10f;
    [SerializeField] float moveDuration = 0.5f;

    [Header("Layer")]
    [SerializeField] string normalLayerName = "Marble";
    [SerializeField] string phaseLayerName = "PhaseMarble";

    [Header("Laser Visual")]
    [SerializeField] GameObject[] connectedVisuals;
    [SerializeField] GameObject[] disconnectedVisuals;

    [Header("Powered Visual")]
    [SerializeField] GameObject poweredVisual;

    [Header("Moving Visual")]
    [SerializeField] GameObject movingVisual;

    [Header("Flow Particle")]
    [SerializeField] ParticleSystem flowParticle;
    [SerializeField] float particleFlowSpeed = 3f;

    [Header("Exit Force")]
    [SerializeField] float exitHorizontalSpeed = 8f;

    [Header("Exit Point")]
    [SerializeField] Transform exitPoint;

    [Header("Phase Visual")]
    [SerializeField] Color phaseColor = new Color(0.2f, 1f, 0.45f, 0.55f);

    [Header("Audio")]
    [SerializeField] SciFiAudioPlayer audioPlayer;

    HashSet<Rigidbody2D> movingBalls = new HashSet<Rigidbody2D>();

    Dictionary<Rigidbody2D, BallPhaseState> phaseStates = new Dictionary<Rigidbody2D, BallPhaseState>();

    class BallPhaseState
    {
        public GameObject ball;
        public SpriteRenderer[] renderers;
        public Color[] colors;
    }

    int normalLayer;
    int phaseLayer;
    bool isPowered;

    void Awake()
    {
        normalLayer = LayerMask.NameToLayer(normalLayerName);
        phaseLayer = LayerMask.NameToLayer(phaseLayerName);

        if (poweredVisual != null)
        {
            poweredVisual.SetActive(false);

            if (flowParticle == null)
                flowParticle = poweredVisual.GetComponentInChildren<ParticleSystem>(true);
        }

        if (movingVisual != null)
            movingVisual.SetActive(false);

        RefreshPower();
        RefreshVisual();
    }

    void Update()
    {
        RefreshPower();
        RefreshVisual();
    }

    public void HandleTriggerEnter(Collider2D other)
    {
        if (!isPowered) return;

        Rigidbody2D ballRb = other.attachedRigidbody;
        if (ballRb == null) return;

        if (movingBalls.Contains(ballRb)) return;

        if (!other.CompareTag("Marble") && ballRb.gameObject.layer != normalLayer)
            return;

        StartCoroutine(PhaseMoveRoutine(ballRb));
    }

    IEnumerator PhaseMoveRoutine(Rigidbody2D ballRb)
    {
        if (ballRb == null) yield break;

        movingBalls.Add(ballRb);

        audioPlayer?.PlayWarp();

        if (movingVisual != null)
            movingVisual.SetActive(true);
        GameObject ball = ballRb.gameObject;

        SpriteRenderer[] renderers = ball.GetComponentsInChildren<SpriteRenderer>();
        Color[] originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].color;
            renderers[i].color = phaseColor;
        }

        phaseStates[ballRb] = new BallPhaseState
        {
            ball = ball,
            renderers = renderers,
            colors = originalColors
        };

        SetLayerRecursive(ball, phaseLayer);

        Vector2 startPos = ballRb.position;
        float flipSign = transform.lossyScale.x < 0f ? -1f : 1f;

        Vector2 targetPos = exitPoint != null
            ? (Vector2)exitPoint.position
            : startPos + Vector2.right * flipSign * moveDistance;

        ballRb.velocity = Vector2.zero;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            if (ballRb == null || ball == null)
                break;

            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            float easedT = t * t * (3f - 2f * t);

            Vector2 nextPos = Vector2.Lerp(startPos, targetPos, easedT);
            ballRb.MovePosition(nextPos);

            if (t >= 1f)
            {
                ballRb.position = targetPos;

                SetLayerRecursive(ball, normalLayer);
                ballRb.velocity = new Vector2(exitHorizontalSpeed * flipSign, 0f);

                break;
            }

            yield return new WaitForFixedUpdate();
        }

        RestoreBall(ballRb);

        if (movingVisual != null)
            movingVisual.SetActive(false);

        if (ballRb != null)
            movingBalls.Remove(ballRb);
    }

    void RestoreBall(Rigidbody2D ballRb)
    {
        if (ballRb == null) return;
        if (!phaseStates.TryGetValue(ballRb, out BallPhaseState state)) return;

        RestoreColors(state.renderers, state.colors);

        if (state.ball != null)
            SetLayerRecursive(state.ball, normalLayer);

        phaseStates.Remove(ballRb);
    }

    void RefreshPower()
    {
        isPowered = sensor != null && sensor.IsReceivingLaser;
    }

    void RefreshVisual()
    {
        SetVisualsActive(connectedVisuals, isPowered);
        SetVisualsActive(disconnectedVisuals, !isPowered);

        SetPoweredVisual(isPowered);
    }

    void SetPoweredVisual(bool powered)
    {
        if (poweredVisual != null)
            poweredVisual.SetActive(powered);

        RefreshParticleDirection();

        if (flowParticle == null) return;

        if (powered)
        {
            if (!flowParticle.isPlaying)
                flowParticle.Play(true);
        }
        else
        {
            flowParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void RefreshParticleDirection()
    {
        if (flowParticle == null) return;

        float flipSign = transform.lossyScale.x < 0f ? -1f : 1f;

        var velocity = flowParticle.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(particleFlowSpeed * flipSign);
    }

    void RestoreColors(SpriteRenderer[] renderers, Color[] colors)
    {
        if (renderers == null || colors == null) return;

        int count = Mathf.Min(renderers.Length, colors.Length);

        for (int i = 0; i < count; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = colors[i];
        }
    }

    void SetVisualsActive(GameObject[] visuals, bool active)
    {
        if (visuals == null) return;

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null)
                visuals[i].SetActive(active);
        }
    }

    void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    public void ResetState()
    {
        if (sensor != null)
            sensor.ResetState();

        if (poweredVisual != null)
            poweredVisual.SetActive(false);

        if (movingVisual != null)
            movingVisual.SetActive(false);

        if (flowParticle != null)
            flowParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        RestoreAllMovingBalls();

        movingBalls.Clear();
        phaseStates.Clear();
        StopAllCoroutines();

        RefreshPower();
        RefreshVisual();
    }

    void RestoreAllMovingBalls()
    {
        foreach (var pair in phaseStates)
        {
            BallPhaseState state = pair.Value;
            if (state == null) continue;

            RestoreColors(state.renderers, state.colors);

            if (state.ball != null)
                SetLayerRecursive(state.ball, normalLayer);
        }
    }

    void OnDisable()
    {
        RestoreAllMovingBalls();

        movingBalls.Clear();
        phaseStates.Clear();
        StopAllCoroutines();

        if (movingVisual != null)
            movingVisual.SetActive(false);

        if (flowParticle != null)
            flowParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
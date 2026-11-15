// 실내 사격장 진행
//   왼손 검지 트리거 (키보드 Space) : 표적 보내기(10 m) / 돌아오게 하기 / 새 표적지로 교체
//   오른손 검지 트리거 (키보드 F)   : 발사
//   왼손 그립 길게 (키보드 F9)      : 두더지 잡기 씬으로 (SceneSwitcher)
//
// 흐름: 대기(표적이 가까이) → [왼손 트리거] 표적이 레일을 따라 10 m로 이동 → 사격 (기본 10발)
//       → 다 쏘면 자동으로(또는 [왼손 트리거]) 표적이 돌아옴 → 탄흔 확인 → [왼손 트리거] 새 표적지로 다시 보내기
// 레인 오른쪽 모니터: 표적지 그림 위에 방금 맞은 위치(빨강)와 이전 탄착(회색), 점수·거리·남은 탄 표시
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ShootingRangeManager : MonoBehaviour
{
    public enum State { Parked, Sending, Ready, Returning, Review }

    [Header("표적")]
    public RangeTarget target;
    [Tooltip("레일을 따라 움직이는 표적 운반대 (Z축 이동)")]
    public Transform carrier;
    [Tooltip("대기 위치 (사수로부터 m)")]
    public float nearDistance = 1.8f;
    [Tooltip("사격 거리 (m)")]
    public float farDistance = 10f;
    public float carrierSpeed = 2.4f;

    [Header("총")]
    public RangePistol pistol;
    public int roundsPerSession = 10;
    public float maxRange = 60f;

    [Header("진동 (컨트롤러 + ArduinoHaptics로 아두이노에도 같은 값 전송)")]
    [Tooltip("발사 반동 진동 세기 (0~1) → VIB 255 70")]
    [Range(0f, 1f)] public float fireVibration = 1f;
    public float fireVibrationSeconds = 0.07f;
    [Tooltip("빈 격발(딸깍) 진동 세기 → VIB 26 30")]
    [Range(0f, 1f)] public float dryFireVibration = 0.1f;
    public float dryFireVibrationSeconds = 0.03f;

    [Header("모니터")]
    [Tooltip("모니터 속 표적지 그림 (Quad, 표적지와 같은 비율)")]
    public Transform monitorTarget;
    public Material markerLatest;
    public Material markerOld;
    public TextMesh monitorText;
    public TextMesh monitorScore;

    public State Current { get; private set; } = State.Parked;

    int roundsLeft, total, shots;
    int lastScore = -1;
    float autoReturnAt = -1f;
    bool prevLeft, prevRight;
    readonly List<GameObject> markers = new List<GameObject>();

    void Start()
    {
        roundsLeft = roundsPerSession;
        SetCarrier(nearDistance);
        UpdateMonitor();
    }

    void Update()
    {
        bool left = RightHandFollower.GetLeftButton(UnityEngine.XR.CommonUsages.triggerButton);
        bool right = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.triggerButton);
        bool keyLeft = false, keyFire = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) { keyLeft = Keyboard.current.spaceKey.wasPressedThisFrame; keyFire = Keyboard.current.fKey.wasPressedThisFrame; }
#endif
        if ((left && !prevLeft) || keyLeft) OnLeftTrigger();
        if ((right && !prevRight) || keyFire) Fire();
        prevLeft = left; prevRight = right;

        MoveCarrier();
        if (autoReturnAt > 0f && Time.time >= autoReturnAt) { autoReturnAt = -1f; if (Current == State.Ready) Current = State.Returning; }
        UpdateMonitor();
    }

    // ---------- 왼손 트리거 ----------
    public void OnLeftTrigger()
    {
        switch (Current)
        {
            case State.Parked:
                StartSession();
                break;
            case State.Ready:
                Current = State.Returning; // 쏘다가 언제든 표적을 불러올 수 있음
                break;
            case State.Review:
                // 새 표적지로 교체 후 다시 보내기
                target.ClearHoles();
                ClearMarkers();
                StartSession();
                break;
        }
    }

    void StartSession()
    {
        roundsLeft = roundsPerSession;
        total = 0; shots = 0; lastScore = -1;
        Current = State.Sending;
        RightHandFollower.Vibrate(0.3f, 0.05f);
    }

    // ---------- 표적 이동 ----------
    float CarrierDistance => carrier.localPosition.z;

    void SetCarrier(float z)
    {
        Vector3 p = carrier.localPosition; p.z = z; carrier.localPosition = p;
    }

    void MoveCarrier()
    {
        float goal = Current == State.Sending ? farDistance : (Current == State.Returning ? nearDistance : CarrierDistance);
        if (Current != State.Sending && Current != State.Returning) return;
        float z = CarrierDistance;
        // 출발·도착 때 부드럽게 (모터 가감속 느낌)
        float remain = Mathf.Abs(goal - z), done = Mathf.Abs(z - (Current == State.Sending ? nearDistance : farDistance));
        float speed = carrierSpeed * Mathf.Clamp(Mathf.Min(remain, done + 0.15f) / 0.8f, 0.15f, 1f);
        z = Mathf.MoveTowards(z, goal, speed * Time.deltaTime);
        SetCarrier(z);
        if (Mathf.Approximately(z, goal))
        {
            Current = Current == State.Sending ? State.Ready : State.Review;
            target.Flutter(0.5f); // 멈출 때 살짝 흔들림
            RightHandFollower.Vibrate(0.2f, 0.04f);
        }
    }

    // ---------- 발사 ----------
    public void Fire()
    {
        if (pistol == null) return;
        if (Current != State.Ready || roundsLeft <= 0)
        {
            pistol.PlayDryFire();               // 사격 준비가 안 됐을 때는 '딸깍'
            RightHandFollower.Vibrate(dryFireVibration, dryFireVibrationSeconds);
            return;
        }
        roundsLeft--; shots++;
        pistol.PlayFire();
        RightHandFollower.Vibrate(fireVibration, fireVibrationSeconds); // 발사 반동 → 아두이노 VIB 255 70

        Transform m = pistol.muzzle != null ? pistol.muzzle : pistol.transform;
        int score = 0;
        if (Physics.Raycast(m.position, m.forward, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Collide)
            && hit.collider.GetComponentInParent<RangeTarget>() == target && target.IsOnPaper(hit.point))
        {
            Vector2 tc = target.ToTargetCoords(hit.point);
            score = target.Score(tc);
            target.AddHole(hit.point);
            target.Flutter(1f);
            AddMarker(hit.point);
        }
        lastScore = score;
        total += score;
        if (roundsLeft <= 0) autoReturnAt = Time.time + 1.5f; // 다 쏘면 잠시 후 표적이 돌아옴
    }

    // ---------- 모니터 ----------
    void AddMarker(Vector3 world)
    {
        if (monitorTarget == null) return;
        foreach (var old in markers) if (old) old.GetComponent<Renderer>().sharedMaterial = markerOld;
        Vector3 l = target.paper.InverseTransformPoint(world); // -0.5~0.5 (표적지와 모니터 그림이 같은 비율)
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        g.name = "Marker";
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(monitorTarget, false);
        g.transform.localPosition = new Vector3(l.x, l.y, -0.01f);
        float s = 0.045f;
        g.transform.localScale = new Vector3(s * target.paperSize.y / target.paperSize.x, s, 1f);
        g.GetComponent<Renderer>().sharedMaterial = markerLatest;
        markers.Add(g);
    }

    void ClearMarkers()
    {
        foreach (var g in markers) if (g) Destroy(g);
        markers.Clear();
    }

    void UpdateMonitor()
    {
        if (monitorText != null)
        {
            string state = Current switch
            {
                State.Parked => "LEFT TRIGGER\nTO SEND TARGET",
                State.Sending => "TARGET MOVING...",
                State.Ready => roundsLeft > 0 ? "FIRE (RIGHT TRIGGER)" : "SESSION COMPLETE",
                State.Returning => "TARGET RETURNING...",
                _ => "LEFT TRIGGER\nFOR NEW TARGET",
            };
            float avg = shots > 0 ? (float)total / shots : 0f;
            monitorText.text =
                $"LANE 3\n" +
                $"DIST  {CarrierDistance:0.0} m\n" +
                $"ROUNDS {roundsLeft}/{roundsPerSession}\n" +
                $"TOTAL {total}\n" +
                $"AVG   {avg:0.0}\n\n" +
                state;
        }
        if (monitorScore != null)
        {
            monitorScore.text = lastScore < 0 ? "-" : (lastScore == 0 ? "MISS" : lastScore.ToString());
            monitorScore.color = lastScore >= 9 ? new Color(1f, 0.85f, 0.2f) : lastScore == 0 ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }
    }
}

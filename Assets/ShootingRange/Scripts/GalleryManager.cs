// 오락실 사격 게임 진행
//   5 m 앞에 원형 표적지 3×3 (큰 표적 10점 · 중간 20점 · 작은 표적 30점)
//   오른손 A (키보드 Space) : 시작 → 3·2·1 → 30초 동안 맞힌 점수 합계
//   발사: 오른손 검지 트리거 / 키보드 F / 하우징 버튼(아두이노 FIRE)
//   실제 게임: 표적이 누워 있다가 무작위로 튀어 오르고 3~5초 뒤 내려감 (맞히면 바로 쓰러짐)
//   연습(게임 전·후): 표적이 모두 서 있고, 맞은 표적은 5초 뒤 다시 일어남
//   게임 중이 아닐 때도 연습 사격 가능 (점수는 안 올라감)
//   총구에서 레이저가 나가 탄이 맞을 곳을 보여 줌 (LaserSight)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GalleryManager : MonoBehaviour
{
    public enum State { Idle, Countdown, Playing, Result }

    [Header("표적·총")]
    public PopTarget[] targets;
    public RangePistol pistol;
    public float maxRange = 40f;
    [Tooltip("연사 간격 (s)")]
    public float fireCooldown = 0.12f;

    [Header("게임")]
    public float roundSeconds = 30f;
    [Tooltip("연습 모드: 맞은 표적지가 다시 일어나기까지 (s)")]
    public float respawnSeconds = 5f;
    [Header("실제 게임: 표적이 무작위로 튀어 오름")]
    [Tooltip("올라와 있는 시간 (s, 최소~최대 사이 무작위). 지나면 맞히지 못한 채 내려감")]
    public Vector2 upSeconds = new Vector2(3f, 5f);
    [Tooltip("내려간 뒤 다시 올라오기까지 (s, 최소~최대 사이 무작위)")]
    public Vector2 downSeconds = new Vector2(1.5f, 4f);
    [Tooltip("게임 시작 후 첫 표적들이 올라오는 시간 차 (s)")]
    public float firstPopSpread = 2.5f;
    [Tooltip("동시에 올라와 있을 수 있는 최대 표적 수")]
    public int maxUpAtOnce = 4;
    public float countdownStep = 0.8f;
    [Tooltip("결과를 보여 준 뒤 연습 모드로 돌아가기까지 (s)")]
    public float resultSeconds = 4f;

    [Header("진동 (컨트롤러 + 아두이노 VIB)")]
    [Range(0f, 1f)] public float fireVibration = 1f;
    public float fireVibrationSeconds = 0.07f;

    [Header("표시")]
    [Tooltip("표적 위 큰 점수판: 점수")]
    public TextMesh boardScore;
    [Tooltip("표적 위 점수판: 남은 시간 / 안내")]
    public TextMesh boardInfo;
    [Tooltip("3·2·1·GO! 큰 글자")]
    public TextMesh countdownText;
    [Tooltip("맞힌 표적 위에 떠오르는 +점수 (꺼 둔 견본)")]
    public TextMesh popupTemplate;
    [Tooltip("시야 HUD: 점수")]
    public TextMesh hudMain;
    [Tooltip("시야 HUD: 남은 시간")]
    public TextMesh hudSub;
    public Transform hudPanel;
    public Vector2 hudPanelPadding = new Vector2(0.018f, 0.012f);

    public State Current { get; private set; } = State.Idle;
    public int Score => score;

    int score, hits, shots, best;
    float timeLeft, lastFire = -10f, resultUntil, hudPop = -10f;
    bool prevA, prevTrig;
    ImuInput imu;
    Coroutine countdown;
    Vector3 hudMainScale = Vector3.one;
    readonly List<(TextMesh t, float start, Vector3 from)> popups = new List<(TextMesh, float, Vector3)>();

    void OnEnable()
    {
        imu = ImuInput.Instance != null ? ImuInput.Instance : FindAnyObjectByType<ImuInput>();
        if (imu != null) imu.Fire += Fire;
    }

    void OnDisable() { if (imu != null) imu.Fire -= Fire; }

    void Start()
    {
        if (hudMain != null) hudMainScale = hudMain.transform.localScale;
        if (popupTemplate != null) popupTemplate.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        foreach (var t in targets) if (t != null) t.SetUp(true, true);
        UpdateDisplays();
    }

    void Update()
    {
        bool a = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.primaryButton);
        bool trig = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.triggerButton);
        bool keyStart = false, keyFire = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) { keyStart = Keyboard.current.spaceKey.wasPressedThisFrame; keyFire = Keyboard.current.fKey.wasPressedThisFrame; }
#endif
        if ((a && !prevA) || keyStart) ToggleRound();
        if ((trig && !prevTrig) || keyFire) Fire();
        prevA = a; prevTrig = trig;

        if (Current == State.Playing)
        {
            PopTargets();
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f) EndRound();
        }
        else if (Current == State.Result && Time.time >= resultUntil)
        {
            Current = State.Idle;
            foreach (var t in targets) if (t != null) t.SetUp(true);   // 연습 모드: 다시 일어남
        }
        UpdatePopups();
        UpdateDisplays();
    }

    public void ToggleRound()
    {
        if (Current == State.Playing || Current == State.Countdown) { EndRound(); return; }
        if (countdown != null) StopCoroutine(countdown);
        countdown = StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        Current = State.Countdown;
        if (imu != null) imu.Recenter();   // 시작할 때 하우징이 향한 방향 = 정면 (표적 가운데를 겨누고 A)
        foreach (var t in targets) if (t != null) t.SetUp(false);
        string[] steps = { "3", "2", "1", "GO!" };
        Color[] cols = { new Color(1f, 0.3f, 0.25f), new Color(1f, 0.75f, 0.15f), new Color(0.35f, 0.9f, 0.35f), new Color(1f, 0.92f, 0.2f) };
        for (int i = 0; i < steps.Length; i++)
        {
            if (countdownText != null)
            {
                countdownText.gameObject.SetActive(true);
                countdownText.text = steps[i];
                countdownText.color = cols[i];
            }
            RightHandFollower.Vibrate(i == 3 ? 0.6f : 0.25f, 0.06f);
            if (i == 3)
            {
                score = 0; hits = 0; shots = 0;
                timeLeft = roundSeconds;
                Current = State.Playing;
                ScheduleFirstPops();
            }
            yield return new WaitForSeconds(countdownStep);
        }
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        countdown = null;
    }

    void EndRound()
    {
        if (countdown != null) { StopCoroutine(countdown); countdown = null; }
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        bool finished = Current == State.Playing;
        Current = finished ? State.Result : State.Idle;
        if (finished)
        {
            best = Mathf.Max(best, score);
            resultUntil = Time.time + resultSeconds;
            foreach (var t in targets) if (t != null) t.Knock(0f);   // 끝: 전부 쓰러짐
            RightHandFollower.Vibrate(0.5f, 0.2f);
            Debug.Log($"[사격] 끝 – 점수 {score}, 맞힘 {hits}/{shots}");
        }
        else foreach (var t in targets) if (t != null) t.SetUp(true);
    }

    // ── 실제 게임: 표적마다 '누워 있다가 튀어 오름 → 3~5초 뒤 내려감'을 반복 ──
    float[] nextChange;

    void ScheduleFirstPops()
    {
        nextChange = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) targets[i].SetUp(false, true);
            nextChange[i] = Time.time + Random.Range(0f, firstPopSpread);
        }
    }

    void PopTargets()
    {
        if (nextChange == null || nextChange.Length != targets.Length) ScheduleFirstPops();
        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            if (t == null || Time.time < nextChange[i]) continue;
            if (t.IsDown)
            {
                int up = 0;
                foreach (var o in targets) if (o != null && !o.IsDown) up++;
                if (up >= maxUpAtOnce) { nextChange[i] = Time.time + Random.Range(0.3f, 0.8f); continue; }   // 너무 많으면 잠시 뒤에
                t.SetUp(true);                             // 튀어 오름
                nextChange[i] = Time.time + Random.Range(upSeconds.x, upSeconds.y);
            }
            else
            {
                t.SetUp(false);                            // 시간 초과 – 못 맞히고 내려감
                nextChange[i] = Time.time + Random.Range(downSeconds.x, downSeconds.y);
            }
        }
    }

    public void Fire()
    {
        if (pistol == null || Time.time - lastFire < fireCooldown) return;
        if (Current == State.Countdown || Current == State.Result) return;
        lastFire = Time.time;
        pistol.PlayFire();
        RightHandFollower.Vibrate(fireVibration, fireVibrationSeconds);   // 발사 반동 → 아두이노 VIB
        if (Current == State.Playing) shots++;

        Transform m = pistol.muzzle != null ? pistol.muzzle : pistol.transform;
        if (!Physics.Raycast(m.position, m.forward, out RaycastHit hit, maxRange, ~0, QueryTriggerInteraction.Ignore)) return;
        PopTarget t = hit.collider.GetComponentInParent<PopTarget>();
        if (t == null || !t.IsUp) return;
        if (Current == State.Playing)
        {
            t.Knock(0f);                                   // 실제 게임: 쓰러진 뒤 무작위 시간 후 다시 튀어 오름
            int i = System.Array.IndexOf(targets, t);
            if (i >= 0) nextChange[i] = Time.time + Random.Range(downSeconds.x, downSeconds.y);
        }
        else t.Knock(respawnSeconds);                      // 연습: 5초 뒤 다시 일어남
        if (Current == State.Playing)
        {
            score += t.points;
            hits++;
            hudPop = Time.time;
        }
        Popup(hit.point, Current == State.Playing ? $"+{t.points}" : $"{t.points}", t.points);
    }

    void Popup(Vector3 at, string text, int points)
    {
        if (popupTemplate == null) return;
        var p = Instantiate(popupTemplate, at + Vector3.up * 0.12f - Vector3.forward * 0.1f, popupTemplate.transform.rotation);
        p.gameObject.SetActive(true);
        p.text = text;
        p.color = points >= 30 ? new Color(1f, 0.85f, 0.15f) : points >= 20 ? new Color(0.45f, 0.85f, 1f) : Color.white;
        popups.Add((p, Time.time, p.transform.position));
    }

    void UpdatePopups()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var (t, start, from) = popups[i];
            float k = (Time.time - start) / 0.9f;
            if (t == null || k >= 1f) { if (t != null) Destroy(t.gameObject); popups.RemoveAt(i); continue; }
            t.transform.position = from + Vector3.up * (0.35f * k);
            t.transform.localScale = popupTemplate.transform.localScale * (1f + Mathf.Sin(Mathf.Min(k * 4f, 1f) * Mathf.PI) * 0.3f);
            Color c = t.color; c.a = 1f - k * k; t.color = c;
        }
    }

    void UpdateDisplays()
    {
        string info, main, sub;
        int tl = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
        switch (Current)
        {
            case State.Countdown:
                info = "GET READY!"; main = "GET READY!"; sub = "";
                break;
            case State.Playing:
                info = $"TIME  {tl}"; main = $"SCORE  {score}"; sub = $"TIME  {tl}";
                break;
            case State.Result:
                info = $"TIME UP!     BEST {best}"; main = $"SCORE  {score}"; sub = "TIME UP!";
                break;
            default:
                info = best > 0 ? $"BEST {best}     PRESS [A] TO START" : "PRESS [A] TO START";
                main = $"SCORE  {score}"; sub = "[A] START";
                break;
        }
        // 점수판: 내 점수를 크게 (설명은 아래 한 줄만)
        if (boardScore != null) boardScore.text = $"SCORE {score}";
        if (boardInfo != null) boardInfo.text = info;
        if (hudMain != null)
        {
            hudMain.text = main;
            float k = Mathf.Clamp01((Time.time - hudPop) / 0.25f);
            hudMain.transform.localScale = hudMainScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.3f);
            hudMain.color = Current == State.Playing && timeLeft <= 5f ? new Color(1f, 0.55f, 0.4f) : Color.white;
        }
        if (hudSub != null)
        {
            hudSub.gameObject.SetActive(sub.Length > 0);
            hudSub.text = sub;
            hudSub.color = Current == State.Playing && timeLeft <= 5f ? new Color(1f, 0.55f, 0.4f) : Color.white;
        }
        foreach (var t in new[] { hudMain, hudSub, boardScore, boardInfo })
            if (t != null)
                foreach (var s in t.GetComponentsInChildren<TextMesh>(true))
                    if (s != t) s.text = t.text;   // 테두리 글자도 같은 내용
        FitHudPanel();
    }

    void FitHudPanel()
    {
        if (hudPanel == null || hudPanel.parent == null) return;
        Transform space = hudPanel.parent;
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        foreach (var t in new[] { hudMain, hudSub })
        {
            if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
            Bounds b = t.GetComponent<MeshRenderer>().localBounds;
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, 0f));
                Vector3 l = space.InverseTransformPoint(t.transform.TransformPoint(c));
                if (!any) { min = max = l; any = true; } else { min = Vector2.Min(min, l); max = Vector2.Max(max, l); }
            }
        }
        hudPanel.gameObject.SetActive(any);
        if (!any) return;
        Vector2 size = max - min + hudPanelPadding * 2f, center = (min + max) * 0.5f;
        hudPanel.localPosition = new Vector3(center.x, center.y, 0.004f);
        hudPanel.localScale = new Vector3(size.x, size.y, 1f);
    }
}

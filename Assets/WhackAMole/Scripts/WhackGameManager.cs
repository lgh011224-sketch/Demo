// 두더지 게임 전체 진행
// 도구: 드럼 스틱(Wood) / 쇠 망치(Hammer)   ※ 사격(Gun)은 별도 씬으로 분리 예정 (allowGunMode로만 켜짐)
// 두더지: 귀여운 갈색 두더지 = 드럼 스틱으로만, 초록 우락부락 두더지 = 쇠 망치로만 잡힘.
//         틀린 도구로 치면 두더지가 고개를 저으며 버팀 (WRONG + MISS로 기록)
// 버전: useModule 체크 해제 = 컨트롤러 버전, 체크 = 모듈(아두이노) 버전
//
// 조작
//   오른손 A 버튼          : 게임 시작 / 중지
//   왼손 검지 트리거 / B    : 드럼 스틱 ↔ 쇠 망치 전환 (게임 중에도 가능)
//   키보드 Space           : 게임 시작 / 중지
//   키보드 Tab / 1 / 2     : 도구 전환 / 드럼 스틱 / 쇠 망치
//   키보드 H               : HIT 메시지 흉내 (아두이노 없이 모듈 버전 시험용)
//
// 시작 연출: A(또는 Space)를 누르면 점수판이 서서히 사라지고 3 → 2 → 1 → START! 카운트 후 게임 시작.
//           게임이 끝나면 점수판이 다시 서서히 나타나 결과를 보여 줍니다. 카운트 중 A를 다시 누르면 취소.
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum ToolMode { Wood, Hammer, Gun }

public class WhackGameManager : MonoBehaviour
{
    [Header("버전")]
    [Tooltip("체크하면 모듈(아두이노) 버전: 아두이노 HIT 메시지로 타격을 판정합니다")]
    public bool useModule = false;
    public ArduinoSerial arduino;

    [Header("도구")]
    public ToolMode startMode = ToolMode.Wood;
    [Tooltip("사격 모드 사용 여부 (사격 게임은 별도 씬으로 분리 예정이라 기본은 끔)")]
    public bool allowGunMode = false;
    public GameObject woodTool;
    public GameObject hammerTool;
    public GameObject gunTool;
    public ToolHead woodHead;
    public ToolHead hammerHead;
    public GunShooter gun;

    [Header("두더지")]
    [Tooltip("탁자 위 두더지 (나무 망치·쇠 망치 모드)")]
    public Mole[] tableMoles;
    [Tooltip("벽의 표적 (사격 모드)")]
    public Mole[] gunTargets;

    [Header("게임 설정")]
    public float roundSeconds = 60f;
    public float spawnIntervalMin = 0.6f;
    public float spawnIntervalMax = 1.2f;
    [Tooltip("두더지가 올라와 머무는 시간 (s)")]
    public float moleStayTime = 1.0f;
    [Tooltip("동시에 올라와 있을 수 있는 최대 두더지 수")]
    public int maxMolesUp = 2;
    [Tooltip("올라오는 두더지 중 초록 우락부락 두더지(쇠 망치용)의 비율 (0~1)")]
    [Range(0f, 1f)] public float badMoleRatio = 0.4f;

    [Header("맞혔을 때 진동 (컨트롤러 버전)")]
    [Tooltip("귀여운 두더지를 맞혔을 때 진동 세기 (0~1)")]
    [Range(0f, 1f)] public float normalHitVibration = 0.5f;
    [Tooltip("초록 우락부락 두더지를 맞혔을 때 진동 세기 (0~1, 1이 컨트롤러 최대)")]
    [Range(0f, 1f)] public float badHitVibration = 1f;
    [Tooltip("맞혔을 때 진동 시간 (초) - 두 종류 공통")]
    public float hitVibrationSeconds = 0.08f;

    [Header("아두이노가 보내는 메시지 (모듈 버전)")]
    public string hitMessage = "HIT";
    public string buttonMessage = "BTN";

    [Header("아두이노로 보내는 명령 (모듈 버전, 여러 개는 ; 로 구분)")]
    [Tooltip("나무 망치 모드로 바꿀 때 모듈 형태 변경 명령")]
    public string woodCommand = "";
    [Tooltip("쇠 망치 모드로 바꿀 때 모듈 형태 변경 명령")]
    public string hammerCommand = "";
    [Tooltip("사격 모드로 바꿀 때 모듈 형태 변경 명령")]
    public string gunCommand = "";
    [Tooltip("두더지를 맞혔을 때 보낼 명령 (비워 두면 보내지 않음)")]
    public string hitFeedbackCommand = "";

    [Header("화면 표시")]
    public TextMesh statusText;
    [Tooltip("점수판 전체 (배경 + 글자). 게임 시작 시 페이드 아웃, 종료 시 페이드 인")]
    public Transform statusBoard;
    [Tooltip("3 2 1 START! 를 보여 줄 큰 글자")]
    public TextMesh countdownText;
    [Tooltip("게임 중 남은 시간·점수를 보여 줄 작은 글자 (점수판이 숨겨진 동안 사용)")]
    public TextMesh playHudText;
    [Tooltip("점수판 배경이 페이드되는 동안만 쓰는 반투명 머티리얼 (평소엔 불투명 배경 사용)")]
    public Material boardFadeMaterial;
    [Tooltip("이번 판 결과를 강조해 보여 줄 노란 테두리 상자와 글자")]
    public GameObject resultBox;
    public TextMesh resultText;

    [Header("시작 연출")]
    public float boardFadeTime = 0.6f;
    [Tooltip("3, 2, 1 각 숫자가 보이는 시간 (s)")]
    public float countdownStep = 0.8f;
    public Color[] countdownColors =
    {
        new Color(1f, 0.30f, 0.25f), new Color(1f, 0.75f, 0.15f), new Color(0.35f, 0.85f, 0.35f), new Color(1f, 0.92f, 0.2f)
    };

    public ToolMode Mode { get; private set; }
    public bool Playing { get; private set; }
    public bool CountingDown { get; private set; }

    Coroutine countdownRoutine, boardRoutine;
    Renderer[] boardRenderers;
    Material boardFadeMat, boardOpaqueMat;
    Renderer boardBackdrop;
    Color boardBaseColor = new Color(0.08f, 0.06f, 0.05f, 0.95f);
    Color statusBaseColor = Color.white;
    Vector3 countdownBaseScale = Vector3.one;

    int hits, misses, wrongs;
    float timeLeft, spawnTimer;
    bool prevA, prevB, prevTrigger, prevLeftTrigger;
    Transform boardLegend;
    TextMesh[] legendTexts;
    Vector3 legendBaseScale = Vector3.one;
    string lastResult = "";

    void Awake()
    {
        // 컨트롤러 버전이면 아두이노 연결을 시도하지 않음
        // (진동을 아두이노로 보내는 ArduinoHaptics가 켜져 있으면 컨트롤러 버전에서도 연결 유지)
        var haptics = arduino != null ? arduino.GetComponent<ArduinoHaptics>() : null;
        if (arduino != null && !useModule && (haptics == null || !haptics.sendToArduino))
        {
            arduino.connectOnStart = false;
            arduino.enabled = false;
        }
    }

    void Start()
    {
        if (arduino != null && useModule)
        {
            arduino.OnLine += HandleArduinoLine;
            if (!arduino.IsOpen) arduino.Open();
        }
        HideAll();
        SetMode(startMode);
        PrepareStartFx();
        // 포트를 열면 아두이노가 재시작되므로, 첫 모드 명령은 잠시 뒤에 다시 보냄
        if (useModule) Invoke(nameof(ResendModeCommand), 2.5f);
    }

    void ResendModeCommand()
    {
        if (arduino == null) return;
        arduino.Send(Mode == ToolMode.Wood ? woodCommand : Mode == ToolMode.Hammer ? hammerCommand : gunCommand);
    }

    void OnDestroy()
    {
        if (arduino != null) arduino.OnLine -= HandleArduinoLine;
    }

    void Update()
    {
        ReadInputs();

        if (Playing)
        {
            timeLeft -= Time.deltaTime;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnOne();
                spawnTimer = Random.Range(spawnIntervalMin, spawnIntervalMax);
            }
            if (timeLeft <= 0f) EndRound();
        }
        UpdateText();
    }

    // ---------- 입력 ----------
    void ReadInputs()
    {
        bool a = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.primaryButton);
        bool b = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.secondaryButton);
        bool trig = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.triggerButton);

        bool leftTrig = RightHandFollower.GetLeftButton(UnityEngine.XR.CommonUsages.triggerButton);

        if (a && !prevA) ToggleRound();
        // 도구 전환: 왼손 검지 트리거 또는 B (게임 중에도 가능)
        if ((leftTrig && !prevLeftTrigger) || (b && !prevB) || KeyDown(KeyId.Tab)) SwitchTool();
        if (trig && !prevTrigger && Mode == ToolMode.Gun && gun != null) gun.Fire();
        prevA = a; prevB = b; prevTrigger = trig; prevLeftTrigger = leftTrig;

        if (KeyDown(KeyId.Space)) ToggleRound();
        if (KeyDown(KeyId.One)) ChangeTool(ToolMode.Wood);
        if (KeyDown(KeyId.Two)) ChangeTool(ToolMode.Hammer);
        if (allowGunMode && !Playing && !CountingDown && KeyDown(KeyId.Three)) SetMode(ToolMode.Gun);
        if (KeyDown(KeyId.H)) ModuleStrike();
        if (KeyDown(KeyId.F) && Mode == ToolMode.Gun && gun != null) gun.Fire();
    }

    // 드럼 스틱 ↔ 쇠 망치 (사격 모드였다면 드럼 스틱으로)
    void SwitchTool() => ChangeTool(Mode == ToolMode.Wood ? ToolMode.Hammer : ToolMode.Wood);

    // 게임 중에도 두더지를 숨기지 않고 도구만 바꿈
    void ChangeTool(ToolMode mode)
    {
        if (mode == Mode) return;
        if (Mode == ToolMode.Gun && (Playing || CountingDown)) return;
        SetMode(mode, hideMoles: !(Playing || CountingDown));
        RightHandFollower.Vibrate(0.35f, 0.04f);
    }

    enum KeyId { Space, One, Two, Three, H, F, Tab }

    bool KeyDown(KeyId k)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        switch (k)
        {
            case KeyId.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyId.One: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case KeyId.Two: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case KeyId.Three: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case KeyId.H: return kb.hKey.wasPressedThisFrame;
            case KeyId.F: return kb.fKey.wasPressedThisFrame;
            case KeyId.Tab: return kb.tabKey.wasPressedThisFrame;
        }
        return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
        switch (k)
        {
            case KeyId.Space: return Input.GetKeyDown(KeyCode.Space);
            case KeyId.One: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case KeyId.Two: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case KeyId.Three: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case KeyId.H: return Input.GetKeyDown(KeyCode.H);
            case KeyId.F: return Input.GetKeyDown(KeyCode.F);
            case KeyId.Tab: return Input.GetKeyDown(KeyCode.Tab);
        }
        return false;
#else
        return false;
#endif
    }

    void HandleArduinoLine(string line)
    {
        string msg = line.Trim().ToUpperInvariant();
        if (msg == hitMessage.Trim().ToUpperInvariant())
        {
            ModuleStrike();
        }
        else if (msg == buttonMessage.Trim().ToUpperInvariant())
        {
            if (Mode == ToolMode.Gun && gun != null) gun.Fire();
        }
    }

    // ---------- 모드 ----------
    public void SetMode(ToolMode mode) => SetMode(mode, true);

    public void SetMode(ToolMode mode, bool hideMoles)
    {
        if (mode == ToolMode.Gun && !allowGunMode) mode = ToolMode.Wood;
        Mode = mode;
        if (woodTool) woodTool.SetActive(mode == ToolMode.Wood);
        if (hammerTool) hammerTool.SetActive(mode == ToolMode.Hammer);
        if (gunTool) gunTool.SetActive(mode == ToolMode.Gun);
        if (hideMoles)
        {
            HideAll();
            lastResult = "";
        }

        if (useModule && arduino != null)
        {
            string cmd = mode == ToolMode.Wood ? woodCommand : mode == ToolMode.Hammer ? hammerCommand : gunCommand;
            arduino.Send(cmd);
        }
    }

    Mole[] ActiveMoles => Mode == ToolMode.Gun ? gunTargets : tableMoles;
    ToolHead ActiveHead => Mode == ToolMode.Wood ? woodHead : Mode == ToolMode.Hammer ? hammerHead : null;

    // ---------- 진행 ----------
    void ToggleRound()
    {
        if (Playing) EndRound();
        else if (CountingDown) CancelCountdown();
        else BeginCountdown();
    }

    // ---------- 시작 연출 (점수판 페이드 + 3 2 1 START!) ----------
    void PrepareStartFx()
    {
        if (statusText != null) statusBaseColor = statusText.color;
        if (statusBoard != null)
        {
            // 배경은 다른 오브젝트와 같은 머티리얼을 쓰므로, 반투명이 되는 전용 머티리얼로 교체
            boardRenderers = statusBoard.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in boardRenderers)
            {
                if (r.name != "Backdrop") continue;
                boardBackdrop = r;
                boardOpaqueMat = r.sharedMaterial;
                if (boardFadeMaterial != null)
                {
                    boardFadeMat = new Material(boardFadeMaterial);
                    boardBaseColor = boardFadeMat.HasProperty("_BaseColor") ? boardFadeMat.GetColor("_BaseColor") : boardBaseColor;
                }
                else
                {
                    // 예전 장면 호환: 페이드 머티리얼이 없으면 Sprites/Default로 대체
                    Shader fadeShader = Shader.Find("Sprites/Default");
                    if (fadeShader != null) boardFadeMat = new Material(fadeShader) { color = boardBaseColor };
                }
            }
            // 안내용 두더지 모형·설명 글자 (Legend) - 점수판과 함께 나타나고 사라짐
            boardLegend = statusBoard.Find("Legend");
            if (boardLegend != null)
            {
                legendBaseScale = boardLegend.localScale;
                legendTexts = boardLegend.GetComponentsInChildren<TextMesh>(true);
            }
        }
        if (countdownText != null)
        {
            countdownBaseScale = countdownText.transform.localScale;
            countdownText.gameObject.SetActive(false);
        }
        if (playHudText != null) playHudText.gameObject.SetActive(false);
    }

    void BeginCountdown()
    {
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    void CancelCountdown()
    {
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = null;
        CountingDown = false;
        ShowCountdown(null, Color.clear, 1f);
        FadeBoard(true);
    }

    IEnumerator CountdownRoutine()
    {
        CountingDown = true;
        HideAll();
        lastResult = "";
        FadeBoard(false);
        yield return new WaitForSeconds(boardFadeTime);

        string[] steps = { "3", "2", "1" };
        for (int i = 0; i < steps.Length; i++)
        {
            RightHandFollower.Vibrate(0.25f, 0.05f);
            yield return PopText(steps[i], ColorAt(i), countdownStep, 1.7f, 1f);
        }

        // START! 와 동시에 게임 시작
        CountingDown = false;
        StartRound();
        RightHandFollower.Vibrate(0.7f, 0.15f);
        yield return PopText("START!", ColorAt(3), 0.9f, 0.6f, 1.25f);
        countdownRoutine = null;
    }

    Color ColorAt(int i) => countdownColors != null && countdownColors.Length > 0
        ? countdownColors[Mathf.Clamp(i, 0, countdownColors.Length - 1)] : Color.white;

    // 글자가 크게 튀어나왔다가(팝) 제자리 크기로 줄어든 뒤 서서히 사라짐
    IEnumerator PopText(string text, Color color, float duration, float fromScale, float toScale)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float pop = Mathf.Clamp01(t / 0.22f);
            float ease = 1f - Mathf.Pow(1f - pop, 3f);                       // 빠르게 줄어드는 ease-out
            float scale = Mathf.Lerp(fromScale, toScale, ease) + Mathf.Sin(pop * Mathf.PI) * 0.08f; // 살짝 통통 튀는 느낌
            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            ShowCountdown(text, color, scale, alpha);
            yield return null;
        }
        ShowCountdown(null, Color.clear, 1f);
    }

    void ShowCountdown(string text, Color color, float scale, float alpha = 1f)
    {
        if (countdownText == null) return;
        bool on = !string.IsNullOrEmpty(text);
        countdownText.gameObject.SetActive(on);
        if (!on) return;
        countdownText.text = text;
        color.a = alpha;
        countdownText.color = color;
        countdownText.transform.localScale = countdownBaseScale * scale;
        // 그림자(자식 TextMesh)도 같은 글자·투명도로
        foreach (TextMesh child in countdownText.GetComponentsInChildren<TextMesh>(true))
        {
            if (child == countdownText) continue;
            child.text = text;
            Color c = child.color; c.a = alpha * 0.85f; child.color = c;
        }
    }

    void FadeBoard(bool show)
    {
        if (statusBoard == null) return;
        if (boardRoutine != null) StopCoroutine(boardRoutine);
        boardRoutine = StartCoroutine(FadeBoardRoutine(show));
    }

    IEnumerator FadeBoardRoutine(bool show)
    {
        statusBoard.gameObject.SetActive(true);
        // 페이드하는 동안만 반투명 머티리얼 사용
        if (boardBackdrop != null && boardFadeMat != null) boardBackdrop.sharedMaterial = boardFadeMat;
        float from = show ? 0f : 1f, to = show ? 1f : 0f;
        float t = 0f;
        while (t < boardFadeTime)
        {
            t += Time.deltaTime;
            SetBoardAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / boardFadeTime)));
            yield return null;
        }
        SetBoardAlpha(to);
        if (boardBackdrop != null && boardOpaqueMat != null) boardBackdrop.sharedMaterial = boardOpaqueMat;
        if (!show) statusBoard.gameObject.SetActive(false);
        boardRoutine = null;
    }

    void SetBoardAlpha(float a)
    {
        if (boardFadeMat != null)
        {
            Color c = boardBaseColor; c.a *= a;
            if (boardFadeMat.HasProperty("_BaseColor")) boardFadeMat.SetColor("_BaseColor", c);
            else boardFadeMat.color = c;
        }
        if (statusText != null) { Color c = statusBaseColor; c.a *= a; statusText.color = c; }
        if (boardLegend != null)
        {
            // 두더지 모형은 투명해질 수 없으므로 작아지며 사라지고, 글자는 함께 투명해짐
            boardLegend.localScale = legendBaseScale * Mathf.SmoothStep(0f, 1f, a);
            if (legendTexts != null)
                foreach (TextMesh tm in legendTexts) { Color c = tm.color; c.a = a; tm.color = c; }
        }
    }

    public void StartRound()
    {
        HideAll();
        hits = 0;
        misses = 0;
        wrongs = 0;
        timeLeft = roundSeconds;
        spawnTimer = 0.8f;
        lastResult = "";
        Playing = true;
    }

    public void EndRound()
    {
        Playing = false;
        HideAll();
        int total = hits + misses;
        float acc = total > 0 ? 100f * hits / total : 0f;
        lastResult = $"HIT {hits}    MISS {misses}    WRONG TOOL {wrongs}    ({acc:0}%)";
        Debug.Log($"[두더지] 종료 - 맞힘 {hits}, 빗맞음 {misses} (틀린 도구 {wrongs})");
        if (playHudText != null) playHudText.gameObject.SetActive(false);
        FadeBoard(true);
    }

    void SpawnOne()
    {
        Mole[] set = ActiveMoles;
        if (set == null || set.Length == 0) return;

        int up = 0;
        foreach (Mole m in set) if (m != null && !m.IsDown) up++;
        if (up >= maxMolesUp) return;

        for (int tries = 0; tries < 20; tries++)
        {
            Mole m = set[Random.Range(0, set.Length)];
            if (m != null && m.IsDown)
            {
                // 탁자 두더지는 종류를 무작위로 (초록 우락부락 = badMoleRatio 확률)
                MoleKind kind = Mode != ToolMode.Gun && Random.value < badMoleRatio ? MoleKind.Bad : MoleKind.Normal;
                m.PopUp(moleStayTime, kind);
                return;
            }
        }
    }

    void HideAll()
    {
        if (tableMoles != null) foreach (Mole m in tableMoles) if (m) m.HideNow();
        if (gunTargets != null) foreach (Mole m in gunTargets) if (m) m.HideNow();
    }

    // 모듈 버전: 아두이노 HIT가 오면 망치 머리 바로 아래 구멍을 찾아 판정
    public void ModuleStrike()
    {
        ToolHead head = ActiveHead;
        if (head == null) return;

        Mole best = null;
        float bestDist = head.hitRadius;
        Vector3 hp = head.transform.position;
        foreach (Mole m in tableMoles)
        {
            if (m == null) continue;
            Vector3 mp = m.transform.position;
            float d = Vector2.Distance(new Vector2(hp.x, hp.z), new Vector2(mp.x, mp.z));
            if (d <= bestDist)
            {
                bestDist = d;
                best = m;
            }
        }
        OnStrike(best);
    }

    // 이 도구로 이 두더지를 잡을 수 있는지: 귀여운 두더지 = 드럼 스틱, 초록 두더지 = 쇠 망치 (사격은 구분 없음)
    bool ToolMatches(Mole target) =>
        Mode == ToolMode.Gun ||
        (target.Kind == MoleKind.Normal && Mode == ToolMode.Wood) ||
        (target.Kind == MoleKind.Bad && Mode == ToolMode.Hammer);

    // 모든 타격·발사의 최종 판정. target이 null이거나 숨은 두더지면 빗맞음
    public void OnStrike(Mole target)
    {
        if (!Playing) return;
        if (target != null && target.IsHittable && !ToolMatches(target))
        {
            // 틀린 도구: 두더지가 버티며 고개를 젓고, 약한 진동으로 알려 줌
            wrongs++;
            misses++;
            target.Deny();
            // (진동은 ArduinoHaptics가 아두이노로도 같은 값을 보냄: VIB 38 200)
            RightHandFollower.Vibrate(0.15f, 0.2f);
            return;
        }
        if (target != null && target.TryHit())
        {
            hits++;
            // 초록 두더지는 더 강하게 (시간은 같음) → 아두이노로도 VIB 255 80 / VIB 128 80
            RightHandFollower.Vibrate(target.Kind == MoleKind.Bad ? badHitVibration : normalHitVibration, hitVibrationSeconds);
            if (useModule && arduino != null) arduino.Send(hitFeedbackCommand); // 모듈 버전 전용 추가 명령 (비워 두면 안 보냄)
        }
        else
        {
            misses++;
        }
    }

    // ---------- 화면 ----------
    void UpdateText()
    {
        if (statusText == null) return;
        string modeName = Mode == ToolMode.Wood ? "DRUM STICK" : Mode == ToolMode.Hammer ? "HAMMER" : "SHOOTING";
        string ver = useModule ? "MODULE" : "CONTROLLER";
        if (Playing)
        {
            string hud = $"{modeName}    TIME {Mathf.CeilToInt(Mathf.Max(timeLeft, 0f))}    HIT {hits}    MISS {misses}";
            if (playHudText != null)
            {
                playHudText.gameObject.SetActive(true);
                playHudText.text = hud;
            }
            statusText.text = $"{modeName}\n{hud}";
        }
        else if (CountingDown)
        {
            // 카운트 중에는 점수판 글자를 바꾸지 않음 (페이드 아웃 중)
        }
        else
        {
            string link = useModule ? (arduino != null && arduino.IsOpen ? "  [Arduino OK]" : "  [Arduino X]") : "";
            // 이번 판 결과는 노란 테두리 상자에 따로 (상자가 없으면 예전처럼 본문에)
            bool box = resultBox != null && resultText != null;
            if (box)
            {
                resultBox.SetActive(lastResult.Length > 0);
                resultText.text = lastResult;
            }
            statusText.text = $"NOW HOLDING : {modeName}{link}\n" +
                              (!box && lastResult.Length > 0 ? "RESULT  " + lastResult + "\n" : "\n") +
                              "\nRIGHT  [A]  :  START / STOP\n" +
                              "LEFT  [TRIGGER]  :  STICK <-> HAMMER\n" +
                              "LEFT  [Y]  :  SHOOTING RANGE";
        }
    }
}

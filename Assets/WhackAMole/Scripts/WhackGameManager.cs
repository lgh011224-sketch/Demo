// 두더지 게임 전체 진행
// ── 단계 진행 (useStages, 오락실 방식) ──────────────────────────────
//   A → 3·2·1·GO! → 1단계(30초): 징그러운 초록 두더지만, 쇠 망치. 한 마리 5점, 20마리 잡으면 즉시 통과 (못 잡으면 게임 오버)
//   → (자동) 1단계 CLEAR·점수 → "STAGE 2 / CUTE MOLES!" + 변신 이펙트로 드럼 스틱으로 교체 + 아두이노 "2"(스테퍼 아래로, 통과 즉시)
//   → 3·2·1·GO! → 2단계(30초): 귀여운 두더지만, 드럼 스틱 → 끝나면 1단계+2단계 총점
// 하우징 (Arduino/PartsTest, 시리얼 숫자 명령): 쇠 망치 = 스테퍼 위 "1", 드럼 스틱 = 스테퍼 아래 "2", 사격장에서 돌아오면 스크류 당기기 "5"
//   게임 실행 즉시 1단계용 쇠 망치 → "1" 전송 (스테퍼 위로)
// ────────────────────────────────────────────────────────────────
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
    [Tooltip("도구가 바뀔 때 하우징(스테퍼) 명령을 아두이노로 보냄 (컨트롤러 버전에서도)")]
    public bool sendHousingCommands = true;

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
    [Tooltip("다음 두더지가 올라오기까지 간격 (s, 최소~최대 사이 무작위)")]
    public float spawnIntervalMin = 0.8f;
    public float spawnIntervalMax = 1.5f;
    [Tooltip("두더지가 올라와 머무는 시간 (s)")]
    public float moleStayTime = 1.3f;
    [Tooltip("동시에 올라와 있을 수 있는 최대 두더지 수")]
    public int maxMolesUp = 2;
    [Tooltip("올라오는 두더지 중 초록 우락부락 두더지(쇠 망치용)의 비율 (0~1)")]
    [Range(0f, 1f)] public float badMoleRatio = 0.4f;

    [Header("단계 (오락실 방식)")]
    [Tooltip("켜면 1단계(징그러운 초록 두더지·망치) → 2단계(귀여운 두더지·드럼 스틱)로 진행. 끄면 예전처럼 섞여 나오는 한 판")]
    public bool useStages = true;
    public float stage1Seconds = 30f;
    public float stage2Seconds = 30f;
    [Tooltip("두더지 한 마리당 점수")]
    public int pointsPerMole = 5;
    [Tooltip("1단계 통과 기준 (잡은 두더지 수)")]
    public int stage1Goal = 20;
    [Tooltip("1단계 CLEAR와 점수를 보여 주는 시간 (s)")]
    public float clearShowSeconds = 3f;
    [Tooltip("2단계 안내 + 변신 이펙트 시간 (s) – 하우징이 변형될 시간")]
    public float stage2IntroSeconds = 4f;
    [Tooltip("도구 변신 이펙트 (ToolRoot에 붙어 있음)")]
    public ToolTransformFx transformFx;

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

    [Header("아두이노로 보내는 명령 (여러 개는 ; 로 구분, 아두이노 오브젝트가 켜져 있으면 전송)")]
    [Tooltip("스테퍼 위로 (쇠 망치·징그러운 두더지, 게임 시작·1단계) - PartsTest 스케치 '1'")]
    public string stepperUpCommand = "1";
    [Tooltip("스테퍼 아래로 (드럼 스틱·귀여운 두더지, 2단계 진입 즉시) - PartsTest 스케치 '2'")]
    public string stepperDownCommand = "2";
    [Tooltip("사격장에서 돌아왔을 때 스크류 당기기 - PartsTest 스케치 '5'")]
    public string screwPullCommand = "5";
    [Tooltip("(예전 HousingControl용, 사용 안 함)")]
    public string woodCommand = "MODE STICK";
    [Tooltip("(예전 HousingControl용, 사용 안 함)")]
    public string hammerCommand = "MODE HAMMER";
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
    int stage, stageCatches;                 // 현재 단계(0 = 게임 밖), 이번 단계에서 잡은 수
    readonly int[] stageScore = new int[3];  // [1], [2] 단계별 점수
    public int Stage => stage;
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
        if (arduino != null && !useModule && !sendHousingCommands && (haptics == null || !haptics.sendToArduino))
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
        if (arduino != null && sendHousingCommands && !arduino.IsOpen) arduino.Open();
        // 사격장에서 돌아왔으면 스크류 당기기 "5" (밀려 있을 때만)
        if (arduino != null && sendHousingCommands) HousingState.SetScrew(arduino, false, "4", screwPullCommand);
        HideAll();
        // 단계 모드: 1단계가 쇠 망치이므로 처음부터 망치 (→ 스테퍼 맨 위)
        // 포트를 막 열어 아두이노가 재시작 중이면 ArduinoSerial이 명령을 모아 두었다가 준비되면 보냄
        SetMode(useStages ? ToolMode.Hammer : startMode);
        PrepareStartFx();
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
            if (timeLeft <= 0f)
            {
                if (useStages && stage == 1) StageFailed();   // 1단계 시간 초과 = 게임 오버
                else EndRound();                              // 2단계 종료(또는 단계 없는 한 판) = 최종 결과
            }
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
        // 도구 전환: 왼손 검지 트리거 또는 B
        //   단계 모드에서는 게임 중 도구가 단계에 따라 자동으로 바뀌므로, 대기 중에만 직접 바꿀 수 있음
        bool manualSwitchOk = !useStages || (!Playing && !CountingDown);
        if (manualSwitchOk && ((leftTrig && !prevLeftTrigger) || (b && !prevB) || KeyDown(KeyId.Tab))) SwitchTool();
        if (trig && !prevTrigger && Mode == ToolMode.Gun && gun != null) gun.Fire();
        prevA = a; prevB = b; prevTrigger = trig; prevLeftTrigger = leftTrig;

        if (KeyDown(KeyId.Space)) ToggleRound();
        if (manualSwitchOk && KeyDown(KeyId.One)) ChangeTool(ToolMode.Wood);
        if (manualSwitchOk && KeyDown(KeyId.Two)) ChangeTool(ToolMode.Hammer);
        if (allowGunMode && !Playing && !CountingDown && KeyDown(KeyId.Three)) SetMode(ToolMode.Gun);
        if (KeyDown(KeyId.H)) ModuleStrike();
        if (KeyDown(KeyId.F) && Mode == ToolMode.Gun && gun != null) gun.Fire();
    }

    // 드럼 스틱 ↔ 쇠 망치 (사격 모드였다면 드럼 스틱으로)
    void SwitchTool() => ChangeTool(Mode == ToolMode.Wood ? ToolMode.Hammer : ToolMode.Wood);

    // 게임 중에도 두더지를 숨기지 않고 도구만 바꿈. 변신 이펙트가 있으면 오오라 속에서 교체
    void ChangeTool(ToolMode mode)
    {
        if (mode == Mode) return;
        if (Mode == ToolMode.Gun && (Playing || CountingDown)) return;
        if (transformFx != null && transformFx.Playing) return;
        bool hide = !(Playing || CountingDown);
        if (transformFx != null && transformFx.isActiveAndEnabled)
        {
            transformFx.Play(CurrentToolObject(), () => SetMode(mode, hide), CurrentToolObject);
        }
        else
        {
            SetMode(mode, hide);
            RightHandFollower.Vibrate(0.35f, 0.04f);
        }
    }

    GameObject CurrentToolObject() => Mode == ToolMode.Wood ? woodTool : Mode == ToolMode.Hammer ? hammerTool : gunTool;

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

        // 하우징 (Arduino/PartsTest): 쇠 망치(징그러운 두더지) = 스테퍼 위 "1", 드럼 스틱(귀여운 두더지) = 스테퍼 아래 "2"
        // 이미 그 위치면 보내지 않음 (HousingState가 기억)
        if (arduino != null && sendHousingCommands)
        {
            if (mode == ToolMode.Hammer) HousingState.SetStepper(arduino, true, stepperUpCommand, stepperDownCommand);
            else if (mode == ToolMode.Wood) HousingState.SetStepper(arduino, false, stepperUpCommand, stepperDownCommand);
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
        if (playHudText != null) { playHudBaseScale = playHudText.transform.localScale; playHudText.gameObject.SetActive(false); }
    }

    Vector3 playHudBaseScale = Vector3.one;

    void BeginCountdown()
    {
        // 센서 버전: 시작할 때 하우징을 가운데 구멍 쪽으로 들고 있으면 그 자세가 정면이 됨
        if (ImuInput.Instance != null) ImuInput.Instance.Recenter();
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    // 카운트다운·단계 전환 중 A를 누르면 취소 (전환 중이면 그때까지 점수로 결과 표시)
    void CancelCountdown()
    {
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = null;
        CountingDown = false;
        ShowCountdown(null, Color.clear, 1f);
        if (useStages && stageScore[1] > 0) lastResult = StageResultText();
        stage = 0;
        if (playHudText != null) playHudText.gameObject.SetActive(false);
        FadeBoard(true);
    }

    IEnumerator CountdownRoutine()
    {
        CountingDown = true;
        HideAll();
        lastResult = "";
        stage = 0; stageScore[1] = stageScore[2] = 0;
        FadeBoard(false);
        // 단계 모드: 1단계는 쇠 망치 (드럼 스틱을 들고 있었다면 변신 → 스테퍼 맨 위)
        if (useStages && Mode != ToolMode.Hammer) ChangeTool(ToolMode.Hammer);
        yield return new WaitForSeconds(boardFadeTime);
        while (transformFx != null && transformFx.Playing) yield return null;

        yield return Count321(1);
        countdownRoutine = null;
    }

    // 3 → 2 → 1 → GO! (GO와 동시에 다음 단계 시작)
    IEnumerator Count321(int nextStage)
    {
        string[] steps = { "3", "2", "1" };
        for (int i = 0; i < steps.Length; i++)
        {
            RightHandFollower.Vibrate(0.25f, 0.05f);
            yield return PopText(steps[i], ColorAt(i), countdownStep, 1.7f, 1f);
        }
        CountingDown = false;
        if (useStages) StartStage(nextStage);
        else StartRound();
        RightHandFollower.Vibrate(0.7f, 0.15f);
        yield return PopText("GO!", ColorAt(3), countdownStep, 1.7f, 1f);   // 3·2·1과 같은 모양·속도로 사라짐
    }

    // ---------- 단계 ----------
    void StartStage(int s)
    {
        StartRound();
        stage = s;
        stageCatches = 0;
        stageScore[s] = 0;
        timeLeft = s == 1 ? stage1Seconds : stage2Seconds;
    }

    // 1단계 통과 → 점수 표시 → 2단계 안내 + 변신(하우징 변형 시간) → 3·2·1·GO! → 2단계 (입력 없이 자동)
    IEnumerator Stage2Transition()
    {
        Playing = false;
        CountingDown = true;
        HideAll();
        if (playHudText != null) playHudText.gameObject.SetActive(false);
        RightHandFollower.Vibrate(0.8f, 0.2f);
        // 1단계 통과 즉시 스테퍼 아래로 "2" (드럼 스틱 변신 때 SetMode가 다시 보내지 않음)
        if (arduino != null && sendHousingCommands) HousingState.SetStepper(arduino, false, stepperUpCommand, stepperDownCommand);
        yield return PopText("STAGE 1\nCLEAR!", ColorAt(3), clearShowSeconds * 0.5f, 0.8f, 0.42f);
        yield return PopText($"SCORE  {stageScore[1]}", Color.white, clearShowSeconds * 0.5f, 0.7f, 0.42f);

        // 2단계 안내와 함께 드럼 스틱으로 변신 (스테퍼 "2"는 통과 즉시 이미 보냄)
        ChangeTool(ToolMode.Wood);
        yield return PopText("STAGE 2\nCUTE MOLES!", new Color(1f, 0.75f, 0.55f), stage2IntroSeconds, 0.7f, 0.38f);
        while (transformFx != null && transformFx.Playing) yield return null;

        yield return Count321(2);
        countdownRoutine = null;
    }

    // 1단계 시간 안에 20마리를 못 잡음 → 게임 오버
    void StageFailed()
    {
        Playing = false;
        HideAll();
        lastResult = $"STAGE 1 FAILED    {stageCatches}/{stage1Goal}    SCORE {stageScore[1]}";
        Debug.Log($"[두더지] 1단계 실패 - {stageCatches}/{stage1Goal}, {stageScore[1]}점");
        stage = 0;
        if (playHudText != null) playHudText.gameObject.SetActive(false);
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(GameOverRoutine("TIME UP!"));
    }

    IEnumerator GameOverRoutine(string title)
    {
        yield return PopText(title, ColorAt(0), 1.4f, 0.8f, 0.45f);
        FadeBoard(true);
        countdownRoutine = null;
    }

    string StageResultText() =>
        $"STAGE1 {stageScore[1]}  +  STAGE2 {stageScore[2]}  =  TOTAL {stageScore[1] + stageScore[2]}";

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
        FitToScreen(countdownText, 0.92f);
    }

    [Tooltip("점수판 화면 안쪽 크기 (m) – 카운트다운·점수 글자가 이 안에 들어가게 줄임")]
    public Vector2 screenSize = new Vector2(1.06f, 0.52f);

    // 글자가 점수판 화면(검은 네모) 밖으로 나가지 않게 크기를 줄임
    void FitToScreen(TextMesh t, float fill)
    {
        if (t == null) return;
        var r = t.GetComponent<MeshRenderer>();
        if (r == null) return;
        Bounds b = r.localBounds;
        Vector3 ls = t.transform.lossyScale;
        float w = b.size.x * ls.x, h = b.size.y * ls.y;
        if (w <= 0f || h <= 0f) return;
        float k = Mathf.Min(1f, screenSize.x * fill / w, screenSize.y * fill / h);
        if (k < 1f) t.transform.localScale *= k;
    }

    // 위 전광판은 이제 "WHACK-A-MOLE" 타이틀 그림으로 고정 → 사라지거나 나타나지 않음 (정보는 상판 LED 화면)
    void FadeBoard(bool show) { }

    IEnumerator FadeBoardRoutine(bool show)
    {
        statusBoard.gameObject.SetActive(true);
        // 점수판(검은 화면)은 계속 켜 두고, 그 안의 설명 글·두더지 그림만 사라지고 나타남
        //  → 3·2·1과 점수가 같은 화면 안에 표시됨
        if (statusText != null) statusText.gameObject.SetActive(true);
        if (boardLegend != null) boardLegend.gameObject.SetActive(true);
        float from = show ? 0f : 1f, to = show ? 1f : 0f;
        float t = 0f;
        while (t < boardFadeTime)
        {
            t += Time.deltaTime;
            SetBoardAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / boardFadeTime)));
            yield return null;
        }
        SetBoardAlpha(to);
        if (!show)
        {
            if (statusText != null) statusText.gameObject.SetActive(false);
            if (boardLegend != null) boardLegend.gameObject.SetActive(false);
        }
        boardRoutine = null;
    }

    void SetBoardAlpha(float a)
    {
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
        if (useStages && stage > 0)
        {
            lastResult = StageResultText();
            Debug.Log($"[두더지] 종료 - {lastResult}");
            bool finished = stage == 2 && timeLeft <= 0f;
            stage = 0;
            if (playHudText != null) playHudText.gameObject.SetActive(false);
            if (finished)
            {
                // 2단계까지 끝까지 마침: "FINISH!" 후 결과판
                if (countdownRoutine != null) StopCoroutine(countdownRoutine);
                countdownRoutine = StartCoroutine(GameOverRoutine("FINISH!"));
            }
            else FadeBoard(true); // A로 중간에 멈춤
            return;
        }
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
                // 단계 모드: 1단계 = 징그러운 초록 두더지만, 2단계 = 귀여운 두더지만
                // 단계 없는 한 판: 무작위 (초록 우락부락 = badMoleRatio 확률)
                MoleKind kind = useStages ? (stage == 1 ? MoleKind.Bad : MoleKind.Normal)
                    : (Mode != ToolMode.Gun && Random.value < badMoleRatio ? MoleKind.Bad : MoleKind.Normal);
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

    // 센서 버전: 내려친 순간 도구가 있던 좌우 위치(세계 X)에서 가장 가까운 구멍으로 판정
    //   구멍 간격의 절반보다 멀면(줄 바깥) 빗맞음
    public void StrikeAtX(float worldX)
    {
        Mole best = null;
        float bestDist = float.MaxValue, minX = float.MaxValue, maxX = float.MinValue;
        int count = 0;
        foreach (Mole m in tableMoles)
        {
            if (m == null) continue;
            float x = m.transform.position.x;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); count++;
            float d = Mathf.Abs(x - worldX);
            if (d < bestDist) { bestDist = d; best = m; }
        }
        float halfGap = count > 1 ? (maxX - minX) / (count - 1) * 0.5f : 0.08f;
        OnStrike(bestDist <= halfGap + 0.01f ? best : null);
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
            if (useStages && stage > 0)
            {
                stageScore[stage] += pointsPerMole;
                stageCatches++;
                // 1단계: 20마리를 잡는 순간 통과 → 자동으로 2단계 전환
                if (stage == 1 && stageCatches >= stage1Goal)
                {
                    if (countdownRoutine != null) StopCoroutine(countdownRoutine);
                    countdownRoutine = StartCoroutine(Stage2Transition());
                }
            }
        }
        else
        {
            misses++;
        }
    }

    // ---------- 화면 ----------
    // 위 전광판 = "WHACK-A-MOLE" 타이틀 그림 (고정)
    // 구멍 뒤 초록 상판의 LED 화면 = 정보: 큰 점수(playHudText) + 짧은 안내 한 줄(statusText) + 3·2·1(countdownText)
    void UpdateText()
    {
        if (statusText == null) return;
        if (resultBox != null) resultBox.SetActive(false);   // 결과는 큰 점수로 대신 보여 줌
        string big, small;
        Color bigColor = new Color(1f, 0.85f, 0.15f), smallColor = Color.white;
        bool bigTextShowing = countdownText != null && countdownText.gameObject.activeSelf;
        if (Playing && bigTextShowing)
        {
            big = ""; small = "";   // GO!가 완전히 사라진 뒤에 점수 표시
        }
        else if (Playing)
        {
            int tl = Mathf.CeilToInt(Mathf.Max(timeLeft, 0f));
            int total = useStages ? stageScore[1] + stageScore[2] : hits * pointsPerMole;
            big = $"SCORE {total}";
            small = useStages && stage > 0
                ? $"STAGE {stage}   TIME {tl}   " + (stage == 1 ? $"MOLES {stageCatches}/{stage1Goal}" : "CUTE MOLES!")
                : $"TIME {tl}   HIT {hits}   MISS {misses}";
            if (tl <= 5) smallColor = new Color(1f, 0.5f, 0.4f);
        }
        else if (CountingDown)
        {
            big = ""; small = "";   // 3·2·1과 단계 안내는 countdownText가 같은 화면에 크게
        }
        else if (lastResult.Length > 0)
        {
            int total = useStages ? stageScore[1] + stageScore[2] : hits * pointsPerMole;
            big = $"SCORE {total}";
            small = "PRESS [A] TO PLAY AGAIN";
        }
        else
        {
            big = "READY?";
            small = "PRESS [A] TO START";
        }
        SetLine(playHudText, big, bigColor, 0.95f, 0.62f);
        SetLine(statusText, small, smallColor, 0.95f, 0.3f);
    }

    // 상판 화면 안에 한 줄 표시 (폭·높이 비율에 맞게 자동 축소)
    void SetLine(TextMesh t, string text, Color color, float fillW, float fillH)
    {
        if (t == null) return;
        bool on = !string.IsNullOrEmpty(text);
        if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        if (!on || (t.text == text && t.color == color)) return;
        t.text = text;
        t.color = color;
        if (!lineBaseScale.ContainsKey(t)) lineBaseScale[t] = t.transform.localScale;
        t.transform.localScale = lineBaseScale[t];
        var r = t.GetComponent<MeshRenderer>();
        Bounds b = r.localBounds; Vector3 ls = t.transform.lossyScale;
        float w = b.size.x * ls.x, h = b.size.y * ls.y;
        if (w > 0f && h > 0f)
        {
            float k = Mathf.Min(1f, screenSize.x * fillW / w, screenSize.y * fillH / h);
            if (k < 1f) t.transform.localScale *= k;
        }
    }

    readonly System.Collections.Generic.Dictionary<TextMesh, Vector3> lineBaseScale = new System.Collections.Generic.Dictionary<TextMesh, Vector3>();
}

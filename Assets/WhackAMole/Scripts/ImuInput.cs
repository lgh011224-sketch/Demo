// 하우징(검 모양) 끝에 붙인 가속도·자이로 센서 1개(GY-6500 / GY-9250 = MPU-6500 / MPU-9250)로 하우징의 "회전"을 그대로 읽는 스크립트
//
// 아두이노(Arduino/ImuHaptic/ImuHaptic.ino)가 115200bps로 1초에 약 200번 한 줄씩 보냅니다:
//     I <dt μs> <ax> <ay> <az> <gx> <gy> <gz>      (센서 원시값: 가속도 ±4g, 자이로 ±1000°/s)
//     FIRE / CENTER                                 (하우징 버튼, 선택)
// 자이로(회전 속도)를 적분하고 가속도(중력)로 기울기를 보정해서 하우징의 3D 회전(Orientation)을 계산합니다.
//     Orientation = 하우징의 회전 (앞 = 칼끝 방향, 위 = 칼등 방향). 가운데 맞추기 때 칼끝이 향한 방향이 정면(+Z)
//     Yaw   = 칼끝의 좌우 방향 (도, + = 오른쪽)
//     Pitch = 칼끝의 위아래 기울기 (도, + = 위, 수평 = 0)
// 내려치기: 칼끝이 빠르게 아래로 회전하다가 멈추면(또는 충격이 오면) Strike 이벤트.
//
// ── 처음 한 번: 센서 붙인 방향 맞추기 (자세 2개) ─────────────────────
//   ① 칼끝을 하늘로 똑바로 세우고 가만히  → 센서의 어느 쪽이 칼끝 방향인지
//   ② 칼을 정면으로 수평으로 들고 가만히  → 센서의 어느 쪽이 칼등(위) 방향인지 + 가운데 맞추기
//   VR 화면 앞에 안내가 뜹니다. 결과는 저장되어 다음부터는 바로 사용 (센서를 다시 붙였을 때만 다시 하면 됨)
//   시작: 저장된 값이 없으면 자동 / 오른손 썸스틱 누르기 / 키보드 K
//
// 가운데 맞추기(좌우 기준만 다시): 오른손 그립 / 키보드 C / 아두이노 "CENTER" / 두더지 게임 시작 때 자동
// 센서 하나로는 위치(이동)를 알 수 없으므로 회전만 사용합니다. 자이로는 좌우가 조금씩 틀어지니 가끔 가운데 맞추기를 해 주세요.
// 센서가 없을 때(에디터 시험용): 마우스 이동 = 방향, 마우스 왼쪽 클릭 = 내려치기
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ImuInput : MonoBehaviour
{
    public static ImuInput Instance { get; private set; }

    public ArduinoSerial arduino;

    [Header("센서 붙인 방향 (자세 맞추기로 자동 설정·저장)")]
    [Tooltip("센서 좌표에서 칼끝 방향")]
    public Vector3 forwardInSensor = Vector3.right;
    [Tooltip("센서 좌표에서 칼등(위) 방향")]
    public Vector3 upInSensor = Vector3.forward;
    [Tooltip("저장된 자세 맞추기 값이 없으면 시작할 때 자동으로 안내")]
    public bool autoCalibrateIfMissing = true;
    [Tooltip("안내 문구를 보여 줄 글자 (머리 앞에 붙어 있음)")]
    public TextMesh promptText;

    [Header("센서 값 변환 (아두이노 설정과 같아야 함)")]
    public float accelLsbPerG = 8192f;    // ±4g
    public float gyroLsbPerDps = 32.8f;   // ±1000°/s
    [Tooltip("가속도로 기울기를 보정하는 세기 (클수록 빨리 보정, 대신 흔들림에 민감)")]
    public float tiltCorrection = 1.5f;
    [Tooltip("시작할 때 자이로 영점을 잡는 시간 (s). 이 동안 하우징을 가만히 두세요")]
    public float gyroCalibSeconds = 1.5f;
    [Tooltip("이보다 느린 회전(°/s)이 0.4초 이어지면 '가만히 있음' → 방향 고정 + 영점 다듬기 (좌우가 저절로 돌아가는 것 방지)")]
    public float stillGyroDps = 1.5f;

    [Header("내려치기 감지")]
    [Tooltip("이 속도(°/s) 이상으로 아래로 휘둘러야 내려치기로 인정")]
    public float strikeMinDownSpeed = 150f;
    [Tooltip("최고 속도의 이 비율 아래로 갑자기 멈추면 '맞음' 순간으로 봄")]
    [Range(0.1f, 0.9f)] public float strikeStopRatio = 0.35f;
    [Tooltip("이 세기(g) 이상의 충격이 오면 바로 '맞음'")]
    public float strikeImpactG = 2.2f;
    [Tooltip("한 번 친 뒤 다음 내려치기를 받기까지 최소 시간 (s)")]
    public float strikeRearmSeconds = 0.25f;

    [Header("센서 없을 때 시험 (에디터)")]
    public bool simulateWhenNoSensor = true;
    public float mouseDegPerPixel = 0.12f;

    [Header("상태 (보기용)")]
    [SerializeField] string status = "센서 기다리는 중";
    [SerializeField] float yawView, pitchView, rollView;

    public Quaternion Orientation { get; private set; } = Quaternion.identity;
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float DownSpeed { get; private set; }
    public bool HasSensor => Time.unscaledTime - lastSampleTime < 0.5f;
    public bool Calibrated { get; private set; }
    public bool AxisCalibrating => calStep > 0;
    // 마우스 시험은 아두이노가 아예 연결 안 됐을 때만 (연결돼 있는데 센서가 끊긴 경우엔 마우스로 바꾸지 않고 화면에 알림)
    public bool Simulating => simulateWhenNoSensor && !HasSensor && !(arduino != null && arduino.IsOpen);
    public string Status => status;

    public event Action<float> Strike;
    public event Action Fire;
    public event Action Recentered;

    const string PrefFwd = "ImuForwardInSensor", PrefUp = "ImuUpInSensor";

    // 자세 (센서 좌표 → 세계 좌표, 오른손 좌표계, 세계 Z = 위)
    Quaternion q = Quaternion.identity;
    Vector3 gyroBias, biasSum;
    int biasCount;
    float biasStart = -1f, lastSampleTime = -10f, sensorTime;
    float heading0;          // 가운데 맞추기 때 칼끝의 세계 방향 (Unity 기준, 도)
    float prevPitch; bool hasPrev;
    float peakDown, lastStrikeTime = -10f;
    bool armed = true;
    bool prevGrip, prevStick;
    // 자세 맞추기 (1 = 칼끝 위로, 2 = 수평으로)
    int calStep;
    float stillTime;
    Vector3 stillSum; int stillCount;
    bool movedSinceStep;
    Vector3 stepStartUp;
    Vector3 accelSmooth;
    Vector3 gyroSmooth;
    float accelMagSmooth = 1f, stillFor;
    public bool IsStill { get; private set; }
    float promptUntil;

    void Awake()
    {
        Instance = this;
        if (arduino == null) arduino = GetComponent<ArduinoSerial>();
        LoadAxes();
    }

    void OnEnable()
    {
        if (arduino != null) { arduino.enabled = true; arduino.OnLine += OnLine; }
    }

    void Start()
    {
        if (arduino != null && !arduino.IsOpen) arduino.Open();
        ShowPrompt(null);
    }

    void OnDisable()
    {
        if (arduino != null) arduino.OnLine -= OnLine;
        if (Instance == this) Instance = null;
    }

    void LoadAxes()
    {
        if (PlayerPrefs.HasKey(PrefFwd) && PlayerPrefs.HasKey(PrefUp))
        {
            forwardInSensor = Parse(PlayerPrefs.GetString(PrefFwd), forwardInSensor);
            upInSensor = Parse(PlayerPrefs.GetString(PrefUp), upInSensor);
        }
    }

    public bool HasSavedAxes => PlayerPrefs.HasKey(PrefFwd) && PlayerPrefs.HasKey(PrefUp);

    static Vector3 Parse(string s, Vector3 fallback)
    {
        var p = s.Split(',');
        if (p.Length != 3) return fallback;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return float.TryParse(p[0], System.Globalization.NumberStyles.Float, ci, out float x) &&
               float.TryParse(p[1], System.Globalization.NumberStyles.Float, ci, out float y) &&
               float.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out float z) ? new Vector3(x, y, z) : fallback;
    }

    static string Str(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);

    // ───────────── 아두이노 메시지 ─────────────
    public void OnLine(string line)
    {
        if (line.Length > 2 && line[0] == 'I' && line[1] == ' ') { ParseSample(line); return; }
        string msg = line.Trim().ToUpperInvariant();
        if (msg == "FIRE" || msg == "BTN") Fire?.Invoke();
        else if (msg == "CENTER") Recenter();
        else if (msg.Contains("IMU")) status = msg.Substring(msg.IndexOf("IMU"));   // 재시작 직후 잡음 글자 제거
    }

    readonly int[] vals = new int[7];

    void ParseSample(string line)
    {
        int n = 0, i = 2, len = line.Length;
        while (i < len && n < 7)
        {
            while (i < len && line[i] == ' ') i++;
            int sign = 1, v = 0; bool any = false;
            if (i < len && line[i] == '-') { sign = -1; i++; }
            while (i < len && line[i] >= '0' && line[i] <= '9') { v = v * 10 + (line[i] - '0'); i++; any = true; }
            if (!any) return;
            vals[n++] = v * sign;
        }
        if (n < 7) return;
        float dt = Mathf.Clamp(vals[0] * 1e-6f, 0.0005f, 0.05f);
        AddSample(dt, new Vector3(vals[1], vals[2], vals[3]) / accelLsbPerG, new Vector3(vals[4], vals[5], vals[6]) / gyroLsbPerDps);
    }

    // 센서 값 하나 처리 (a = g 단위, gDps = °/s). 시험용으로 직접 불러도 됨
    public void AddSample(float dt, Vector3 a, Vector3 gDps)
    {
        lastSampleTime = Time.unscaledTime;
        sensorTime += dt;

        // 1) 처음: 자이로 영점
        if (!Calibrated)
        {
            if (biasStart < 0f || gDps.magnitude > 20f)
            {
                biasStart = sensorTime; biasSum = Vector3.zero; biasCount = 0;
                status = "영점 잡는 중 – 하우징을 가만히 두세요";
                if (gDps.magnitude > 20f) return;
            }
            biasSum += gDps; biasCount++;
            if (sensorTime - biasStart >= gyroCalibSeconds && biasCount > 20)
            {
                gyroBias = biasSum / biasCount;
                q = Quaternion.FromToRotation(a.normalized, Vector3.forward);
                Calibrated = true;
                status = "센서 준비 완료";
                if (autoCalibrateIfMissing && !HasSavedAxes) BeginAxisCalibration();
                else Recenter();
            }
            return;
        }

        accelSmooth = Vector3.Lerp(accelSmooth == Vector3.zero ? a : accelSmooth, a, 0.08f);   // 그립으로 자세 저장할 때 씀

        // 2) 자세 갱신 (Mahony: 자이로 적분 + 가속도로 기울기 보정)
        // 가만히 있는지 판단 (센서 잡음이 있어 부드럽게 한 값으로): 회전 1.5°/s 미만 + 가속도 ≈ 1g
        Vector3 gc = gDps - gyroBias;
        float k = 1f - Mathf.Exp(-dt / 0.25f);
        gyroSmooth = Vector3.Lerp(gyroSmooth, gc, k);
        accelMagSmooth = Mathf.Lerp(accelMagSmooth, a.magnitude, k);
        bool stillNow = gyroSmooth.magnitude < stillGyroDps && gc.magnitude < 12f && Mathf.Abs(accelMagSmooth - 1f) < 0.05f;
        stillFor = stillNow ? stillFor + dt : 0f;
        IsStill = stillFor > 0.4f;
        if (IsStill)
        {
            // 영점 계속 다듬기: 가만히 있는 동안의 평균 회전값이 진짜 영점 (켜진 직후 값이 조금 변하므로)
            gyroBias = Vector3.Lerp(gyroBias, gDps, 1f - Mathf.Exp(-dt / 3f));
            gc = Vector3.zero;   // 가만히 있으면 방향을 고정 (남은 미세 회전이 쌓여 돌아가지 않게)
        }

        Vector3 w = gc * Mathf.Deg2Rad;
        float am = a.magnitude;
        if (am > 0.6f && am < 1.4f)
        {
            Vector3 upS = Quaternion.Inverse(q) * Vector3.forward;
            w += Vector3.Cross(a / am, upS) * tiltCorrection;
        }
        Vector3 h = w * (0.5f * dt);
        q = Normalize(q * new Quaternion(h.x, h.y, h.z, 1f));

        // 3) 자세 맞추기 중이면 가만히 있는 동안의 중력 방향을 모음
        if (calStep == 3) { CollectTwist(dt, gDps - gyroBias); return; }
        if (calStep > 0) { CollectStill(dt, a, gDps - gyroBias); return; }

        // 4) 칼의 회전 → Unity
        UpdateOrientation();

        // 5) 내려치기 감지
        float rate = hasPrev ? (Pitch - prevPitch) / dt : 0f;
        prevPitch = Pitch; hasPrev = true;
        DownSpeed = Mathf.Lerp(DownSpeed, -rate, 0.5f);
        DetectStrike(am);
    }

    // 센서 세계(오른손, Z 위) 벡터 → Unity 세계(X 오른쪽, Y 위, Z 앞) 같은 물리 방향
    static Vector3 ToUnity(Vector3 v) => new Vector3(v.x, v.z, v.y);

    void Blade(out Vector3 f, out Vector3 u)
    {
        f = ToUnity(q * forwardInSensor.normalized);
        u = ToUnity(q * upInSensor.normalized);
    }

    void UpdateOrientation()
    {
        Blade(out Vector3 f, out Vector3 u);
        Quaternion fix = Quaternion.Euler(0f, -heading0, 0f);
        f = fix * f; u = fix * u;
        Orientation = Quaternion.LookRotation(f, u);
        Yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        Pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // ───────────── 자세 맞추기 ─────────────
    public void BeginAxisCalibration()
    {
        calStep = 1; ResetStep();
        ShowPrompt("SENSOR SETUP 1/3\nPoint the tip straight UP\nthen press GRIP");
        status = "자세 맞추기 1/3";
    }

    // 한 단계 새로 시작: 모은 값 지우고, "움직였는지"도 다시 확인
    void ResetStep()
    {
        stillTime = 0f; stillSum = Vector3.zero; stillCount = 0;
        movedSinceStep = false; stepStartUp = Vector3.zero;
    }

    void CollectStill(float dt, Vector3 a, Vector3 w)
    {
        // 안내가 뜬 뒤 하우징을 실제로 들어 움직여야(회전 40°/s 이상 또는 30° 이상 기울기 변화) 그다음 정지 자세를 받음
        //  → 책상에 놓여 있던 자세를 잘못 잡지 않도록
        if (stepStartUp == Vector3.zero) stepStartUp = a.normalized;
        if (w.magnitude > 40f || Vector3.Angle(a, stepStartUp) > 30f) movedSinceStep = true;
        if (!movedSinceStep) return;
        // 손으로 들고 있으면 조금 떨리므로 넉넉하게 (회전 15°/s 미만, 1초)
        bool still = w.magnitude < 15f && a.magnitude > 0.85f && a.magnitude < 1.15f;
        if (!still) { stillTime = 0f; stillSum = Vector3.zero; stillCount = 0; return; }
        stillTime += dt; stillSum += a; stillCount++;
        if (stillTime < 1.0f) return;
        AcceptPose((stillSum / stillCount).normalized);
    }

    // 그립 버튼: 지금 자세를 바로 저장하고 다음 단계로
    public void ConfirmStep()
    {
        if (calStep == 1 || calStep == 2) AcceptPose(accelSmooth.normalized);
        else if (calStep == 3)
        {
            if (twistTime >= 0.25f) FinishTwist();
            else ShowPrompt("SENSOR SETUP 3/3\nFirst TWIST the handle\nleft and right");
        }
    }

    // g = 센서 좌표에서 본 '위' (중력 반대 방향)
    void AcceptPose(Vector3 g)
    {
        if (calStep == 1)
        {
            calTipUp = g;                                  // 칼끝이 위를 향했을 때의 위 (칼끝이 어느 쪽인지 정하는 데만 씀)
            calStep = 2; ResetStep();
            ShowPrompt("SENSOR SETUP 2/3\nHold it LEVEL, pointing FORWARD\nthen press GRIP");
            status = "자세 맞추기 2/3";
            return;
        }
        if (Vector3.Angle(g, calTipUp) < 45f)
        {
            ResetStep();
            ShowPrompt("Still pointing up\nHold it LEVEL, pointing FORWARD");
            return;
        }
        calLevelUp = g;                                    // 칼이 수평일 때의 위 = 칼등 방향
        calStep = 3; ResetTwist();
        ShowPrompt("SENSOR SETUP 3/3\nTWIST the handle left and right a few times\n(like a screwdriver) then press GRIP");
        status = "자세 맞추기 3/3";
    }

    // ③ 비틀기: 손잡이 축으로 돌리는 동안의 회전축 = 칼 방향 (가장 정확). 칼끝 쪽 부호는 ①에서 정함
    Vector3 calTipUp, calLevelUp, twistSum, twistRef;
    float twistTime, twistAbs;

    void ResetTwist() { twistSum = Vector3.zero; twistRef = Vector3.zero; twistTime = 0f; twistAbs = 0f; }

    void CollectTwist(float dt, Vector3 w)
    {
        if (w.magnitude < 40f) return;                     // 확실히 돌리는 순간만
        if (twistRef == Vector3.zero) twistRef = w.normalized;
        Vector3 s = Vector3.Dot(w, twistRef) >= 0f ? w : -w;   // 왼쪽·오른쪽 비틀기를 같은 축으로 모음
        twistSum += s * dt; twistAbs += w.magnitude * dt; twistTime += dt;
        if (twistTime < 0.8f) return;
        FinishTwist();
    }

    void FinishTwist()
    {
        Vector3 blade = twistSum.normalized;
        float purity = twistSum.magnitude / Mathf.Max(twistAbs, 1e-4f);   // 1에 가까울수록 한 축으로만 돌림
        if (purity < 0.75f)
        {
            ResetTwist();
            ShowPrompt("Do not swing it\nJust TWIST around the handle");
            return;
        }
        float tip = Vector3.Dot(blade, calTipUp);
        if (Mathf.Abs(tip) < 0.5f)
        {
            // ①에서 칼끝이 위를 향하지 않았던 것 → 처음부터 다시
            BeginAxisCalibration();
            ShowPrompt("Step 1 was not pointing up\nRetry: point the tip straight UP\nthen press GRIP");
            return;
        }
        forwardInSensor = tip > 0f ? blade : -blade;
        Vector3 up = calLevelUp - forwardInSensor * Vector3.Dot(calLevelUp, forwardInSensor);
        if (up.sqrMagnitude < 0.2f)
        {
            calStep = 2; ResetStep();
            ShowPrompt("Step 2 was not level\nHold it LEVEL, pointing FORWARD\nthen press GRIP");
            return;
        }
        upInSensor = up.normalized;
        PlayerPrefs.SetString(PrefFwd, Str(forwardInSensor));
        PlayerPrefs.SetString(PrefUp, Str(upInSensor));
        PlayerPrefs.Save();
        calStep = 0;
        status = "센서 방향 저장 완료";
        // 다시 정면으로: 사용자가 비틀기 직후라 아직 정면 근처를 보고 있음
        Recenter();
        ShowPrompt("DONE!\nPoint forward + GRIP to re-center", 2.5f);
    }

    void DetectStrike(float accelG)
    {
        float now = sensorTime;
        if (!armed)
        {
            if (now - lastStrikeTime > strikeRearmSeconds && DownSpeed < 30f) { armed = true; peakDown = 0f; }
            return;
        }
        if (DownSpeed > peakDown) peakDown = DownSpeed;
        if (DownSpeed < 0f && peakDown < strikeMinDownSpeed) peakDown = 0f;
        bool fast = peakDown >= strikeMinDownSpeed;
        bool stopped = DownSpeed < peakDown * strikeStopRatio;
        bool impact = accelG >= strikeImpactG && DownSpeed > strikeMinDownSpeed * 0.5f;
        if (fast && (stopped || impact)) FireStrike(peakDown);
    }

    void FireStrike(float speed)
    {
        armed = false;
        lastStrikeTime = sensorTime;
        peakDown = 0f;
        Strike?.Invoke(speed);
    }

    // 지금 칼끝이 향한 좌우 방향을 정면으로 (기울기는 그대로)
    public void Recenter()
    {
        if (Calibrated && HasSensor && calStep == 0)
        {
            Blade(out Vector3 f, out _);
            heading0 = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            hasPrev = false;
            UpdateOrientation();
            if (Time.unscaledTime > promptUntil) ShowPrompt("CENTERED", 0.8f);
        }
        else if (Simulating) { Yaw = 0f; }
        Recentered?.Invoke();
    }

    void ShowPrompt(string text, float seconds = 0f)
    {
        if (promptText == null) return;
        promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        if (!string.IsNullOrEmpty(text)) promptText.text = text;
        promptUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
    }

    void Update()
    {
        // 그립(중지) 버튼: 오른손·왼손 어느 쪽이든
        bool grip = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.gripButton) || RightHandFollower.GetLeftButton(UnityEngine.XR.CommonUsages.gripButton);
        bool stick = RightHandFollower.GetButton(UnityEngine.XR.CommonUsages.primary2DAxisClick);
        bool keyC = false, keyK = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) { keyC = Keyboard.current.cKey.wasPressedThisFrame; keyK = Keyboard.current.kKey.wasPressedThisFrame; }
#endif
        bool gripDown = (grip && !prevGrip) || keyC;
        if (gripDown && calStep > 0) ConfirmStep();   // 방향 맞추기 중: 지금 자세 저장 → 다음 단계
        else if (gripDown) Recenter();
        if (((stick && !prevStick) || keyK) && Calibrated) BeginAxisCalibration();
        prevGrip = grip; prevStick = stick;

        // 시작할 때 자이로 영점 잡는 동안 안내
        if (HasSensor && !Calibrated) ShowPrompt("CALIBRATING...\nPut it down and keep it still", 0.3f);
        if (calStep == 0 && promptText != null && promptText.gameObject.activeSelf && Time.unscaledTime > promptUntil) ShowPrompt(null);

        if (Simulating) Simulate();
        else if (!HasSensor)
        {
            status = arduino != null && arduino.IsOpen ? "센서 값이 안 들어옴 (배선 확인)" : "아두이노 연결 안 됨";
            // 아두이노는 연결됐는데 센서 값이 끊기면 VR 화면에 알림 (다시 들어오면 자동으로 사라짐)
            if (arduino != null && arduino.IsOpen && Time.unscaledTime > 4f && calStep == 0) ShowPrompt("SENSOR DISCONNECTED\nCheck the wiring (SDA / SCL / NCS)", 0.5f);
        }
        yawView = Yaw; pitchView = Pitch; rollView = Orientation.eulerAngles.z;
    }

    float simPitch;
    float simSlam = -1f;

    void Simulate()
    {
        status = "센서 없음 – 마우스로 시험 중";
#if ENABLE_INPUT_SYSTEM
        Mouse m = Mouse.current;
        if (m != null)
        {
            Vector2 d = m.delta.ReadValue();
            Yaw = Mathf.Clamp(Yaw + d.x * mouseDegPerPixel, -80f, 80f);
            simPitch = Mathf.Clamp(simPitch + d.y * mouseDegPerPixel, -60f, 40f);
            if (m.leftButton.wasPressedThisFrame) simSlam = 0f;
        }
#endif
        float p = simPitch;
        if (simSlam >= 0f)
        {
            simSlam += Time.deltaTime;
            p = simPitch - 50f * Mathf.Sin(Mathf.Clamp01(simSlam / 0.25f) * Mathf.PI);
            if (simSlam >= 0.12f && armed) FireStrike(400f);
            if (simSlam >= 0.25f) { simSlam = -1f; armed = true; }
        }
        Pitch = p;
        Orientation = Quaternion.Euler(-Pitch, Yaw, 0f);
    }

    public void SimulateStrike() => FireStrike(400f);

    static Quaternion Normalize(Quaternion a)
    {
        float m = Mathf.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w);
        return m > 1e-9f ? new Quaternion(a.x / m, a.y / m, a.z / m, a.w / m) : Quaternion.identity;
    }
}

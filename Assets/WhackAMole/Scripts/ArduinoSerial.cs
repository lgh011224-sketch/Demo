// 아두이노와 USB 시리얼로 주고받는 스크립트 (Windows PC 전용)
// - 아두이노가 보내는 한 줄(예: "# BUTTON PRESSED")을 받아 OnLine 이벤트로 알려줍니다.
// - Send("UP") 처럼 명령을 보냅니다. 세미콜론(;)으로 여러 명령을 한 번에 보낼 수 있습니다.
// - 포트는 씬이 바뀌어도 닫지 않고 계속 씁니다 (게임 종료 시에만 닫음).
//   → 씬 전환 때 아두이노가 재시작되지 않아 스테퍼 위치 기억이 유지됩니다.
// - 포트를 처음 열면 아두이노가 재시작되므로, "READY"가 오거나 2.5초가 지날 때까지 보낼 명령을 모아 두었다가 보냅니다.
// 주의: Project Settings > Player > Api Compatibility Level 을 ".NET Framework" 로 바꿔야 동작합니다.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

// 하우징(Arduino/PartsTest) 상태 기억 - 씬이 바뀌어도 유지 (같은 명령을 두 번 보내 끝을 넘어가지 않도록)
//   "1" 스테퍼 위로 6000스텝 / "2" 아래로 6000스텝 / "4" 스크류 밀기 / "5" 스크류 당기기
//   게임 시작(포트를 처음 열어 아두이노 재시작) 시점: 스테퍼 맨 아래, 스크류 당겨진 상태로 가정
public static class HousingState
{
    public static bool StepperUp;
    public static bool ScrewPushed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { StepperUp = false; ScrewPushed = false; }

    public static void SetStepper(ArduinoSerial arduino, bool up, string upCmd = "1", string downCmd = "2")
    {
        if (arduino == null || StepperUp == up) return;
        StepperUp = up;
        arduino.Send(up ? upCmd : downCmd);
    }

    public static void SetScrew(ArduinoSerial arduino, bool push, string pushCmd = "4", string pullCmd = "5")
    {
        if (arduino == null || ScrewPushed == push) return;
        ScrewPushed = push;
        arduino.Send(push ? pushCmd : pullCmd);
    }
}

public class ArduinoSerial : MonoBehaviour
{
    [Tooltip("장치 관리자 > 포트(COM & LPT)에 보이는 아두이노 포트 이름")]
    public string portName = "COM3";
    [Tooltip("아두이노 코드의 Serial.begin(...) 값과 같아야 합니다 (HousingControl = 115200)")]
    public int baudRate = 115200;
    [Tooltip("시작할 때 자동으로 연결")]
    public bool connectOnStart = true;
    [Tooltip("받은 메시지를 Console 창에 표시")]
    public bool logIncoming = true;
    [Tooltip("이 글자로 시작하는 메시지는 표시하지 않음 (센서 값 \"I ...\"는 1초에 200번 오므로)")]
    public string quietPrefix = "I ";

    [Tooltip("보낸 명령을 Console 창에 표시 (연결 안 됐을 때도 '보냈을 명령'으로 표시)")]
    public bool logOutgoing = true;
    [Tooltip("최근 보낸 명령 (확인용, 최신이 맨 아래)")]
    public List<string> recentSent = new List<string>();
    const int RecentMax = 30;
    [Tooltip("포트를 연 뒤 아두이노가 재시작을 마칠 때까지 기다리는 최대 시간 (s)")]
    public float bootWaitSeconds = 2.5f;
    [Tooltip("게임을 끌 때 보내는 명령. PartsTest 스케치는 숫자 명령만 쓰므로 비워 둠 (값을 넣어도 무시됨)")]
    public string quitCommand = "";
    static string sQuitCommand = "";

    public event Action<string> OnLine;

    void Remember(string cmd, bool delivered)
    {
        recentSent.Add($"{Time.time,7:0.00}s  {cmd}{(delivered ? "" : "  (미연결)")}");
        if (recentSent.Count > RecentMax) recentSent.RemoveAt(0);
    }

    // ── 모든 씬이 함께 쓰는 포트 (static) ──
    static readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
    static readonly List<string> outbox = new List<string>();   // 아두이노 재시작 중에 보낸 명령
    static float readyAt;                                       // 이 시각(realtime) 이후 바로 전송
    static bool booting;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    static SerialPort port;
    static Thread readThread;
    static volatile bool running;
    static bool quitHooked;
    public bool IsOpen => port != null && port.IsOpen;
#else
    public bool IsOpen => false;
#endif

    void Start()
    {
        sQuitCommand = "";   // PartsTest 스케치: 종료 명령 없음 (HOME 같은 글자를 보내지 않음)
        if (connectOnStart) Open();
    }

    public void Open()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (IsOpen) return;   // 앞 씬에서 열어 둔 포트를 그대로 사용
        try
        {
            port = new SerialPort(portName, baudRate);
            port.NewLine = "\n";
            port.ReadTimeout = 100;
            port.WriteTimeout = 100;
            port.DtrEnable = true;
            port.Open();
            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true };
            readThread.Start();
            booting = true;
            readyAt = Time.realtimeSinceStartup + bootWaitSeconds;
            if (!quitHooked) { Application.quitting += CloseShared; quitHooked = true; }
            Debug.Log($"[Arduino] {portName} 연결됨 ({baudRate}bps). 아두이노 재시작이 끝나면 모아 둔 명령을 보냅니다.");
        }
        catch (Exception e)
        {
            port = null;
            Debug.LogWarning($"[Arduino] {portName} 연결 실패: {e.Message}\n- 포트 이름 확인\n- 아두이노 IDE 시리얼 모니터를 닫았는지 확인\n- Api Compatibility Level이 .NET Framework인지 확인");
        }
#else
        Debug.LogWarning("[Arduino] 시리얼 통신은 Windows PC에서만 동작합니다.");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    static void ReadLoop()
    {
        while (running)
        {
            try
            {
                string line = port.ReadLine();
                if (line == null) continue;
                line = line.Trim();
                if (line.Length > 0) inbox.Enqueue(line);
            }
            catch (TimeoutException) { }
            catch (Exception)
            {
                if (!running) break;
                Thread.Sleep(50);
            }
        }
    }
#endif

    void Update()
    {
        while (inbox.TryDequeue(out string line))
        {
            if (booting && line.ToUpperInvariant().Contains("READY")) readyAt = 0f;   // 아두이노가 준비됐다고 알림
            if (logIncoming && (string.IsNullOrEmpty(quietPrefix) || !line.StartsWith(quietPrefix))) Debug.Log("[Arduino →] " + line);
            OnLine?.Invoke(line);
        }
        if (booting && Time.realtimeSinceStartup >= readyAt)
        {
            booting = false;
            var pending = outbox.ToArray();
            outbox.Clear();
            foreach (string cmd in pending) Write(cmd);
        }
    }

    public void Send(string commands)
    {
        if (string.IsNullOrWhiteSpace(commands)) return;
        foreach (string raw in commands.Split(';'))
        {
            string cmd = raw.Trim();
            if (cmd.Length == 0) continue;
            if (IsOpen && booting)
            {
                outbox.Add(cmd);   // 재시작 중 → 준비되면 보냄
                if (logOutgoing) Debug.Log("[→ Arduino (대기)] " + cmd);
                continue;
            }
            Write(cmd);
        }
    }

    void Write(string cmd)
    {
        bool delivered = false;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (IsOpen)
        {
            try { port.WriteLine(cmd); delivered = true; }
            catch (Exception e) { Debug.LogWarning("[Arduino] 전송 실패: " + e.Message); }
        }
#endif
        Remember(cmd, delivered);
        // 연결이 없어도 무엇을 보냈을지 확인할 수 있게 로그로 남김
        if (logOutgoing) Debug.Log(delivered ? "[→ Arduino] " + cmd : "[→ Arduino (미연결·확인용)] " + cmd);
    }

    // 포트를 완전히 닫음 (게임 종료 시 자동 호출). 씬 전환 때는 닫지 않음
    public void Close() => CloseShared();

    static void CloseShared()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // 다음 실행 때 아두이노가 재시작하며 '맨 아래·스크류 들어감'으로 가정하므로 그 상태로 돌려 둠
        // PartsTest: 스테퍼가 올라가 있으면 "2"(내리기), 스크류가 밀려 있으면 "5"(당기기)를 보내 원위치
        if (port != null && port.IsOpen && !booting)
        {
            string home = (HousingState.StepperUp ? "2" : "") + (HousingState.ScrewPushed ? "5" : "") + sQuitCommand.Trim();
            if (home.Length > 0)
            {
                try { port.WriteLine(home); Thread.Sleep(50); } catch { }
            }
        }
        HousingState.StepperUp = false;
        HousingState.ScrewPushed = false;
        running = false;
        try { if (readThread != null && readThread.IsAlive) readThread.Join(300); } catch { }
        try { if (port != null && port.IsOpen) port.Close(); } catch { }
        port = null;
        readThread = null;
#endif
        booting = false;
        outbox.Clear();
        while (inbox.TryDequeue(out _)) { }
    }
}

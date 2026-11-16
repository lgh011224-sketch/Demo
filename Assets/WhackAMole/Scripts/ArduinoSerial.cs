// 아두이노와 USB 시리얼로 주고받는 스크립트 (Windows PC 전용)
// - 아두이노가 보내는 한 줄(예: "HIT")을 받아 OnLine 이벤트로 알려줍니다.
// - Send("m3 on") 처럼 명령을 보냅니다. 세미콜론(;)으로 여러 명령을 한 번에 보낼 수 있습니다.
// 주의: Project Settings > Player > Api Compatibility Level 을 ".NET Framework" 로 바꿔야 동작합니다.
using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

public class ArduinoSerial : MonoBehaviour
{
    [Tooltip("장치 관리자 > 포트(COM & LPT)에 보이는 아두이노 포트 이름")]
    public string portName = "COM3";
    [Tooltip("아두이노 코드의 Serial.begin(...) 값과 같아야 합니다 (센서 스케치 ImuHaptic = 115200)")]
    public int baudRate = 115200;
    [Tooltip("모듈 버전이 아니면 게임 매니저가 자동으로 끕니다")]
    public bool connectOnStart = true;
    [Tooltip("받은 메시지를 Console 창에 표시")]
    public bool logIncoming = true;
    [Tooltip("이 글자로 시작하는 메시지는 표시하지 않음 (센서 값 \"I ...\"는 1초에 200번 오므로)")]
    public string quietPrefix = "I ";

    [Tooltip("보낸 명령을 Console 창에 표시 (연결 안 됐을 때도 '보냈을 명령'으로 표시)")]
    public bool logOutgoing = true;
    [Tooltip("최근 보낸 명령 (확인용, 최신이 맨 아래)")]
    public System.Collections.Generic.List<string> recentSent = new System.Collections.Generic.List<string>();
    const int RecentMax = 30;

    public event Action<string> OnLine;

    void Remember(string cmd, bool delivered)
    {
        recentSent.Add($"{Time.time,7:0.00}s  {cmd}{(delivered ? "" : "  (미연결)")}");
        if (recentSent.Count > RecentMax) recentSent.RemoveAt(0);
    }

    readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    SerialPort port;
    Thread readThread;
    volatile bool running;
    public bool IsOpen => port != null && port.IsOpen;
#else
    public bool IsOpen => false;
#endif

    void Start()
    {
        if (connectOnStart) Open();
    }

    public void Open()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (IsOpen) return;
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
            Debug.Log($"[Arduino] {portName} 연결됨 ({baudRate}bps). 아두이노가 재시작되므로 약 2초 뒤부터 명령이 전달됩니다.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Arduino] {portName} 연결 실패: {e.Message}\n- 포트 이름 확인\n- 아두이노 IDE 시리얼 모니터를 닫았는지 확인\n- Api Compatibility Level이 .NET Framework인지 확인");
        }
#else
        Debug.LogWarning("[Arduino] 시리얼 통신은 Windows PC에서만 동작합니다.");
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    void ReadLoop()
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
            if (logIncoming && (string.IsNullOrEmpty(quietPrefix) || !line.StartsWith(quietPrefix))) Debug.Log("[Arduino →] " + line);
            OnLine?.Invoke(line);
        }
    }

    public void Send(string commands)
    {
        if (string.IsNullOrWhiteSpace(commands)) return;
        foreach (string raw in commands.Split(';'))
        {
            string cmd = raw.Trim();
            if (cmd.Length == 0) continue;
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
    }

    public void Close()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        running = false;
        try { if (readThread != null && readThread.IsAlive) readThread.Join(300); } catch { }
        try { if (port != null && port.IsOpen) port.Close(); } catch { }
        port = null;
#endif
    }

    void OnDestroy() { Close(); }
    void OnApplicationQuit() { Close(); }
}

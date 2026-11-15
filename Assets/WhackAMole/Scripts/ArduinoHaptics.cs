// 컨트롤러 진동을 아두이노로도 똑같이 내보내는 다리 역할
//   RightHandFollower.Vibrate(세기, 시간)이 불릴 때마다 아두이노로 한 줄을 보냅니다.
//   형식 (기본): "VIB <세기 0~255> <시간 ms>"   예) VIB 128 80
//
// 지금 보내는 진동 (두더지 잡기 / 사격장)
//   귀여운 두더지 맞힘   VIB 128 80     (0.5)
//   초록 두더지 맞힘     VIB 255 80     (1.0)
//   틀린 도구            VIB 38 200     (0.15, 길게)
//   도구 전환            VIB 89 40
//   카운트다운 3·2·1     VIB 64 50,  START!  VIB 178 150
//   권총 발사            VIB 255 70
//   빈 격발(딸깍)        VIB 26 30
//   표적 도착/출발       VIB 51 40 / VIB 76 50
// 아두이노가 연결되지 않아도 "보냈을 명령"이 Console과 ArduinoSerial 인스펙터(Recent Sent)에 남아 확인할 수 있습니다.
using UnityEngine;

public class ArduinoHaptics : MonoBehaviour
{
    public ArduinoSerial arduino;
    [Tooltip("체크하면 진동을 아두이노로 보냄 (컨트롤러 버전에서도 동작)")]
    public bool sendToArduino = true;
    [Tooltip("보낼 문자열 형식: {0}=세기(0~255), {1}=시간(ms)")]
    public string format = "VIB {0} {1}";
    [Tooltip("이 값보다 약한 진동은 보내지 않음 (0~255)")]
    public int minPwm = 1;

    void OnEnable() { RightHandFollower.Vibrated += OnVibrate; }
    void OnDisable() { RightHandFollower.Vibrated -= OnVibrate; }

    void Start()
    {
        if (arduino == null) arduino = GetComponent<ArduinoSerial>();
        // ArduinoSerial이 스스로 연결하지 않게 설정된 경우에만 대신 연결 (중복 연결 시도·경고 방지)
        if (sendToArduino && arduino != null && !arduino.connectOnStart && !arduino.IsOpen) arduino.Open();
    }

    void OnVibrate(float amplitude, float seconds)
    {
        if (!sendToArduino || arduino == null) return;
        int pwm = Mathf.RoundToInt(Mathf.Clamp01(amplitude) * 255f);
        if (pwm < minPwm) return;
        int ms = Mathf.Max(1, Mathf.RoundToInt(seconds * 1000f));
        arduino.Send(string.Format(format, pwm, ms));
    }
}

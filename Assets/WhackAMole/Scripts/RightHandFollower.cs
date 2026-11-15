// 오른손 Quest 컨트롤러의 위치·방향을 따라가는 스크립트
// 이 오브젝트(ToolRoot) 아래에 나무 망치, 쇠 망치, 총이 들어 있습니다.
// 반드시 XR Origin 안의 "Camera Offset" 아래에 두어야 위치가 맞습니다. (장면 만들기 메뉴가 자동으로 해 줍니다)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class RightHandFollower : MonoBehaviour
{
    [Header("도구를 손에 맞추는 보정값")]
    [Tooltip("컨트롤러 기준 도구 위치 보정 (m)")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("컨트롤러 기준 도구 회전 보정 (도). 모듈에 컨트롤러를 거꾸로 꽂으면 X를 180 근처로 조정")]
    public Vector3 rotationOffset = Vector3.zero;

    static readonly List<InputDevice> devices = new List<InputDevice>();

    public static InputDevice GetRightController()
    {
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
        return devices.Count > 0 ? devices[0] : default;
    }

    public static bool GetButton(InputFeatureUsage<bool> usage)
    {
        InputDevice d = GetRightController();
        return d.isValid && d.TryGetFeatureValue(usage, out bool v) && v;
    }

    // 왼손 컨트롤러 버튼 (도구 전환용 검지 트리거 등)
    static readonly List<InputDevice> leftDevices = new List<InputDevice>();

    public static bool GetLeftButton(InputFeatureUsage<bool> usage)
    {
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, leftDevices);
        if (leftDevices.Count == 0) return false;
        InputDevice d = leftDevices[0];
        return d.isValid && d.TryGetFeatureValue(usage, out bool v) && v;
    }

    // 진동이 날 때마다 알림 (세기 0~1, 시간 초) – ArduinoHaptics가 받아 아두이노로 같은 진동을 내보냄
    public static event System.Action<float, float> Vibrated;

    public static void Vibrate(float amplitude, float seconds)
    {
        Vibrated?.Invoke(Mathf.Clamp01(amplitude), seconds);
        InputDevice d = GetRightController();
        if (d.isValid) d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
    }

    void OnEnable() { Application.onBeforeRender += Follow; }
    void OnDisable() { Application.onBeforeRender -= Follow; }
    void Update() { Follow(); }

    void Follow()
    {
        InputDevice d = GetRightController();
        if (!d.isValid) return;
        if (d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos) &&
            d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
        {
            Quaternion r = rot * Quaternion.Euler(rotationOffset);
            transform.localPosition = pos + rot * positionOffset;
            transform.localRotation = r;
        }
    }
}

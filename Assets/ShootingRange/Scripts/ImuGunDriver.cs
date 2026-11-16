// [센서 버전] 사격장 권총: 몸체는 내 시점(머리)에 고정, 총구 방향만 하우징 센서(ImuInput)를 따라 돌아갑니다. (ToolRoot에 붙임)
//  - 위치: 머리 기준 오른쪽 아래 viewOffset 자리 (머리를 돌리면 따라옴)
//  - 방향: 하우징의 좌우(Yaw)·위아래(Pitch) 각도. 가운데 맞추기 했을 때 = 사로(표적) 정면
//  - 발사: 오른손 컨트롤러 트리거 / 키보드 F / 아두이노 "FIRE" 버튼 (ShootingRangeManager가 처리)
// 이 스크립트가 켜져 있는 동안은 컨트롤러 위치 추적(RightHandFollower)을 끕니다.
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ImuGunDriver : MonoBehaviour
{
    public ImuInput imu;
    [Tooltip("총구 (앞쪽 = 발사 방향)")]
    public Transform muzzle;
    [Tooltip("머리 (Main Camera)")]
    public Transform head;

    [Header("몸체 위치 (머리 기준, m)")]
    public Vector3 viewOffset = new Vector3(0f, -0.11f, 0.42f);   // 시야 가운데, 눈높이보다 조금 아래
    [Tooltip("머리를 위아래로 숙여도 총 높이는 그대로 (좌우 회전만 따라감)")]
    public bool followHeadYawOnly = true;

    [Header("조준")]
    [Tooltip("사로 정면 방향 (세계 Y 각도, 도). 표적이 +Z 방향이면 0")]
    public float rangeForwardYaw = 0f;
    [Tooltip("하우징 각도에 곱하는 배율 (1 = 그대로)")]
    public float aimGain = 1f;
    [Tooltip("총 모양 회전 보정 (총이 옆으로 누워 보이면 Z를 조절)")]
    public float rollOffset = 0f;
    [Tooltip("조준 떨림 줄이기 (클수록 빠르게 따라감)")]
    public float smoothing = 30f;
    [Tooltip("총구 레이저 (총이 움직인 직후 갱신)")]
    public LaserSight laser;

    [Header("정면 자세 (이 자세로 들면 표적 정면을 겨눔)")]
    public bool useRestPose = true;
    [Tooltip("정면을 겨누는 자세로 가만히 들었을 때의 가속도 센서 값 (센서 좌표, g)")]
    public Vector3 restAccel = new Vector3(1.0128f, 0.0238f, 0.0813f);

    // 정면 자세의 회전 (좌우는 0, 위아래·비틀기는 그 자세 그대로) – 센서 붙인 방향(ImuInput)으로 계산
    Quaternion RestRotation()
    {
        if (!useRestPose || imu == null || restAccel.sqrMagnitude < 0.25f) return Quaternion.identity;
        Vector3 g = restAccel.normalized;                         // 센서 좌표에서 본 '위'
        Vector3 f = imu.forwardInSensor.normalized, u = imu.upInSensor.normalized;
        Vector3 Z = f - g * Vector3.Dot(f, g);                    // 수평 앞
        if (Z.sqrMagnitude < 1e-4f) return Quaternion.identity;
        Z.Normalize();
        Vector3 X = Vector3.Cross(Z, g);                          // 오른쪽
        Vector3 fw = new Vector3(Vector3.Dot(f, X), Vector3.Dot(f, g), Vector3.Dot(f, Z));
        Vector3 uw = new Vector3(Vector3.Dot(u, X), Vector3.Dot(u, g), Vector3.Dot(u, Z));
        return Quaternion.LookRotation(fw, uw);
    }

    RightHandFollower follower;
    Quaternion muzzleInRoot = Quaternion.identity;
    Quaternion aim = Quaternion.identity;
    bool hasAim;

    void OnEnable()
    {
        follower = GetComponent<RightHandFollower>();
        if (follower != null) follower.enabled = false;
        if (imu == null) imu = ImuInput.Instance != null ? ImuInput.Instance : FindAnyObjectByType<ImuInput>();
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (muzzle != null) muzzleInRoot = Quaternion.Inverse(transform.rotation) * muzzle.rotation;
        Application.onBeforeRender += Pose;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= Pose;
        if (follower != null) follower.enabled = true;
    }

    void LateUpdate() { Pose(); }

    void Pose()
    {
        if (imu == null || head == null) return;
        Quaternion hr = followHeadYawOnly ? Quaternion.Euler(0f, head.eulerAngles.y, 0f) : head.rotation;
        transform.position = head.position + hr * viewOffset;

        // 하우징의 회전 그대로 (비틀기 포함). aimGain이 1이 아니면 좌우·위아래만 배율 적용
        Quaternion want = Mathf.Approximately(aimGain, 1f)
            ? Quaternion.Euler(0f, rangeForwardYaw, 0f) * imu.Orientation * Quaternion.Inverse(RestRotation()) * Quaternion.Euler(0f, 0f, rollOffset)
            : Quaternion.Euler(-imu.Pitch * aimGain, rangeForwardYaw + imu.Yaw * aimGain, rollOffset);
        aim = hasAim ? Quaternion.Slerp(aim, want, 1f - Mathf.Exp(-smoothing * Time.deltaTime)) : want;
        hasAim = true;
        // 총구가 aim 방향을 보도록 몸체 전체를 회전 (총구의 위쪽 = 세계 위쪽)
        transform.rotation = aim * Quaternion.Inverse(muzzleInRoot);
        if (laser != null) laser.UpdateBeam();   // 총이 움직인 직후 레이저도 같이
    }
}

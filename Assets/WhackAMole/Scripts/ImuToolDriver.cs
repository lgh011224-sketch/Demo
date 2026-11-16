// [센서 버전] 하우징 센서(ImuInput)의 회전을 드럼 스틱 / 쇠 망치에 그대로(1:1) 옮기는 스크립트 (ToolRoot에 붙임)
//  - 도구의 회전 = 하우징의 실제 회전 (좌우·위아래·비틀기 모두)
//  - 센서로 위치는 알 수 없으므로 손목(회전 중심)은 구멍 줄 앞쪽에 두고, 칼끝이 향한 구멍 쪽으로 좌우로만 미끄러져 갑니다.
//    (하우징을 좌우로 degreesPerHole 도 돌리면 한 칸)
//  - 도구가 탁자 아래로 파고들지 않도록 내려가는 각도만 막습니다.
//  - 센서가 "내려치기"를 감지하면 그 순간 칼끝 방향의 구멍으로 판정합니다.
// 이 스크립트가 켜져 있는 동안은 컨트롤러 위치 추적(RightHandFollower)을 끕니다.
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ImuToolDriver : MonoBehaviour
{
    public ImuInput imu;
    public WhackGameManager game;

    [Header("구멍 고르기")]
    [Tooltip("하우징을 좌우로 이만큼(도) 돌리면 옆 구멍으로")]
    public float degreesPerHole = 14f;

    [Header("손목 위치 (보이는 모습)")]
    [Tooltip("손목(쥔 곳)이 구멍 윗면보다 얼마나 위에 있는지 (m). 낮을수록 거의 수평으로 휘둘러도 구멍에 닿음")]
    public float pivotHeight = 0.08f;
    [Tooltip("도구 크기 배율 (실제 하우징 길이에 맞춤)")]
    public float toolScale = 1.25f;
    [Tooltip("손으로 쥐는 곳: 손잡이 끝에서 이만큼(m, 원래 크기 기준) 안쪽")]
    public float gripFromEnd = 0.04f;
    [Tooltip("타격이 감지된 순간 도구가 구멍에 닿는 모습을 보여 주는 시간 (s)")]
    public float contactTime = 0.12f;
    [Tooltip("손목 좌우 이동을 부드럽게 (클수록 빠름)")]
    public float smoothing = 20f;
    [Tooltip("도구 모양 회전 보정 (도구가 비틀려 보이면 조절)")]
    public Vector3 toolRotationOffset = Vector3.zero;

    public int TargetIndex { get; private set; }

    RightHandFollower follower;
    float shownPivotX;
    bool hasShown;

    void OnEnable()
    {
        follower = GetComponent<RightHandFollower>();
        if (follower != null) follower.enabled = false;
        if (imu == null) imu = ImuInput.Instance != null ? ImuInput.Instance : FindAnyObjectByType<ImuInput>();
        if (game == null) game = FindAnyObjectByType<WhackGameManager>();
        if (imu != null) imu.Strike += OnStrike;
        Application.onBeforeRender += Pose;
    }

    void OnDisable()
    {
        if (imu != null) imu.Strike -= OnStrike;
        Application.onBeforeRender -= Pose;
        if (follower != null) follower.enabled = true;
    }

    void LateUpdate() { Pose(); }

    bool Row(out float minX, out float maxX, out float rowZ, out float topY, out int count)
    {
        minX = float.MaxValue; maxX = float.MinValue; rowZ = 0f; topY = 0f; count = 0;
        if (game == null || game.tableMoles == null) return false;
        foreach (Mole m in game.tableMoles)
        {
            if (m == null) continue;
            Vector3 p = m.transform.position;
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            rowZ += p.z; topY += p.y; count++;
        }
        if (count == 0) return false;
        rowZ /= count; topY /= count;
        return true;
    }

    // 지금 도구의 손잡이 끝(쥐는 곳)과 타격점 위치 (ToolRoot 로컬 Z, 원래 크기 기준)
    readonly System.Collections.Generic.Dictionary<GameObject, float> buttZ = new System.Collections.Generic.Dictionary<GameObject, float>();

    void ToolAxis(out float handZ, out float headZ)
    {
        handZ = 0f; headZ = 0.3f;
        if (game == null) return;
        bool hammer = game.Mode == ToolMode.Hammer;
        ToolHead head = hammer ? game.hammerHead : game.woodHead;
        GameObject tool = hammer ? game.hammerTool : game.woodTool;
        if (head == null || tool == null) return;
        headZ = head.transform.localPosition.z + head.transform.parent.localPosition.z;
        if (!buttZ.TryGetValue(tool, out float bz))
        {
            bz = 0f;
            foreach (var mf in tool.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 l = Quaternion.Inverse(transform.rotation) * (mf.transform.TransformPoint(c) - transform.position) / Mathf.Max(1e-4f, transform.lossyScale.z);
                    bz = Mathf.Min(bz, l.z);
                }
            }
            buttZ[tool] = bz;
        }
        handZ = Mathf.Min(bz + gripFromEnd, headZ * 0.5f);
    }

    // 칼끝 좌우 각도 → 구멍 줄 위의 X (판정과 손목 위치에 같이 씀)
    float AimX(float minX, float maxX, int count)
    {
        float spacing = count > 1 ? (maxX - minX) / (count - 1) : 0.16f;
        float half = (count - 1) * 0.5f;
        float idx = Mathf.Clamp(imu.Yaw / degreesPerHole, -half - 0.6f, half + 0.6f);
        TargetIndex = Mathf.Clamp(Mathf.RoundToInt(idx + half), 0, count - 1);
        return (minX + maxX) * 0.5f + idx * spacing;
    }

    void Pose()
    {
        if (imu == null || !Row(out float minX, out float maxX, out float rowZ, out float topY, out int count)) return;
        transform.localScale = Vector3.one * toolScale;
        ToolAxis(out float handZ, out float headZ);
        float L = Mathf.Max(0.12f, (headZ - handZ) * toolScale);     // 손(쥔 곳) ~ 타격점 길이
        float h = Mathf.Min(pivotHeight, L * 0.9f);
        float d = Mathf.Sqrt(L * L - h * h);                         // 손 ~ 구멍 줄 수평 거리
        float strikePitch = Mathf.Asin(h / L) * Mathf.Rad2Deg;       // 칼끝이 구멍에 닿는 각도 (아래로)

        // 회전: 하우징 그대로. 단, 구멍보다 아래로는 안 내려가게 (칼끝 방향만 들어 올림)
        Quaternion rot = imu.Orientation;
        Vector3 f = rot * Vector3.forward;
        float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        float target = pitch < -strikePitch ? -strikePitch : pitch;
        // 타격이 감지된 순간: 잠깐 구멍에 '탁' 닿는 모습
        float k = (Time.time - contactStart) / Mathf.Max(0.01f, contactTime);
        if (k >= 0f && k < 1f)
        {
            float s = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;
            target = Mathf.Lerp(target, -strikePitch, Mathf.Clamp01(s));
        }
        if (Mathf.Abs(target - pitch) > 0.01f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, f);
            if (axis.sqrMagnitude > 1e-6f) rot = Quaternion.AngleAxis(pitch - target, axis.normalized) * rot;
        }

        // 손: 칼끝이 향한 구멍에 닿도록 좌우로 미끄러짐
        float aimX = AimX(minX, maxX, count);
        float yawRad = imu.Yaw * Mathf.Deg2Rad;
        float pivotX = aimX - d * Mathf.Sin(yawRad);
        float pivotZ = rowZ - d * Mathf.Cos(yawRad);
        if (!hasShown) { shownPivotX = pivotX; hasShown = true; }
        shownPivotX = Mathf.Lerp(shownPivotX, pivotX, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        Quaternion finalRot = rot * Quaternion.Euler(toolRotationOffset);
        Vector3 hand = new Vector3(shownPivotX, topY + h, pivotZ);
        transform.rotation = finalRot;
        // ToolRoot 원점이 아니라 '손잡이 끝(쥔 곳)'이 손 위치에 오도록
        transform.position = hand - finalRot * (Vector3.forward * handZ * toolScale);
    }

    float contactStart = -10f;

    void OnStrike(float speed)
    {
        contactStart = Time.time;
        if (game == null || !Row(out float minX, out float maxX, out _, out _, out int count)) return;
        game.StrikeAtX(AimX(minX, maxX, count));
    }
}

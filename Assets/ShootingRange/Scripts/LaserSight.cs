// 총구에서 나가는 빨간 레이저 + 맞는 곳에 빨간 점 (탄이 어디로 갈지 보여 줌)
//   발사 판정과 같은 방향(총구 앞쪽)으로 쏘므로 점이 있는 곳에 맞습니다.
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserSight : MonoBehaviour
{
    [Tooltip("총구 (앞쪽 = 발사 방향)")]
    public Transform muzzle;
    [Tooltip("맞는 곳에 표시할 점")]
    public Transform dot;
    public float maxRange = 40f;
    [Tooltip("점 크기 (가까울 때 ~ 멀 때, m)")]
    public Vector2 dotSize = new Vector2(0.012f, 0.035f);

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
    }

    void LateUpdate() { UpdateBeam(); }

    public void UpdateBeam()
    {
        if (muzzle == null || line == null) return;
        Vector3 o = muzzle.position, d = muzzle.forward;
        bool hit = Physics.Raycast(o, d, out RaycastHit h, maxRange, ~0, QueryTriggerInteraction.Ignore);
        float len = hit ? h.distance : maxRange;
        line.SetPosition(0, o);
        line.SetPosition(1, o + d * len);
        if (dot != null)
        {
            dot.gameObject.SetActive(hit);
            if (hit)
            {
                dot.position = h.point - d * 0.004f;
                dot.localScale = Vector3.one * Mathf.Lerp(dotSize.x, dotSize.y, Mathf.Clamp01(len / 8f));
            }
        }
    }
}

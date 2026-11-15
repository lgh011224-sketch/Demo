// 사격장 표적지 한 장
// 구조: Swing(집게에 매달린 회전 중심, 표적지 윗변) > Paper(표적지 Quad + 얇은 BoxCollider)
//  - 맞으면 아주 약하게 앞뒤로 흔들리다(펄럭) 잦아듦, 평소엔 공기 흐름처럼 미세하게 흔들림
//  - 맞은 자리에 탄흔(작은 구멍)을 붙이고, 중심에서의 거리로 점수(1~10, 표적지 밖 0)를 계산
using System.Collections.Generic;
using UnityEngine;

public class RangeTarget : MonoBehaviour
{
    [Tooltip("표적지 윗변(집게) 위치의 회전 중심")]
    public Transform swing;
    [Tooltip("표적지 Quad (로컬 -0.5~0.5 범위가 표적지 전체)")]
    public Transform paper;
    [Tooltip("표적지 크기 (m)")]
    public Vector2 paperSize = new Vector2(0.45f, 0.6f);
    [Tooltip("과녁 중심 (표적지 중심 기준, m)")]
    public Vector2 bullseyeOffset = new Vector2(0f, 0.04f);
    [Tooltip("10점~1점 고리 하나의 폭 (m). 바깥 1점 고리 반지름 = 10 × 폭")]
    public float ringWidth = 0.02f;
    public Material holeMaterial;
    [Tooltip("탄흔 지름 (m) - 9mm 권총탄 기준 약간 크게")]
    public float holeSize = 0.012f;

    [Header("펄럭임")]
    [Tooltip("맞았을 때 흔들림 각도 (도)")]
    public float hitSwingDegrees = 2.2f;
    public float swingFrequency = 3.2f;
    public float swingDamping = 2.6f;
    [Tooltip("평소 미세한 흔들림 (도)")]
    public float idleSwayDegrees = 0.25f;

    readonly List<GameObject> holes = new List<GameObject>();
    float hitTime = -10f, hitAmp, hitTwist;
    float seed;

    void Awake() { seed = Random.value * 100f; }

    // 월드 좌표 → 과녁 중심 기준 (m)
    public Vector2 ToTargetCoords(Vector3 world)
    {
        Vector3 l = paper.InverseTransformPoint(world); // -0.5~0.5
        return new Vector2(l.x * paperSize.x, l.y * paperSize.y) - bullseyeOffset;
    }

    public bool IsOnPaper(Vector3 world)
    {
        Vector3 l = paper.InverseTransformPoint(world);
        return Mathf.Abs(l.x) <= 0.5f && Mathf.Abs(l.y) <= 0.5f;
    }

    public int Score(Vector2 targetCoords)
    {
        float d = targetCoords.magnitude;
        if (d > ringWidth * 10f) return 0;
        return Mathf.Clamp(10 - Mathf.FloorToInt(d / ringWidth), 1, 10);
    }

    public void AddHole(Vector3 world)
    {
        if (holeMaterial == null) return;
        Vector3 l = paper.InverseTransformPoint(world);
        foreach (float side in new[] { -1f, 1f }) // 앞면·뒷면 모두 구멍
        {
            var h = GameObject.CreatePrimitive(PrimitiveType.Quad);
            h.name = "BulletHole";
            Destroy(h.GetComponent<Collider>());
            h.transform.SetParent(paper, false);
            h.transform.localPosition = new Vector3(l.x, l.y, side * 0.0015f);
            h.transform.localRotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, Random.Range(0f, 360f));
            h.transform.localScale = new Vector3(holeSize / paperSize.x, holeSize / paperSize.y, 1f);
            var r = h.GetComponent<Renderer>();
            r.sharedMaterial = holeMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holes.Add(h);
        }
    }

    public void ClearHoles()
    {
        foreach (var h in holes) if (h) Destroy(h);
        holes.Clear();
    }

    // 맞았을 때 펄럭 (strength 0~1)
    public void Flutter(float strength = 1f)
    {
        hitTime = Time.time;
        hitAmp = hitSwingDegrees * Mathf.Clamp01(strength) * Random.Range(0.85f, 1.15f);
        hitTwist = Random.Range(-0.6f, 0.6f);
    }

    void Update()
    {
        if (swing == null) return;
        float t = Time.time;
        // 평소: 공기 흐름처럼 아주 느리고 작게
        float idleX = (Mathf.PerlinNoise(seed, t * 0.35f) - 0.5f) * 2f * idleSwayDegrees;
        float idleY = (Mathf.PerlinNoise(seed + 7f, t * 0.25f) - 0.5f) * 2f * idleSwayDegrees;
        // 맞았을 때: 뒤로 밀렸다가 감쇠 진동
        float k = t - hitTime;
        float hit = k >= 0f && k < 3f ? hitAmp * Mathf.Exp(-k * swingDamping) * Mathf.Sin(k * swingFrequency * Mathf.PI * 2f + 0.3f) : 0f;
        swing.localRotation = Quaternion.Euler(idleX + hit, idleY + hit * hitTwist, hit * hitTwist * 0.5f);
    }
}

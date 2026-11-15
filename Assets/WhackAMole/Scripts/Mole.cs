// 두더지(또는 사격 표적) 한 마리
// 구멍 위치에 놓인 빈 오브젝트(Mole)가 이 스크립트를 갖고, 자식 Body가 위아래(로컬 Y축)로 움직입니다.
// 두더지 종류
//   Normal : 귀여운 갈색 두더지  → 드럼 스틱으로만 잡을 수 있음
//   Bad    : 초록색 우락부락 두더지 → 쇠 망치로만 잡을 수 있음
// 올라올 때마다 GameManager가 종류를 정하고, 해당 모양(normalVisual / badVisual)만 보여 줍니다.
using System.Collections.Generic;
using UnityEngine;

public enum MoleKind { Normal, Bad }

public class Mole : MonoBehaviour
{
    public Transform body;
    [Tooltip("숨었을 때 Body 높이 (로컬 Y, m)")]
    public float downHeight = -0.09f;
    [Tooltip("올라왔을 때 Body 높이 (로컬 Y, m)")]
    public float upHeight = 0.06f;
    [Tooltip("오르내리는 속도 (m/s)")]
    public float speed = 0.8f;
    public Color normalColor = new Color(0.45f, 0.30f, 0.20f);
    public Color hitColor = new Color(1f, 0.2f, 0.2f);

    [Header("두더지 종류별 모양 (비워 두면 종류 구분 없이 Body 전체 사용)")]
    public GameObject normalVisual;
    public GameObject badVisual;

    [Tooltip("맞았을 때 찌그러지는 정도 (0 = 효과 없음)")]
    public float hitSquash = 0.3f;

    enum State { Down, Rising, Up, Falling }
    State state = State.Down;
    float stayLeft;
    bool wasHit;
    Renderer[] furRenderers;
    MaterialPropertyBlock mpb;
    Vector3 baseScale;
    Quaternion baseRot;
    float squashTime = -1f;
    float denyTime = -1f;

    public MoleKind Kind { get; private set; } = MoleKind.Normal;
    public bool IsDown => state == State.Down;
    public bool IsHittable => (state == State.Rising || state == State.Up) && !wasHit;

    void Awake()
    {
        if (body == null) body = transform;
        baseScale = body.localScale;
        baseRot = body.localRotation;
        mpb = new MaterialPropertyBlock();
        ApplyKind(MoleKind.Normal);
        SetHeight(downHeight);
    }

    public void PopUp(float stayTime) => PopUp(stayTime, MoleKind.Normal);

    public void PopUp(float stayTime, MoleKind kind)
    {
        if (state != State.Down) return;
        ApplyKind(kind);
        wasHit = false;
        stayLeft = stayTime;
        state = State.Rising;
        ResetColor();
    }

    public bool TryHit()
    {
        if (!IsHittable) return false;
        wasHit = true;
        state = State.Falling;
        SetColor(hitColor);
        squashTime = 0f;
        return true;
    }

    // 틀린 도구로 맞았을 때: 잡히지 않고 고개를 좌우로 흔듦
    public void Deny()
    {
        if (!IsHittable) return;
        denyTime = 0f;
    }

    public void HideNow()
    {
        state = State.Down;
        wasHit = false;
        SetHeight(downHeight);
        ResetColor();
        squashTime = -1f;
        denyTime = -1f;
        body.localScale = baseScale;
        body.localRotation = baseRot;
    }

    void ApplyKind(MoleKind kind)
    {
        Kind = kind;
        if (normalVisual != null) normalVisual.SetActive(kind == MoleKind.Normal);
        if (badVisual != null) badVisual.SetActive(kind == MoleKind.Bad);

        // 색을 바꿀 대상: 털/피부(MoleFur, MoleBad…) 머티리얼을 쓰는 조각들. 없으면 첫 번째 렌더러 (예전 캡슐 모양 호환)
        GameObject vis = kind == MoleKind.Bad ? badVisual : normalVisual;
        Transform root = vis != null ? vis.transform : body;
        var list = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial != null && (r.sharedMaterial.name.StartsWith("MoleFur") || r.sharedMaterial.name.StartsWith("MoleBad")))
                list.Add(r);
        if (list.Count == 0)
        {
            Renderer first = root.GetComponentInChildren<Renderer>();
            if (first != null) list.Add(first);
        }
        furRenderers = list.ToArray();
    }

    void Update()
    {
        float y = body.localPosition.y;
        float dt = Time.deltaTime;
        switch (state)
        {
            case State.Rising:
                y = Mathf.MoveTowards(y, upHeight, speed * dt);
                if (Mathf.Approximately(y, upHeight)) state = State.Up;
                break;
            case State.Up:
                stayLeft -= dt;
                if (stayLeft <= 0f) state = State.Falling;
                break;
            case State.Falling:
                y = Mathf.MoveTowards(y, downHeight, (wasHit ? 2f : 1f) * speed * dt);
                if (Mathf.Approximately(y, downHeight))
                {
                    state = State.Down;
                    wasHit = false;
                    ResetColor();
                }
                break;
        }
        SetHeight(y);
        UpdateSquash(dt);
        UpdateDeny(dt);
    }

    // 맞으면 위아래로 납작해졌다가(0.25초) 원래대로 돌아옴
    void UpdateSquash(float dt)
    {
        if (squashTime < 0f) return;
        squashTime += dt;
        float k = Mathf.Clamp01(squashTime / 0.25f);
        float s = Mathf.Sin(k * Mathf.PI) * hitSquash;
        body.localScale = Vector3.Scale(baseScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
        if (k >= 1f) { squashTime = -1f; body.localScale = baseScale; }
    }

    // 틀린 도구: 0.4초 동안 좌우로 고개 젓기 (Body의 로컬 Y축 기준 회전)
    void UpdateDeny(float dt)
    {
        if (denyTime < 0f) return;
        denyTime += dt;
        float k = Mathf.Clamp01(denyTime / 0.4f);
        float angle = Mathf.Sin(k * Mathf.PI * 4f) * 25f * (1f - k);
        body.localRotation = baseRot * Quaternion.AngleAxis(angle, Vector3.up);
        if (k >= 1f) { denyTime = -1f; body.localRotation = baseRot; }
    }

    void SetHeight(float y)
    {
        Vector3 p = body.localPosition;
        p.y = y;
        body.localPosition = p;
    }

    void SetColor(Color c)
    {
        if (furRenderers == null) return;
        foreach (Renderer rend in furRenderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c); // URP
            mpb.SetColor("_Color", c);     // Built-in
            rend.SetPropertyBlock(mpb);
        }
    }

    // 머티리얼 원래 색으로 (종류별로 색이 다르므로 덮어쓴 색을 지움)
    void ResetColor()
    {
        if (furRenderers == null) return;
        foreach (Renderer rend in furRenderers)
            if (rend != null) rend.SetPropertyBlock(null);
    }
}

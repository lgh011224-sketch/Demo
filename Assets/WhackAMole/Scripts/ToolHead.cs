// 망치 머리(나무 망치, 쇠 망치)에 붙는 스크립트
// [컨트롤러 버전] 머리가 두더지나 판자에 아래 방향으로 빠르게 닿으면 타격으로 처리합니다.
// [모듈 버전] 충돌로는 판정하지 않고, 아두이노의 HIT 메시지가 오면 게임 매니저가 머리 위치로 판정합니다.
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ToolHead : MonoBehaviour
{
    public WhackGameManager manager;
    [Tooltip("모듈 버전에서 이 거리(m) 안에 있는 구멍을 맞은 것으로 판정")]
    public float hitRadius = 0.07f;
    [Tooltip("컨트롤러 버전에서 타격으로 인정하는 최소 내려치기 속도 (m/s)")]
    public float minSwingSpeed = 0.6f;
    [Tooltip("연속 타격 방지 시간 (s)")]
    public float cooldown = 0.25f;

    Vector3 lastPos;
    Vector3 velocity;
    float lastStrikeTime = -10f;
    Mole pendingMole;
    bool pendingBoard;

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void OnEnable()
    {
        lastPos = transform.position;
        velocity = Vector3.zero;
        pendingMole = null;
        pendingBoard = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (manager == null || manager.useModule) return;
        if (-velocity.y < minSwingSpeed) return;
        Mole m = other.GetComponentInParent<Mole>();
        if (m != null && m.IsHittable) pendingMole = m;
        else if (other.GetComponentInParent<BoardSurface>() != null) pendingBoard = true;
    }

    void Update()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        velocity = (transform.position - lastPos) / dt;
        lastPos = transform.position;

        if (pendingMole == null && !pendingBoard) return;
        if (Time.time - lastStrikeTime >= cooldown)
        {
            lastStrikeTime = Time.time;
            manager.OnStrike(pendingMole); // pendingMole이 없으면 빗맞음
        }
        pendingMole = null;
        pendingBoard = false;
    }
}

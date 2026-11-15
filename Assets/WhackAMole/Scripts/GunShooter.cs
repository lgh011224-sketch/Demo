// 사격 모드의 총
// 총구에서 앞으로 레이저를 표시하고, 발사하면 레이저 끝에 맞은 표적을 판정합니다.
// 발사: 컨트롤러 트리거 / 키보드 F / (모듈 버전) 아두이노 버튼 메시지
using UnityEngine;

public class GunShooter : MonoBehaviour
{
    public WhackGameManager manager;
    public Transform muzzle;
    public LineRenderer laser;
    public float maxDistance = 15f;
    public float fireCooldown = 0.2f;
    public Color laserColor = new Color(1f, 0.2f, 0.2f, 0.8f);
    public Color flashColor = Color.yellow;

    float lastFire = -10f;
    float flashUntil;

    void Update()
    {
        if (muzzle == null || laser == null) return;
        Vector3 start = muzzle.position;
        Vector3 end = start + muzzle.forward * maxDistance;
        if (Physics.Raycast(start, muzzle.forward, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
            end = hit.point;
        laser.SetPosition(0, start);
        laser.SetPosition(1, end);
        Color c = Time.time < flashUntil ? flashColor : laserColor;
        laser.startColor = c;
        laser.endColor = c;
    }

    public void Fire()
    {
        if (!isActiveAndEnabled || muzzle == null) return;
        if (Time.time - lastFire < fireCooldown) return;
        lastFire = Time.time;
        flashUntil = Time.time + 0.08f;

        Mole target = null;
        if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, maxDistance, ~0, QueryTriggerInteraction.Collide))
            target = hit.collider.GetComponentInParent<Mole>();

        if (manager != null) manager.OnStrike(target);
    }
}

// 사격 게임의 원형 표적지 하나: 맞으면 뒤로 쓰러지고, 일정 시간 뒤 다시 일어남
//   hinge(아래쪽 경첩)를 X축으로 돌려서 세우고/눕힘. 표적지(원판)는 hinge의 자식
using UnityEngine;

public class PopTarget : MonoBehaviour
{
    [Tooltip("맞혔을 때 점수 (큰 표적 10, 중간 20, 작은 표적 30)")]
    public int points = 10;
    [Tooltip("돌아가는 경첩 (표적지 아래쪽)")]
    public Transform hinge;
    [Tooltip("누웠을 때 각도 (뒤쪽으로)")]
    public float downAngle = 85f;
    public float fallTime = 0.18f;
    public float riseTime = 0.35f;

    public bool IsUp => wantUp && angle < 20f;
    public bool IsDown => !wantUp;

    float angle;          // 0 = 서 있음, downAngle = 누움
    bool wantUp = true;
    float riseAt = -1f;   // 이 시각에 다시 일어남 (-1 = 자동으로 안 일어남)
    float moveStart, moveFrom;
    AudioSource audioSrc;
    static AudioClip clang, whoosh;

    void Awake()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0.8f;
        audioSrc.minDistance = 2f;
        if (clang == null) clang = SynthClang();
        if (whoosh == null) whoosh = SynthWhoosh();
    }

    // 맞음: 쓰러지고 respawnSeconds 뒤 다시 일어남 (0 이하면 그대로 누워 있음)
    public void Knock(float respawnSeconds)
    {
        if (!wantUp) return;
        StartMove(false);
        riseAt = respawnSeconds > 0f ? Time.time + respawnSeconds : -1f;
        audioSrc.pitch = Random.Range(0.92f, 1.08f) * (points >= 30 ? 1.25f : points >= 20 ? 1.1f : 1f);
        audioSrc.PlayOneShot(clang, 0.9f);
    }

    public void SetUp(bool up, bool instant = false)
    {
        riseAt = -1f;
        if (instant) { wantUp = up; angle = up ? 0f : downAngle; Apply(); return; }
        if (up != wantUp) StartMove(up);
    }

    void StartMove(bool up)
    {
        wantUp = up;
        moveStart = Time.time;
        moveFrom = angle;
        if (up) audioSrc.PlayOneShot(whoosh, 0.35f);
    }

    void Update()
    {
        if (!wantUp && riseAt > 0f && Time.time >= riseAt) { riseAt = -1f; StartMove(true); }
        float target = wantUp ? 0f : downAngle;
        float dur = wantUp ? riseTime : fallTime;
        float k = Mathf.Clamp01((Time.time - moveStart) / dur);
        // 쓰러질 땐 빠르게 + 끝에서 살짝 튕김, 일어날 땐 부드럽게
        float e = wantUp ? k * k * (3f - 2f * k) : 1f - Mathf.Pow(1f - k, 3f);
        float a = Mathf.Lerp(moveFrom, target, e);
        if (!wantUp && k >= 1f) a = target - Mathf.Sin(Mathf.Clamp01((Time.time - moveStart - dur) / 0.15f) * Mathf.PI) * 6f;
        if (Mathf.Abs(a - angle) > 0.001f) { angle = a; Apply(); }
    }

    void Apply() { if (hinge != null) hinge.localRotation = Quaternion.Euler(angle, 0f, 0f); }

    static AudioClip SynthClang()
    {
        const int rate = 44100; int n = (int)(rate * 0.45f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 9f);
            d[i] = (Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1318f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 2210f * t) * 0.2f * Mathf.Exp(-t * 20f)) * env * 0.8f;
        }
        var c = AudioClip.Create("TargetClang", n, 1, rate, false); c.SetData(d, 0); return c;
    }

    static AudioClip SynthWhoosh()
    {
        const int rate = 44100; int n = (int)(rate * 0.3f);
        var d = new float[n]; var rng = new System.Random(4); float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f;
            d[i] = lp * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.8f;
        }
        var c = AudioClip.Create("TargetRise", n, 1, rate, false); c.SetData(d, 0); return c;
    }
}

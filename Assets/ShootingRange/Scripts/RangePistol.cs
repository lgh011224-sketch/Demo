// 권총 연출: 총구 화염(빛 + 섬광), 반동, 총소리
// 총소리는 오디오 파일 없이 실행 시 합성 (잡음 폭발음 + 낮은 울림)
using UnityEngine;

public class RangePistol : MonoBehaviour
{
    [Tooltip("총구 (앞쪽 = 발사 방향)")]
    public Transform muzzle;
    [Tooltip("반동으로 움직일 권총 모델")]
    public Transform visual;
    public Light flashLight;
    public Renderer flashRenderer;
    public AudioSource audioSource;

    [Header("반동")]
    public float recoilBack = 0.025f;
    public float recoilPitch = 9f;
    public float recoilTime = 0.18f;

    Vector3 basePos;
    Quaternion baseRot;
    Vector3 gunRight, gunBack;   // 총 기준 축 (visual 부모 좌표) – 컨트롤러가 뒤집혀 있어도 총 모양 기준으로 반동
    float fireTime = -10f;
    AudioClip shot, click;

    void Awake()
    {
        if (visual != null)
        {
            basePos = visual.localPosition; baseRot = visual.localRotation;
            Transform parent = visual.parent;
            Transform m = muzzle != null ? muzzle : visual;
            gunRight = parent != null ? parent.InverseTransformDirection(m.right) : m.right;
            gunBack = parent != null ? -parent.InverseTransformDirection(m.forward) : -m.forward;
        }
        if (flashLight) flashLight.enabled = false;
        if (flashRenderer) flashRenderer.enabled = false;
        shot = SynthShot();
        click = SynthClick();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.6f;
    }

    public void PlayFire()
    {
        fireTime = Time.time;
        if (flashRenderer) flashRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(shot, 1f);
    }

    public void PlayDryFire()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(click, 0.6f);
    }

    void Update()
    {
        float k = Time.time - fireTime;
        bool flash = k < 0.045f;
        if (flashLight) flashLight.enabled = flash;
        if (flashRenderer) flashRenderer.enabled = flash;
        if (visual == null) return;
        if (k < recoilTime)
        {
            // 빠르게 튀었다가 천천히 복귀
            float x = k / recoilTime;
            float r = x < 0.15f ? x / 0.15f : Mathf.Pow(1f - (x - 0.15f) / 0.85f, 2f);
            // 총열 반대쪽으로 밀리고, 손잡이를 축으로 총구가 위(가늠자 쪽)로 들림
            visual.localPosition = basePos + gunBack * recoilBack * r;
            visual.localRotation = Quaternion.AngleAxis(-recoilPitch * r, gunRight) * baseRot;
        }
        else
        {
            visual.localPosition = basePos;
            visual.localRotation = baseRot;
        }
    }

    static AudioClip SynthShot()
    {
        const int rate = 44100; float len = 0.6f;
        int n = (int)(rate * len);
        var data = new float[n];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.35f;                                   // 살짝 둔한 잡음
            float crack = noise * Mathf.Exp(-t * 55f);                     // 날카로운 폭발음
            float body = lp * Mathf.Exp(-t * 14f) * 0.9f;                  // 실내 잔향 느낌
            float thump = Mathf.Sin(2f * Mathf.PI * 62f * t) * Mathf.Exp(-t * 20f) * 0.9f; // 낮은 울림
            data[i] = Mathf.Clamp((crack * 0.9f + body + thump) * 0.8f, -1f, 1f);
        }
        var clip = AudioClip.Create("PistolShot", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip SynthClick()
    {
        const int rate = 44100; int n = rate / 20;
        var data = new float[n];
        var rng = new System.Random(3);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 300f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 400f) * 0.4f;
        }
        var clip = AudioClip.Create("DryClick", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

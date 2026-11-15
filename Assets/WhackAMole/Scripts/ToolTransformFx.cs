// 도구 변신 이펙트 (마법소녀 변신 느낌)
//   도구 둘레에 빛의 고리 3개가 돌며 퍼지고, 반짝이 입자·분홍/금빛 섬광이 터지는 동안
//   기존 도구는 회전하며 작아져 사라지고 → (가운데에서 교체) → 새 도구가 통통 튀며 나타남
//   효과음(맑은 아르페지오)은 실행 중에 합성, 진동도 짧게 여러 번
// 사용: Play(oldTool, swap, getNewTool) – swap 안에서 실제 도구 교체(SetMode)를 수행
// 머티리얼(고리·반짝이·빛무리)은 에디터 설정(WhackToolFxSetup)이 만들어 연결합니다.
using System;
using System.Collections;
using UnityEngine;

public class ToolTransformFx : MonoBehaviour
{
    [Tooltip("이펙트 중심 (도구 머리 근처). 비워 두면 이 오브젝트 + centerOffset")]
    public Transform center;
    public Vector3 centerOffset = new Vector3(0f, 0f, 0.18f);
    public Material ringMaterial;
    public Material sparkleMaterial;
    public Material glowMaterial;
    public Color colorA = new Color(1f, 0.45f, 0.85f);   // 분홍
    public Color colorB = new Color(1f, 0.85f, 0.35f);   // 금빛
    public Color colorC = new Color(0.55f, 0.85f, 1f);   // 하늘
    public float duration = 1.6f;

    public bool Playing { get; private set; }

    Transform fxRoot;
    Transform[] rings;
    Material[] ringMats;
    Transform glow;
    Material glowMat;
    ParticleSystem sparkles;
    Light flash;
    AudioSource audioSrc;
    AudioClip chime;

    void Build()
    {
        fxRoot = new GameObject("TransformFx").transform;
        fxRoot.SetParent(transform, false);
        rings = new Transform[3]; ringMats = new Material[3];
        Color[] cols = { colorA, colorB, colorC };
        Vector3[] tilts = { new Vector3(90f, 0f, 0f), new Vector3(30f, 60f, 0f), new Vector3(-30f, -60f, 0f) };
        for (int i = 0; i < 3; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "AuraRing" + i;
            q.transform.SetParent(fxRoot, false);
            q.transform.localRotation = Quaternion.Euler(tilts[i]);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (ringMaterial != null) { ringMats[i] = new Material(ringMaterial); r.sharedMaterial = ringMats[i]; }
            rings[i] = q.transform;
            SetColor(ringMats[i], cols[i], 0f);
        }
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(g.GetComponent<Collider>());
        g.name = "AuraGlow";
        g.transform.SetParent(fxRoot, false);
        var gr = g.GetComponent<MeshRenderer>();
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (glowMaterial != null) { glowMat = new Material(glowMaterial); gr.sharedMaterial = glowMat; }
        glow = g.transform;

        // 반짝이 입자
        var ps = new GameObject("Sparkles").AddComponent<ParticleSystem>();
        ps.transform.SetParent(fxRoot, false);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(colorA, 0f), new GradientColorKey(colorB, 0.5f), new GradientColorKey(colorC, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f; // 살짝 위로 떠오름
        main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40), new ParticleSystem.Burst(0.45f, 60), new ParticleSystem.Burst(0.8f, 30) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
        vel.orbitalY = new ParticleSystem.MinMaxCurve(4f, 7f);           // 도구 둘레를 빙글빙글
        vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
        var col = ps.colorOverLifetime; col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
        var rnd = ps.GetComponent<ParticleSystemRenderer>();
        rnd.material = sparkleMaterial;
        rnd.renderMode = ParticleSystemRenderMode.Billboard;
        sparkles = ps;

        flash = new GameObject("AuraLight").AddComponent<Light>();
        flash.transform.SetParent(fxRoot, false);
        flash.type = LightType.Point; flash.range = 1.5f; flash.intensity = 0f; flash.color = colorA; flash.shadows = LightShadows.None;

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false; audioSrc.spatialBlend = 0.5f; audioSrc.volume = 0.6f;
        chime = SynthChime();
        fxRoot.gameObject.SetActive(false);
    }

    // oldTool: 지금 보이는 도구, swap: 실제 교체(새 도구 활성화), getNewTool: 교체 뒤 보이는 도구
    public void Play(GameObject oldTool, Action swap, Func<GameObject> getNewTool, Action done = null)
    {
        if (fxRoot == null) Build();
        StopAllCoroutines();
        StartCoroutine(Run(oldTool, swap, getNewTool, done));
    }

    IEnumerator Run(GameObject oldTool, Action swap, Func<GameObject> getNewTool, Action done)
    {
        Playing = true;
        fxRoot.gameObject.SetActive(true);
        Transform c = center != null ? center : transform;
        fxRoot.position = c.TransformPoint(centerOffset);
        sparkles.Clear(); sparkles.Play();
        if (chime != null) audioSrc.PlayOneShot(chime);
        Vector3 oldScale = oldTool != null ? oldTool.transform.localScale : Vector3.one;
        Quaternion oldRot = oldTool != null ? oldTool.transform.localRotation : Quaternion.identity;
        bool swapped = false;
        GameObject newTool = null; Vector3 newScale = Vector3.one; Quaternion newRot = Quaternion.identity;
        float t = 0f, half = duration * 0.45f;
        int buzz = 0;
        var cam = Camera.main;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t / duration;
            fxRoot.position = c.TransformPoint(centerOffset);
            // 고리: 회전하며 커졌다가 사라짐
            for (int i = 0; i < 3; i++)
            {
                float s = Mathf.Lerp(0.08f, 0.5f, Mathf.SmoothStep(0f, 1f, k)) * (1f + 0.15f * Mathf.Sin(k * 20f + i));
                rings[i].localScale = new Vector3(s, s, 1f);
                rings[i].Rotate(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 360f * Time.deltaTime, Space.Self);
                float a = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                SetColor(ringMats[i], i == 0 ? colorA : i == 1 ? colorB : colorC, a);
            }
            // 빛무리: 교체 순간 가장 밝게
            float g = Mathf.Exp(-Mathf.Pow((k - 0.45f) / 0.18f, 2f));
            glow.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.55f, g);
            if (cam != null) glow.rotation = Quaternion.LookRotation(glow.position - cam.transform.position);
            SetColor(glowMat, Color.Lerp(colorA, Color.white, g * 0.6f), g);
            flash.intensity = g * 3f;
            flash.color = Color.Lerp(colorA, colorB, k);
            // 진동: 짧게 세 번 (교체 순간에 가장 강하게)
            if (buzz == 0 && k > 0.05f) { RightHandFollower.Vibrate(0.25f, 0.04f); buzz++; }
            if (buzz == 1 && k > 0.45f) { RightHandFollower.Vibrate(0.6f, 0.08f); buzz++; }
            if (buzz == 2 && k > 0.8f) { RightHandFollower.Vibrate(0.3f, 0.05f); buzz++; }

            if (t < half)
            {
                // 기존 도구: 빙글 돌며 작아짐
                float p = t / half;
                if (oldTool != null)
                {
                    oldTool.transform.localScale = oldScale * (1f - Mathf.SmoothStep(0f, 1f, p));
                    oldTool.transform.localRotation = oldRot * Quaternion.Euler(0f, 0f, p * p * 540f);
                }
            }
            else
            {
                if (!swapped)
                {
                    swapped = true;
                    if (oldTool != null) { oldTool.transform.localScale = oldScale; oldTool.transform.localRotation = oldRot; }
                    swap?.Invoke();
                    newTool = getNewTool?.Invoke();
                    if (newTool != null) { newScale = newTool.transform.localScale; newRot = newTool.transform.localRotation; }
                }
                // 새 도구: 반대로 돌며 통통 튀어나옴 (살짝 커졌다가 제자리)
                float p = Mathf.Clamp01((t - half) / (duration - half));
                float bounce = 1f + Mathf.Sin(p * Mathf.PI) * 0.25f * (1f - p);
                if (newTool != null)
                {
                    newTool.transform.localScale = newScale * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 1.6f)) * bounce;
                    newTool.transform.localRotation = newRot * Quaternion.Euler(0f, 0f, -(1f - p) * (1f - p) * 360f);
                }
            }
            yield return null;
        }
        if (!swapped) { swap?.Invoke(); newTool = getNewTool?.Invoke(); }
        if (newTool != null) { newTool.transform.localScale = newScale; newTool.transform.localRotation = newRot; }
        flash.intensity = 0f;
        fxRoot.gameObject.SetActive(false);
        Playing = false;
        done?.Invoke();
    }

    static void SetColor(Material m, Color c, float a)
    {
        if (m == null) return;
        c.a = Mathf.Clamp01(a);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // 맑은 "띠리리링" 아르페지오 (C6 E6 G6 C7 E7) + 반짝이는 고음
    static AudioClip SynthChime()
    {
        const int rate = 44100; float len = 1.4f;
        int n = (int)(rate * len);
        var d = new float[n];
        float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f };
        for (int j = 0; j < notes.Length; j++)
        {
            int start = (int)(rate * j * 0.09f);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)rate;
                float env = Mathf.Exp(-t * 4.5f) * Mathf.Min(1f, t * 200f);
                d[i] += (Mathf.Sin(2f * Mathf.PI * notes[j] * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[j] * 2.01f * t)) * env * 0.18f;
            }
        }
        var rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            d[i] += (float)(rng.NextDouble() - 0.5) * 0.04f * Mathf.Exp(-t * 3f) * Mathf.Abs(Mathf.Sin(t * 60f)); // 반짝임
            d[i] = Mathf.Clamp(d[i], -1f, 1f);
        }
        var clip = AudioClip.Create("TransformChime", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}

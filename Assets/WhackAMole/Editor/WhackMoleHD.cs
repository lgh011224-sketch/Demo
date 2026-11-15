// 두더지 HD 모델 – "찰흙 덩어리" 느낌을 줄이고, 주변 실사 배경에 어울리는 반실사(애니메이션 영화 풍) 두더지를 만듭니다.
//  - 몸통: 기본 도형을 붙이지 않고 한 덩어리로 매끄럽게 깎은 몸 (머리가 몸통에 자연스럽게 이어짐)
//  - 털: 짧은 벨벳 털 (껍질 겹 Shell 10장 + 털 결 텍스처, 등은 진하고 배·얼굴은 밝게, 볼 홍조)
//  - 얼굴: 촉촉한 분홍 코끝 + 콧구멍, 무광 검은 눈, 수염, 앞니
//  - 앞발: 두더지 특유의 넓적한 삽 모양 손 + 발톱 5개
// 게임 판정(콜라이더·Mole 스크립트)은 그대로이고, 겉모양만 바뀝니다.
// 메뉴: Tools > Whack-A-Mole > Regenerate HD Mole Assets (텍스처·메시 다시 만들기) → Restyle Game Moles
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class WhackMoleHD
{
    const string Dir = "Assets/WhackAMole/MoleHD";
    public const int ShellCount = 10;
    const float FurLength = 0.016f;     // 모델 단위 (탁자 두더지 크기 0.2배 → 실제 약 3 mm 벨벳 털)
    const float Y0 = -0.07f, H = 0.64f; // 몸통 아래 끝 / 높이 (예전 캡슐과 같은 틀)

    class Kind
    {
        public string key; public bool bad;
        public float[] prof; public float dome, sx, sz, lean;
        public Color back, side, belly; public bool blush;
        public Mesh body, snout; public Mesh[] shells;
        public Material bodyMat; public Material[] shellMats;
        public Vector3 snoutA, snoutB;
    }

    static Kind normal, bad;
    static Mesh claw;
    static Material snoutMat, noseMat, eyeMat, pawMat, clawMat, whiskerMat, toothMat, nostrilMat;
    static Material bSnoutMat, bNoseMat, bPawMat, bClawMat, bSclera, bPupil, bDark, bScar, bWhisker;

    [MenuItem("Tools/Whack-A-Mole/Regenerate HD Mole Assets")]
    public static void Regenerate() { normal = null; Ensure(true); }

    // 상반신 두더지. 기준: 바닥 = y 0 근처, 얼굴 = +Z (예전 BuildFigure와 같은 틀)
    public static Transform Build(Transform parent, string name, Vector3 pos, float scale, Quaternion rot, bool shadows, bool isBad)
    {
        Ensure(false);
        Kind k = isBad ? bad : normal;
        Transform g = Group(parent, name, pos, rot, scale);
        MeshObj(g, "Body", k.body, k.bodyMat, shadows);
        Transform fur = Group(g, "Fur", Vector3.zero, Quaternion.identity, 1f);
        for (int i = 0; i < ShellCount; i++) MeshObj(fur, "Shell" + i.ToString("00"), k.shells[i], k.shellMats[i], false);
        if (isBad) BadFace(g, k); else CuteFace(g, k);
        return g;
    }

    // ───────────────────────── 얼굴 · 손 ─────────────────────────
    static void CuteFace(Transform g, Kind k)
    {
        MeshObj(g, "Snout", k.snout, snoutMat, false);
        Vector3 dir = (k.snoutB - k.snoutA).normalized;
        Vector3 tip = k.snoutB - dir * 0.008f;
        Quaternion look = Quaternion.LookRotation(dir);
        Prim(PrimitiveType.Sphere, "NoseTip", g, tip, new Vector3(0.066f, 0.052f, 0.042f), noseMat, look);
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Sphere, "Nostril", g, tip + dir * 0.017f + look * new Vector3(sx * 0.012f, 0.004f, 0f), new Vector3(0.012f, 0.009f, 0.008f), nostrilMat, look);
            Prim(PrimitiveType.Cube, "Tooth", g, tip + look * new Vector3(sx * 0.0085f, -0.043f, -0.014f), new Vector3(0.015f, 0.026f, 0.008f), toothMat, look);

            // 눈: 털 위로 살짝 나온 검은 구슬 (반짝임 없이 무광)
            Vector3 ep = SurfacePoint(k, sx * 0.072f, 0.455f, out Vector3 en);
            Prim(PrimitiveType.Sphere, "Eye", g, ep + en * 0.008f, Vector3.one * 0.05f, eyeMat, Quaternion.identity);

            // 수염 3가닥
            for (int w = 0; w < 3; w++)
            {
                Vector3 root = Vector3.Lerp(k.snoutA, k.snoutB, 0.62f) + new Vector3(sx * 0.05f, -0.008f + (w - 1) * 0.012f, 0f);
                Vector3 wd = new Vector3(sx, (w - 1) * 0.28f + 0.04f, -0.12f).normalized;
                Whisker(g, root, wd, 0.105f, 0.0035f, whiskerMat);
            }
            Paw(g, k, sx, pawMat, clawMat);
        }
    }

    static void BadFace(Transform g, Kind k)
    {
        MeshObj(g, "Snout", k.snout, bSnoutMat, false);
        Vector3 dir = (k.snoutB - k.snoutA).normalized;
        Vector3 tip = k.snoutB - dir * 0.008f;
        Quaternion look = Quaternion.LookRotation(dir);
        Prim(PrimitiveType.Sphere, "NoseTip", g, tip, new Vector3(0.085f, 0.062f, 0.05f), bNoseMat, look);
        Prim(PrimitiveType.Cube, "Mouth", g, tip + look * new Vector3(0f, -0.05f, -0.022f), new Vector3(0.09f, 0.011f, 0.01f), nostrilMat, look);

        // 머리 위 가시 (좌우로 벌어진 모히칸)
        for (int i = -1; i <= 1; i++)
        {
            float x = i * 0.085f;
            float t = TopT(k, x);
            Vector3 p = BodyPoint(k, 0.5f, t); p.x = x; p.y -= 0.012f; p.z = k.lean * t * t - 0.02f;
            Vector3 up = new Vector3(i * 0.4f, 1f, -0.1f).normalized;
            ClawObj(g, "Spike", p, up, new Vector3(0.065f, 0.065f, 0.1f), bDark);
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            // 송곳니 (아래로 삐죽)
            ClawObj(g, "Fang", tip + look * new Vector3(sx * 0.028f, -0.048f, -0.016f), new Vector3(sx * 0.1f, -1f, 0.15f).normalized, new Vector3(0.022f, 0.016f, 0.042f), toothMat);

            // 가늘고 성난 빨간 눈 + V자 눈썹 (털 위로 올라오게)
            Vector3 ep = SurfacePoint(k, sx * 0.085f, 0.44f, out Vector3 en);
            Quaternion el = Quaternion.LookRotation(en);
            Prim(PrimitiveType.Sphere, "Eye", g, ep + en * 0.012f, new Vector3(0.068f, 0.046f, 0.04f), bSclera, el);
            Prim(PrimitiveType.Sphere, "Pupil", g, ep + en * 0.03f + new Vector3(-sx * 0.005f, 0f, 0f), Vector3.one * 0.028f, bPupil, el);
            Vector3 bp = SurfacePoint(k, sx * 0.085f, 0.488f, out Vector3 bn);
            Prim(PrimitiveType.Cube, "Brow", g, bp + bn * 0.02f, new Vector3(0.125f, 0.03f, 0.035f), bDark, Quaternion.LookRotation(bn) * Quaternion.Euler(0f, 0f, sx * 22f));

            Whisker(g, Vector3.Lerp(k.snoutA, k.snoutB, 0.6f) + new Vector3(sx * 0.06f, -0.01f, 0f), new Vector3(sx, 0.1f, -0.1f).normalized, 0.09f, 0.0045f, bWhisker);
            Paw(g, k, sx, bPawMat, bClawMat, 1.15f);
        }
        // 얼굴 흉터 (사선 + 꿰맨 자국) – 털 위에
        Vector3 sp = SurfacePoint(k, -0.135f, 0.42f, out Vector3 sn);
        Quaternion sl = Quaternion.LookRotation(sn) * Quaternion.Euler(0f, 0f, 28f);
        Prim(PrimitiveType.Cube, "Scar", g, sp + sn * 0.018f, new Vector3(0.012f, 0.1f, 0.008f), bScar, sl);
        for (int i = -1; i <= 1; i++)
            Prim(PrimitiveType.Cube, "Stitch", g, sp + sn * 0.02f + sl * new Vector3(0f, i * 0.028f, 0f), new Vector3(0.038f, 0.008f, 0.008f), bScar, sl);
    }

    // 두더지 특유의 넓적한 삽 모양 앞발 + 발톱 5개
    static void Paw(Transform g, Kind k, float sx, Material paw, Material clawM, float size = 1f)
    {
        Vector3 p = SurfacePoint(k, sx * 0.155f, 0.12f, out Vector3 n);
        Vector3 pc = p + n * 0.025f + Vector3.forward * 0.028f;
        Quaternion pr = Quaternion.Euler(-14f, sx * -22f, sx * -12f);
        Prim(PrimitiveType.Sphere, "Paw", g, pc, new Vector3(0.155f, 0.052f, 0.13f) * size, paw, pr);
        for (int c = 0; c < 5; c++)
        {
            float lx = (c - 2) * 0.027f * size;
            Vector3 basePos = pc + pr * new Vector3(lx, 0.004f, 0.05f * size);
            Vector3 d = pr * new Vector3(lx * 2.2f, -0.3f, 1f).normalized;
            float len = (c == 0 || c == 4 ? 0.045f : 0.058f) * size;
            ClawObj(g, "Claw", basePos, d, new Vector3(0.022f, 0.014f, len) * size, clawM);
        }
    }

    static void Whisker(Transform g, Vector3 root, Vector3 dir, float len, float thick, Material m)
    {
        Prim(PrimitiveType.Cylinder, "Whisker", g, root + dir * (len * 0.5f), new Vector3(thick, len * 0.5f, thick), m, Quaternion.FromToRotation(Vector3.up, dir));
    }

    static void ClawObj(Transform g, string name, Vector3 pos, Vector3 dir, Vector3 scale, Material m)
    {
        Transform t = MeshObj(g, name, claw, m, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up);
        t.localScale = scale;
    }

    // ───────────────────────── 몸통 모양 ─────────────────────────
    static float Radius(Kind k, float t)
    {
        float[] p = k.prof; int n = p.Length - 1;
        float x = Mathf.Clamp01(t) * n; int i = Mathf.Min((int)x, n - 1); float f = x - i;
        float p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, n)];
        float r = 0.5f * (2f * p1 + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f * f + (-p0 + 3f * p1 - 3f * p2 + p3) * f * f * f);
        if (t > k.dome) { float d = (t - k.dome) / (1f - k.dome); r *= Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)); }
        return r;
    }

    static Vector3 BodyPoint(Kind k, float u, float t)
    {
        float r = Radius(k, t), phi = Mathf.PI + 2f * Mathf.PI * u;  // u 0 = 등, 0.5 = 얼굴(+Z)
        return new Vector3(r * k.sx * Mathf.Sin(phi), Y0 + t * H, r * k.sz * Mathf.Cos(phi) + k.lean * t * t);
    }

    static Vector3 BodyAxis(Kind k, float t) => new Vector3(0f, Y0 + t * H, k.lean * t * t);

    // 몸 앞쪽 표면에서 (x, y) 위치의 점과 바깥 방향
    static Vector3 SurfacePoint(Kind k, float x, float y, out Vector3 n)
    {
        float t = Mathf.Clamp01((y - Y0) / H);
        float r = Mathf.Max(Radius(k, t), 1e-4f);
        float s = Mathf.Clamp(x / (r * k.sx), -0.999f, 0.999f);
        float u = 0.5f + Mathf.Asin(s) / (2f * Mathf.PI);
        n = SurfNormal((a, b) => BodyPoint(k, a, b), b => BodyAxis(k, b), u, t);
        return BodyPoint(k, u, t);
    }

    static float TopT(Kind k, float x)
    {
        for (float t = 1f; t > 0.5f; t -= 0.002f) if (Radius(k, t) * k.sx >= Mathf.Abs(x)) return t;
        return 0.5f;
    }

    // ───────────────────────── 에셋 생성 ─────────────────────────
    static void Ensure(bool force)
    {
        if (!force && normal != null && normal.body != null && claw != null) return;
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/WhackAMole", "MoleHD");

        normal = new Kind
        {
            key = "Cute", bad = false,
            prof = new[] { 0.215f, 0.245f, 0.25f, 0.228f, 0.195f, 0.17f }, dome = 0.68f, sx = 1f, sz = 0.9f, lean = 0.03f,
            back = new Color(0.25f, 0.175f, 0.13f), side = new Color(0.40f, 0.29f, 0.21f), belly = new Color(0.72f, 0.58f, 0.47f), blush = true,
        };
        bad = new Kind
        {
            key = "Brute", bad = true,
            prof = new[] { 0.24f, 0.275f, 0.29f, 0.28f, 0.235f, 0.2f }, dome = 0.66f, sx = 1.08f, sz = 0.92f, lean = 0.025f,
            back = new Color(0.12f, 0.21f, 0.08f), side = new Color(0.27f, 0.44f, 0.16f), belly = new Color(0.58f, 0.64f, 0.36f), blush = false,
        };

        claw = SaveMesh(Surface("Claw", Tube(Vector3.zero, Vector3.forward, s => 0.5f * Mathf.Pow(1f - s, 0.85f), 1f, 1f), s => Vector3.forward * s, 10, 8, 0f, false), "Claw");

        foreach (Kind k in new[] { normal, bad })
        {
            string prefix = k.bad ? "MoleBad" : "MoleFur";   // Mole.cs가 맞았을 때 색을 바꿀 부위로 인식하는 이름
            Texture2D tex = FurTexture(k);
            Func<float, float, Vector3> P = (u, t) => BodyPoint(k, u, t);
            Func<float, Vector3> A = t => BodyAxis(k, t);
            k.body = SaveMesh(Surface(k.key + "Body", P, A, 56, 40, 0f, true), k.key + "Body");
            k.shells = new Mesh[ShellCount];
            k.shellMats = new Material[ShellCount];
            for (int i = 0; i < ShellCount; i++)
            {
                float f = (i + 1f) / ShellCount;
                k.shells[i] = SaveMesh(Surface(k.key + "Shell" + i, P, A, 56, 40, FurLength * f, false), k.key + "Shell" + i.ToString("00"));
                Material m = Lit(prefix + "Shell_" + i.ToString("00"), Color.white * Mathf.Lerp(0.62f, 1.12f, f), 0.18f + 0.12f * f, tex);
                MakeCutout(m, Mathf.Lerp(0.08f, 0.8f, f));
                k.shellMats[i] = m;
            }
            k.bodyMat = Lit(prefix + (k.bad ? "FurHD" : "HD"), Color.white * 0.5f, 0.12f, tex);

            // 주둥이: 얼굴 앞에서 살짝 아래로 뻗은 원뿔꼴
            Vector3 sb = SurfacePoint(k, 0f, 0.37f, out _);
            k.snoutA = sb + new Vector3(0f, 0.006f, -0.05f);
            k.snoutB = sb + new Vector3(0f, k.bad ? -0.014f : -0.022f, k.bad ? 0.07f : 0.088f);
            float r0 = k.bad ? 0.088f : 0.072f, r1 = k.bad ? 0.06f : 0.042f;
            k.snout = SaveMesh(Surface(k.key + "Snout", Tube(k.snoutA, k.snoutB, s =>
            {
                float r = Mathf.Lerp(r0, r1, s);
                if (s > 0.72f) { float d = (s - 0.72f) / 0.28f; r *= Mathf.Sqrt(Mathf.Max(0f, 1f - d * d)); }
                return r;
            }, 1.15f, 0.9f), s => Vector3.Lerp(k.snoutA, k.snoutB, s), 28, 16, 0f, false), k.key + "Snout");
        }

        snoutMat = Lit("MoleSnoutHD", new Color(0.90f, 0.62f, 0.58f), 0.42f);
        noseMat = Lit("MoleNoseHD", new Color(0.86f, 0.42f, 0.50f), 0.88f);
        nostrilMat = Lit("MoleNostrilHD", new Color(0.12f, 0.05f, 0.06f), 0.6f);
        eyeMat = Lit("MoleEyeHD", new Color(0.03f, 0.03f, 0.035f), 0.25f);
        pawMat = Lit("MolePawHD", new Color(0.88f, 0.63f, 0.58f), 0.35f);
        clawMat = Lit("MoleClawHD", new Color(0.93f, 0.88f, 0.78f), 0.62f);
        whiskerMat = Lit("MoleWhiskerHD", new Color(0.92f, 0.9f, 0.85f), 0.5f);
        toothMat = Lit("MoleToothHD", new Color(0.97f, 0.95f, 0.88f), 0.7f);

        bSnoutMat = Lit("MoleBadSnoutHD", new Color(0.22f, 0.36f, 0.14f), 0.3f);
        bNoseMat = Lit("MoleBadNoseHD", new Color(0.06f, 0.09f, 0.05f), 0.85f);
        bPawMat = Lit("MoleBadPawHD", new Color(0.17f, 0.30f, 0.12f), 0.3f);
        bClawMat = Lit("BadClawHD", new Color(0.13f, 0.12f, 0.11f), 0.55f);
        bSclera = Lit("BadScleraHD", new Color(0.95f, 0.92f, 0.8f), 0.25f);
        bPupil = Lit("BadEye", new Color(0.85f, 0.08f, 0.04f), 0.25f, null, null, "Assets/WhackAMole/Materials/Env");
        bPupil.DisableKeyword("_EMISSION"); bPupil.SetColor("_EmissionColor", Color.black); // 빛나지 않게
        bDark = Lit("MoleBadDark", new Color(0.14f, 0.26f, 0.10f), 0.25f, null, null, "Assets/WhackAMole/Materials/Env");
        bScar = Lit("BadScar", new Color(0.78f, 0.45f, 0.45f), 0.3f, null, null, "Assets/WhackAMole/Materials/Env");
        bWhisker = Lit("BadWhiskerHD", new Color(0.1f, 0.12f, 0.08f), 0.4f);
        AssetDatabase.SaveAssets();
    }

    // 털 결 텍스처 (RGB = 털 색 · 무늬, A = 털 한 가닥의 높이). 몸통과 털 껍질이 함께 씀
    static Texture2D FurTexture(Kind k)
    {
        string path = $"{Dir}/{k.key}Fur.png";
        const int W = 1024, HH = 512, C = 4;
        int cw = W / C, ch = HH / C;
        var rnd = new System.Random(k.bad ? 17 : 5);
        float[] hgt = new float[cw * ch], br = new float[cw * ch], jx = new float[cw * ch], jy = new float[cw * ch];
        for (int i = 0; i < hgt.Length; i++)
        {
            hgt[i] = 0.45f + 0.55f * (float)rnd.NextDouble();
            br[i] = 0.84f + 0.32f * (float)rnd.NextDouble();
            jx[i] = ((float)rnd.NextDouble() - 0.5f) * 0.9f;
            jy[i] = ((float)rnd.NextDouble() - 0.5f) * 0.9f;
        }
        var px = new Color32[W * HH];
        for (int y = 0; y < HH; y++)
            for (int x = 0; x < W; x++)
            {
                int id = (y / C) * cw + (x / C);
                float dx = (x % C) + 0.5f - (C * 0.5f + jx[id]);
                float dy = (y % C) + 0.5f - (C * 0.5f + jy[id]);
                float a = hgt[id] * Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 2.4f);
                Color c = Field(k, (x + 0.5f) / W, (y + 0.5f) / HH) * br[id] * (0.86f + 0.26f * a);
                px[y * W + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), a);
            }
        var tex = new Texture2D(W, HH, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.sRGBTexture = true;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.mipMapsPreserveCoverage = true;
        ti.alphaTestReferenceValue = 0.35f;
        ti.wrapModeU = TextureWrapMode.Repeat;
        ti.wrapModeV = TextureWrapMode.Clamp;
        ti.anisoLevel = 4;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 몸 위치별 털 색: 등은 진하게, 옆은 기본, 배와 얼굴은 밝게, (귀여운 두더지는) 볼 홍조
    static Color Field(Kind k, float u, float v)
    {
        float f = Mathf.Cos(2f * Mathf.PI * (u - 0.5f));                 // 1 = 정면, -1 = 등
        Color c = Color.Lerp(k.side, k.back, Mathf.Clamp01(-f * 1.1f));
        c = Color.Lerp(c, k.back, Smooth(0.85f, 1f, v) * 0.5f);            // 정수리 진하게
        float bellyW = Smooth(0.25f, 0.85f, f) * (1f - Smooth(0.48f, 0.66f, v));
        float faceW = Smooth(0.55f, 0.95f, f) * Smooth(0.6f, 0.7f, v) * (1f - Smooth(0.8f, 0.9f, v)) * 0.55f;
        c = Color.Lerp(c, k.belly, Mathf.Max(bellyW, faceW));
        c *= 0.9f + 0.18f * Mathf.PerlinNoise(u * 9f, v * 5f);              // 큰 얼룩 (단조로움 줄이기)
        if (k.blush)
            foreach (float sx in new[] { -1f, 1f })
            {
                float du = u - (0.5f + sx * 0.098f), dv = v - 0.725f;
                float w = Mathf.Exp(-(du * du / (0.026f * 0.026f) + dv * dv / (0.04f * 0.04f)));
                c = Color.Lerp(c, new Color(0.93f, 0.56f, 0.55f), w * 0.6f);
            }
        c.a = 1f;
        return c;
    }

    static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    // ───────────────────────── 메시 도우미 ─────────────────────────
    static Func<float, float, Vector3> Tube(Vector3 a, Vector3 b, Func<float, float> rad, float ex, float ey)
    {
        Vector3 f = (b - a).normalized;
        Vector3 r = Vector3.Cross(Vector3.up, f); if (r.sqrMagnitude < 1e-6f) r = Vector3.right; r.Normalize();
        Vector3 up = Vector3.Cross(f, r);
        return (u, s) =>
        {
            float ang = 2f * Mathf.PI * u, rr = rad(s);
            return Vector3.Lerp(a, b, s) + (r * Mathf.Cos(ang) * ex + up * Mathf.Sin(ang) * ey) * rr;
        };
    }

    static Vector3 SurfNormal(Func<float, float, Vector3> P, Func<float, Vector3> axis, float u, float s)
    {
        const float e = 1e-3f;
        // 미분값을 e로 나눠 키움 (Unity normalized는 길이 1e-5 미만이면 0을 돌려줌)
        Vector3 ds = (P(u, Mathf.Min(s + e, 1f)) - P(u, Mathf.Max(s - e, 0f))) / e;
        Vector3 du = (P(u + e, s) - P(u - e, s)) / e;
        Vector3 n = Vector3.Cross(ds, du);
        if (n.sqrMagnitude < 1e-8f)
        {
            Vector3 tan = (axis(Mathf.Min(s + e, 1f)) - axis(Mathf.Max(s - e, 0f))) / e;
            n = s > 0.5f ? tan : -tan;
        }
        else if (Vector3.Dot(n, P(u, s) - axis(s)) < 0f) n = -n;
        return n.normalized;
    }

    static Mesh Surface(string name, Func<float, float, Vector3> P, Func<float, Vector3> axis, int seg, int rings, float offset, bool capStart)
    {
        var v = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float s = (float)j / rings;
            for (int i = 0; i <= seg; i++)
            {
                float u = (float)i / seg;
                Vector3 n = SurfNormal(P, axis, u, s);
                v.Add(P(u, s) + n * offset); nr.Add(n); uv.Add(new Vector2(u, s));
            }
        }
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = j * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                tri.Add(a); tri.Add(c); tri.Add(b);
                tri.Add(b); tri.Add(c); tri.Add(d);
            }
        // 앞면 방향 확인 (Unity는 시계 방향이 앞면) – 반대면 뒤집기
        int m0 = (rings / 2) * (seg + 1) + seg / 4;
        Vector3 fn = Vector3.Cross(v[m0 + seg + 1] - v[m0], v[m0 + 1] - v[m0]);
        if (Vector3.Dot(fn, nr[m0]) < 0f)
            for (int t = 0; t < tri.Count; t += 3) { int tmp = tri[t + 1]; tri[t + 1] = tri[t + 2]; tri[t + 2] = tmp; }
        if (capStart)
        {
            Vector3 cn = -(axis(0.01f) - axis(0f)).normalized;
            int center = v.Count; v.Add(axis(0f)); nr.Add(cn); uv.Add(new Vector2(0.5f, 0f));
            int ring = v.Count;
            for (int i = 0; i <= seg; i++) { v.Add(v[i]); nr.Add(cn); uv.Add(new Vector2((float)i / seg, 0f)); }
            for (int i = 0; i < seg; i++)
            {
                int a = ring + i, b = ring + i + 1;
                Vector3 g = Vector3.Cross(v[a] - v[center], v[b] - v[center]);
                if (Vector3.Dot(g, cn) >= 0f) { tri.Add(center); tri.Add(a); tri.Add(b); }
                else { tri.Add(center); tri.Add(b); tri.Add(a); }
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetNormals(nr); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string file)
    {
        string path = $"{Dir}/{file}.asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        // CopySerialized는 법선이 0으로 저장되는 문제가 있어 값을 직접 복사
        existing.Clear();
        existing.name = mesh.name;
        existing.vertices = mesh.vertices;
        existing.normals = mesh.normals;
        existing.tangents = mesh.tangents;
        existing.uv = mesh.uv;
        existing.triangles = mesh.triangles;
        existing.RecalculateBounds();
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static Material Lit(string name, Color c, float smooth, Texture tex = null, Color? emission = null, string dir = Dir)
    {
        string path = $"{dir}/{name}.mat";
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        if (m.shader != sh) m.shader = sh;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", Vector2.one);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void MakeCutout(Material m, float cutoff)
    {
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", cutoff);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        if (m.HasProperty("_AlphaToMask")) m.SetFloat("_AlphaToMask", 1f);
        EditorUtility.SetDirty(m);
    }

    static Transform Group(Transform parent, string name, Vector3 pos, Quaternion rot, float scale)
    {
        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, "Build mole");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = Vector3.one * scale;
        return g.transform;
    }

    static Transform MeshObj(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return g.transform;
    }

    static void Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
    }
}

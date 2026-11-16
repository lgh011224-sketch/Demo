// 그래피티 · 레트로 간판 그림을 미리 렌더링해서 PNG로 굽는 도구
//   그래피티: 스프레이 구름 + 검은 외곽선 굵은 글자(글자마다 기울기·높이 다르게) + 입체 그림자 + 흘러내린 물감 + 별
//   레트로 간판(마키): 80년대 미국 오락실 고전 게임기 위 간판 – 노을 원 + 원근 격자 + 크롬 느낌 기울임 제목
// 바탕을 검정/흰색으로 두 번 찍어 투명도(알파)를 계산 → 벽에 붙이면 글자 둘레만 보임
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GraffitiBaker
{
    const string Dir = ArcadeBuilder.Dir + "/Graffiti";
    static Font font;
    static Material textMat;
    static Texture2D blobTex;

    static readonly Color[][] Palettes =
    {
        new[] { new Color(1f, 0.25f, 0.7f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.9f, 0.2f) },
        new[] { new Color(0.55f, 1f, 0.25f), new Color(0.7f, 0.3f, 1f), new Color(1f, 0.55f, 0.15f) },
        new[] { new Color(0.2f, 0.95f, 1f), new Color(1f, 0.2f, 0.55f), new Color(1f, 1f, 1f) },
        new[] { new Color(1f, 0.8f, 0.1f), new Color(1f, 0.25f, 0.25f), new Color(0.4f, 0.4f, 1f) },
        new[] { new Color(0.85f, 0.4f, 1f), new Color(0.3f, 1f, 0.75f), new Color(1f, 0.4f, 0.8f) },
    };

    static void Init()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder(ArcadeBuilder.Dir, "Graffiti");
        if (font == null) font = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/NotoSansKR.ttf");
        if (textMat == null) textMat = TextMat.For(font);
        if (blobTex == null)
        {
            const int N = 128;
            blobTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N * 0.5f, N * 0.5f)) / (N * 0.5f);
                    px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
                }
            blobTex.SetPixels(px); blobTex.Apply();
        }
    }

    // ───────────── 그래피티 한 점 ─────────────
    public static Material Graffiti(string key, string text, int palette, int seed)
    {
        Init();
        string path = $"{Dir}/G_{key}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            tex = Bake(path, 1024, 512, 1f, root =>
            {
                var rng = new System.Random(seed);
                var pal = Palettes[palette % Palettes.Length];
                // 스프레이 구름
                for (int i = 0; i < 6; i++)
                    Blob(root, new Vector3(((float)rng.NextDouble() - 0.5f) * 2.8f, ((float)rng.NextDouble() - 0.5f) * 0.9f, 0.2f), 0.9f + (float)rng.NextDouble() * 1.2f, pal[rng.Next(pal.Length)] * 0.9f, 0.45f);
                // 글자
                float h = 0.85f;
                float adv = h * 0.66f;
                float total = text.Length * adv;
                float scale = Mathf.Min(1f, 3.6f / total);
                h *= scale; adv *= scale;
                float x0 = -(text.Length - 1) * adv * 0.5f;
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    if (ch == ' ') continue;
                    float x = x0 + i * adv;
                    float y = ((float)rng.NextDouble() - 0.5f) * 0.12f;
                    float rot = ((float)rng.NextDouble() - 0.5f) * 18f;
                    Color fill = pal[i % 2], deep = pal[i % 2] * 0.45f; deep.a = 1f;
                    Letter(root, ch.ToString(), new Vector3(x, y, 0f), rot, h, fill, deep, FontStyle.Bold, 0.055f * scale + 0.02f);
                    if (rng.Next(3) == 0)   // 흘러내린 물감
                    {
                        float len = 0.15f + (float)rng.NextDouble() * 0.3f;
                        Quad(root, new Vector3(x + ((float)rng.NextDouble() - 0.5f) * adv * 0.5f, y - h * 0.42f - len * 0.5f, 0.02f), new Vector2(0.035f, len), fill);
                        Blob(root, new Vector3(x + 0f, y - h * 0.42f - len, 0.015f), 0.07f, fill, 1f, true);
                    }
                }
                // 별 장식
                for (int s = 0; s < 3; s++)
                    Letter(root, "★", new Vector3((s - 1) * 1.6f + ((float)rng.NextDouble() - 0.5f) * 0.4f, 0.62f + ((float)rng.NextDouble() - 0.5f) * 0.2f, -0.05f), ((float)rng.NextDouble() - 0.5f) * 30f, 0.32f, pal[2], pal[2] * 0.4f, FontStyle.Normal, 0.03f);
            });
        }
        var m = ArcadeBuilder.Mat("Graffiti_" + key, Color.white, 0.25f, 0f, Color.white * 0.35f, tex);   // 블랙라이트 형광 페인트처럼 살짝 빛남
        m.SetTexture("_EmissionMap", tex);
        MakeTransparent(m);
        return m;
    }

    // ───────────── 레트로 오락기 간판 ─────────────
    public static Material RetroMarquee(string key, string title, string sub, string bottom, int texW = 1600, int texH = 350)
    {
        Init();
        string path = $"{Dir}/Marquee_{key}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            tex = Bake(path, texW, texH, 1f, root =>
            {
                float W = 2f * texW / texH;   // 가로 길이 (세로 = 2)
                var bg = BackgroundTexture(texW, texH);
                var q = Quad(root, new Vector3(0f, 0f, 1f), new Vector2(W, 2f), Color.white);
                q.GetComponent<Renderer>().sharedMaterial.SetTexture("_BaseMap", bg);
                // 제목: 빨강 외곽선 → 주황 입체(여러 겹) → 노랑 → 흰 반사
                var italic = FontStyle.BoldAndItalic;
                float h = Mathf.Min(0.95f, W / (title.Length * 0.62f));
                for (int k = 6; k >= 1; k--)
                    TextObj(root, title, new Vector3(0.012f * k, -0.012f * k - 0.05f, 0.1f + k * 0.01f), h, Color.Lerp(new Color(0.85f, 0.25f, 0.05f), new Color(0.45f, 0.05f, 0.1f), k / 6f), italic);
                for (int a = 0; a < 12; a++)
                {
                    float ang = a * Mathf.PI * 2f / 12f;
                    TextObj(root, title, new Vector3(Mathf.Cos(ang) * 0.035f, Mathf.Sin(ang) * 0.035f - 0.05f, 0.09f), h, new Color(0.1f, 0f, 0.15f), italic);
                }
                TextObj(root, title, new Vector3(0f, -0.05f, 0.05f), h, new Color(1f, 0.85f, 0.15f), italic);
                TextObj(root, title, new Vector3(-0.008f, -0.035f, 0.04f), h * 0.985f, new Color(1f, 0.97f, 0.7f, 0.55f), italic);
                TextObj(root, sub, new Vector3(0f, 0.72f, 0.05f), 0.28f, new Color(0.3f, 0.95f, 1f), FontStyle.Bold);
                TextObj(root, bottom, new Vector3(0f, -0.78f, 0.05f), 0.22f, new Color(1f, 0.4f, 0.8f), FontStyle.Bold);
                foreach (float sx in new[] { -1f, 1f })
                    TextObj(root, "★", new Vector3(sx * (W * 0.5f - 0.5f), 0.05f, 0.05f), 0.6f, new Color(1f, 0.9f, 0.3f), FontStyle.Normal);
            }, opaque: true);
        }
        var m = ArcadeBuilder.Mat("Marquee_" + key, Color.white * 0.35f, 0.85f, 0f, Color.white * 1.15f, tex);   // 뒤에서 빛나는 아크릴 간판
        m.SetTexture("_EmissionMap", tex);
        return m;
    }

    // 80년대 노을 + 원근 격자 배경
    static Texture2D BackgroundTexture(int W, int H)
    {
        var t = new Texture2D(W, H, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[W * H];
        Vector2 sun = new Vector2(W * 0.5f, H * 0.42f); float sunR = H * 0.45f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = y / (float)H;
                Color c = Color.Lerp(new Color(0.02f, 0.0f, 0.06f), new Color(0.25f, 0.02f, 0.35f), Mathf.Clamp01((v - 0.2f) * 1.4f));
                float d = Vector2.Distance(new Vector2(x, y), sun);
                if (d < sunR && y > H * 0.42f)
                {
                    float sv = (y - H * 0.42f) / sunR;
                    bool gap = sv < 0.5f && Mathf.Repeat(sv * 14f, 1f) < 0.35f;   // 노을 줄무늬
                    if (!gap) c = Color.Lerp(new Color(1f, 0.25f, 0.55f), new Color(1f, 0.85f, 0.2f), sv);
                }
                if (y < H * 0.42f)   // 바닥 원근 격자
                {
                    float gy = (H * 0.42f - y) / (H * 0.42f);
                    float depth = 1f / (gy + 0.05f);
                    bool hline = Mathf.Repeat(depth * 0.6f, 1f) < 0.08f * depth * 0.4f + 0.02f;
                    float gx = (x - W * 0.5f) / (W * 0.5f) / (1.05f - gy);
                    bool vline = Mathf.Repeat(gx * 6f, 1f) < 0.04f + gy * 0.05f;
                    c = new Color(0.03f, 0f, 0.07f);
                    if (hline || vline) c = Color.Lerp(new Color(1f, 0.2f, 0.75f), new Color(0.4f, 0.1f, 0.6f), 1f - gy);
                }
                px[y * W + x] = c;
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    // ───────────── 굽기 ─────────────
    static Texture2D Bake(string path, int W, int H, float halfHeight, System.Action<Transform> build, bool opaque = false)
    {
        var root = new GameObject("__GraffitiBake") { hideFlags = HideFlags.HideAndDontSave };
        root.transform.position = new Vector3(0f, -800f, 0f);
        build(root.transform);
        var camGo = new GameObject("__GraffitiCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = halfHeight;
        cam.transform.position = root.transform.position + new Vector3(0f, 0f, -5f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = false; cam.allowMSAA = true;
        var data = camGo.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false; data.renderShadows = false;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { antiAliasing = 8 };
        cam.targetTexture = rt;
        cam.aspect = W / (float)H;

        Color[] Shoot(Color bg)
        {
            cam.backgroundColor = bg;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false, true);
            t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply();
            RenderTexture.active = prev;
            var p = t.GetPixels(); Object.DestroyImmediate(t);
            return p;
        }
        var black = Shoot(Color.black);
        var outPx = new Color[W * H];
        if (opaque)
            for (int i = 0; i < outPx.Length; i++) { var c = black[i]; outPx[i] = new Color(Mathf.LinearToGammaSpace(c.r), Mathf.LinearToGammaSpace(c.g), Mathf.LinearToGammaSpace(c.b), 1f); }
        else
        {
            var white = Shoot(Color.white);
            for (int i = 0; i < outPx.Length; i++)
            {
                Color b = black[i], w = white[i];
                float a = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                Color c = a > 0.002f ? new Color(b.r / a, b.g / a, b.b / a) : Color.black;
                outPx[i] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(c.r)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.g)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.b)), a);
            }
        }
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(root);

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
        tex.SetPixels(outPx); tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.alphaIsTransparency = !opaque; ti.wrapMode = TextureWrapMode.Clamp; ti.anisoLevel = 4; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 글자 하나: 검은 외곽선(여러 방향) + 입체 그림자 + 채움색
    static void Letter(Transform root, string s, Vector3 p, float rot, float h, Color fill, Color deep, FontStyle style, float outline)
    {
        var g = new GameObject("L"); g.transform.SetParent(root, false); g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
        for (int a = 0; a < 16; a++)
        {
            float ang = a * Mathf.PI * 2f / 16f;
            TextObj(g, s, new Vector3(Mathf.Cos(ang) * outline + 0.03f, Mathf.Sin(ang) * outline - 0.03f, 0.06f), h, new Color(0.03f, 0.02f, 0.05f), style);
        }
        for (int k = 3; k >= 1; k--) TextObj(g, s, new Vector3(0.012f * k, -0.012f * k, 0.03f + k * 0.002f), h, deep, style);
        TextObj(g, s, Vector3.zero, h, fill, style);
        TextObj(g, s, new Vector3(-0.012f, 0.012f, -0.005f), h * 0.97f, new Color(1f, 1f, 1f, 0.35f), style);
    }

    static void TextObj(Transform parent, string s, Vector3 p, float h, Color c, FontStyle style) => TextObj(parent.gameObject, s, p, h, c, style);

    static void TextObj(GameObject parent, string s, Vector3 p, float h, Color c, FontStyle style)
    {
        var g = new GameObject("T"); g.transform.SetParent(parent.transform, false); g.transform.localPosition = p;
        var tm = g.AddComponent<TextMesh>();
        tm.font = font; tm.fontSize = 96; tm.characterSize = h / (96f * 0.1f); tm.fontStyle = style;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c; tm.text = s;
        g.GetComponent<MeshRenderer>().sharedMaterial = textMat;
    }

    static Transform Quad(Transform root, Vector3 p, Vector2 size, Color c)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.hideFlags = HideFlags.HideAndDontSave;
        q.transform.SetParent(root, false); q.transform.localPosition = p; q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
        m.SetColor("_BaseColor", c);
        q.GetComponent<Renderer>().sharedMaterial = m;
        return q.transform;
    }

    static void Blob(Transform root, Vector3 p, float size, Color c, float alpha, bool hard = false)
    {
        var q = Quad(root, p, new Vector2(size, size), new Color(c.r, c.g, c.b, alpha));
        var m = q.GetComponent<Renderer>().sharedMaterial;
        m.SetTexture("_BaseMap", blobTex);
        MakeTransparent(m);
        if (hard) m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
    }

    public static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        // URP 기본 '반사광 유지'를 끄고 반사·하이라이트도 끔 → 투명한 부분에 네모 판이 비쳐 보이지 않게
        if (m.HasProperty("_BlendModePreserveSpecular")) m.SetFloat("_BlendModePreserveSpecular", 0f);
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        if (!m.hideFlags.HasFlag(HideFlags.DontSave)) EditorUtility.SetDirty(m);
    }

    // 벽에 그래피티 한 장 붙이기 (앞 = 보는 쪽에서 읽힘)
    public static Transform Place(Transform parent, Material m, Vector3 pos, float yaw, float width)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Graffiti";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.position = pos;
        q.transform.rotation = Quaternion.Euler(0f, yaw, 0f);   // Quad 앞면은 -Z → yaw 방향을 보는 사람에게 보임
        q.transform.localScale = new Vector3(width, width * 0.5f, 1f);
        var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
        return q.transform;
    }
}

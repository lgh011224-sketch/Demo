// 도구 변신 이펙트(ToolTransformFx) 설정: 빛 고리·반짝이·빛무리 텍스처와 가산(Additive) 파티클 머티리얼을 만들어
// 두더지 씬의 ToolRoot에 붙이고 GameManager에 연결
// 메뉴: Tools > Whack-A-Mole > Setup Tool Transform Fx
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackToolFxSetup
{
    const string Dir = "Assets/WhackAMole/Materials/Fx";

    [MenuItem("Tools/Whack-A-Mole/Setup Tool Transform Fx")]
    public static string Setup()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Materials", "Fx");
        var ring = Additive("AuraRing", Tex("AuraRing.png", DrawRing));
        var spark = Additive("AuraSparkle", Tex("AuraSparkle.png", DrawSparkle));
        var glow = Additive("AuraGlow", Tex("AuraGlow.png", DrawGlow));

        var gm = Object.FindAnyObjectByType<WhackGameManager>();
        var toolRoot = GameObject.Find("ToolRoot");
        if (gm == null || toolRoot == null) return "GameManager/ToolRoot 없음";
        var fx = toolRoot.GetComponent<ToolTransformFx>();
        if (fx == null) fx = Undo.AddComponent<ToolTransformFx>(toolRoot);
        Undo.RecordObject(fx, "Fx");
        fx.ringMaterial = ring; fx.sparkleMaterial = spark; fx.glowMaterial = glow;
        // 도구 머리 쪽 (도구가 ToolRoot의 +Z로 뻗음)
        fx.center = toolRoot.transform; fx.centerOffset = new Vector3(0f, 0f, 0.18f);
        Undo.RecordObject(gm, "Fx");
        gm.transformFx = fx;
        if (string.IsNullOrEmpty(gm.woodCommand)) gm.woodCommand = "MODE STICK";
        if (string.IsNullOrEmpty(gm.hammerCommand)) gm.hammerCommand = "MODE HAMMER";
        EditorUtility.SetDirty(fx); EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        return "transform fx ready";
    }

    // URP 파티클 Unlit 가산 혼합 (빛나는 효과), 양면, 깊이 기록 안 함
    static Material Additive(string name, Texture2D tex)
    {
        string path = $"{Dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // Additive
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    delegate Color Painter(float u, float v);

    static Texture2D Tex(string file, Painter p)
    {
        string path = $"{Dir}/{file}";
        const int S = 256;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                t.SetPixel(x, y, p((x + 0.5f) / S * 2f - 1f, (y + 0.5f) / S * 2f - 1f));
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 가는 빛의 고리 + 고리 위 반짝이 점들 + 룬 무늬 느낌의 끊긴 안쪽 고리
    static Color DrawRing(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
        float main = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.035f, 2f));
        float inner = Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.02f, 2f)) * (Mathf.Sin(a * 12f) > 0.2f ? 1f : 0f) * 0.7f;
        float dots = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.06f, 2f)) * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a * 8f)), 30f);
        float halo = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.15f, 2f)) * 0.25f;
        float alpha = Mathf.Clamp01(main + inner + dots + halo);
        return new Color(1f, 1f, 1f, alpha);
    }

    // 네 갈래 별 반짝이
    static Color DrawSparkle(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float star = Mathf.Max(Mathf.Exp(-Mathf.Abs(u) * 18f) * Mathf.Exp(-Mathf.Abs(v) * 2.2f), Mathf.Exp(-Mathf.Abs(v) * 18f) * Mathf.Exp(-Mathf.Abs(u) * 2.2f));
        float core = Mathf.Exp(-r * r * 30f);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(star + core));
    }

    // 부드러운 빛무리
    static Color DrawGlow(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Exp(-r * r * 4f) - 0.02f));
    }
}

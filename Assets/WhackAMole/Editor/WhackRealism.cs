// 세미 실사 룩 적용 (배경은 실사, 두더지는 캐릭터 유지)
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Apply Semi-Realistic Look
//  1. 하늘: Poly Haven HDRI(meadow_2) 스카이박스 + 환경광·반사, 태양 방향을 HDRI 속 해에 맞춤
//  2. 재질: 사진 기반 PBR 텍스처 (잔디, 흙, 낡은 흰 페인트 나무 울타리, 초록 판자 탁자, 나무 스틱·자루)
//  3. 렌더링: PC(Link)용 고품질 URP 설정을 복제해 High Fidelity 품질에 연결 (HDR, 4단 그림자, SSAO)
//     - VR 템플릿의 URP-Performant 원본은 수정하지 않으며, Quest 단독(Android)은 기존 설정 유지
//  4. 후처리: ACES 톤매핑, 은은한 블룸, 색 보정 (Global Volume)
// 에셋: Assets/WhackAMole/External/PolyHaven (CC0)
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class WhackRealism
{
    const string PH = "Assets/WhackAMole/External/PolyHaven";
    const string OutDir = "Assets/WhackAMole/Realism";
    const string HdriPath = PH + "/HDRI/meadow_2_4k.hdr";

    [MenuItem("Tools/Whack-A-Mole/Apply Semi-Realistic Look")]
    public static void ApplyMenu() => Debug.Log("[실사] " + Apply());

    public static string Apply()
    {
        if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/WhackAMole", "Realism");
        var log = new System.Text.StringBuilder();
        log.AppendLine(SetupTextures());
        log.AppendLine(SetupSky());
        log.AppendLine(SetupMaterials());
        log.AppendLine(SetupPipeline());
        log.AppendLine(SetupPostProcessing());
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManagerActive());
        return log.ToString();
    }

    static UnityEngine.SceneManagement.Scene SceneManagerActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    // ───────────────────────── 1. 텍스처 가져오기 설정 ─────────────────────────
    static string SetupTextures()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PH + "/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool changed = false;
            void Set<T>(Func<T> get, Action<T> set, T v) { if (!Equals(get(), v)) { set(v); changed = true; } }
            if (path.Contains("_nor_gl")) Set(() => imp.textureType, v => imp.textureType = v, TextureImporterType.NormalMap);
            if (path.Contains("_rough") || path.Contains("_ao")) Set(() => imp.sRGBTexture, v => imp.sRGBTexture = v, false);
            Set(() => imp.anisoLevel, v => imp.anisoLevel = v, 8);           // VR에서 비스듬히 보는 바닥이 뭉개지지 않게
            Set(() => imp.maxTextureSize, v => imp.maxTextureSize = v, 2048);
            Set(() => imp.wrapMode, v => imp.wrapMode = v, TextureWrapMode.Repeat);
            if (changed) { imp.SaveAndReimport(); n++; }
        }
        var hdr = AssetImporter.GetAtPath(HdriPath) as TextureImporter;
        if (hdr != null && (hdr.maxTextureSize != 4096 || hdr.mipmapEnabled || hdr.wrapModeU != TextureWrapMode.Repeat))
        {
            hdr.maxTextureSize = 4096; hdr.mipmapEnabled = false;
            hdr.wrapModeU = TextureWrapMode.Repeat; hdr.wrapModeV = TextureWrapMode.Clamp;
            hdr.SaveAndReimport(); n++;
        }
        return $"textures reimported: {n}";
    }

    // ───────────────────────── 2. 하늘 · 태양 · 환경광 ─────────────────────────
    static string SetupSky()
    {
        var hdri = AssetDatabase.LoadAssetAtPath<Texture2D>(HdriPath);
        if (hdri == null) return "HDRI missing";
        Material sky = LoadOrCreate($"{OutDir}/Sky_Meadow.mat", "Skybox/Panoramic");
        sky.SetTexture("_MainTex", hdri);
        sky.SetFloat("_Exposure", 1.0f);
        if (sky.HasProperty("_Mapping")) sky.SetFloat("_Mapping", 1f); // Latitude-Longitude
        if (sky.HasProperty("_ImageType")) sky.SetFloat("_ImageType", 0f);

        // HDRI에서 가장 밝은 점(해)과 지평선 색을 찾음
        FindSun(hdri, out Vector3 sunTex, out Color horizon, out Color sunColor);
        // 해가 플레이어 뒤쪽(-Z) 오른쪽 위에서 비추도록 스카이박스를 Y축으로 돌림 → 정면 하늘이 눈부시지 않고 두더지 얼굴이 밝음
        Vector3 desired = new Vector3(0.45f, 0f, -0.9f).normalized;
        float bestRot = 0f, bestDot = -2f;
        for (int r = 0; r < 360; r++)
        {
            Vector3 w = InvRotY(sunTex, r); Vector3 flat = new Vector3(w.x, 0f, w.z).normalized;
            float d = Vector3.Dot(flat, desired);
            if (d > bestDot) { bestDot = d; bestRot = r; }
        }
        sky.SetFloat("_Rotation", bestRot);
        EditorUtility.SetDirty(sky);
        Vector3 sunWorld = InvRotY(sunTex, bestRot);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        // 안개: 지평선 색으로 아주 멀리서만 (HDRI 풍경과 초원 경계를 자연스럽게)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        // (HDRI 지평선은 숲이라 어두움 → 옅은 대기 색을 직접 지정)
        RenderSettings.fogColor = new Color(0.58f, 0.65f, 0.66f);
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 260f;

        Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            Undo.RecordObject(sun, "Sun"); Undo.RecordObject(sun.transform, "Sun");
            // 태양 고도가 너무 낮거나 높으면 보기 좋은 범위(25~60도)로 맞춤
            float elev = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(sunWorld.y, -1f, 1f)) * Mathf.Rad2Deg, 25f, 60f);
            Vector3 flat = new Vector3(sunWorld.x, 0f, sunWorld.z).normalized;
            Vector3 toSun = (flat * Mathf.Cos(elev * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(elev * Mathf.Deg2Rad)).normalized;
            sun.transform.rotation = Quaternion.LookRotation(-toSun);
            sun.color = Color.Lerp(Color.white, sunColor, 0.5f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;
        }
        DynamicGI.UpdateEnvironment();
        return $"sky rot={bestRot} sunTex={sunTex:F2} horizon={horizon}";
    }

    static Vector3 InvRotY(Vector3 v, float deg)
    {
        // Skybox/Panoramic는 조회 방향을 +deg 회전 → 월드 방향은 -deg 회전
        float a = -deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector3(c * v.x - s * v.z, v.y, s * v.x + c * v.z);
    }

    static void FindSun(Texture2D hdri, out Vector3 dir, out Color horizon, out Color sunColor)
    {
        const int W = 512, H = 256;
        var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(hdri, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGBAHalf, false, true);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels(); Object.DestroyImmediate(tex);

        int best = 0; float bestL = -1f;
        Color hsum = Color.black; int hn = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color c = px[y * W + x];
                float l = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                if (y > H / 2 && l > bestL) { bestL = l; best = y * W + x; } // 하늘 쪽 반구에서만
                if (Mathf.Abs(y - H / 2 - 6) < 3) { hsum += c; hn++; }     // 지평선 바로 위 띠
            }
        int bx = best % W, by = best / W;
        float u = (bx + 0.5f) / W, v = (by + 0.5f) / H;
        float lon = (u - 0.5f) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
        dir = new Vector3(Mathf.Cos(lon) * Mathf.Sin(lat), Mathf.Cos(lat), Mathf.Sin(lon) * Mathf.Sin(lat)).normalized;
        horizon = hn > 0 ? hsum / hn : new Color(0.7f, 0.8f, 0.9f);
        float m = Mathf.Max(horizon.r, horizon.g, horizon.b);
        if (m > 1f) horizon /= m;
        horizon.a = 1f;
        Color sc = px[best]; float sm = Mathf.Max(sc.r, sc.g, sc.b, 1e-4f);
        sunColor = new Color(sc.r / sm, sc.g / sm, sc.b / sm, 1f);
    }

    // ───────────────────────── 3. PBR 재질 ─────────────────────────
    static string SetupMaterials()
    {
        var sb = new System.Text.StringBuilder();
        // (머티리얼 경로, 텍스처 id, 타일링, 색 보정, 매끄러움)
        var jobs = new (string mat, string tex, Vector2 tile, Color tint, float smooth)[]
        {
            // 잔디: Poly Haven 잔디는 마른 톤이라 녹색 쪽으로 보정 (1 넘는 값은 해당 채널 강조)
            ("Assets/WhackAMole/Materials/Floor.mat",            "grass_ground",                 new Vector2(8f, 8f),     new Color(0.8f, 1.2f, 0.6f), 0.1f),
            ("Assets/WhackAMole/Materials/Env/Meadow.mat",       "grass_ground",                 new Vector2(150f, 150f), new Color(0.75f, 1.12f, 0.58f), 0.06f),
            ("Assets/WhackAMole/Materials/Env/Dirt.mat",         "park_dirt",                    new Vector2(2f, 2f),     Color.white, 0.12f),
            ("Assets/WhackAMole/Materials/Env/DirtDark.mat",     "red_dirt_mud_01",              new Vector2(1f, 1f),     new Color(0.6f, 0.5f, 0.45f), 0.2f),
            ("Assets/WhackAMole/Materials/Env/FenceWhite.mat",   "wood_peeling_paint_weathered", new Vector2(1f, 1f),     Color.white, 0.2f),
            ("Assets/WhackAMole/Materials/Table.mat",            "green_rough_planks",           new Vector2(1f, 1f),     Color.white, 0.2f),
            ("Assets/WhackAMole/Materials/DrumWood.mat",         "fine_grained_wood",            new Vector2(1f, 4f),     new Color(1f, 0.92f, 0.8f), 0.45f),
            ("Assets/WhackAMole/Materials/WoodLight.mat",        "fine_grained_wood",            new Vector2(1f, 3f),     Color.white, 0.35f),
        };
        foreach (var j in jobs)
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(j.mat);
            if (m == null) { sb.Append($"[no {j.mat}] "); continue; }
            string b = $"{PH}/Textures/{j.tex}/{j.tex}";
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_diff_2k.jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_nor_gl_2k.jpg");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(b + "_ao_2k.jpg");
            m.SetTexture("_BaseMap", diff); m.SetTextureScale("_BaseMap", j.tile);
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", diff); m.SetTextureScale("_MainTex", j.tile); }
            m.SetColor("_BaseColor", j.tint);
            if (nor != null) { m.SetTexture("_BumpMap", nor); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", j.smooth);
            EditorUtility.SetDirty(m);
            sb.Append($"{System.IO.Path.GetFileNameWithoutExtension(j.mat)}={j.tex} ");
        }

        // 초원은 넓게 (지평선까지 이어지도록) – HDRI 풍경과 만나는 경계는 안개가 가림
        GameObject meadow = GameObject.Find("Environment/Ground/Meadow");
        if (meadow != null) { Undo.RecordObject(meadow.transform, "Meadow"); meadow.transform.localScale = new Vector3(40f, 1f, 40f); }

        // 울타리: 흰 페인트 칠한 나무 (색 텍스처 대신 나뭇결 노멀만 사용 → 어둡게 얼룩지지 않음)
        var fence = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Env/FenceWhite.mat");
        if (fence != null)
        {
            fence.SetTexture("_BaseMap", null);
            fence.SetColor("_BaseColor", new Color(0.93f, 0.92f, 0.88f));
            fence.SetFloat("_OcclusionStrength", 0.5f);
            fence.SetFloat("_BumpScale", 0.6f);
            EditorUtility.SetDirty(fence);
        }

        // 로우폴리 나무·덤불: 사진 같은 하늘과 어울리도록 채도·밝기를 자연색으로 낮춤
        var natural = new (string n, Color c, float s)[]
        {
            ("Leaf", new Color(0.24f, 0.40f, 0.15f), 0.12f), ("LeafLight", new Color(0.32f, 0.48f, 0.18f), 0.12f),
            ("LeafDark", new Color(0.16f, 0.30f, 0.11f), 0.1f), ("PineNeedle", new Color(0.12f, 0.25f, 0.14f), 0.08f),
            ("Bark", new Color(0.30f, 0.23f, 0.17f), 0.05f), ("Stem", new Color(0.22f, 0.38f, 0.14f), 0.1f),
        };
        foreach (var (n, c, s) in natural)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/WhackAMole/Materials/Env/{n}.mat");
            if (m != null) { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", s); EditorUtility.SetDirty(m); }
        }

        // 칠한 나무·금속 느낌: 부스 색 재질은 약간 매끄럽게
        foreach (string n in new[] { "CarnivalRed", "CarnivalYellow", "CarnivalBlue", "White" })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/WhackAMole/Materials/Env/{n}.mat");
            if (m != null) { m.SetFloat("_Smoothness", 0.55f); EditorUtility.SetDirty(m); }
        }
        return "materials: " + sb;
    }

    // ───────────────────────── 4. PC용 고품질 URP ─────────────────────────
    static string SetupPipeline()
    {
        var src = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        string rpPath = $"{OutDir}/URP-WhackPC.asset", rdPath = $"{OutDir}/URP-WhackPC-Renderer.asset";
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
        if (rp == null && src != null)
        {
            var srcRd = new SerializedObject(src).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(srcRd), rdPath);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(src), rpPath);
            AssetDatabase.ImportAsset(rdPath); AssetDatabase.ImportAsset(rpPath);
            rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rpPath);
            var so0 = new SerializedObject(rp);
            so0.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rdPath);
            so0.ApplyModifiedPropertiesWithoutUndo();
        }
        if (rp == null) return "pipeline: source not found";

        var so = new SerializedObject(rp);
        var missing = new System.Collections.Generic.List<string>();
        void SetB(string p, bool v) { var x = so.FindProperty(p); if (x != null) x.boolValue = v; else missing.Add(p); }
        void SetI(string p, int v) { var x = so.FindProperty(p); if (x != null) x.intValue = v; else missing.Add(p); }
        void SetF(string p, float v) { var x = so.FindProperty(p); if (x != null) x.floatValue = v; else missing.Add(p); }
        SetB("m_SupportsHDR", true);
        SetB("m_RequireDepthTexture", true);   // SSAO
        SetI("m_MSAA", 4);
        SetF("m_ShadowDistance", 35f);
        SetI("m_ShadowCascadeCount", 4);
        SetI("m_MainLightShadowmapResolution", 4096);
        SetB("m_SoftShadowsSupported", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        // SSAO (화면 공간 앰비언트 오클루전) – 사물이 바닥에 붙어 있는 느낌
        var rd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rdPath);
        string ssao = "ssao: skipped";
        if (rd != null && !rd.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
        {
            Type t = typeof(ScriptableRendererFeature).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (t != null)
            {
                var feat = (ScriptableRendererFeature)ScriptableObject.CreateInstance(t);
                feat.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feat, rd);
                rd.rendererFeatures.Add(feat);
                var rso = new SerializedObject(rd);
                var map = rso.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string _, out long lid))
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = lid;
                    rso.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(rd);
                ssao = "ssao: added";
            }
        }
        else if (rd != null) ssao = "ssao: present";

        // High Fidelity 품질 단계에 연결하고, PC(Standalone) 기본 품질을 High Fidelity로
        int hi = Array.IndexOf(QualitySettings.names, "High Fidelity");
        string q = "quality: none";
        if (hi >= 0)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var qso = new SerializedObject(qs);
            var levels = qso.FindProperty("m_QualitySettings");
            levels.GetArrayElementAtIndex(hi).FindPropertyRelative("customRenderPipeline").objectReferenceValue = rp;
            var per = qso.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; per != null && i < per.arraySize; i++)
            {
                var e = per.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("first").stringValue == "Standalone") e.FindPropertyRelative("second").intValue = hi;
            }
            qso.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(hi, true);
            q = $"quality: High Fidelity -> {rp.name}, standalone default={hi}";
        }
        return $"pipeline: {rp.name} {ssao} {q}" + (missing.Count > 0 ? " missing=" + string.Join(",", missing) : "");
    }

    // ───────────────────────── 5. 후처리 ─────────────────────────
    static string SetupPostProcessing()
    {
        string path = $"{OutDir}/PostFX_Profile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }

        T Get<T>() where T : VolumeComponent
        {
            if (!profile.TryGet(out T c)) { c = profile.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, profile); }
            return c;
        }
        var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
        var bloom = Get<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
        var ca = Get<ColorAdjustments>(); ca.postExposure.Override(0.25f); ca.contrast.Override(12f); ca.saturation.Override(10f);
        var wb = Get<WhiteBalance>(); wb.temperature.Override(4f);
        EditorUtility.SetDirty(profile);

        GameObject vol = GameObject.Find("Global Volume (PostFX)");
        if (vol == null) { vol = new GameObject("Global Volume (PostFX)"); Undo.RegisterCreatedObjectUndo(vol, "Volume"); }
        var v = vol.GetComponent<Volume>();
        if (v == null) v = vol.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 1f; v.sharedProfile = profile;

        // XR 카메라에 후처리 켜기
        int cams = 0;
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var cd = cam.GetComponent<UniversalAdditionalCameraData>();
            if (cd == null) cd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            Undo.RecordObject(cd, "Camera PP");
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.None; // MSAA 4x 사용
            EditorUtility.SetDirty(cd);
            cams++;
        }
        return $"postfx: volume + {cams} camera(s)";
    }

    static Material LoadOrCreate(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }
}

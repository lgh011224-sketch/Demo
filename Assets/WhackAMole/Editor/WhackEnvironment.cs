// 두더지 게임 주변 환경(정원 + 카니발 게임 부스)을 기본 도형으로 꾸며 주는 에디터 스크립트
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Build Environment  (다시 누르면 지우고 새로 만듦)
//         Tools > Whack-A-Mole > Remove Environment          (장식만 삭제)
// - 장식은 모두 "Environment" 오브젝트 아래에 만들어지며 게임 오브젝트(WhackAMole, XR Origin)는 건드리지 않습니다.
// - 장식에는 콜라이더를 붙이지 않아 망치 판정·총 레이저에 영향을 주지 않습니다.
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackEnvironment
{
    const string MatDir = "Assets/WhackAMole/Materials/Env";
    const string RootName = "Environment";
    // 이 씬은 두더지 잡기 전용: 사격장(표적 벽 장식·흙길)은 만들지 않음 (사격 게임은 별도 씬 예정)
    const bool IncludeShootingGallery = false;
    // 바닥 수풀(덤불·풀·꽃)은 사용자 요청으로 제외
    const bool IncludeUndergrowth = false;
    // 울타리: 원래 8.5 m → 6 m로 좁힘 (휑한 느낌 줄이기). 안쪽에 로우폴리 들꽃(WhackWildflowers)
    public const float FenceRadius = 6.0f;
    public static readonly Vector3 FenceCenter = new Vector3(0f, 0f, 1.5f);
    static readonly List<Vector2> hillSpots = new List<Vector2>();

    // 나무 임포스터는 다시 찍지 않고 울타리·흙더미·들꽃만 다시 만듦 (빠름)
    [MenuItem("Tools/Whack-A-Mole/Rebuild Fence And Flowers")]
    public static void RebuildFenceAndFlowersMenu() => Debug.Log("[환경] " + RebuildFenceAndFlowers());

    public static string RebuildFenceAndFlowers()
    {
        GameObject envGo = GameObject.Find(RootName);
        if (envGo == null) return "Environment 없음 – Build Environment 먼저";
        Transform r = envGo.transform;
        foreach (string n in new[] { "Fence", "MoleHills", "Wildflowers" })
        {
            var c = r.Find(n);
            if (c != null) Undo.DestroyObjectImmediate(c.gameObject);
        }
        MakeMaterials();
        var rng = new System.Random(20261115);
        BuildFence(r);
        BuildMoleHills(r, rng);
        string fl = WhackWildflowers.Build(r, new System.Random(4242), FenceRadius, FenceCenter, hillSpots);
        // 기본 색 설정이 실사 재질을 덮어쓰지 않도록 다시 적용
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Realism/Sky_Meadow.mat") != null) WhackRealism.Apply();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(envGo.scene);
        return $"fence r={FenceRadius} hills={hillSpots.Count} {fl}";
    }

    // 플레이 영역: 플레이어(0,0,0) → 탁자(z 0.58) → 표적 벽(z 4.2). 이 통로에는 장식을 두지 않음
    static bool InPlayCorridor(float x, float z) => Mathf.Abs(x) < 2.6f && z > -2f && z < 5.2f;

    static Material grass, meadow, dirt, dirtDark, mole, moleNose, black, white, red, yellow, blue,
        bark, leaf, leafDark, fence, bulb, stem, petalPink, petalYellow, petalPurple, petalWhite, flowerCenter;

    [MenuItem("Tools/Whack-A-Mole/Build Environment")]
    public static void BuildMenu()
    {
        Build();
        EditorUtility.DisplayDialog("두더지 게임", "주변 환경을 만들었습니다.\nFile > Save로 장면을 저장하세요.", "확인");
    }

    [MenuItem("Tools/Whack-A-Mole/Remove Environment")]
    public static void Remove()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
            EditorSceneManager.MarkSceneDirty(SceneManagerActive());
        }
    }

    public static GameObject Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        MakeMaterials();
        var rng = new System.Random(20261114); // 매번 같은 배치

        GameObject root = new GameObject(RootName);
        Transform r = root.transform;

        BuildGround(r);
        BuildWhackBooth(r);
        if (IncludeShootingGallery) BuildShootingGallery(r);
        BuildBunting(r);
        BuildFence(r);
        BuildMoleHills(r, rng);
        if (IncludeUndergrowth) BuildFlowers(r, rng);
        Debug.Log("[환경] " + WhackWildflowers.Build(r, new System.Random(4242), FenceRadius, FenceCenter, hillSpots));
        BuildTrees(r, rng);
        BuildLighting();

        Undo.RegisterCreatedObjectUndo(root, "Build Whack-A-Mole Environment");
        // 세미 실사 룩을 이미 적용한 프로젝트면 다시 적용 (위의 기본 색 설정이 실사 재질을 덮어쓰지 않도록)
        if (AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Realism/Sky_Meadow.mat") != null)
            Debug.Log("[실사] " + WhackRealism.Apply());
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }

    // ───────────────────────── 바닥 ─────────────────────────
    static void BuildGround(Transform root)
    {
        Transform g = Group(root, "Ground");
        // 게임 바닥(Floor, 20 m)은 잔디색으로, 바깥은 넓은 초원으로
        Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Floor.mat");
        if (floorMat != null) SetColor(floorMat, new Color(0.38f, 0.62f, 0.28f), 0f, 0.15f);
        Prim(PrimitiveType.Plane, "Meadow", g, new Vector3(0f, -0.01f, 0f), new Vector3(10f, 1f, 10f), meadow, shadows: false);
        // 부스 아래 흙바닥 + 표적 벽까지 이어지는 흙길
        Prim(PrimitiveType.Cylinder, "BoothDirt", g, new Vector3(0f, 0.002f, 0.45f), new Vector3(2.4f, 0.002f, 2.4f), dirt, shadows: false);
        if (IncludeShootingGallery)
        {
            Prim(PrimitiveType.Cube, "DirtPath", g, new Vector3(0f, 0.001f, 2.9f), new Vector3(1.4f, 0.002f, 3.4f), dirt, shadows: false);
            Prim(PrimitiveType.Cylinder, "GalleryDirt", g, new Vector3(0f, 0.0015f, 4.4f), new Vector3(4.2f, 0.002f, 1.8f), dirt, shadows: false);
        }
    }

    // ───────────────────────── 두더지 게임 부스 ─────────────────────────
    static void BuildWhackBooth(Transform root)
    {
        Transform b = Group(root, "WhackBooth");
        float px = 0.62f, zFront = 0.2f, zBack = 0.98f, top = 2.2f;

        // 빨강·흰색 줄무늬 기둥 4개
        foreach (float x in new[] { -px, px })
            foreach (float z in new[] { zFront, zBack })
                StripedPost(b, new Vector3(x, 0f, z), top, 0.07f, 10);

        // 위쪽 들보
        Prim(PrimitiveType.Cube, "BeamFront", b, new Vector3(0f, top, zFront), new Vector3(px * 2f + 0.1f, 0.08f, 0.08f), yellow);
        Prim(PrimitiveType.Cube, "BeamBack", b, new Vector3(0f, top, zBack), new Vector3(px * 2f + 0.1f, 0.08f, 0.08f), yellow);

        // 줄무늬 차양 (앞쪽이 살짝 낮게 기울어짐)
        int stripes = 8;
        float w = (px * 2f + 0.3f) / stripes;
        float depth = zBack - zFront + 0.35f;
        for (int i = 0; i < stripes; i++)
        {
            float x = -px - 0.15f + w * (i + 0.5f);
            var s = Prim(PrimitiveType.Cube, $"Awning_{i}", b, new Vector3(x, top + 0.12f, (zFront + zBack) * 0.5f - 0.1f),
                new Vector3(w, 0.02f, depth), i % 2 == 0 ? red : white);
            s.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            // 앞쪽 물결 장식 (반원 느낌의 납작한 구)
            Prim(PrimitiveType.Sphere, $"Scallop_{i}", b, new Vector3(x, top + 0.02f, zFront - 0.2f),
                new Vector3(w, 0.16f, 0.02f), i % 2 == 0 ? red : white, shadows: false);
        }

        // 간판 "WHACK-A-MOLE!" + 전구
        Vector3 signPos = new Vector3(0f, top + 0.42f, zFront - 0.12f);
        Prim(PrimitiveType.Cube, "SignBoard", b, signPos, new Vector3(1.25f, 0.3f, 0.04f), yellow);
        Prim(PrimitiveType.Cube, "SignBorder", b, signPos + new Vector3(0f, 0f, 0.01f), new Vector3(1.33f, 0.38f, 0.03f), red);
        Text(b, "SignText", "WHACK-A-MOLE!", signPos + new Vector3(0f, 0f, -0.025f), 0.011f, new Color(0.75f, 0.08f, 0.08f));
        BulbRow(b, signPos + new Vector3(-0.62f, 0.17f, -0.03f), signPos + new Vector3(0.62f, 0.17f, -0.03f), 9);
        BulbRow(b, signPos + new Vector3(-0.62f, -0.17f, -0.03f), signPos + new Vector3(0.62f, -0.17f, -0.03f), 9);

        // 간판 위 두더지 마스코트
        MoleFigure(b, "Mascot", signPos + new Vector3(0.52f, 0.26f, 0f), 0.55f, Quaternion.Euler(0f, 180f, 0f));
    }

    // ───────────────────────── 사격장 장식 (표적 벽) ─────────────────────────
    static void BuildShootingGallery(Transform root)
    {
        Transform s = Group(root, "ShootingGallery");
        float z = 4.12f;
        // 양쪽 기둥 + 위 들보 + 아래 받침
        StripedPost(s, new Vector3(-1.7f, 0f, z), 2.85f, 0.14f, 12);
        StripedPost(s, new Vector3(1.7f, 0f, z), 2.85f, 0.14f, 12);
        Prim(PrimitiveType.Cube, "TopBeam", s, new Vector3(0f, 2.75f, z - 0.05f), new Vector3(3.6f, 0.36f, 0.3f), red);
        Prim(PrimitiveType.Cube, "BottomRail", s, new Vector3(0f, 0.15f, z - 0.12f), new Vector3(3.4f, 0.3f, 0.1f), red);
        Text(s, "GalleryText", "SHOOTING GALLERY", new Vector3(0f, 2.75f, z - 0.21f), 0.022f, new Color(1f, 0.85f, 0.2f));
        BulbRow(s, new Vector3(-1.7f, 2.97f, z - 0.2f), new Vector3(1.7f, 2.97f, z - 0.2f), 15);
        BulbRow(s, new Vector3(-1.7f, 2.53f, z - 0.2f), new Vector3(1.7f, 2.53f, z - 0.2f), 15);
        // 위쪽 별 장식
        for (int i = -1; i <= 1; i += 2)
        {
            var star = Prim(PrimitiveType.Cube, $"Star_{i}", s, new Vector3(i * 1.25f, 3.15f, z - 0.05f), new Vector3(0.22f, 0.22f, 0.04f), yellow);
            star.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, $"StarB_{i}", s, new Vector3(i * 1.25f, 3.15f, z - 0.05f), new Vector3(0.22f, 0.22f, 0.04f), yellow);
        }
    }

    // ───────────────────────── 깃발 줄 (머리 위) ─────────────────────────
    static void BuildBunting(Transform root)
    {
        Transform t = Group(root, "Bunting");
        Material[] flagMats = { red, yellow, blue, white };
        // 부스 지붕 → 사격장 들보, 양옆 두 줄 (머리 위 높이라 시야·사격을 가리지 않음)
        int f = 0;
        // 부스 → 사격장 들보로 이어지는 두 줄 (사격장이 있을 때만)
        foreach (float x in IncludeShootingGallery ? new[] { -0.75f, 0.75f } : new float[0])
        {
            Vector3 a = new Vector3(x, 2.35f, 1.0f), c = new Vector3(x * 2.1f, 2.95f, 4.0f);
            Sagging(t, a, c, 0.25f, 14, flagMats, ref f);
        }
        // 부스 양옆에서 바깥 장대로
        foreach (float sgn in new[] { -1f, 1f })
        {
            Vector3 pole = new Vector3(sgn * 3.2f, 0f, 0.6f);
            Prim(PrimitiveType.Cylinder, $"Pole_{sgn}", t, pole + new Vector3(0f, 1.4f, 0f), new Vector3(0.06f, 1.4f, 0.06f), fence);
            Prim(PrimitiveType.Sphere, $"PoleTop_{sgn}", t, pole + new Vector3(0f, 2.82f, 0f), Vector3.one * 0.1f, yellow);
            Sagging(t, new Vector3(sgn * 0.68f, 2.25f, 0.6f), pole + new Vector3(0f, 2.75f, 0f), 0.3f, 12, flagMats, ref f);
        }
    }

    static void Sagging(Transform parent, Vector3 a, Vector3 b, float sag, int flags, Material[] mats, ref int f)
    {
        Vector3 prev = a;
        for (int i = 1; i <= flags; i++)
        {
            float t = i / (float)(flags + 1);
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (4f * sag * t * (1f - t));
            Line(parent, $"String_{f}", prev, p, 0.006f, black);
            var flag = Prim(PrimitiveType.Cube, $"Flag_{f}", parent, p + Vector3.down * 0.07f, new Vector3(0.1f, 0.1f, 0.004f), mats[f % mats.Length], shadows: false);
            Vector3 dir = (b - a); dir.y = 0f;
            flag.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir.normalized, Vector3.up)) * Quaternion.Euler(0f, 0f, 45f);
            prev = p; f++;
        }
        Line(parent, $"String_{f}_end", prev, b, 0.006f, black);
    }

    // ───────────────────────── 울타리 ─────────────────────────
    static void BuildFence(Transform root)
    {
        Transform t = Group(root, "Fence");
        Vector3 center = FenceCenter;
        float radius = FenceRadius;
        int segments = Mathf.RoundToInt(2f * Mathf.PI * radius / 1.21f); // 칸 길이 약 1.2 m 유지
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 p0 = center + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
            Vector3 p1 = center + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
            Vector3 mid = (p0 + p1) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(p1 - p0);
            float len = Vector3.Distance(p0, p1);
            foreach (float y in new[] { 0.3f, 0.7f })
            {
                var rail = Prim(PrimitiveType.Cube, $"Rail_{i}_{y}", t, mid + Vector3.up * y, new Vector3(0.03f, 0.07f, len), fence, shadows: false);
                rail.transform.rotation = rot;
            }
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = Vector3.Lerp(p0, p1, (k + 0.5f) / 4f);
                var picket = Prim(PrimitiveType.Cube, $"Picket_{i}_{k}", t, p + Vector3.up * 0.45f, new Vector3(0.09f, 0.9f, 0.025f), fence, shadows: false);
                picket.transform.rotation = rot * Quaternion.Euler(0f, 90f, 0f);
                var tip = Prim(PrimitiveType.Cube, $"PicketTip_{i}_{k}", t, p + Vector3.up * 0.9f, new Vector3(0.064f, 0.064f, 0.025f), fence, shadows: false);
                tip.transform.rotation = rot * Quaternion.Euler(0f, 90f, 45f);
            }
        }
    }

    // ───────────────────────── 두더지 흙더미 ─────────────────────────
    static void BuildMoleHills(Transform root, System.Random rng)
    {
        Transform t = Group(root, "MoleHills");
        Mesh[] holeMeshes = WhackHoleModels.GetMeshes();
        Material[] holeMats = WhackHoleModels.GetMaterials();
        int placed = 0, tries = 0;
        hillSpots.Clear();
        while (placed < 10 && tries++ < 800)
        {
            float x = Rand(rng, -FenceRadius, FenceRadius), z = FenceCenter.z + Rand(rng, -FenceRadius, FenceRadius);
            if (InPlayCorridor(x, z) || new Vector2(x, z - FenceCenter.z).magnitude > FenceRadius - 0.9f) continue;
            if (hillSpots.Exists(h => (h - new Vector2(x, z)).magnitude < 1.4f)) continue; // 흙더미끼리 겹치지 않게
            float size = Rand(rng, 0.45f, 0.7f);
            Vector3 p = new Vector3(x, 0f, z);
            hillSpots.Add(new Vector2(x, z));
            // 탁자 구멍과 같은 흙구멍 메시를 크게 (높이는 더 불룩하게)
            float s = size / 0.18f;
            var hill = new GameObject($"Hill_{placed}");
            hill.transform.SetParent(t, false);
            hill.transform.localPosition = p;
            hill.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
            hill.transform.localScale = new Vector3(s, s * 1.8f, s);
            hill.AddComponent<MeshFilter>().sharedMesh = holeMeshes[placed % holeMeshes.Length];
            var hr = hill.AddComponent<MeshRenderer>();
            hr.sharedMaterials = holeMats;
            hill.isStatic = true;
            // 흙 부스러기
            for (int k = 0; k < 3; k++)
            {
                float a = Rand(rng, 0f, Mathf.PI * 2f), d = size * Rand(rng, 0.55f, 0.8f);
                Prim(PrimitiveType.Sphere, $"Clod_{placed}_{k}", t, p + new Vector3(Mathf.Cos(a) * d, 0.02f, Mathf.Sin(a) * d), Vector3.one * size * 0.12f, dirtDark, shadows: false);
            }
            // 절반 정도는 두더지가 고개를 내밀고 플레이어 쪽을 바라봄
            if (placed % 2 == 0)
            {
                Vector3 look = new Vector3(-x, 0f, -z);
                MoleFigure(t, $"PeekingMole_{placed}", p + Vector3.up * (size * 0.12f), size * 0.55f, Quaternion.LookRotation(look));
            }
            placed++;
        }
    }

    // ───────────────────────── 꽃 ─────────────────────────
    static void BuildFlowers(Transform root, System.Random rng)
    {
        // 실사 덤불·풀·고사리·꽃 (Poly Haven) - WhackFoliage.cs
        Transform t = Group(root, "Flowers");
        WhackFoliage.PrepareAll();
        Debug.Log("[환경] " + WhackFoliage.PlantsPlace(t, rng));
    }

    // ───────────────────────── 나무 (울타리 밖) ─────────────────────────
    // 실사 나무 (Poly Haven island_tree_02, jacaranda_tree) - WhackFoliage.cs
    // (예전 로우폴리 나무 생성기 WhackTreeModels.cs는 남아 있지만 사용하지 않음)
    static void BuildTrees(Transform root, System.Random rng)
    {
        Transform t = Group(root, "Trees");
        Debug.Log("[환경] " + WhackFoliage.PlaceTrees(t, rng));
    }

    // ───────────────────────── 조명 ─────────────────────────
    static void BuildLighting()
    {
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null)
        {
            Undo.RecordObject(sun, "Sun");
            Undo.RecordObject(sun.transform, "Sun");
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f); // 오후 햇살, 플레이어 뒤쪽에서 비춤
        }
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.9f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 60f;
    }

    // ───────────────────────── 조립 부품 ─────────────────────────
    static void StripedPost(Transform parent, Vector3 basePos, float height, float d, int bands)
    {
        Transform p = Group(parent, "Post");
        p.localPosition = basePos;
        float bh = height / bands;
        for (int i = 0; i < bands; i++)
            Prim(PrimitiveType.Cylinder, $"Band_{i}", p, new Vector3(0f, bh * (i + 0.5f), 0f), new Vector3(d, bh * 0.5f, d), i % 2 == 0 ? red : white);
        Prim(PrimitiveType.Sphere, "Knob", p, new Vector3(0f, height + d * 0.4f, 0f), Vector3.one * d * 1.5f, yellow);
    }

    static void BulbRow(Transform parent, Vector3 a, Vector3 b, int count)
    {
        for (int i = 0; i < count; i++)
            Prim(PrimitiveType.Sphere, "Bulb", parent, Vector3.Lerp(a, b, count == 1 ? 0.5f : i / (float)(count - 1)), Vector3.one * 0.045f, bulb, shadows: false);
    }

    // 귀여운 두더지 상반신 - 게임 두더지와 같은 디자인 (WhackMoleModels.cs)
    static void MoleFigure(Transform parent, string name, Vector3 pos, float scale, Quaternion rot)
    {
        WhackMoleModels.BuildFigure(parent, name, pos, scale, rot, true);
    }

    static void Text(Transform parent, string name, string text, Vector3 pos, float charSize, Color color)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        TextMesh tm = g.AddComponent<TextMesh>();
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font != null) { tm.font = font; g.GetComponent<MeshRenderer>().sharedMaterial = TextMat.For(font); } // 깊이 검사하는 글자 머티리얼
        tm.text = text; tm.fontSize = 64; tm.characterSize = charSize; tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
    }

    static void Line(Transform parent, string name, Vector3 a, Vector3 b, float d, Material m)
    {
        var g = Prim(PrimitiveType.Cylinder, name, parent, (a + b) * 0.5f, new Vector3(d, Vector3.Distance(a, b) * 0.5f, d), m, shadows: false);
        g.transform.up = (b - a).normalized;
    }

    static Transform Group(Transform parent, string name)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g.transform;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool shadows = true)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        Collider col = g.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        g.isStatic = true; // 움직이지 않는 장식 → 배칭 대상
        return g;
    }

    static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static UnityEngine.SceneManagement.Scene SceneManagerActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    // ───────────────────────── 머티리얼 ─────────────────────────
    static void MakeMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Materials", "Env");
        grass = Mat("Grass", new Color(0.38f, 0.62f, 0.28f), 0f, 0.15f);
        meadow = Mat("Meadow", new Color(0.33f, 0.55f, 0.25f), 0f, 0.1f);
        dirt = Mat("Dirt", new Color(0.50f, 0.36f, 0.22f), 0f, 0.1f);
        dirtDark = Mat("DirtDark", new Color(0.24f, 0.16f, 0.10f), 0f, 0.05f);
        mole = Mat("MoleFur", new Color(0.36f, 0.25f, 0.18f), 0f, 0.2f);
        moleNose = Mat("MoleSkin", new Color(0.93f, 0.68f, 0.62f), 0f, 0.3f);
        black = Mat("Black", new Color(0.05f, 0.05f, 0.06f), 0f, 0.8f);
        white = Mat("White", new Color(0.96f, 0.95f, 0.92f), 0f, 0.4f);
        red = Mat("CarnivalRed", new Color(0.85f, 0.13f, 0.13f), 0f, 0.45f);
        yellow = Mat("CarnivalYellow", new Color(1f, 0.78f, 0.15f), 0f, 0.45f);
        blue = Mat("CarnivalBlue", new Color(0.18f, 0.42f, 0.85f), 0f, 0.45f);
        bark = Mat("Bark", new Color(0.40f, 0.27f, 0.16f), 0f, 0.1f);
        leaf = Mat("Leaf", new Color(0.30f, 0.58f, 0.22f), 0f, 0.15f);
        leafDark = Mat("LeafDark", new Color(0.20f, 0.44f, 0.17f), 0f, 0.15f);
        fence = Mat("FenceWhite", new Color(0.93f, 0.92f, 0.88f), 0f, 0.3f);
        stem = Mat("Stem", new Color(0.25f, 0.5f, 0.2f), 0f, 0.2f);
        petalPink = Mat("PetalPink", new Color(0.97f, 0.55f, 0.72f), 0f, 0.3f);
        petalYellow = Mat("PetalYellow", new Color(1f, 0.88f, 0.3f), 0f, 0.3f);
        petalPurple = Mat("PetalPurple", new Color(0.62f, 0.45f, 0.9f), 0f, 0.3f);
        petalWhite = Mat("PetalWhite", new Color(1f, 1f, 0.97f), 0f, 0.3f);
        flowerCenter = Mat("FlowerCenter", new Color(0.95f, 0.7f, 0.1f), 0f, 0.3f);
        bulb = Mat("Bulb", new Color(1f, 0.9f, 0.55f), 0f, 0.6f);
        if (bulb.HasProperty("_EmissionColor"))
        {
            bulb.EnableKeyword("_EMISSION");
            bulb.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.4f) * 2.2f);
            bulb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(bulb);
        }
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            Shader s = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            m = new Material(s);
            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
        }
        SetColor(m, color, metallic, smoothness);
        return m;
    }

    static void SetColor(Material m, Color color, float metallic, float smoothness)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
    }
}

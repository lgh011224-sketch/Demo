// 한국 인형뽑기방 느낌의 오락실 배경 (두더지 잡기 · 사격 두 씬 공통)
//   - 어두운 벽 + 분홍·하늘색 네온관, 네온 무늬 카펫, LED 천장
//   - 벽을 따라 인형뽑기 기계 (불 들어오는 간판 + 깜빡이는 전구 + 유리 안 인형 더미 + 집게)
//   - 캡슐 뽑기 기계, 동전 교환기, 네온 간판 ("인형뽑기", "GAME ZONE" 등)
//   - 게임 공간(clear)에는 아무것도 놓지 않음
// 메뉴: Tools > Arcade > Rebuild Background (지금 열린 씬)
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ArcadeBuilder
{
    public const string Dir = "Assets/Arcade";
    const string MatDir = Dir + "/Materials";

    public struct Layout
    {
        public float xMin, xMax, zMin, zMax, height;
        public Rect[] clear;   // x, z 사각형 (기계를 놓지 않는 곳)
    }

    public static Layout WhackLayout => new Layout
    {
        xMin = -4.6f, xMax = 4.6f, zMin = -3.4f, zMax = 6.4f, height = 3.4f,
        clear = new[] { Rect.MinMaxRect(-1.7f, -1.9f, 1.7f, 2.3f) }
    };

    public static Layout GalleryLayout => new Layout
    {
        xMin = -4.6f, xMax = 4.6f, zMin = -3.2f, zMax = 6.8f, height = 3.4f,
        clear = new[] { Rect.MinMaxRect(-2.0f, 0.4f, 2.0f, 6.9f), Rect.MinMaxRect(-1.5f, -1.6f, 1.5f, 1.0f) }
    };

    static Font kr;
    static Material textMat;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static readonly List<Renderer> staticRenderers = new List<Renderer>();

    [MenuItem("Tools/Arcade/Rebuild Background (open scene)")]
    static void RebuildMenu()
    {
        bool gallery = Object.FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include) != null;
        Debug.Log("[Arcade] " + Build(gallery ? GalleryLayout : WhackLayout, gallery ? 11 : 7));
    }

    // 지금 열린 씬에 오락실 배경을 (다시) 만듦. 이전 배경("Arcade")은 지움
    public static string Build(Layout L, int seed)
    {
        Init();
        var old = GameObject.Find("Arcade");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Arcade").transform;
        var rng = new System.Random(seed);
        staticRenderers.Clear();

        Room(root, L);
        Lights(root, L);
        int machines = Machines(root, L, rng);
        Signs(root, L);
        Graffiti(root, L);
        Ambient();

        foreach (var r in staticRenderers)
            if (r != null) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return $"오락실 배경: 기계 {machines}대, 렌더러 {root.GetComponentsInChildren<Renderer>().Length}개";
    }

    // ───────────────────────── 방 ─────────────────────────
    static void Room(Transform root, Layout L)
    {
        var room = Group(root, "Room", Vector3.zero);
        float w = L.xMax - L.xMin, d = L.zMax - L.zMin, cx = (L.xMin + L.xMax) * 0.5f, cz = (L.zMin + L.zMax) * 0.5f, H = L.height;

        // 카펫: 네온 무늬 + 실제 카펫 결(노멀맵)
        var carpet = Mat("Carpet", new Color(0.85f, 0.85f, 0.9f), 0.08f, 0f, null, CarpetTexture());
        carpet.SetTextureScale("_BaseMap", new Vector2(w / 0.8f, d / 0.8f));   // 무늬를 작게 (실제 오락실 카펫처럼)
        SetNormal(carpet, PH("dirty_carpet", "nor_gl"), 1.2f);
        Box(room, "Floor", new Vector3(cx, -0.01f, cz), new Vector3(w, 0.02f, d), carpet, true);
        // 천장: 검은 텍스(흡음판) 느낌 + 노멀
        var ceil = Mat("Ceiling", new Color(0.09f, 0.09f, 0.1f), 0.15f, 0f, null, PH("plastered_wall_04", "diff"));
        ceil.SetTextureScale("_BaseMap", new Vector2(w / 2f, d / 2f));
        SetNormal(ceil, PH("plastered_wall_04", "nor_gl"), 0.8f);
        Box(room, "Ceiling", new Vector3(cx, H + 0.05f, cz), new Vector3(w, 0.1f, d), ceil, true);

        // 벽: 보랏빛 페인트 칠한 미장 벽 (실제 벽 결)
        var wall = Mat("Wall", new Color(0.36f, 0.3f, 0.52f), 0.3f, 0f, null, PH("plastered_wall_04", "diff"));
        wall.SetTextureScale("_BaseMap", new Vector2(4f, 1.5f));
        SetNormal(wall, PH("plastered_wall_04", "nor_gl"), 1f);
        var wainscot = Mat("Wainscot", new Color(0.55f, 0.16f, 0.45f), 0.55f, 0.2f, null, PH("metal_plate_02", "diff"));
        wainscot.SetTextureScale("_BaseMap", new Vector2(6f, 1f));
        SetNormal(wainscot, PH("metal_plate_02", "nor_gl"), 0.6f);
        var pink = Neon("NeonPurple", new Color(0.72f, 0.3f, 1f), 6f);
        var cyan = Neon("NeonCyan", new Color(0.2f, 0.85f, 1f), 4.5f);
        // 4면 벽: (가운데, 길이, 안쪽 방향)
        var walls = new[]
        {
            (new Vector3(cx, 0f, L.zMax), w, Vector3.back),
            (new Vector3(cx, 0f, L.zMin), w, Vector3.forward),
            (new Vector3(L.xMin, 0f, cz), d, Vector3.right),
            (new Vector3(L.xMax, 0f, cz), d, Vector3.left),
        };
        foreach (var (c, len, inward) in walls)
        {
            Quaternion rot = Quaternion.LookRotation(inward);
            var g = Group(room, "Wall", c); g.rotation = rot;
            Box(g, "Panel", new Vector3(0f, H * 0.5f, -0.05f), new Vector3(len, H, 0.1f), wall, true);
            Box(g, "Wainscot", new Vector3(0f, 0.55f, 0.01f), new Vector3(len, 1.1f, 0.02f), wainscot, false);
            Tube(g, "NeonLow", new Vector3(-len * 0.5f, 1.12f, 0.04f), new Vector3(len * 0.5f, 1.12f, 0.04f), 0.018f, cyan);
            Tube(g, "NeonHigh", new Vector3(-len * 0.5f, 2.42f, 0.04f), new Vector3(len * 0.5f, 2.42f, 0.04f), 0.022f, pink);
        }
        // 천장 LED 판 (격자)
        var led = Neon("CeilingLED", new Color(0.7f, 0.4f, 1f), 3.2f);   // 보라 네온 천장
        for (float x = L.xMin + 1.2f; x < L.xMax - 0.6f; x += 2.2f)
            for (float z = L.zMin + 1.2f; z < L.zMax - 0.6f; z += 2.2f)
                Box(room, "CeilingLight", new Vector3(x, H - 0.005f, z), new Vector3(0.6f, 0.02f, 0.6f), led, false);
    }

    static void Lights(Transform root, Layout L)
    {
        var g = Group(root, "Lights", Vector3.zero);
        int i = 0;
        for (float x = L.xMin + 1.6f; x < L.xMax - 1f; x += 3.1f)
            for (float z = L.zMin + 1.6f; z < L.zMax - 1f; z += 3.3f)
                PointLight(g, "Ceiling" + (i), new Vector3(x, L.height - 0.35f, z), (i++ % 2 == 0) ? new Color(0.72f, 0.45f, 1f) : new Color(0.95f, 0.45f, 0.95f), 3.6f, 7f);
        // 벽 쪽 색 조명 (네온 반사)
        PointLight(g, "PinkL", new Vector3(L.xMin + 0.6f, 2.2f, (L.zMin + L.zMax) * 0.5f), new Color(0.9f, 0.3f, 1f), 2.6f, 6f);
        PointLight(g, "CyanR", new Vector3(L.xMax - 0.6f, 2.2f, (L.zMin + L.zMax) * 0.5f), new Color(0.55f, 0.4f, 1f), 2.6f, 6f);
        PointLight(g, "Back", new Vector3(0f, 2.2f, L.zMax - 0.6f), new Color(0.8f, 0.4f, 1f), 2f, 6f);
        // 가운데 위 그림자 드리우는 조명 하나 (바닥에 기계·사람 그림자 → 입체감)
        var key = new GameObject("KeySpot");
        key.transform.SetParent(g, false);
        key.transform.position = new Vector3((L.xMin + L.xMax) * 0.5f, L.height - 0.1f, (L.zMin + L.zMax) * 0.5f);
        key.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var kl = key.AddComponent<Light>();
        kl.type = LightType.Spot; kl.spotAngle = 140f; kl.innerSpotAngle = 90f; kl.range = 8f; kl.intensity = 3.5f;
        kl.color = new Color(0.88f, 0.8f, 1f); kl.shadows = LightShadows.Soft; kl.shadowStrength = 0.7f;
        // 반사 프로브 (기계 유리·크롬에 오락실이 비치게)
        var probeGo = new GameObject("ReflectionProbe");
        probeGo.transform.SetParent(g, false);
        probeGo.transform.position = new Vector3((L.xMin + L.xMax) * 0.5f, 1.6f, (L.zMin + L.zMax) * 0.5f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.size = new Vector3(L.xMax - L.xMin, L.height, L.zMax - L.zMin);
        probe.boxProjection = true;
        probe.intensity = 1f;
    }

    static void PointLight(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
    }

    static void Ambient()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.32f, 0.62f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.24f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.17f, 0.11f, 0.24f);
        RenderSettings.fog = false;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.015f, 0.03f);
        }
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.gameObject.SetActive(false);   // 실내: 햇빛 끔
    }

    // ───────────────────────── 기계 배치 ─────────────────────────
    static int Machines(Transform root, Layout L, System.Random rng)
    {
        var g = Group(root, "Machines", Vector3.zero);
        int n = 0, slot = 0;
        const float inset = 0.5f, step = 0.95f;
        // (시작점, 끝점, 기계가 바라보는 방향)
        var runs = new[]
        {
            (new Vector3(L.xMin + inset, 0f, L.zMin + 1.0f), new Vector3(L.xMin + inset, 0f, L.zMax - 0.6f), Vector3.right),
            (new Vector3(L.xMax - inset, 0f, L.zMin + 1.0f), new Vector3(L.xMax - inset, 0f, L.zMax - 0.6f), Vector3.left),
            (new Vector3(L.xMin + 1.2f, 0f, L.zMax - inset), new Vector3(L.xMax - 1.2f, 0f, L.zMax - inset), Vector3.back),
            (new Vector3(L.xMin + 1.2f, 0f, L.zMin + inset), new Vector3(L.xMax - 1.2f, 0f, L.zMin + inset), Vector3.forward),
        };
        foreach (var (a, b, face) in runs)
        {
            float len = Vector3.Distance(a, b);
            int count = Mathf.FloorToInt(len / step) + 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, count == 1 ? 0.5f : i / (float)(count - 1));
                if (Blocked(L, p, 0.55f)) continue;
                float yaw = Quaternion.LookRotation(face).eulerAngles.y;
                slot++;
                if (slot % 7 == 4) GachaBank(g, p, yaw, rng, slot);
                else if (slot % 11 == 6) ChangeMachine(g, p, yaw, slot);
                else ClawMachine(g, p, yaw, rng, slot);
                n++;
            }
        }
        return n;
    }

    static bool Blocked(Layout L, Vector3 p, float r)
    {
        foreach (var c in L.clear)
            if (p.x + r > c.xMin && p.x - r < c.xMax && p.z + r > c.yMin && p.z - r < c.yMax) return true;
        return false;
    }

    static readonly Color[] BodyColors =
    {
        new Color(1f, 0.42f, 0.7f), new Color(0.3f, 0.8f, 0.95f), new Color(1f, 0.82f, 0.25f),
        new Color(0.62f, 0.45f, 1f), new Color(0.35f, 0.9f, 0.6f), new Color(1f, 0.55f, 0.3f),
    };
    static readonly Color[] NeonColors =
    {
        new Color(1f, 0.25f, 0.7f), new Color(0.2f, 0.85f, 1f), new Color(1f, 0.85f, 0.2f),
        new Color(0.7f, 0.4f, 1f), new Color(0.3f, 1f, 0.55f), new Color(1f, 0.45f, 0.2f),
    };
    static readonly string[] SignWords = { "CLAW KING", "LUCKY", "CATCH!", "TOY BOX", "HAPPY", "PRIZE", "SUPER CLAW", "GET IT!", "JACKPOT" };
    static readonly Color[] PlushColors =
    {
        new Color(1f, 0.78f, 0.85f), new Color(1f, 0.93f, 0.55f), new Color(0.72f, 0.92f, 0.82f), new Color(0.8f, 0.75f, 1f),
        new Color(0.97f, 0.96f, 0.93f), new Color(0.72f, 0.52f, 0.36f), new Color(0.65f, 0.85f, 1f), new Color(1f, 0.7f, 0.5f),
    };

    // 인형뽑기 기계 (원점 = 바닥 가운데, 앞 = +Z)
    static void ClawMachine(Transform parent, Vector3 pos, float yaw, System.Random rng, int idx)
    {
        var m = Group(parent, "ClawMachine_" + idx, pos); m.rotation = Quaternion.Euler(0f, yaw, 0f);
        int ci = rng.Next(BodyColors.Length);
        var body = Mat("Body" + ci, BodyColors[ci], 0.82f, 0f);   // 광택 도장 (반사 프로브가 비침)
        var dark = Mat("CabinetDark", new Color(0.06f, 0.06f, 0.08f), 0.5f);
        var chrome = Mat("Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f);
        var gold = Mat("Gold", new Color(1f, 0.78f, 0.3f), 0.85f, 1f);
        var glass = Glass();
        var neon = Neon("SignNeon" + ci, NeonColors[ci], 2.6f);

        S(RBox(m, "Cabinet", new Vector3(0f, 0.43f, 0f), new Vector3(0.82f, 0.86f, 0.82f), body, true, 0.03f));
        // 유리 안: 뒤쪽 빛나는 포스터 + 천장 조명 줄 + 앞 기둥 LED
        S(Box(m, "BackPoster", new Vector3(0f, 1.31f, -0.385f), new Vector3(0.74f, 0.82f, 0.01f), PosterMat(ci), false));
        S(Box(m, "InnerLight", new Vector3(0f, 1.735f, 0.3f), new Vector3(0.72f, 0.012f, 0.04f), Neon("InnerLED", new Color(1f, 0.97f, 0.92f), 3.5f), false));
        var led = Neon("PillarLED" + ci, NeonColors[(ci + 3) % NeonColors.Length], 3.2f);
        foreach (float sx in new[] { -1f, 1f })
            S(Box(m, "PillarLED", new Vector3(sx * 0.395f, 1.31f, 0.412f), new Vector3(0.012f, 0.84f, 0.006f), led, false));
        S(Box(m, "Kick", new Vector3(0f, 0.04f, 0.01f), new Vector3(0.84f, 0.08f, 0.82f), dark, false));
        var deck = Box(m, "Deck", new Vector3(0f, 0.9f, 0.36f), new Vector3(0.8f, 0.07f, 0.2f), dark, false);
        deck.localRotation = Quaternion.Euler(-12f, 0f, 0f); S(deck);
        S(Cyl(m, "Stick", new Vector3(-0.17f, 0.97f, 0.37f), new Vector3(0.022f, 0.05f, 0.022f), chrome));
        S(Sph(m, "Knob", new Vector3(-0.17f, 1.03f, 0.37f), 0.055f, Mat("KnobRed", new Color(0.95f, 0.1f, 0.12f), 0.8f)));
        S(Cyl(m, "Button", new Vector3(0.12f, 0.955f, 0.38f), new Vector3(0.07f, 0.012f, 0.07f), Neon("BtnGlow" + ci, NeonColors[(ci + 2) % NeonColors.Length], 2f)));
        S(Box(m, "Chute", new Vector3(0.22f, 0.36f, 0.412f), new Vector3(0.26f, 0.28f, 0.02f), dark, false));
        S(Box(m, "ChuteFrame", new Vector3(0.22f, 0.51f, 0.415f), new Vector3(0.3f, 0.02f, 0.02f), chrome, false));
        // 반실사 부품: 동전 투입구 문, 옆면 인쇄 아트, 스피커 그릴, 크롬 모서리
        ArcadeDetails.CoinDoor(m, new Vector3(-0.2f, 0.45f, 0.416f), 0.2f, 0.3f);
        var art = ArcadeDetails.ArtMat(ci, BodyColors[ci], NeonColors[(ci + 2) % NeonColors.Length] * 0.8f);
        foreach (float sx in new[] { -1f, 1f })
            S(Box(m, "SideArt", new Vector3(sx * 0.412f, 0.45f, 0f), new Vector3(0.004f, 0.62f, 0.66f), art, false));
        S(RBox(m, "SpeakerRim", new Vector3(0.22f, 0.7f, 0.414f), new Vector3(0.2f, 0.1f, 0.01f), chrome, false, 0.015f));
        S(Box(m, "Speaker", new Vector3(0.22f, 0.7f, 0.42f), new Vector3(0.18f, 0.08f, 0.004f), ArcadeDetails.GrilleMat(), false));
        ArcadeDetails.CornerTrims(m, new Vector3(0f, 0.43f, 0f), new Vector3(0.82f, 0.82f, 0.82f), chrome);
        // 유리 상자
        S(Box(m, "CaseFloor", new Vector3(0f, 0.875f, 0f), new Vector3(0.78f, 0.02f, 0.78f), Mat("CaseFloor", new Color(0.95f, 0.95f, 1f), 0.6f), false));
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                S(RBox(m, "Pillar", new Vector3(sx * 0.395f, 1.31f, sz * 0.395f), new Vector3(0.022f, 0.86f, 0.022f), chrome, false, 0.009f));
        Box(m, "Glass", new Vector3(0f, 1.31f, 0f), new Vector3(0.78f, 0.86f, 0.78f), glass, false);
        // 인형 더미
        int count = 9 + rng.Next(5);
        for (int i = 0; i < count; i++)
        {
            float s = 0.09f + (float)rng.NextDouble() * 0.05f;
            float layer = i < 7 ? 0f : 1f;
            Vector3 p = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.56f, 0.885f + s * 0.5f + layer * s * 0.8f, ((float)rng.NextDouble() - 0.5f) * 0.5f - 0.03f);
            if (p.x > 0.12f && p.z > 0.12f) p.x -= 0.2f;   // 경품 구멍 자리 비워 둠
            Plush(m, p, s, rng);
        }
        S(Box(m, "PrizeHole", new Vector3(0.24f, 0.89f, 0.24f), new Vector3(0.2f, 0.01f, 0.2f), dark, false));
        // 집게
        float cx = ((float)rng.NextDouble() - 0.5f) * 0.4f, cz = ((float)rng.NextDouble() - 0.5f) * 0.4f;
        foreach (float sz in new[] { -1f, 1f })
            S(Box(m, "Rail", new Vector3(0f, 1.72f, sz * 0.34f), new Vector3(0.74f, 0.02f, 0.025f), chrome, false));
        S(Box(m, "Bridge", new Vector3(cx, 1.72f, 0f), new Vector3(0.025f, 0.02f, 0.7f), chrome, false));
        S(Box(m, "Carriage", new Vector3(cx, 1.7f, cz), new Vector3(0.08f, 0.04f, 0.08f), dark, false));
        S(Cyl(m, "Cable", new Vector3(cx, 1.6f, cz), new Vector3(0.006f, 0.09f, 0.006f), dark));
        S(Sph(m, "ClawHub", new Vector3(cx, 1.5f, cz), 0.045f, gold));
        for (int k = 0; k < 3; k++)
        {
            var arm = Group(m, "ClawArm", new Vector3(cx, 1.49f, cz));
            arm.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
            var prong = Box(arm, "Prong", new Vector3(0f, -0.04f, 0.03f), new Vector3(0.012f, 0.09f, 0.012f), gold, false);
            prong.localRotation = Quaternion.Euler(28f, 0f, 0f); S(prong);
            var tip = Box(arm, "Tip", new Vector3(0f, -0.09f, 0.045f), new Vector3(0.012f, 0.035f, 0.012f), gold, false);
            tip.localRotation = Quaternion.Euler(-20f, 0f, 0f); S(tip);
        }
        // 위 간판 + 전구
        S(RBox(m, "Header", new Vector3(0f, 1.92f, 0f), new Vector3(0.82f, 0.32f, 0.82f), body, false, 0.03f));
        S(Box(m, "SignPanel", new Vector3(0f, 1.92f, 0.412f), new Vector3(0.7f, 0.2f, 0.01f), neon, false));
        S(Box(m, "Crown", new Vector3(0f, 2.1f, 0f), new Vector3(0.7f, 0.04f, 0.7f), dark, false));
        Text(m, SignWords[idx % SignWords.Length], new Vector3(0f, 1.92f, 0.42f), 0.017f, new Color(0.06f, 0.03f, 0.08f), true);
        var bulbs = new List<Renderer>();
        var bulbMat = Neon("Bulb", new Color(1f, 0.85f, 0.4f), 3f);
        for (int i = 0; i < 11; i++)
        {
            float x = -0.38f + i * 0.076f;
            bulbs.Add(Sph(m, "Bulb", new Vector3(x, 2.075f, 0.418f), 0.024f, bulbMat).GetComponent<Renderer>());
            bulbs.Add(Sph(m, "Bulb", new Vector3(x, 1.765f, 0.418f), 0.024f, bulbMat).GetComponent<Renderer>());
        }
        var mq = m.gameObject.AddComponent<MarqueeLights>();
        mq.bulbs = bulbs.ToArray();
        mq.phase = idx * 1.7f;
        mq.speed = 5f + (idx % 3);
    }

    // 인형: 몸통 + 머리 + 귀 (곰 / 토끼)
    static void Plush(Transform parent, Vector3 p, float s, System.Random rng)
    {
        int c = rng.Next(PlushColors.Length);
        // 인형 천: 실제 인조털 결(텍스처+노멀)에 색만 입힘
        var mat = Mat("Plush" + c, PlushColors[c], 0.04f);
        mat.SetTexture("_BaseMap", null);                         // 색은 밝게 그대로, 털 결만 노멀맵으로
        mat.SetTextureScale("_BaseMap", Vector2.one * 0.6f);
        SetNormal(mat, PH("faux_fur_geometric", "nor_gl"), 1.6f);
        var g = Group(parent, "Plush", p);
        g.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 40f, ((float)rng.NextDouble() - 0.5f) * 120f, ((float)rng.NextDouble() - 0.5f) * 30f);
        S(Sph(g, "Body", Vector3.zero, s, mat));
        S(Sph(g, "Head", new Vector3(0f, s * 0.62f, 0.05f * s), s * 0.78f, mat));
        var eye = Mat("PlushEye", new Color(0.03f, 0.03f, 0.04f), 0.85f);
        foreach (float ex in new[] { -1f, 1f })
            S(Sph(g, "Eye", new Vector3(ex * s * 0.13f, s * 0.68f, s * 0.36f), s * 0.09f, eye));
        bool bunny = rng.Next(3) == 0;
        foreach (float sx in new[] { -1f, 1f })
        {
            var ear = Sph(g, "Ear", bunny ? new Vector3(sx * s * 0.17f, s * 1.2f, 0f) : new Vector3(sx * s * 0.3f, s * 0.95f, 0f), 1f, mat);
            ear.localScale = bunny ? new Vector3(s * 0.2f, s * 0.6f, s * 0.13f) : Vector3.one * s * 0.3f;
            S(ear);
        }
    }

    // 캡슐 뽑기 기계 3대 묶음
    static void GachaBank(Transform parent, Vector3 pos, float yaw, System.Random rng, int idx)
    {
        var m = Group(parent, "GachaBank_" + idx, pos); m.rotation = Quaternion.Euler(0f, yaw, 0f);
        var glass = Glass();
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 0.29f;
            int ci = (idx + i) % BodyColors.Length;
            var body = Mat("Body" + ci, BodyColors[ci], 0.55f, 0.1f);
            S(Box(m, "Stand", new Vector3(x, 0.35f, 0f), new Vector3(0.27f, 0.7f, 0.3f), body, true));
            S(Box(m, "Base", new Vector3(x, 0.84f, 0f), new Vector3(0.27f, 0.28f, 0.3f), Mat("GachaWhite", new Color(0.95f, 0.95f, 0.97f), 0.6f), false));
            S(Cyl(m, "Knob", new Vector3(x, 0.84f, 0.16f), new Vector3(0.09f, 0.012f, 0.09f), Mat("Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f)).Rotated(new Vector3(90f, 0f, 0f)));
            S(Box(m, "Outlet", new Vector3(x, 0.74f, 0.152f), new Vector3(0.08f, 0.06f, 0.01f), Mat("CabinetDark", new Color(0.06f, 0.06f, 0.08f), 0.5f), false));
            var dome = Sph(m, "Dome", new Vector3(x, 1.12f, 0f), 0.27f, glass);
            for (int k = 0; k < 14; k++)
            {
                Vector3 cp = new Vector3(x + ((float)rng.NextDouble() - 0.5f) * 0.16f, 1.02f + (float)rng.NextDouble() * 0.12f, ((float)rng.NextDouble() - 0.5f) * 0.16f);
                int cc = rng.Next(NeonColors.Length);
                S(Sph(m, "Capsule", cp, 0.055f, Mat("Capsule" + cc, NeonColors[cc] * 0.9f + Color.white * 0.1f, 0.85f)));
            }
            S(Cyl(m, "Cap", new Vector3(x, 1.27f, 0f), new Vector3(0.12f, 0.02f, 0.12f), body));
        }
        S(Box(m, "Sign", new Vector3(0f, 1.48f, 0.05f), new Vector3(0.84f, 0.16f, 0.03f), Neon("SignNeon2", NeonColors[2], 2.2f), false));
        Text(m, "CAPSULE TOYS", new Vector3(0f, 1.48f, 0.07f), 0.014f, new Color(0.1f, 0.05f, 0.1f), true);
    }

    // 동전 교환기
    static void ChangeMachine(Transform parent, Vector3 pos, float yaw, int idx)
    {
        var m = Group(parent, "ChangeMachine_" + idx, pos); m.rotation = Quaternion.Euler(0f, yaw, 0f);
        S(Box(m, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 1.7f, 0.55f), Mat("ChangeBody", new Color(0.85f, 0.87f, 0.9f), 0.7f, 0.6f), true));
        S(Box(m, "Screen", new Vector3(0f, 1.3f, 0.28f), new Vector3(0.5f, 0.3f, 0.01f), Neon("ScreenBlue", new Color(0.2f, 0.55f, 1f), 1.4f), false));
        S(Box(m, "BillSlot", new Vector3(0.15f, 0.95f, 0.28f), new Vector3(0.16f, 0.04f, 0.01f), Mat("CabinetDark", new Color(0.06f, 0.06f, 0.08f), 0.5f), false));
        S(Box(m, "Tray", new Vector3(0f, 0.45f, 0.29f), new Vector3(0.3f, 0.12f, 0.04f), Mat("Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f), false));
        Text(m, "CHANGE", new Vector3(0f, 1.3f, 0.29f), 0.013f, Color.white, true);
    }

    // ───────────────────────── 네온 간판 ─────────────────────────
    static void Signs(Transform root, Layout L)
    {
        var g = Group(root, "Signs", Vector3.zero);
        float y = 2.9f;
        // yaw = 간판을 보는 사람이 바라보는 방향 (뒷벽은 +Z를 보므로 0)
        NeonSign(g, "CLAW MACHINES", new Vector3(-2.6f, y, L.zMax - 0.08f), 0f, new Color(1f, 0.3f, 0.7f));
        NeonSign(g, "GAME ZONE", new Vector3(2.6f, y, L.zMax - 0.08f), 0f, new Color(0.25f, 0.85f, 1f));
        NeonSign(g, "★ LUCKY PRIZE ★", new Vector3(L.xMin + 0.08f, y, (L.zMin + L.zMax) * 0.5f), -90f, new Color(1f, 0.85f, 0.25f));
        NeonSign(g, "ARCADE", new Vector3(L.xMax - 0.08f, y, (L.zMin + L.zMax) * 0.5f), 90f, new Color(0.4f, 1f, 0.55f));
        NeonSign(g, "PLAY & WIN", new Vector3(0f, y, L.zMin + 0.08f), 180f, new Color(0.75f, 0.45f, 1f));
    }

    // 벽 위쪽(네온관과 천장 사이)에 그래피티 – 네온 간판 자리는 비움
    static readonly string[] GraffitiWords = { "PLAY", "GAME ON!", "HI-SCORE", "INSERT COIN", "WIN!", "LEVEL UP", "PRESS START", "1UP", "COMBO!", "BONUS", "READY?", "PLAYER 1" };

    static void Graffiti(Transform root, Layout L)
    {
        var g = Group(root, "Graffiti", Vector3.zero);
        float y = 2.9f, w = 1.45f;
        var signs = new[] { new Vector3(-2.6f, 0f, L.zMax), new Vector3(2.6f, 0f, L.zMax), new Vector3(L.xMin, 0f, (L.zMin + L.zMax) * 0.5f), new Vector3(L.xMax, 0f, (L.zMin + L.zMax) * 0.5f), new Vector3(0f, 0f, L.zMin) };
        // (시작, 끝, 보는 방향 yaw, 벽 안쪽으로 띄움)
        var walls = new[]
        {
            (new Vector3(L.xMin + 0.9f, y, L.zMax - 0.07f), new Vector3(L.xMax - 0.9f, y, L.zMax - 0.07f), 0f),
            (new Vector3(L.xMax - 0.9f, y, L.zMin + 0.07f), new Vector3(L.xMin + 0.9f, y, L.zMin + 0.07f), 180f),
            (new Vector3(L.xMin + 0.07f, y, L.zMin + 0.9f), new Vector3(L.xMin + 0.07f, y, L.zMax - 0.9f), -90f),
            (new Vector3(L.xMax - 0.07f, y, L.zMax - 0.9f), new Vector3(L.xMax - 0.07f, y, L.zMin + 0.9f), 90f),
        };
        int n = 0;
        foreach (var (a, b, yaw) in walls)
        {
            float len = Vector3.Distance(a, b);
            int count = Mathf.Max(1, Mathf.FloorToInt(len / 1.75f));
            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (i + 0.5f) / count);
                bool nearSign = false;
                foreach (var sp in signs) if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(sp.x, sp.z)) < 1.7f) nearSign = true;
                if (nearSign) continue;
                string word = GraffitiWords[n % GraffitiWords.Length];
                var mat = GraffitiBaker.Graffiti(word.Replace(" ", "_").Replace("!", "").Replace("?", "").Replace("-", ""), word, n, 100 + n);
                var q = GraffitiBaker.Place(g, mat, p + Vector3.up * ((n % 3) - 1) * 0.06f, yaw, w * (0.9f + (n % 3) * 0.08f));
                q.Rotate(0f, 0f, ((n % 5) - 2) * 2.5f, Space.Self);
                n++;
            }
        }
        // 천장 가운데 보라 네온관 2줄
        var purple = Neon("CeilingTube", new Color(0.75f, 0.35f, 1f), 6f);
        foreach (float x in new[] { -1.2f, 1.2f })
            Tube(g, "CeilingNeon", new Vector3(x, L.height - 0.04f, L.zMin + 0.6f), new Vector3(x, L.height - 0.04f, L.zMax - 0.6f), 0.02f, purple);
    }

    // 벽에 붙은 네온 간판: 검은 판 + 빛나는 테두리 + 글자 + 뒤쪽 은은한 빛
    public static void NeonSign(Transform parent, string text, Vector3 pos, float yaw, Color c)
    {
        Init();
        var g = Group(parent, "NeonSign", pos); g.rotation = Quaternion.Euler(0f, yaw, 0f);
        float w = Mathf.Max(1.2f, text.Length * 0.2f + 0.3f), h = 0.5f;
        S(Box(g, "Board", new Vector3(0f, 0f, 0.01f), new Vector3(w, h, 0.02f), Mat("SignBoard", new Color(0.02f, 0.02f, 0.03f), 0.6f), false));
        S(Box(g, "Glow", new Vector3(0f, 0f, -0.001f), new Vector3(w - 0.08f, h - 0.08f, 0.002f), Neon("Glow_" + ColorUtility.ToHtmlStringRGB(c), c, 0.35f), false));
        var tube = Neon("Tube_" + ColorUtility.ToHtmlStringRGB(c), c, 5f);
        Tube(g, "T", new Vector3(-w * 0.5f, h * 0.5f, -0.01f), new Vector3(w * 0.5f, h * 0.5f, -0.01f), 0.012f, tube);
        Tube(g, "B", new Vector3(-w * 0.5f, -h * 0.5f, -0.01f), new Vector3(w * 0.5f, -h * 0.5f, -0.01f), 0.012f, tube);
        Tube(g, "L", new Vector3(-w * 0.5f, -h * 0.5f, -0.01f), new Vector3(-w * 0.5f, h * 0.5f, -0.01f), 0.012f, tube);
        Tube(g, "R", new Vector3(w * 0.5f, -h * 0.5f, -0.01f), new Vector3(w * 0.5f, h * 0.5f, -0.01f), 0.012f, tube);
        Text(g, text, new Vector3(0f, 0f, -0.02f), 0.032f, Color.Lerp(c, Color.white, 0.55f), false);
    }

    // ───────────────────────── 재료 ─────────────────────────
    static void Init()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Arcade");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Dir, "Materials");
        if (kr == null) kr = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/NotoSansKR.ttf");
        if (textMat == null && kr != null) textMat = TextMat.For(kr);
    }

    public static Material Mat(string name, Color c, float smooth, float metal = 0f, Color? emission = null, Texture tex = null)
    {
        string key = name;
        if (cache.TryGetValue(key, out var m) && m != null) return m;
        string path = $"{MatDir}/{name}.mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        cache[key] = m;
        return m;
    }

    public static Material Neon(string name, Color c, float intensity) => Mat(name, c * 0.6f, 0.7f, 0f, c * intensity);

    public static Material Glass()
    {
        var m = Mat("Glass", new Color(0.85f, 0.95f, 1f, 0.12f), 0.96f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    // 네온 무늬 카펫 (반복 무늬)
    static Texture2D CarpetTexture()
    {
        string path = Dir + "/Carpet.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int N = 512;
        var px = new Color[N * N];
        var bg = new Color(0.05f, 0.045f, 0.11f);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        var rng = new System.Random(21);
        Color[] cols = { new Color(1f, 0.3f, 0.7f), new Color(0.2f, 0.85f, 1f), new Color(1f, 0.85f, 0.25f), new Color(0.65f, 0.4f, 1f) };
        void Put(int x, int y, Color c) { x = ((x % N) + N) % N; y = ((y % N) + N) % N; px[y * N + x] = c; }
        for (int s = 0; s < 70; s++)
        {
            Color c = cols[rng.Next(cols.Length)] * 0.8f;
            int cx = rng.Next(N), cy = rng.Next(N), kind = rng.Next(3), r = 6 + rng.Next(10);
            if (kind == 0)          // 고리
                for (int y = -r - 2; y <= r + 2; y++) for (int x = -r - 2; x <= r + 2; x++) { float d = Mathf.Sqrt(x * x + y * y); if (d > r - 1.5f && d < r + 1.5f) Put(cx + x, cy + y, c); }
            else if (kind == 1)     // 삼각형 테두리
                for (int t = 0; t < 3; t++)
                {
                    float a0 = t * 2.094f, a1 = (t + 1) * 2.094f;
                    Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                    for (float k = 0; k <= 1f; k += 0.02f) { Vector2 p = Vector2.Lerp(p0, p1, k); for (int o = -1; o <= 1; o++) Put(cx + (int)p.x + o, cy + (int)p.y, c); }
                }
            else                    // 지그재그
                for (int x = 0; x < r * 3; x++) { int y = (x / 4) % 2 == 0 ? x % 4 : 3 - x % 4; for (int o = 0; o < 2; o++) Put(cx + x, cy + y * 2 + o, c); }
        }
        for (int s = 0; s < 400; s++) Put(rng.Next(N), rng.Next(N), cols[rng.Next(cols.Length)] * 0.6f);   // 작은 점
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ───────────────────────── 실사 재질 도우미 ─────────────────────────
    // Poly Haven 텍스처 (Assets/Arcade/External/PolyHaven/Textures/<name>/<name>_<map>_1k.jpg)
    public static Texture2D PH(string name, string map)
    {
        string path = $"{Dir}/External/PolyHaven/Textures/{name}/{name}_{map}_1k.jpg";
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return null;
        bool normal = map.StartsWith("nor");
        bool linear = map == "rough" || map == "ao";
        if ((normal && ti.textureType != TextureImporterType.NormalMap) || (linear && ti.sRGBTexture) || ti.anisoLevel < 4)
        {
            if (normal) ti.textureType = TextureImporterType.NormalMap;
            if (linear) ti.sRGBTexture = false;
            ti.anisoLevel = 4;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static void SetNormal(Material m, Texture2D n, float scale)
    {
        if (m == null || n == null) return;
        m.SetTexture("_BumpMap", n);
        m.SetFloat("_BumpScale", scale);
        m.SetTextureScale("_BumpMap", m.GetTextureScale("_BaseMap"));
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
    }

    // 기계 유리 안 뒤쪽 포스터 (빛나는 그라데이션 + 별·하트 무늬)
    static Material PosterMat(int ci)
    {
        string path = $"{Dir}/Poster{ci}.png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            const int W = 256, H = 256;
            var px = new Color[W * H];
            Color top = NeonColors[ci], bottom = Color.Lerp(NeonColors[(ci + 1) % NeonColors.Length], Color.white, 0.3f);
            var rng = new System.Random(ci * 13 + 1);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = Color.Lerp(bottom, top, y / (float)H) * 0.85f;
            for (int s = 0; s < 22; s++)   // 반짝이 별
            {
                int cx = rng.Next(W), cy = rng.Next(H), r = 3 + rng.Next(6);
                for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) * Mathf.Abs(dy) > r) continue;   // 십자 별 모양
                    int xx = cx + dx, yy = cy + dy;
                    if (xx >= 0 && xx < W && yy >= 0 && yy < H) px[yy * W + xx] = Color.white;
                }
            }
            var t = new Texture2D(W, H, TextureFormat.RGB24, true);
            t.SetPixels(px); t.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var m = Mat("Poster" + ci, Color.white * 0.5f, 0.6f, 0f, Color.white * 0.9f, tex);
        m.SetTexture("_EmissionMap", tex);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 모서리가 둥근 상자 (실제 기계 캐비닛처럼 빛이 모서리에 맺힘)
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    public static Transform RBox(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider, float radius)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.AddComponent<MeshFilter>().sharedMesh = RoundedCube(size, radius);
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
        if (collider) g.AddComponent<BoxCollider>().size = size;
        staticRenderers.Add(r);
        return g.transform;
    }

    public static Mesh RoundedCube(Vector3 size, float r)
    {
        string key = string.Format(System.Globalization.CultureInfo.InvariantCulture, "RB_{0:F3}_{1:F3}_{2:F3}_{3:F3}", size.x, size.y, size.z, r);
        if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
        if (!AssetDatabase.IsValidFolder(Dir + "/Meshes")) AssetDatabase.CreateFolder(Dir, "Meshes");
        string path = $"{Dir}/Meshes/{key}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { meshCache[key] = existing; return existing; }

        Vector3 h = size * 0.5f;
        // 축마다 샘플: 가장자리 근처(둥근 부분)에 촘촘하게
        float[] Samples(float half)
        {
            float rr = Mathf.Min(r, half * 0.45f);
            var list = new List<float> { -half };
            foreach (float t in new[] { 0.15f, 0.45f, 1f }) list.Add(-half + rr * t);
            foreach (float t in new[] { 1f, 0.45f, 0.15f }) list.Add(half - rr * t);
            list.Add(half);
            return list.ToArray();
        }
        float[] sx = Samples(h.x), sy = Samples(h.y), sz = Samples(h.z);
        Vector3 inner = new Vector3(Mathf.Max(0f, h.x - r), Mathf.Max(0f, h.y - r), Mathf.Max(0f, h.z - r));
        float rad = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v, float[] su, float[] sv, float hn)
        {
            int start = verts.Count;
            for (int j = 0; j < sv.Length; j++)
                for (int i = 0; i < su.Length; i++)
                {
                    Vector3 p = n * hn + u * su[i] + v * sv[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - c;
                    Vector3 nn = d.sqrMagnitude > 1e-10f ? d.normalized : n;
                    verts.Add(c + nn * rad); norms.Add(nn); uvs.Add(new Vector2(su[i], sv[j]));
                }
            int w = su.Length;
            for (int j = 0; j < sv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                    // Unity 앞면 = 시계 방향: 면 법선이 n과 같은 쪽이 되도록
                    if (Vector3.Dot(Vector3.Cross(verts[c] - verts[a], verts[b] - verts[a]), n) >= 0f) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                }
        }
        Face(Vector3.right, Vector3.forward, Vector3.up, sz, sy, h.x);
        Face(Vector3.left, Vector3.back, Vector3.up, sz, sy, h.x);
        Face(Vector3.up, Vector3.right, Vector3.forward, sx, sz, h.y);
        Face(Vector3.down, Vector3.right, Vector3.back, sx, sz, h.y);
        Face(Vector3.forward, Vector3.left, Vector3.up, sx, sy, h.z);
        Face(Vector3.back, Vector3.right, Vector3.up, sx, sy, h.z);
        var mesh = new Mesh { name = key };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        meshCache[key] = mesh;
        return mesh;
    }

    // ───────────────────────── 도우미 ─────────────────────────
    public static Transform Group(Transform parent, string name, Vector3 pos)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        return g;
    }

    public static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        staticRenderers.Add(r);
        return g.transform;
    }

    public static Transform Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }

    public static Transform Sph(Transform parent, string name, Vector3 pos, float d, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = Vector3.one * d;
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }

    static Transform Rotated(this Transform t, Vector3 euler) { t.localRotation = Quaternion.Euler(euler); return t; }

    public static void S(Transform t) { var r = t.GetComponent<Renderer>(); if (r != null && !staticRenderers.Contains(r)) staticRenderers.Add(r); }

    // 두 점을 잇는 네온관
    public static void Tube(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material m)
    {
        var t = Cyl(parent, name, (a + b) * 0.5f, new Vector3(radius * 2f, Vector3.Distance(a, b) * 0.5f, radius * 2f), m);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        t.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        S(t);
    }

    // 글자 (frontFacesPlusZ = 앞이 +Z인 물체 앞면에 붙일 때: 글자를 뒤집어 +Z 쪽에서 읽히게)
    public static TextMesh Text(Transform parent, string text, Vector3 pos, float size, Color c, bool frontFacesPlusZ)
    {
        Init();
        var g = new GameObject("Text");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        if (frontFacesPlusZ) g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tm = g.AddComponent<TextMesh>();
        tm.font = kr; tm.fontSize = 64; tm.characterSize = size; tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.color = c; tm.text = text;
        g.GetComponent<MeshRenderer>().sharedMaterial = textMat;
        return tm;
    }
}

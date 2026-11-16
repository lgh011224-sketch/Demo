// 오락실 사격 게임 부스 만들기 + 두 씬을 오락실 배경으로 바꾸기
//   Tools > Arcade > Build Shooting Gallery (ShootingRange 씬)
//   Tools > Arcade > Convert Whack-A-Mole to Arcade (WhackAMole 씬)
// 사격 부스: 앞 계산대 + 줄무늬 부스 + 5 m 앞 원형 표적지 3×3 (큰 10점 · 중간 20점 · 작은 30점) + 점수판 + 총구 레이저
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class GalleryBuilder
{
    public const float TargetZ = 5f;
    const string Dir = ArcadeBuilder.Dir;

    // 크기: 0 = 큰(10점), 1 = 중간(20점), 2 = 작은(30점). 줄마다 섞어서 배치
    static readonly int[,] SizeGrid = { { 0, 1, 2 }, { 1, 2, 0 }, { 2, 0, 1 } };
    static readonly float[] Radius = { 0.17f * 0.85f, 0.12f * 0.85f, 0.075f * 0.85f };   // 처음 크기의 0.85배
    static readonly int[] Points = { 10, 20, 30 };

    [MenuItem("Tools/Arcade/Build Shooting Gallery (ShootingRange scene)")]
    static void BuildMenu() => Debug.Log("[Arcade] " + BuildGalleryScene());

    [MenuItem("Tools/Arcade/Convert Whack-A-Mole to Arcade (WhackAMole scene)")]
    static void WhackMenu() => Debug.Log("[Arcade] " + ConvertWhackScene());

    // ───────────────────────── 두더지 씬 ─────────────────────────
    public static string ConvertWhackScene()
    {
        var env = GameObject.Find("Environment");
        int removed = 0;
        if (env != null)
            for (int i = env.transform.childCount - 1; i >= 0; i--)
            {
                var c = env.transform.GetChild(i);
                if (c.name == "WhackBooth") continue;   // 두더지 게임 부스(줄무늬 기둥·간판)는 유지
                Object.DestroyImmediate(c.gameObject); removed++;
            }
        var floor = GameObject.Find("WhackAMole/Floor");
        if (floor != null) { Object.DestroyImmediate(floor); removed++; }
        string r = ArcadeBuilder.Build(ArcadeBuilder.WhackLayout, 7);
        r += ", " + WhackCabinetBuilder.Build();
        EnglishSwitcher();
        SetPostFx();
        BakeProbe("Whack");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return $"두더지 씬: 야외 배경 {removed}개 삭제, {r}";
    }

    // ───────────────────────── 사격 씬 ─────────────────────────
    public static string BuildGalleryScene()
    {
        var rangeRoot = GameObject.Find("ShootingRange");
        if (rangeRoot == null) return "ShootingRange 루트 없음";

        // 예전 사격장 (레인·표적 운반대·모니터 등) 삭제
        var oldMgr = Object.FindAnyObjectByType<ShootingRangeManager>(FindObjectsInactive.Include);
        TextMesh hudLast = oldMgr != null ? oldMgr.hudLast : null, hudAmmo = oldMgr != null ? oldMgr.hudAmmo : null;
        Transform hudPanel = oldMgr != null ? oldMgr.hudPanel : null;
        RangePistol pistol = oldMgr != null ? oldMgr.pistol : Object.FindAnyObjectByType<RangePistol>(FindObjectsInactive.Include);
        foreach (string n in new[] { "Room", "Lanes", "Lights", "MonitorStand", "LaneMonitor", "Placard", "Props", "ReflectionProbe", "Gallery" })
        {
            var c = rangeRoot.transform.Find(n);
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
        GameObject mgrGo = oldMgr != null ? oldMgr.gameObject : GameObject.Find("GalleryManager") ?? new GameObject("GalleryManager");
        mgrGo.name = "GalleryManager";
        mgrGo.transform.SetParent(rangeRoot.transform, false);
        if (oldMgr != null) Object.DestroyImmediate(oldMgr);
        var mgr = mgrGo.GetComponent<GalleryManager>();
        if (mgr == null) mgr = mgrGo.AddComponent<GalleryManager>();
        if (hudLast != null) mgr.hudMain = hudLast;
        if (hudAmmo != null) mgr.hudSub = hudAmmo;
        if (hudPanel != null) mgr.hudPanel = hudPanel;
        mgr.pistol = pistol;

        string arcade = ArcadeBuilder.Build(ArcadeBuilder.GalleryLayout, 11);
        var g = ArcadeBuilder.Group(rangeRoot.transform, "Gallery", Vector3.zero);
        Booth(g);
        mgr.targets = Targets(g);
        Board(g, mgr);
        Laser(pistol);
        EnglishSwitcher();
        mgr.respawnSeconds = 5f;
        mgr.roundSeconds = 30f;
        EditorUtility.SetDirty(mgr);
        SetPostFx();
        BakeProbe("Gallery");
        EditorSceneManager.MarkSceneDirty(rangeRoot.scene);
        return $"사격 씬: 표적 {mgr.targets.Length}개, {arcade}";
    }

    // 씬 이동 확인 창의 장소 이름 (영어)
    static void EnglishSwitcher()
    {
        foreach (var sw in Object.FindObjectsByType<SceneSwitcher>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sw.targetLabel = sw.targetScene == "ShootingRange" ? "SHOOTING GALLERY" : "WHACK-A-MOLE";
            EditorUtility.SetDirty(sw);
        }
    }

    static void Booth(Transform g)
    {
        // 실사 재질: 페인트 칠한 합판 벽(미장 결), 나무 계산대, 나무 패널 뒷벽, 금속 받침
        var plaster = ArcadeBuilder.PH("plastered_wall_04", "diff");
        var plasterN = ArcadeBuilder.PH("plastered_wall_04", "nor_gl");
        var red = ArcadeBuilder.Mat("BoothRed", new Color(0.95f, 0.2f, 0.22f), 0.6f, 0f, null, plaster);
        ArcadeBuilder.SetNormal(red, plasterN, 0.7f);
        var yellow = ArcadeBuilder.Mat("BoothYellow", new Color(1.05f, 0.85f, 0.3f), 0.6f, 0f, null, plaster);
        ArcadeBuilder.SetNormal(yellow, plasterN, 0.7f);
        var navy = ArcadeBuilder.Mat("BoothNavy", new Color(0.55f, 0.6f, 0.95f), 0.45f, 0f, null, Tex("Assets/ShootingRange/External/PolyHaven/Textures/dark_paneled_wood/dark_paneled_wood_diff_2k.jpg"));
        navy.SetTextureScale("_BaseMap", new Vector2(2f, 1.5f));
        ArcadeBuilder.SetNormal(navy, Tex("Assets/ShootingRange/External/PolyHaven/Textures/dark_paneled_wood/dark_paneled_wood_nor_gl_2k.jpg", true), 1f);
        var wood = ArcadeBuilder.Mat("CounterTop", new Color(1f, 0.92f, 0.85f), 0.62f, 0f, null, Tex("Assets/WhackAMole/External/PolyHaven/Textures/fine_grained_wood/fine_grained_wood_diff_2k.jpg"));
        wood.SetTextureScale("_BaseMap", new Vector2(3f, 0.5f));
        ArcadeBuilder.SetNormal(wood, Tex("Assets/WhackAMole/External/PolyHaven/Textures/fine_grained_wood/fine_grained_wood_nor_gl_2k.jpg", true), 1f);
        var metal = ArcadeBuilder.Mat("RailMetal", new Color(0.7f, 0.72f, 0.78f), 0.7f, 0.9f, null, Tex("Assets/ShootingRange/External/PolyHaven/Textures/metal_plate/metal_plate_diff_2k.jpg"));
        ArcadeBuilder.SetNormal(metal, Tex("Assets/ShootingRange/External/PolyHaven/Textures/metal_plate/metal_plate_nor_gl_2k.jpg", true), 0.8f);

        // 계산대
        ArcadeBuilder.Box(g, "Counter", new Vector3(0f, 0.46f, 0.95f), new Vector3(2.9f, 0.92f, 0.4f), red, true);
        ArcadeBuilder.Box(g, "CounterTop", new Vector3(0f, 0.935f, 0.92f), new Vector3(3.0f, 0.04f, 0.5f), wood, true);
        for (int i = 0; i < 6; i++)
            ArcadeBuilder.Box(g, "Stripe", new Vector3(-1.2f + i * 0.48f, 0.46f, 0.745f), new Vector3(0.2f, 0.9f, 0.01f), yellow, false);
        var sign = ArcadeBuilder.Box(g, "CounterSign", new Vector3(0f, 0.6f, 0.735f), new Vector3(1.6f, 0.36f, 0.01f), ArcadeBuilder.Mat("SignBoard", new Color(0.02f, 0.02f, 0.03f), 0.6f), false);
        ArcadeBuilder.Text(g, "SHOOTING GALLERY", new Vector3(0f, 0.67f, 0.725f), 0.024f, new Color(1f, 0.88f, 0.3f), false);
        ArcadeBuilder.Text(g, "LARGE 10  -  MEDIUM 20  -  SMALL 30", new Vector3(0f, 0.53f, 0.725f), 0.0095f, Color.white, false);

        // 부스 옆벽 (빨강·노랑 줄무늬) + 뒷벽 (남색 + 별)
        for (int i = 0; i < 5; i++)
        {
            float z = 1.6f + i;
            foreach (float sx in new[] { -1f, 1f })
                ArcadeBuilder.Box(g, "SideWall", new Vector3(sx * 1.8f, 1.45f, z), new Vector3(0.08f, 2.9f, 1f), i % 2 == 0 ? red : yellow, true);
        }
        ArcadeBuilder.Box(g, "BackWall", new Vector3(0f, 1.45f, 6.1f), new Vector3(3.7f, 2.9f, 0.1f), navy, true);
        ArcadeBuilder.Box(g, "BoothCeiling", new Vector3(0f, 2.92f, 3.6f), new Vector3(3.7f, 0.05f, 5f), navy, false);
        var star = ArcadeBuilder.Neon("Star", new Color(1f, 0.85f, 0.3f), 1.8f);
        var rng = new System.Random(3);
        for (int i = 0; i < 26; i++)
        {
            var s = ArcadeBuilder.Box(g, "Star", new Vector3(((float)rng.NextDouble() - 0.5f) * 3.4f, 0.3f + (float)rng.NextDouble() * 2.4f, 6.045f), Vector3.one * (0.03f + (float)rng.NextDouble() * 0.04f), star, false);
            s.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        // 앞쪽 간판 (전구 테두리)
        ArcadeBuilder.Box(g, "Header", new Vector3(0f, 3.05f, 1.1f), new Vector3(3.8f, 0.5f, 0.2f), red, false);
        ArcadeBuilder.Box(g, "HeaderFace", new Vector3(0f, 3.05f, 0.995f), new Vector3(3.5f, 0.38f, 0.01f), ArcadeBuilder.Mat("SignBoard", new Color(0.02f, 0.02f, 0.03f), 0.6f), false);
        ArcadeBuilder.Text(g, "★  SHOOTING GALLERY  ★", new Vector3(0f, 3.05f, 0.985f), 0.034f, new Color(1f, 0.9f, 0.35f), false);
        var bulbMat = ArcadeBuilder.Neon("Bulb", new Color(1f, 0.85f, 0.4f), 3f);
        var bulbs = new List<Renderer>();
        for (int i = 0; i < 24; i++)
        {
            float x = -1.84f + i * 0.16f;
            foreach (float y in new[] { 2.82f, 3.28f })
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Bulb"; Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(g, false);
                b.transform.localPosition = new Vector3(x, y, 0.99f);
                b.transform.localScale = Vector3.one * 0.04f;
                var r = b.GetComponent<Renderer>(); r.sharedMaterial = bulbMat; r.shadowCastingMode = ShadowCastingMode.Off;
                bulbs.Add(r);
            }
        }
        var mq = g.gameObject.AddComponent<MarqueeLights>();
        mq.bulbs = bulbs.ToArray(); mq.speed = 7f;

        // 표적 비추는 조명
        foreach (float sx in new[] { -1f, 1f })
        {
            var lg = new GameObject("TargetSpot");
            lg.transform.SetParent(g, false);
            lg.transform.position = new Vector3(sx * 1.2f, 2.8f, 1.6f);
            lg.transform.LookAt(new Vector3(0f, 1.4f, TargetZ));
            var l = lg.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 45f; l.range = 9f; l.intensity = 10f;
            l.color = new Color(0.95f, 0.9f, 1f);
            l.shadows = sx < 0 ? LightShadows.Soft : LightShadows.None;
            lg.transform.position = new Vector3(sx * 1.2f, 2.3f, 1.4f);   // 낮게: 점수판(위쪽)에는 빛이 덜 닿게
            lg.transform.LookAt(new Vector3(0f, 1.3f, TargetZ));
            l.spotAngle = 38f;
        }
        // ── 화려한 네온 (점수판 근처에는 강한 조명을 두지 않음 → 점수가 잘 보임) ──
        var pink = ArcadeBuilder.Neon("GalPink", new Color(1f, 0.25f, 0.7f), 6f);
        var cyan = ArcadeBuilder.Neon("GalCyan", new Color(0.2f, 0.9f, 1f), 5.5f);
        var purple = ArcadeBuilder.Neon("GalPurple", new Color(0.7f, 0.3f, 1f), 6f);
        var yellowN = ArcadeBuilder.Neon("GalYellow", new Color(1f, 0.85f, 0.2f), 5f);
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * 1.745f;
            ArcadeBuilder.Tube(g, "WallNeonTop", new Vector3(x, 2.8f, 1.15f), new Vector3(x, 2.8f, 6.0f), 0.02f, pink);
            ArcadeBuilder.Tube(g, "WallNeonMid", new Vector3(x, 0.55f, 1.15f), new Vector3(x, 0.55f, 6.0f), 0.016f, cyan);
            ArcadeBuilder.Tube(g, "FloorNeon", new Vector3(sx * 1.7f, 0.03f, 1.15f), new Vector3(sx * 1.7f, 0.03f, 6.0f), 0.02f, purple);
            ArcadeBuilder.Tube(g, "EntryNeon", new Vector3(sx * 1.84f, 0.05f, 1.02f), new Vector3(sx * 1.84f, 2.85f, 1.02f), 0.022f, yellowN);
            // 표적 쪽을 가리키는 네온 화살표 ▶▶▶
            for (int k = 0; k < 3; k++)
                ArcadeBuilder.Text(g, "▶", new Vector3(x - sx * 0.01f, 1.35f, 2.6f + k * 0.45f), 0.05f, new Color(1f, 0.85f, 0.3f), false).transform.localRotation = Quaternion.Euler(0f, sx * 90f, 0f);
            // 옆벽 네온 글자
            var shoot = ArcadeBuilder.Text(g, sx < 0 ? "SHOOT!" : "BULLSEYE!", new Vector3(x - sx * 0.01f, 2.25f, 3.6f), 0.045f, sx < 0 ? new Color(1f, 0.45f, 0.85f) : new Color(0.45f, 0.95f, 1f), false);
            shoot.transform.localRotation = Quaternion.Euler(0f, sx * 90f, 0f);
            // 옆벽 아래쪽 은은한 색 조명 (부스 안을 밝히되 점수판엔 닿지 않게 낮게)
            var pg = new GameObject("WallGlow"); pg.transform.SetParent(g, false);
            pg.transform.position = new Vector3(sx * 1.3f, 0.9f, 3.2f);
            var pl = pg.AddComponent<Light>();
            pl.type = LightType.Point; pl.range = 3.2f; pl.intensity = 1.6f; pl.color = sx < 0 ? new Color(1f, 0.35f, 0.8f) : new Color(0.35f, 0.75f, 1f); pl.shadows = LightShadows.None;
            // 옆벽 그래피티
            ArcadeBuilder.S(GraffitiBaker.Place(g, GraffitiBaker.Graffiti(sx < 0 ? "Gal_LOCK_N_LOAD" : "Gal_SHARPSHOOTER", sx < 0 ? "LOCK N LOAD" : "SHARP SHOOTER", sx < 0 ? 0 : 2, sx < 0 ? 41 : 42), new Vector3(x - sx * 0.012f, 1.75f, 2.2f), sx * 90f, 1.6f));
            ArcadeBuilder.S(GraffitiBaker.Place(g, GraffitiBaker.Graffiti(sx < 0 ? "Gal_BANG" : "Gal_HEADSHOT", sx < 0 ? "BANG!" : "NICE SHOT!", sx < 0 ? 4 : 3, sx < 0 ? 43 : 44), new Vector3(x - sx * 0.012f, 1.0f, 4.7f), sx * 90f, 1.3f));
            // 옆벽 위 도는 전구 줄
            var bulbMat2 = ArcadeBuilder.Neon("Bulb", new Color(1f, 0.85f, 0.4f), 3f);
            var row = new List<Renderer>();
            for (int k = 0; k < 20; k++)
            {
                var b = ArcadeBuilder.Sph(g, "Bulb", new Vector3(x - sx * 0.025f, 2.66f, 1.25f + k * 0.24f), 0.04f, bulbMat2);
                row.Add(b.GetComponent<Renderer>());
            }
            var rowGo = new GameObject("BulbChase"); rowGo.transform.SetParent(g, false);
            var mq2 = rowGo.AddComponent<MarqueeLights>(); mq2.bulbs = row.ToArray(); mq2.speed = 9f; mq2.spacing = 2;
        }
        // 뒷벽 네온 테두리
        ArcadeBuilder.Tube(g, "BackNeonT", new Vector3(-1.72f, 2.86f, 6.04f), new Vector3(1.72f, 2.86f, 6.04f), 0.02f, purple);
        ArcadeBuilder.Tube(g, "BackNeonL", new Vector3(-1.72f, 0.05f, 6.04f), new Vector3(-1.72f, 2.86f, 6.04f), 0.02f, purple);
        ArcadeBuilder.Tube(g, "BackNeonR", new Vector3(1.72f, 0.05f, 6.04f), new Vector3(1.72f, 2.86f, 6.04f), 0.02f, purple);
        // 부스 천장 지그재그 네온
        for (int k = 0; k < 8; k++)
        {
            float z0 = 1.3f + k * 0.58f, z1 = z0 + 0.58f;
            float xa = (k % 2 == 0) ? -1.4f : 1.4f;
            ArcadeBuilder.Tube(g, "CeilZig", new Vector3(xa, 2.88f, z0), new Vector3(-xa, 2.88f, z1), 0.016f, k % 2 == 0 ? cyan : pink);
        }
        // 계산대 앞 네온 테두리
        ArcadeBuilder.Tube(g, "CounterNeon", new Vector3(-1.45f, 0.93f, 0.72f), new Vector3(1.45f, 0.93f, 0.72f), 0.016f, pink);
        ArcadeBuilder.Tube(g, "CounterNeon2", new Vector3(-1.45f, 0.06f, 0.72f), new Vector3(1.45f, 0.06f, 0.72f), 0.016f, cyan);
        // 표적 줄 받침
        for (int r = 0; r < 3; r++)
            ArcadeBuilder.Box(g, "Rail", new Vector3(0f, RowY(r) - 0.22f, TargetZ + 0.03f), new Vector3(2.1f, 0.04f, 0.08f), metal, false);
    }

    static Texture2D Tex(string path, bool normal = false)
    {
        if (normal && AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
        { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float RowY(int r) => 1.42f + (1 - r) * 0.5f;

    static PopTarget[] Targets(Transform g)
    {
        var mats = new Material[3];
        for (int s = 0; s < 3; s++) mats[s] = ArcadeBuilder.Mat("TargetFace" + s, Color.white, 0.25f, 0f, null, RingTexture(s));
        var metal = ArcadeBuilder.Mat("RailMetal", new Color(0.25f, 0.26f, 0.3f), 0.7f, 0.8f);
        var list = new List<PopTarget>();
        var root = ArcadeBuilder.Group(g, "Targets", Vector3.zero);
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                int size = SizeGrid[r, c];
                float rad = Radius[size];
                Vector3 hingePos = new Vector3((c - 1) * 0.64f, RowY(r) - 0.2f, TargetZ);
                var t = ArcadeBuilder.Group(root, $"Target_{r}{c}_{Points[size]}pt", hingePos);
                var hinge = ArcadeBuilder.Group(t, "Hinge", Vector3.zero);
                ArcadeBuilder.Box(hinge, "Stalk", new Vector3(0f, 0.02f, 0.012f), new Vector3(0.025f, 0.05f, 0.012f), metal, false);
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Disc";
                Object.DestroyImmediate(disc.GetComponent<Collider>());
                disc.transform.SetParent(hinge, false);
                disc.transform.localPosition = new Vector3(0f, 0.03f + rad, 0f);
                disc.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 둥근 면이 플레이어(-Z)를 향함
                disc.transform.localScale = new Vector3(rad * 2f, 0.006f, rad * 2f);
                disc.GetComponent<Renderer>().sharedMaterial = mats[size];
                var mc = disc.AddComponent<MeshCollider>(); mc.convex = true;
                ArcadeBuilder.Text(t, Points[size].ToString(), new Vector3(0f, -0.02f, -0.03f), 0.011f, new Color(1f, 0.9f, 0.4f), false);
                var pt = t.gameObject.AddComponent<PopTarget>();
                pt.points = Points[size];
                pt.hinge = hinge;
                list.Add(pt);
            }
        return list.ToArray();
    }

    static void Board(Transform g, GalleryManager mgr)
    {
        // 점수판: 반사 없는 검은 LED 판 + 노란 네온 테두리 (조명 반사로 글자가 묻히지 않게)
        ArcadeBuilder.Box(g, "ScoreBoard", new Vector3(0f, 2.55f, 6.03f), new Vector3(3.0f, 0.55f, 0.04f), ArcadeBuilder.Mat("ScoreBoardLED", new Color(0.01f, 0.01f, 0.03f), 0.05f), false);
        var frameN = ArcadeBuilder.Neon("GalYellow", new Color(1f, 0.85f, 0.2f), 5f);
        ArcadeBuilder.Tube(g, "BoardNeonT", new Vector3(-1.52f, 2.84f, 6.0f), new Vector3(1.52f, 2.84f, 6.0f), 0.014f, frameN);
        ArcadeBuilder.Tube(g, "BoardNeonB", new Vector3(-1.52f, 2.26f, 6.0f), new Vector3(1.52f, 2.26f, 6.0f), 0.014f, frameN);
        ArcadeBuilder.Tube(g, "BoardNeonL", new Vector3(-1.52f, 2.26f, 6.0f), new Vector3(-1.52f, 2.84f, 6.0f), 0.014f, frameN);
        ArcadeBuilder.Tube(g, "BoardNeonR", new Vector3(1.52f, 2.26f, 6.0f), new Vector3(1.52f, 2.84f, 6.0f), 0.014f, frameN);
        // 점수: 굵은 오락실 글꼴로 크게 / 안내: 아래 한 줄
        var bigFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/LuckiestGuy-Regular.ttf");
        var bigMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Text/Text3D_LuckiestGuy-Regular.mat");
        mgr.boardScore = ArcadeBuilder.Text(g, "SCORE 0", new Vector3(0f, 2.6f, 6.0f), 0.05f, new Color(1f, 0.88f, 0.2f), false);
        if (bigFont != null) { mgr.boardScore.font = bigFont; mgr.boardScore.fontStyle = FontStyle.Normal; mgr.boardScore.GetComponent<MeshRenderer>().sharedMaterial = bigMat; }
        mgr.boardInfo = ArcadeBuilder.Text(g, "PRESS [A] TO START", new Vector3(0f, 2.355f, 6.0f), 0.012f, Color.white, false);
        mgr.countdownText = ArcadeBuilder.Text(g, "3", new Vector3(0f, 1.5f, 4.6f), 0.12f, Color.white, false);
        mgr.countdownText.gameObject.SetActive(false);
        mgr.popupTemplate = ArcadeBuilder.Text(g, "+10", new Vector3(0f, 1f, 4.8f), 0.022f, Color.white, false);
        mgr.popupTemplate.name = "PopupTemplate";
        mgr.popupTemplate.gameObject.SetActive(false);
    }

    // 총구 레이저 (빨간 선 + 빨간 점)
    static void Laser(RangePistol pistol)
    {
        if (pistol == null || pistol.muzzle == null) return;
        var toolRoot = GameObject.Find("ToolRoot");
        if (toolRoot != null) foreach (var c in toolRoot.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // 총 자체에 레이저가 막히지 않게
        var old = pistol.muzzle.Find("Laser");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var red = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        red.SetColor("_BaseColor", new Color(3f, 0.12f, 0.08f, 1f));
        string path = Dir + "/Materials/LaserRed.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { EditorUtility.CopySerialized(red, existing); red = existing; } else AssetDatabase.CreateAsset(red, path);

        var lg = new GameObject("Laser");
        lg.transform.SetParent(pistol.muzzle, false);
        var lr = lg.AddComponent<LineRenderer>();
        lr.sharedMaterial = red;
        lr.startWidth = 0.004f; lr.endWidth = 0.0025f;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
        lr.useWorldSpace = true;
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "LaserDot";
        Object.DestroyImmediate(dot.GetComponent<Collider>());
        dot.transform.SetParent(lg.transform, false);
        var dr = dot.GetComponent<Renderer>(); dr.sharedMaterial = red; dr.shadowCastingMode = ShadowCastingMode.Off;
        var ls = lg.AddComponent<LaserSight>();
        ls.muzzle = pistol.muzzle;
        ls.dot = dot.transform;
        var drv = Object.FindAnyObjectByType<ImuGunDriver>(FindObjectsInactive.Include);
        if (drv != null) { drv.laser = ls; EditorUtility.SetDirty(drv); }
    }

    // 표적지 무늬: 큰 = 흰 바탕 빨간 고리, 중간 = 하늘색·흰 고리, 작은 = 금색 + 빨간 중심
    static Texture2D RingTexture(int size)
    {
        string path = $"{Dir}/TargetFace{size}.png";
        const int N = 256;
        Color a, b, center;
        switch (size)
        {
            case 0: a = Color.white; b = new Color(0.85f, 0.1f, 0.12f); center = new Color(0.85f, 0.1f, 0.12f); break;
            case 1: a = new Color(0.55f, 0.85f, 1f); b = Color.white; center = new Color(0.1f, 0.25f, 0.75f); break;
            default: a = new Color(1f, 0.8f, 0.2f); b = new Color(0.95f, 0.55f, 0.1f); center = new Color(0.85f, 0.1f, 0.12f); break;
        }
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;   // 0 = 가운데, 1 = 가장자리
                Color c = r < 0.18f ? center : (Mathf.FloorToInt(r * 5f) % 2 == 0 ? a : b);
                if (r > 0.94f) c = new Color(0.1f, 0.1f, 0.12f);   // 검은 테두리
                if (Mathf.Abs(r - 0.18f) < 0.012f) c = Color.black;
                px[y * N + x] = c;
            }
        var tex = new Texture2D(N, N, TextureFormat.RGB24, true);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 오락실용 후처리: 네온이 번지도록 블룸 강하게
    static void SetPostFx()
    {
        const string path = Dir + "/Arcade_PostFX.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            AssetDatabase.CopyAsset("Assets/WhackAMole/Realism/PostFX_Profile.asset", path);
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }
        if (profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
        {
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.7f);
        }
        if (profile.TryGet(out UnityEngine.Rendering.Universal.WhiteBalance wb)) { wb.temperature.Override(0f); wb.tint.Override(0f); }
        if (profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments ca)) { ca.saturation.Override(8f); ca.postExposure.Override(0.45f); ca.contrast.Override(8f); }
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (v.isGlobal) { v.sharedProfile = profile; EditorUtility.SetDirty(v); }
    }

    static void BakeProbe(string tag)
    {
        var probe = Object.FindAnyObjectByType<ReflectionProbe>();
        if (probe == null) return;
        Lightmapping.BakeReflectionProbe(probe, $"{Dir}/ReflectionProbe_{tag}.exr");
    }
}

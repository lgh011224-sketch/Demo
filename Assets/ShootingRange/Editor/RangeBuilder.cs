// 실내 사격장 씬 만들기
// 사용법: 상단 메뉴 Tools > Shooting Range > Build Scene
//  - Assets/Scenes/ShootingRange.unity 가 없으면 두더지 씬을 복사해 XR Origin(카메라·오른손 추적)만 남기고 새로 꾸밈
//  - 이미 있으면 그 씬을 열어 사격장 부분(ShootingRange 오브젝트)만 다시 만듦
//  - 두 씬을 빌드 목록에 넣고, 서로 오갈 수 있게 SceneSwitcher(왼손 그립 길게)를 둘 다에 배치
// 에셋: Assets/ShootingRange/External/PolyHaven (CC0) – 권총 service_pistol, 형광등, 탄약통, 실내 재질
//       표적지·탄흔·총구 화염 텍스처는 이 스크립트가 직접 그림
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RangeBuilder
{
    const string Root = "Assets/ShootingRange";
    const string PH = Root + "/External/PolyHaven";
    const string MatDir = Root + "/Materials";
    const string TexDir = Root + "/Textures";
    public const string ScenePath = "Assets/Scenes/ShootingRange.unity";
    public const string WhackScenePath = "Assets/Scenes/WhackAMole.unity";

    static readonly float[] LaneX = { -2.4f, -1.2f, 0f, 1.2f, 2.4f };
    const int ActiveLane = 2;
    static readonly Vector2 PaperSize = new Vector2(0.45f, 0.6f);

    static Material mFloor, mWall, mCeil, mDivider, mBackstop, mMetal, mDarkMetal, mCounter, mYellow, mPaper, mHole, mFlash,
        mBezel, mScreen, mMonTarget, mMarkLatest, mMarkOld, mPistol, mPlacard, mLightPanel;
    static Font font;

    [MenuItem("Tools/Shooting Range/Build Scene")]
    public static void BuildMenu() => Debug.Log("[사격장] " + Build());

    public static string Build()
    {
        foreach (var d in new[] { Root, MatDir, TexDir })
            if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace('\\', '/'), Path.GetFileName(d));

        // 1) 씬 준비
        EditorSceneManager.SaveOpenScenes();
        bool fresh = !File.Exists(ScenePath);
        if (fresh) AssetDatabase.CopyAsset(WhackScenePath, ScenePath);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (fresh) StripWhackContent();
        var old = GameObject.Find("ShootingRange");
        if (old != null) Object.DestroyImmediate(old);

        // 2) 재질·텍스처
        PrepareTextures();
        MakeMaterials();
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/LuckiestGuy-Regular.ttf");

        // 3) 공간
        var root = new GameObject("ShootingRange").transform;
        BuildRoom(root);
        BuildLanes(root, out RangeTarget activeTarget, out Transform carrier);
        BuildLights(root);
        var mon = BuildMonitor(root);
        BuildPlacard(root);
        BuildProps(root);

        // 4) 권총 (오른손)
        var pistol = BuildPistol();

        // 5) 진행 매니저
        var mgr = new GameObject("RangeManager").AddComponent<ShootingRangeManager>();
        mgr.transform.SetParent(root, false);
        mgr.target = activeTarget; mgr.carrier = carrier; mgr.pistol = pistol;
        mgr.monitorTarget = mon.target; mgr.monitorText = mon.info; mgr.monitorScore = mon.score;
        mgr.markerLatest = mMarkLatest; mgr.markerOld = mMarkOld;

        // 6) 씬 전환 (왼손 Y → 두더지 잡기)
        AddSwitcher(root, "WhackAMole");
        // 7) 아두이노: 총 진동을 하우징 진동 모듈로도 전송 (VIB <세기> <ms>)
        AddArduino(root);

        SetupRenderSettings(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterBuildScenes();
        return "built " + ScenePath;
    }

    // 두더지 씬 복사본에서 XR Origin·볼륨만 남기고 정리
    static void StripWhackContent()
    {
        foreach (string n in new[] { "WhackAMole", "Environment", "XR Interaction Simulator", "SceneSwitcher" })
        {
            var g = GameObject.Find(n);
            if (g != null) Object.DestroyImmediate(g);
        }
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject); // 실내라 햇빛 없음
        var toolRoot = GameObject.Find("ToolRoot");
        if (toolRoot != null)
        {
            for (int i = toolRoot.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(toolRoot.transform.GetChild(i).gameObject);
            var f = toolRoot.GetComponent<RightHandFollower>();
            // 두더지 잡기와 같은 하우징: 컨트롤러를 뒤집어 꽂으므로 X축 180도 보정
            f.rotationOffset = new Vector3(180f, 0f, 0f); f.positionOffset = Vector3.zero;
        }
    }

    // ───────────────────────── 공간 ─────────────────────────
    static void BuildRoom(Transform root)
    {
        var r = Group(root, "Room");
        float zMin = -2.6f, zMax = 14.6f, len = zMax - zMin, zc = (zMin + zMax) * 0.5f;
        Box(r, "Floor", new Vector3(0f, -0.05f, zc), new Vector3(6.4f, 0.1f, len), mFloor, 2.5f);
        Box(r, "Ceiling", new Vector3(0f, 3.25f, zc), new Vector3(6.4f, 0.1f, len), mCeil, 2f);
        Box(r, "WallL", new Vector3(-3.25f, 1.6f, zc), new Vector3(0.1f, 3.2f, len), mWall, 1.6f);
        Box(r, "WallR", new Vector3(3.25f, 1.6f, zc), new Vector3(0.1f, 3.2f, len), mWall, 1.6f);
        Box(r, "WallBack", new Vector3(0f, 1.6f, zMin - 0.05f), new Vector3(6.4f, 3.2f, 0.1f), mWall, 1.6f);
        // 탄막이: 고무 벽 + 기울어진 강철 방탄판 + 바닥 탄받이
        Box(r, "Backstop", new Vector3(0f, 1.6f, zMax + 0.05f), new Vector3(6.4f, 3.2f, 0.1f), mBackstop, 1.5f);
        var plate = Box(r, "DeflectorPlate", new Vector3(0f, 1.25f, zMax - 0.75f), new Vector3(6.4f, 2.9f, 0.06f), mDarkMetal, 2f);
        plate.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
        Box(r, "BulletTrap", new Vector3(0f, 0.2f, zMax - 0.9f), new Vector3(6.4f, 0.4f, 1.6f), mBackstop, 1.2f);
        // 천장 방탄 배플 (사선으로 기울어진 판)
        for (float z = 2.5f; z < 13f; z += 2f)
        {
            var b = Box(r, $"Baffle_{z:0}", new Vector3(0f, 2.78f, z), new Vector3(6.4f, 0.75f, 0.05f), mDivider, 1.5f);
            b.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        }
        // 사격선 (노란 선) + 거리 표시
        Box(r, "FiringLine", new Vector3(0f, 0.003f, 0.98f), new Vector3(6.4f, 0.006f, 0.07f), mYellow, 1f);
        foreach (int m in new[] { 5, 10, 15 })
        {
            if (m > 13) continue;
            foreach (float sx in new[] { -1f, 1f })
            {
                var t = Text(r, $"Dist_{m}_{sx}", $"{m} M", new Vector3(sx * 3.19f, 2.35f, m), Quaternion.Euler(0f, sx * 90f, 0f), 0.035f, new Color(0.95f, 0.8f, 0.2f), font);
                t.fontSize = 96;
            }
            Box(r, $"FloorMark_{m}", new Vector3(0f, 0.003f, m), new Vector3(6.4f, 0.006f, 0.04f), mYellow, 1f);
        }
    }

    static void BuildLanes(Transform root, out RangeTarget active, out Transform activeCarrier)
    {
        var lanes = Group(root, "Lanes");
        active = null; activeCarrier = null;
        // 칸막이 (레인 사이)
        foreach (float x in new[] { -3.0f, -1.8f, -0.6f, 0.6f, 1.8f, 3.0f })
        {
            Box(lanes, $"Divider_{x:0.0}", new Vector3(x, 1.05f, 0.15f), new Vector3(0.06f, 2.1f, 1.7f), mDivider, 1.2f);
            Box(lanes, $"DividerCap_{x:0.0}", new Vector3(x, 2.115f, 0.15f), new Vector3(0.08f, 0.03f, 1.72f), mMetal, 1f);
        }
        var rng = new System.Random(5);
        float[] staticDist = { 7f, 12.5f, 0f, 9f, 5.5f };
        for (int i = 0; i < LaneX.Length; i++)
        {
            float x = LaneX[i];
            var lane = Group(lanes, $"Lane_{i + 1}");
            // 사격대(받침대)
            Box(lane, "CounterTop", new Vector3(x, 1.0f, 0.68f), new Vector3(1.12f, 0.045f, 0.46f), mCounter, 1f);
            Box(lane, "CounterFront", new Vector3(x, 0.5f, 0.89f), new Vector3(1.12f, 0.98f, 0.03f), mMetal, 1f);
            // 레일 + 모터
            // 레일은 표적 바로 위라 그림자를 끔 (표적지에 세로 그림자가 지지 않게)
            Box(lane, "Rail", new Vector3(x, 2.5f, 6.9f), new Vector3(0.05f, 0.05f, 12.2f), mDarkMetal, 1f).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(lane, "Motor", new Vector3(x, 2.46f, 0.85f), new Vector3(0.24f, 0.16f, 0.22f), mDarkMetal, 0.5f);
            Text(lane, "LaneNo", (i + 1).ToString(), new Vector3(x, 2.3f, 0.93f), Quaternion.identity, 0.02f, new Color(0.95f, 0.8f, 0.2f), font);

            // 표적 운반대
            var carrier = new GameObject(i == ActiveLane ? "TargetCarrier" : "StaticCarrier").transform;
            carrier.SetParent(lane, false);
            carrier.localPosition = new Vector3(x, 0f, i == ActiveLane ? 1.8f : staticDist[i]);
            Box(carrier, "Trolley", new Vector3(0f, 2.43f, 0f), new Vector3(0.14f, 0.08f, 0.12f), mDarkMetal, 0.5f);
            Box(carrier, "Rod", new Vector3(0f, 2.09f, 0f), new Vector3(0.014f, 0.6f, 0.014f), mMetal, 0.5f);
            Box(carrier, "Hanger", new Vector3(0f, 1.79f, 0f), new Vector3(0.52f, 0.022f, 0.022f), mMetal, 0.5f);
            foreach (float cx in new[] { -0.16f, 0.16f })
                Box(carrier, "Clip", new Vector3(cx, 1.77f, 0f), new Vector3(0.035f, 0.04f, 0.03f), mDarkMetal, 0.5f);
            var swing = new GameObject("Swing").transform;
            swing.SetParent(carrier, false);
            swing.localPosition = new Vector3(0f, 1.775f, 0f);
            var paper = GameObject.CreatePrimitive(PrimitiveType.Quad);
            paper.name = "Paper";
            Object.DestroyImmediate(paper.GetComponent<Collider>());
            paper.transform.SetParent(swing, false);
            paper.transform.localPosition = new Vector3(0f, -PaperSize.y * 0.5f, 0f);
            paper.transform.localScale = new Vector3(PaperSize.x, PaperSize.y, 1f);
            var pr = paper.GetComponent<MeshRenderer>(); pr.sharedMaterial = mPaper; pr.shadowCastingMode = ShadowCastingMode.TwoSided;
            var col = paper.AddComponent<BoxCollider>(); col.size = new Vector3(1f, 1f, 0.01f);
            // 운반대·걸이·표적지는 그림자를 만들지 않음 (스포트라이트가 표적지 위에 큰 그림자를 드리우지 않게)
            foreach (var cr in carrier.GetComponentsInChildren<MeshRenderer>()) cr.shadowCastingMode = ShadowCastingMode.Off;
            var rt = carrier.gameObject.AddComponent<RangeTarget>();
            rt.swing = swing; rt.paper = paper.transform; rt.paperSize = PaperSize;
            rt.bullseyeOffset = new Vector2(0f, 0.04f); rt.ringWidth = 0.02f; rt.holeMaterial = mHole;
            if (i == ActiveLane) { active = rt; activeCarrier = carrier; }
            else
            {
                // 다른 레인 표적에는 이미 쏜 탄흔 몇 개 (분위기용)
                var holes = Group(paper.transform, "OldHoles");
                int n = rng.Next(4, 9);
                for (int k = 0; k < n; k++)
                {
                    float a = (float)rng.NextDouble() * 6.283f, d = (float)rng.NextDouble() * 0.12f;
                    var h = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(h.GetComponent<Collider>());
                    h.transform.SetParent(holes, false);
                    h.transform.localPosition = new Vector3(Mathf.Cos(a) * d / PaperSize.x, (Mathf.Sin(a) * d + 0.04f) / PaperSize.y, -0.0015f);
                    h.transform.localScale = new Vector3(0.012f / PaperSize.x, 0.012f / PaperSize.y, 1f);
                    h.GetComponent<Renderer>().sharedMaterial = mHole;
                }
            }
        }
    }

    static void BuildLights(Transform root)
    {
        var g = Group(root, "Lights");
        // 천장 형광등 모델 (Poly Haven) – 사격대 위와 레인 위
        var fixture = AssetDatabase.LoadAssetAtPath<GameObject>($"{PH}/Models/mounted_fluorescent_lights/mounted_fluorescent_lights_2k.fbx");
        if (fixture != null)
        {
            Bounds fb = RendererBounds(fixture);
            foreach (float x in new[] { -2.4f, -1.2f, 0f, 1.2f, 2.4f })
                foreach (float z in new[] { -1.0f, 0.6f })
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(fixture);
                    f.transform.SetParent(g, false);
                    f.transform.localPosition = new Vector3(x, 3.2f - fb.max.y, z);
                    f.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    foreach (var mr in f.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = mLightPanel;
                }
        }
        // 실제 조명: 사격대 쪽 밝게, 표적 쪽은 스포트라이트
        Light L(string n, LightType t, Vector3 pos, Vector3 look, float intensity, float range, float angle, Color c, LightShadows sh)
        {
            var lo = new GameObject(n).AddComponent<Light>();
            lo.transform.SetParent(g, false);
            lo.transform.localPosition = pos;
            lo.transform.LookAt(look);
            lo.type = t; lo.intensity = intensity; lo.range = range; lo.spotAngle = angle; lo.color = c; lo.shadows = sh;
            return lo;
        }
        Color cool = new Color(0.92f, 0.96f, 1f), warm = new Color(1f, 0.93f, 0.82f);
        L("BoothLight", LightType.Point, new Vector3(0f, 2.9f, 0.1f), Vector3.zero, 3.6f, 6.5f, 0f, cool, LightShadows.Soft);
        L("BoothLightL", LightType.Point, new Vector3(-2.4f, 2.9f, 0.1f), Vector3.zero, 2.6f, 5.5f, 0f, cool, LightShadows.None);
        L("BoothLightR", LightType.Point, new Vector3(2.4f, 2.9f, 0.1f), Vector3.zero, 2.6f, 5.5f, 0f, cool, LightShadows.None);
        L("TargetSpot10", LightType.Spot, new Vector3(0.7f, 3.05f, 7.0f), new Vector3(0f, 1.3f, 10.2f), 15f, 10f, 60f, warm, LightShadows.Soft);
        L("RangeSpotFar", LightType.Spot, new Vector3(0f, 3.05f, 11f), new Vector3(0f, 0.8f, 14f), 9f, 8f, 85f, warm, LightShadows.None);
        L("RangeFill", LightType.Point, new Vector3(0f, 2.8f, 4.5f), new Vector3(0f, 0f, 5f), 3f, 9f, 0f, warm, LightShadows.None);
        L("RangeFill2", LightType.Point, new Vector3(0f, 2.8f, 8.5f), new Vector3(0f, 0f, 8f), 2.4f, 8f, 0f, warm, LightShadows.None);
    }

    struct Monitor { public Transform target; public TextMesh info, score; }

    static Monitor BuildMonitor(Transform root)
    {
        // 사격대 오른쪽 위, 받침대에 세운 작은 모니터 (칸막이에 끼지 않게, 사수 눈을 향해 기울임)
        var stand = Group(root, "MonitorStand");
        stand.localPosition = new Vector3(0.38f, 1.0225f, 0.7f);
        Box(stand, "Base", new Vector3(0f, 0.006f, 0f), new Vector3(0.14f, 0.012f, 0.1f), mDarkMetal, 0.5f);
        Box(stand, "Pole", new Vector3(0f, 0.11f, 0.02f), new Vector3(0.025f, 0.2f, 0.025f), mDarkMetal, 0.5f);
        var m = Group(root, "LaneMonitor");
        m.localPosition = new Vector3(0.38f, 1.31f, 0.68f);
        m.localScale = Vector3.one * 0.7f;
        Vector3 toEye = new Vector3(0f, 1.6f, -0.1f) - m.localPosition;
        m.localRotation = Quaternion.LookRotation(-toEye.normalized, Vector3.up); // 앞면(-Z)이 사수 눈을 향함
        Box(m, "Bezel", Vector3.zero, new Vector3(0.5f, 0.32f, 0.03f), mBezel, 1f);
        var screen = Quad(m, "Screen", new Vector3(0f, 0f, -0.0155f), new Vector2(0.47f, 0.29f), mScreen);
        var tgt = Quad(m, "TargetView", new Vector3(-0.12f, 0f, -0.016f), new Vector2(0.195f, 0.26f), mMonTarget);
        var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var info = Text(m, "Info", "", new Vector3(0.0f, 0.125f, -0.017f), Quaternion.identity, 0.0021f, new Color(0.55f, 1f, 0.6f), builtin);
        info.anchor = TextAnchor.UpperLeft; info.alignment = TextAlignment.Left;
        var score = Text(m, "Score", "-", new Vector3(0.165f, -0.075f, -0.017f), Quaternion.identity, 0.0075f, Color.white, font != null ? font : builtin);
        Text(m, "ScoreLabel", "LAST", new Vector3(0.165f, -0.025f, -0.017f), Quaternion.identity, 0.0018f, new Color(0.7f, 0.75f, 0.8f), builtin);
        NoColliders(m); NoColliders(stand); // 총알이 모니터에 막히지 않게
        return new Monitor { target = tgt.transform, info = info, score = score };
    }

    static void BuildPlacard(Transform root)
    {
        // 사격대 왼쪽 위에 세운 작은 안내판 (탁상 거치대, 사수 눈을 향해 기울임)
        var p = Group(root, "Placard");
        p.localPosition = new Vector3(-0.38f, 1.13f, 0.66f);
        p.localScale = Vector3.one * 0.75f;
        Vector3 toEye = new Vector3(0f, 1.6f, -0.1f) - p.localPosition;
        p.localRotation = Quaternion.LookRotation(-toEye.normalized, Vector3.up);
        Box(p, "Board", Vector3.zero, new Vector3(0.4f, 0.26f, 0.012f), mPlacard, 1f);
        Box(p, "Easel", new Vector3(0f, -0.06f, 0.06f), new Vector3(0.03f, 0.2f, 0.012f), mDarkMetal, 0.5f).transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        var builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text(p, "Title", "SHOOTING RANGE", new Vector3(0f, 0.085f, -0.008f), Quaternion.identity, 0.0042f, new Color(0.95f, 0.8f, 0.2f), font != null ? font : builtin);
        var t = Text(p, "Body", "LEFT TRIGGER : SEND / RETURN TARGET\nRIGHT TRIGGER : FIRE  (10 ROUNDS)\n\nLEFT Y : WHACK-A-MOLE", new Vector3(0f, -0.03f, -0.008f), Quaternion.identity, 0.0023f, Color.white, builtin);
        t.lineSpacing = 1.15f;
        NoColliders(p);
    }

    static void NoColliders(Transform t)
    {
        foreach (var c in t.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    static void BuildProps(Transform root)
    {
        var g = Group(root, "Props");
        var ammo = AssetDatabase.LoadAssetAtPath<GameObject>($"{PH}/Models/ammo_box/ammo_box_2k.fbx");
        if (ammo == null) return;
        Bounds b = RendererBounds(ammo);
        var mat = PbrMat("AmmoBox", $"{PH}/Models/ammo_box/textures/ammo_box_diff_2k.jpg", null, 0f, 0.35f, 1f);
        // (사수 레인의 탄약통은 안내판 자리를 위해 치움 – 옆 레인에만 둠)
        foreach (var (x, z, yaw) in new[] { (LaneX[1] + 0.3f, 0.66f, -20f), (LaneX[3] - 0.25f, 0.6f, 75f) })
        {
            var a = (GameObject)PrefabUtility.InstantiatePrefab(ammo);
            a.transform.SetParent(g, false);
            float s = 0.32f / Mathf.Max(b.size.x, b.size.z, 0.01f); // 약 30 cm 탄약통
            a.transform.localScale = Vector3.one * s;
            a.transform.localPosition = new Vector3(x, 1.0225f - b.min.y * s, z);
            a.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var mr in a.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = mat;
        }
    }

    // ───────────────────────── 권총 ─────────────────────────
    // 권총 손잡이 기울기: 총열에 수직인 선에서 손잡이 아래쪽이 뒤로 기운 각도 (일반 권총 약 18도)
    public const float PistolGripTilt = 18f;
    // 컨트롤러 손잡이가 컨트롤러 기준 '아래(-Y)'에서 앞쪽으로 기운 각도 (헤드셋에서 확인 후 조정)
    public const float ControllerHandleTilt = 0f;
    // 하우징 장착 보정: 오른손 컨트롤러를 중지(그립) 버튼이 하늘을 보게 놓았을 때 기준,
    // 총의 좌우축(X) 기준 하우징 보정 각도. 시도: +90 → -90 → +180(안 맞음) → -90으로 되돌림 (2026-11-14, 헤드셋 확인 대기)
    public const float HousingRotation = -90f;
    // 최종 정렬 (2026-11-14): -90 상태에서 사용자가 헤드셋으로 본 모습
    //   "탄창 쪽이 하늘, 가늠자가 바닥, 총구가 나를 향하고 오른쪽으로 45도"
    // 를 정면·바로 선 자세로 되돌리도록 역산한 회전 (GripAlign localEulerAngles)
    // + 2차 보정: 하우징을 정면으로 잡으면 총구가 오른쪽으로 20~30도 → 왼쪽으로 25도
    // + 3차 보정: 총구 방향 180도 (수직축) → (296.66, 229.67, 313.53)
    // + 4차 보정(최종, 헤드셋에서 확인): 사용자가 정조준 자세로 잡은 상태에서 총구를 수평으로 52.6도 들어 올림
    public static readonly Vector3 GripAlignEuler = new Vector3(312.78f, 328.94f, 224.89f);
    // 권총 모델에서 손잡이 중심 (모델 좌표, m) – 컨트롤러 손잡이 중심(손바닥 위치)에 맞춤
    static readonly Vector3 PistolGripCenter = new Vector3(0.015f, -0.031f, 0f);

    public static RangePistol BuildPistol()
    {
        var toolRoot = GameObject.Find("ToolRoot");
        if (toolRoot == null) { Debug.LogWarning("[사격장] ToolRoot 없음"); return null; }
        var oldP = toolRoot.transform.Find("PistolTool");
        if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        if (mPistol == null) { PrepareTextures(); MakeMaterials(); }

        var tool = new GameObject("PistolTool").transform;
        tool.SetParent(toolRoot.transform, false);
        var visual = new GameObject("Visual").transform;   // 반동으로 움직이는 부분
        visual.SetParent(tool, false);
        // 권총 손잡이 축을 컨트롤러 손잡이 축에 일치시키는 기울기 (인스펙터에서 X값으로 미세 조정 가능)
        var align = new GameObject("GripAlign").transform;
        align.SetParent(visual, false);
        align.localRotation = Quaternion.Euler(GripAlignEuler);

        // 모델: 나무 손잡이 버전(_a) 부품만, 총구(-X)가 컨트롤러 앞(+Z)을 향하게 Y축 90도
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PH}/Models/service_pistol/service_pistol_2k.fbx");
        var model = new GameObject("Model").transform;
        model.SetParent(align, false);
        model.localRotation = Quaternion.Euler(0f, 90f, 0f);
        foreach (var mf in src.GetComponentsInChildren<MeshFilter>())
        {
            if (!mf.name.EndsWith("_a")) continue;
            var part = new GameObject(mf.name).transform;
            part.SetParent(model, false);
            Matrix4x4 w = mf.transform.localToWorldMatrix;
            part.localPosition = w.GetColumn(3); part.localRotation = w.rotation; part.localScale = w.lossyScale;
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = part.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mPistol;
        }
        // 손잡이 중심이 컨트롤러 원점(손잡이 중심)에 오도록
        model.localPosition = -(model.localRotation * PistolGripCenter);

        // 총구: 모델 (-0.176, 0.062, 0), 총열 방향(모델 -X)을 앞(+Z)으로 → 기울인 총을 따라 발사 방향도 같이 움직임
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(model, false);
        muzzle.localPosition = new Vector3(-0.176f, 0.062f, 0f);
        muzzle.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        var flash = Quad(muzzle, "Flash", new Vector3(0f, 0f, 0.02f), new Vector2(0.09f, 0.09f), mFlash);
        flash.transform.localRotation = Quaternion.identity;
        var fl = new GameObject("FlashLight").AddComponent<Light>();
        fl.transform.SetParent(muzzle, false);
        fl.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        fl.type = LightType.Point; fl.range = 4f; fl.intensity = 6f; fl.color = new Color(1f, 0.75f, 0.4f); fl.shadows = LightShadows.None;

        var audio = tool.gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0.6f;
        var p = tool.gameObject.AddComponent<RangePistol>();
        p.muzzle = muzzle; p.visual = visual; p.flashLight = fl; p.flashRenderer = flash.GetComponent<Renderer>(); p.audioSource = audio;
        return p;
    }

    // ───────────────────────── 씬 전환 · 렌더 설정 ─────────────────────────
    public static void AddSwitcher(Transform parent, string target)
    {
        var old = GameObject.Find("SceneSwitcher");
        if (old != null) Object.DestroyImmediate(old);
        var s = new GameObject("SceneSwitcher").AddComponent<SceneSwitcher>();
        if (parent != null) s.transform.SetParent(parent, false);
        s.targetScene = target;
        // 왼손 Y → "OO(으)로 가시겠습니까?" 확인 창 (한글 폰트 Noto Sans KR)
        s.targetLabel = target == "ShootingRange" ? "사격장" : "두더지 잡기";
        s.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/NotoSansKR.ttf");
        // 예전 버전의 그립 진행 표시 글자는 제거
        foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (tm.name == "SwitchProgress") Object.DestroyImmediate(tm.gameObject);
    }

    // 아두이노 연결 + 진동 전달 (두더지 씬과 같은 포트·속도를 기본으로)
    public static void AddArduino(Transform parent)
    {
        var existing = Object.FindAnyObjectByType<ArduinoSerial>(FindObjectsInactive.Include);
        GameObject go = existing != null ? existing.gameObject : new GameObject("Arduino");
        if (existing == null && parent != null) go.transform.SetParent(parent, false);
        var serial = go.GetComponent<ArduinoSerial>();
        if (serial == null) serial = go.AddComponent<ArduinoSerial>();
        serial.connectOnStart = true;
        var hap = go.GetComponent<ArduinoHaptics>();
        if (hap == null) hap = go.AddComponent<ArduinoHaptics>();
        hap.arduino = serial; hap.sendToArduino = true;
        EditorUtility.SetDirty(go);
    }

    // 시작 씬 = 두더지 잡기 (빌드 목록 첫 번째), 에디터 Play도 두더지 잡기부터
    public static void SetStartScene()
    {
        var list = EditorBuildSettings.scenes.ToList();
        var ordered = new List<EditorBuildSettingsScene>();
        foreach (var path in new[] { WhackScenePath, ScenePath })
        {
            list.RemoveAll(s => s.path == path);
            ordered.Add(new EditorBuildSettingsScene(path, true));
        }
        foreach (var s in list) ordered.Add(new EditorBuildSettingsScene(s.path, false)); // 템플릿 예제 씬은 빌드에서 제외
        EditorBuildSettings.scenes = ordered.ToArray();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WhackScenePath);
    }

    static void SetupRenderSettings(Transform root)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.24f, 0.24f, 0.25f);
        RenderSettings.fog = false;
        RenderSettings.sun = null;
        // 금속(권총·레일)이 주변을 비추도록 반사 프로브를 한 번 구움
        var probeGo = new GameObject("ReflectionProbe");
        probeGo.transform.SetParent(root, false);
        probeGo.transform.localPosition = new Vector3(0f, 1.6f, 1.5f);
        var probe = probeGo.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.size = new Vector3(6.4f, 3.2f, 17f);
        probe.center = new Vector3(0f, 0f, 4.5f);
        probe.boxProjection = true;
        probe.resolution = 256;
        Lightmapping.BakeReflectionProbe(probe, $"{Root}/RangeReflection.exr");
    }

    static void RegisterBuildScenes()
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var path in new[] { WhackScenePath, ScenePath })
            if (!list.Any(s => s.path == path)) list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ───────────────────────── 텍스처 · 재질 ─────────────────────────
    static void PrepareTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PH }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool ch = false;
            if (path.Contains("_nor_gl") && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; ch = true; }
            if ((path.Contains("_rough") || path.Contains("_ao")) && imp.sRGBTexture) { imp.sRGBTexture = false; ch = true; }
            if (imp.anisoLevel < 8) { imp.anisoLevel = 8; ch = true; }
            if (ch) imp.SaveAndReimport();
        }
        WriteTex("TargetPaper.png", DrawTargetPaper(), true, 2048);
        WriteTex("BulletHole.png", DrawHole(), true, 128);
        WriteTex("MuzzleFlash.png", DrawFlash(), true, 256);
        WriteTex("Marker.png", DrawMarker(), true, 128);
    }

    static void MakeMaterials()
    {
        string T(string id, string map) => $"{PH}/Textures/{id}/{id}_{map}_2k.jpg";
        mFloor = PbrMat("Floor", T("brushed_concrete", "diff"), T("brushed_concrete", "nor_gl"), 0f, 0.25f, 1f, T("brushed_concrete", "ao"));
        mWall = PbrMat("Wall", T("concrete_block_wall", "diff"), T("concrete_block_wall", "nor_gl"), 0f, 0.1f, 1f, T("concrete_block_wall", "ao"), new Color(0.82f, 0.82f, 0.8f));
        mCeil = PbrMat("Ceiling", T("concrete_panels", "diff"), T("concrete_panels", "nor_gl"), 0f, 0.1f, 1f, null, new Color(0.35f, 0.35f, 0.36f));
        // 칸막이·배플: 흡음 패널 느낌의 회색 패널
        mDivider = PbrMat("Divider", T("concrete_panels", "diff"), T("concrete_panels", "nor_gl"), 0f, 0.15f, 1f, T("concrete_panels", "ao"), new Color(0.42f, 0.44f, 0.47f));
        mBackstop = PbrMat("Backstop", T("rubber_tiles", "diff"), T("rubber_tiles", "nor_gl"), 0f, 0.15f, 1f, T("rubber_tiles", "ao"), new Color(0.35f, 0.35f, 0.35f));
        mMetal = PbrMat("Metal", T("metal_plate", "diff"), T("metal_plate", "nor_gl"), 0.85f, 0.45f, 1f, T("metal_plate", "ao"), new Color(0.75f, 0.76f, 0.78f));
        mDarkMetal = PbrMat("DarkMetal", T("metal_plate", "diff"), T("metal_plate", "nor_gl"), 0.8f, 0.35f, 1f, null, new Color(0.22f, 0.23f, 0.25f));
        mCounter = PbrMat("Counter", T("metal_plate", "diff"), T("metal_plate", "nor_gl"), 0.6f, 0.35f, 1f, null, new Color(0.45f, 0.46f, 0.48f));
        mYellow = PbrMat("YellowPaint", null, null, 0f, 0.3f, 1f, null, new Color(0.95f, 0.75f, 0.1f));
        mPlacard = PbrMat("Placard", null, null, 0f, 0.4f, 1f, null, new Color(0.08f, 0.09f, 0.1f));
        mBezel = PbrMat("MonitorBezel", null, null, 0.1f, 0.6f, 1f, null, new Color(0.03f, 0.03f, 0.035f));
        mPistol = PbrMat("Pistol", $"{PH}/Models/service_pistol/textures/service_pistol_diff_2k.jpg", null, 0.6f, 0.5f, 1f);
        mPaper = PbrMat("TargetPaper", $"{TexDir}/TargetPaper.png", null, 0f, 0.05f, 1f);
        mPaper.SetFloat("_Cull", (float)CullMode.Off);
        mLightPanel = UnlitMat("LightPanel", null, new Color(1.6f, 1.65f, 1.7f), false);
        mScreen = UnlitMat("MonitorScreen", null, new Color(0.015f, 0.02f, 0.025f), false);
        mMonTarget = UnlitMat("MonitorTarget", $"{TexDir}/TargetPaper.png", new Color(0.85f, 0.85f, 0.85f), false);
        mHole = UnlitMat("BulletHole", $"{TexDir}/BulletHole.png", Color.white, true);
        mMarkLatest = UnlitMat("MarkerLatest", $"{TexDir}/Marker.png", new Color(1f, 0.15f, 0.1f), true);
        mMarkOld = UnlitMat("MarkerOld", $"{TexDir}/Marker.png", new Color(0.35f, 0.55f, 1f), true);
        mFlash = UnlitMat("MuzzleFlash", $"{TexDir}/MuzzleFlash.png", new Color(2.5f, 1.8f, 1.0f), true);
    }

    static Material PbrMat(string name, string diff, string nor, float metal, float smooth, float tile, string ao = null, Color? tint = null)
    {
        var m = LoadOrNew($"{MatDir}/{name}.mat", "Universal Render Pipeline/Lit");
        m.SetTexture("_BaseMap", diff != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(diff) : null);
        m.SetColor("_BaseColor", tint ?? Color.white);
        if (nor != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nor)); m.EnableKeyword("_NORMALMAP"); }
        if (ao != null) { m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ao)); m.EnableKeyword("_OCCLUSIONMAP"); }
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        m.SetTextureScale("_BaseMap", Vector2.one * tile);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material UnlitMat(string name, string tex, Color color, bool cutout)
    {
        var m = LoadOrNew($"{MatDir}/{name}.mat", "Universal Render Pipeline/Unlit");
        m.SetTexture("_BaseMap", tex != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(tex) : null);
        m.SetColor("_BaseColor", color);
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.35f); m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest; m.SetOverrideTag("RenderType", "TransparentCutout");
        }
        m.SetFloat("_Cull", (float)CullMode.Off);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrNew(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static void WriteTex(string file, Texture2D tex, bool alpha, int maxSize)
    {
        string path = $"{TexDir}/{file}";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = alpha; imp.maxTextureSize = maxSize; imp.anisoLevel = 8;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
    }

    // ───────────────────────── 직접 그리는 텍스처 ─────────────────────────
    // 표적지: 미색 종이, 10점~1점 고리(7~10점은 검은 원), 고리 숫자, 테두리
    static Texture2D DrawTargetPaper()
    {
        int W = 1024, H = Mathf.RoundToInt(1024 * PaperSize.y / PaperSize.x);
        float pxPerM = W / PaperSize.x;
        float cx = W * 0.5f, cy = H * 0.5f + 0.04f * pxPerM;
        float ring = 0.02f * pxPerM;
        var px = new Color32[W * H];
        Color32 paper = new Color32(236, 232, 218, 255), ink = new Color32(18, 18, 18, 255), white = new Color32(240, 238, 230, 255);
        var rng = new System.Random(11);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                byte grain = (byte)rng.Next(0, 7);
                Color32 c = new Color32((byte)(paper.r - grain), (byte)(paper.g - grain), (byte)(paper.b - grain), 255);
                bool inBlack = d <= ring * 4f;                 // 7~10점 영역
                if (inBlack) c = ink;
                float frac = d / ring;                         // 고리 경계선
                if (d <= ring * 10f && Mathf.Abs(frac - Mathf.Round(frac)) * ring < 1.4f && Mathf.Round(frac) >= 1f)
                    c = inBlack ? white : ink;
                if (d <= ring * 0.5f && Mathf.Abs(d - ring * 0.5f) < 1.2f) c = white; // 정중앙 X링
                if (x < 14 || x >= W - 14 || y < 14 || y >= H - 14) c = new Color32(120, 120, 115, 255); // 테두리
                px[y * W + x] = c;
            }
        // 고리 숫자 (상하좌우)
        for (int s = 1; s <= 8; s++)
        {
            float r = (10 - s + 0.5f) * ring;
            Color32 col = s >= 7 ? white : ink;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                DrawDigit(px, W, H, s, Mathf.RoundToInt(cx + dx * r), Mathf.RoundToInt(cy + dy * r), 3, col);
        }
        var t = new Texture2D(W, H, TextureFormat.RGBA32, true);
        t.SetPixels32(px); t.Apply();
        return t;
    }

    static readonly string[] Digits =
    {
        "01110100011001110101110011000101110", "00100011000010000100001000010001110", "01110100010000100010001000100011111",
        "11111000100010000010000011000101110", "00010001100101010010111110001000010", "11111100001111000001000011000101110",
        "00110010001000011110100011000101110", "11111000010001000100010000100001000", "01110100011000101110100011000101110",
        "01110100011000101111000010001001100",
    };

    static void DrawDigit(Color32[] px, int W, int H, int digit, int cx, int cy, int scale, Color32 col)
    {
        string g = Digits[digit % 10];
        for (int row = 0; row < 7; row++)
            for (int c = 0; c < 5; c++)
            {
                if (g[row * 5 + c] != '1') continue;
                for (int sy = 0; sy < scale; sy++)
                    for (int sx = 0; sx < scale; sx++)
                    {
                        int x = cx - 5 * scale / 2 + c * scale + sx, y = cy + 7 * scale / 2 - row * scale - sy;
                        if (x >= 0 && x < W && y >= 0 && y < H) px[y * W + x] = col;
                    }
            }
    }

    static Texture2D DrawHole()
    {
        int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        var rng = new System.Random(3);
        float[] edge = new float[32]; for (int i = 0; i < 32; i++) edge[i] = 0.32f + (float)rng.NextDouble() * 0.08f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f, d = Mathf.Sqrt(u * u + v * v);
                float a = (Mathf.Atan2(v, u) / (2f * Mathf.PI) + 0.5f) * 32f;
                float e = Mathf.Lerp(edge[(int)a % 32], edge[((int)a + 1) % 32], a - Mathf.Floor(a));
                Color c;
                if (d < e * 0.75f) c = new Color(0.02f, 0.02f, 0.02f, 1f);                      // 뚫린 구멍
                else if (d < e) c = new Color(0.25f, 0.22f, 0.2f, 1f);                          // 그을린 가장자리
                else if (d < e + 0.06f) c = new Color(0.55f, 0.52f, 0.48f, 0.45f);             // 살짝 번진 자국
                else c = new Color(0, 0, 0, 0);
                t.SetPixel(x, y, c);
            }
        t.Apply(); return t;
    }

    static Texture2D DrawFlash()
    {
        int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f, d = Mathf.Sqrt(u * u + v * v) * 2f;
                float ang = Mathf.Atan2(v, u);
                float star = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 8f) * 0.6f + 0.25f; // 6갈래 섬광
                float a = Mathf.Clamp01(1f - d / star);
                float core = Mathf.Clamp01(1f - d / 0.25f);
                t.SetPixel(x, y, new Color(1f, 0.85f + core * 0.15f, 0.55f + core * 0.45f, Mathf.Max(a, core)));
            }
        t.Apply(); return t;
    }

    static Texture2D DrawMarker()
    {
        int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S - 0.5f, v = (y + 0.5f) / S - 0.5f, d = Mathf.Sqrt(u * u + v * v);
                Color c = d < 0.36f ? Color.white : d < 0.47f ? new Color(0.05f, 0.05f, 0.05f, 1f) : new Color(0, 0, 0, 0);
                t.SetPixel(x, y, c);
            }
        t.Apply(); return t;
    }

    // ───────────────────────── 도우미 ─────────────────────────
    static Transform Group(Transform parent, string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        return g;
    }

    // 면 크기에 맞춘 UV(미터 단위 / tile)를 가진 상자 메시 → 큰 벽도 텍스처가 늘어나지 않음
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float tile)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        var mesh = new Mesh { name = name };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        Vector3 h = size * 0.5f;
        void Face(Vector3 n, Vector3 u, Vector3 w, float su, float sw)
        {
            int i = v.Count;
            Vector3 c = Vector3.Scale(n, h);
            Vector3 a = Vector3.Scale(u, h), b = Vector3.Scale(w, h);
            v.Add(c - a - b); v.Add(c + a - b); v.Add(c + a + b); v.Add(c - a + b);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su / tile, 0)); uv.Add(new Vector2(su / tile, sw / tile)); uv.Add(new Vector2(0, sw / tile));
            tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
        Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
        Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
        Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
        Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
        Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        // 감기 방향 확인: 법선이 바깥을 향하지 않으면 뒤집음
        if (Vector3.Dot(mesh.normals[0], Vector3.forward) < 0f)
        {
            for (int i = 0; i < tri.Count; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; }
            mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents();
        }
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        var bc = g.AddComponent<BoxCollider>(); bc.size = size; // 총알이 벽·바닥에 막히도록
        g.isStatic = true;
        return g;
    }

    static GameObject Quad(Transform parent, string name, Vector3 pos, Vector2 size, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off;
        return q;
    }

    static TextMesh Text(Transform parent, string name, string text, Vector3 pos, Quaternion rot, float charSize, Color color, Font f)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localRotation = rot;
        var tm = g.AddComponent<TextMesh>();
        if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = f; g.GetComponent<MeshRenderer>().sharedMaterial = TextMat.For(f); // 깊이 검사하는 글자 머티리얼
        tm.text = text; tm.fontSize = 64; tm.characterSize = charSize; tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        return tm;
    }

    static Bounds RendererBounds(GameObject prefab)
    {
        Bounds b = new Bounds(); bool first = true;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            var mb = mf.sharedMesh.bounds;
            var w = GeometryUtility.CalculateBounds(new[] { mb.min, mb.max }, mf.transform.localToWorldMatrix);
            if (first) { b = w; first = false; } else b.Encapsulate(w);
        }
        return b;
    }
}

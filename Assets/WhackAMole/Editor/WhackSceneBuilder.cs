// 메뉴 한 번으로 두더지 게임 장면을 만들어 주는 에디터 스크립트
// 사용법: 장면에 XR Origin (VR)을 먼저 추가한 뒤, 상단 메뉴 Tools > Whack-A-Mole > Build Scene 클릭
// 이 파일은 반드시 "Editor" 폴더 안에 있어야 합니다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackSceneBuilder
{
    const string RootDir = "Assets/WhackAMole";
    const string MatDir = RootDir + "/Materials";

    [MenuItem("Tools/Whack-A-Mole/Build Scene")]
    public static void Build()
    {
        Camera cam = FindXRCamera();
        if (cam == null || cam.transform.parent == null)
        {
            EditorUtility.DisplayDialog("두더지 게임",
                "XR Origin을 찾지 못했습니다.\n먼저 Hierarchy 창에서 우클릭 > XR > XR Origin (VR)을 추가한 뒤 다시 실행하세요.",
                "확인");
            return;
        }

        GameObject old = GameObject.Find("WhackAMole");
        Transform oldTools = cam.transform.parent.Find("ToolRoot");
        if (old != null || oldTools != null)
        {
            if (!EditorUtility.DisplayDialog("두더지 게임", "이미 만든 두더지 게임이 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소"))
                return;
            if (old != null) Object.DestroyImmediate(old);
            if (oldTools != null) Object.DestroyImmediate(oldTools.gameObject);
        }

        DisableExtraCameras(cam);
        DisableDefaultControllers(cam.transform.parent);
        EnsureFolders();

        Material matFloor = Mat("Floor", new Color(0.35f, 0.38f, 0.35f));
        Material matTable = Mat("Table", new Color(0.30f, 0.55f, 0.30f));
        Material matHole = Mat("Hole", new Color(0.08f, 0.06f, 0.05f));
        Material matMole = Mat("Mole", new Color(0.45f, 0.30f, 0.20f));
        Material matWall = Mat("Wall", new Color(0.75f, 0.72f, 0.65f));
        Material matWoodLight = Mat("WoodLight", new Color(0.85f, 0.68f, 0.45f));
        Material matWoodDark = Mat("WoodDark", new Color(0.45f, 0.28f, 0.15f));
        Material matMetal = Mat("Metal", new Color(0.55f, 0.57f, 0.60f));
        Material matGun = Mat("Gun", new Color(0.15f, 0.15f, 0.18f));

        GameObject root = new GameObject("WhackAMole");

        // 바닥
        Prim(PrimitiveType.Plane, "Floor", root.transform, Vector3.zero, new Vector3(2f, 1f, 2f), matFloor, true);

        // 두더지 탁자 (윗면 높이 0.75 m, 플레이어 앞 약 0.6 m)
        GameObject table = Prim(PrimitiveType.Cube, "Table", root.transform,
            new Vector3(0f, 0.375f, 0.58f), new Vector3(0.6f, 0.75f, 0.5f), matTable, true);
        table.AddComponent<BoardSurface>();

        // 탁자 두더지 3 x 3
        Mole[] tableMoles = new Mole[9];
        float[] xs = { -0.16f, 0f, 0.16f };
        float[] zs = { 0.43f, 0.58f, 0.73f };
        int n = 0;
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = new Vector3(xs[c], 0.75f, zs[r]);
                Prim(PrimitiveType.Cylinder, $"Hole_{r}{c}", root.transform,
                    p + new Vector3(0f, 0.0015f, 0f), new Vector3(0.12f, 0.002f, 0.12f), matHole, false);

                GameObject moleGo = new GameObject($"Mole_{r}{c}");
                moleGo.transform.SetParent(root.transform, false);
                moleGo.transform.position = p;
                GameObject body = Prim(PrimitiveType.Capsule, "Body", moleGo.transform,
                    new Vector3(0f, -0.09f, 0f), new Vector3(0.1f, 0.07f, 0.1f), matMole, true);

                Mole mole = moleGo.AddComponent<Mole>();
                mole.body = body.transform;
                mole.downHeight = -0.09f;
                mole.upHeight = 0.06f;
                mole.speed = 0.8f;
                tableMoles[n++] = mole;
            }
        }

        // ※ 사격 게임(표적 벽·표적·총)은 별도 씬으로 분리 예정이라 이 씬에서는 만들지 않음

        // 상태 표시 글자
        GameObject board = new GameObject("StatusBoard");
        board.transform.SetParent(root.transform, false);
        board.transform.position = new Vector3(0f, 1.6f, 1.1f);
        Prim(PrimitiveType.Quad, "Backdrop", board.transform, new Vector3(0f, 0f, 0.01f), new Vector3(0.9f, 0.28f, 1f), matHole, false);
        GameObject textGo = new GameObject("StatusText");
        textGo.transform.SetParent(board.transform, false);
        TextMesh tm = textGo.AddComponent<TextMesh>();
        Font font = LoadFont();
        if (font != null)
        {
            tm.font = font;
            textGo.GetComponent<MeshRenderer>().sharedMaterial = TextMat.For(font); // 깊이 검사하는 글자 머티리얼
        }
        tm.fontSize = 64;
        tm.characterSize = 0.005f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = "Whack-A-Mole";

        // 오른손 도구 (XR Origin > Camera Offset 아래)
        GameObject toolRoot = new GameObject("ToolRoot");
        toolRoot.transform.SetParent(cam.transform.parent, false);
        toolRoot.AddComponent<RightHandFollower>();

        // 드럼 스틱 (Wood 모드) - 겉모양은 WhackToolModels.cs에서 만듦
        GameObject wood = new GameObject("WoodTool");
        wood.transform.SetParent(toolRoot.transform, false);
        ToolHead woodHead = MakeHitPoint(wood.transform, WhackToolModels.DrumTipPos, 0.04f, 0.08f);
        WhackToolModels.BuildDrumStick(wood.transform);

        // 쇠 망치 (Hammer 모드) - 겉모양은 WhackToolModels.cs에서 만듦
        GameObject hammer = new GameObject("HammerTool");
        hammer.transform.SetParent(toolRoot.transform, false);
        ToolHead hammerHead = MakeHitPoint(hammer.transform, WhackToolModels.HammerHitPos, 0.045f, 0.06f);
        WhackToolModels.BuildHammer(hammer.transform);

        // 게임 매니저와 아두이노
        GameObject arduinoGo = new GameObject("Arduino");
        arduinoGo.transform.SetParent(root.transform, false);
        ArduinoSerial arduino = arduinoGo.AddComponent<ArduinoSerial>();

        GameObject managerGo = new GameObject("GameManager");
        managerGo.transform.SetParent(root.transform, false);
        WhackGameManager gm = managerGo.AddComponent<WhackGameManager>();
        gm.arduino = arduino;
        gm.woodTool = wood;
        gm.hammerTool = hammer;
        gm.woodHead = woodHead;
        gm.hammerHead = hammerHead;
        gm.tableMoles = tableMoles;
        gm.gunTargets = new Mole[0];
        gm.allowGunMode = false;
        gm.startMode = ToolMode.Wood;
        gm.statusText = tm;
        woodHead.manager = gm;
        hammerHead.manager = gm;

        // 주변 환경 장식 (정원 + 카니발 부스) - WhackEnvironment.cs
        WhackEnvironment.Build();
        // 시작 카운트다운(3 2 1 START!)과 게임 중 점수 글자 - WhackStartFx.cs
        WhackStartFx.Setup();
        // 두더지·표적 겉모양 (배경 두더지와 같은 디자인, 표적은 구멍에서 빼꼼) - WhackMoleModels.cs
        WhackMoleModels.RestyleGameMoles(gm);
        // 탁자 구멍: 원판 대신 흙구멍 - WhackHoleModels.cs
        WhackHoleModels.RestyleTableHoles(gm);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = managerGo;
        EditorUtility.DisplayDialog("두더지 게임",
            "장면을 만들었습니다.\nFile > Save로 장면을 저장한 뒤 Play를 누르세요.\n설정은 Hierarchy의 WhackAMole > GameManager에서 바꿉니다.",
            "확인");
    }

    static ToolHead MakeHitPoint(Transform parent, Vector3 localPos, float colliderRadius, float hitRadius)
    {
        GameObject hp = new GameObject("HitPoint");
        hp.transform.SetParent(parent, false);
        hp.transform.localPosition = localPos;
        SphereCollider sc = hp.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = colliderRadius;
        ToolHead th = hp.AddComponent<ToolHead>(); // Rigidbody는 자동으로 붙음
        Rigidbody rb = hp.GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        th.hitRadius = hitRadius;
        return th;
    }

    static Camera FindXRCamera()
    {
#if UNITY_2023_1_OR_NEWER
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        Camera[] cams = Object.FindObjectsOfType<Camera>();
#endif
        foreach (Camera c in cams)
        {
            Transform p = c.transform.parent;
            if (p != null && (p.name.Contains("Offset") || (p.parent != null && p.parent.name.Contains("XR"))))
                return c;
        }
        foreach (Camera c in cams)
            if (c.transform.parent != null) return c;
        return null;
    }

    static void DisableExtraCameras(Camera keep)
    {
#if UNITY_2023_1_OR_NEWER
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        Camera[] cams = Object.FindObjectsOfType<Camera>();
#endif
        foreach (Camera c in cams)
        {
            if (c == keep || c.transform.parent != null) continue;
            c.gameObject.SetActive(false);
            Debug.Log($"[두더지] 기본 카메라 '{c.name}'를 껐습니다. (XR Origin 카메라만 사용)");
        }
    }

    // XR Origin에 기본으로 들어 있는 컨트롤러 모델·레이저를 끔 (도구와 겹치지 않도록)
    static void DisableDefaultControllers(Transform cameraOffset)
    {
        foreach (Transform child in cameraOffset)
        {
            string nm = child.name.ToLowerInvariant();
            if (child.GetComponent<Camera>() != null || child.name == "ToolRoot") continue;
            if (nm.Contains("controller") || nm.Contains("hand"))
            {
                child.gameObject.SetActive(false);
                Debug.Log($"[두더지] XR Origin의 '{child.name}'를 껐습니다. (오른손 도구가 대신 따라갑니다)");
            }
        }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootDir)) AssetDatabase.CreateFolder("Assets", "WhackAMole");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(RootDir, "Materials");
    }

    static Shader DefaultShader()
    {
        RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultMaterial != null) return rp.defaultMaterial.shader;
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        return s;
    }

    static Material Mat(string name, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(DefaultShader());
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Font LoadFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            Collider col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        return g;
    }
}

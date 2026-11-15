// 오른손 도구(쇠 망치, 드럼 스틱)의 겉모양을 기본 도형으로 만들어 주는 에디터 스크립트
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Rebuild Tool Models
//  - 이미 만든 장면의 HammerTool / WoodTool 겉모양만 새로 만듭니다.
//  - 판정용 HitPoint(콜라이더 + ToolHead)는 지우지 않고 위치만 맞추므로 GameManager 연결은 그대로 유지됩니다.
// 좌표 기준: ToolRoot 로컬 +Z 방향이 도구가 뻗는 방향, z = 0 이 손(컨트롤러) 위치입니다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackToolModels
{
    const string MatDir = "Assets/WhackAMole/Materials";

    // 쇠 망치 머리 중심 / 드럼 스틱 끝 위치 (HitPoint가 여기에 놓임)
    public static readonly Vector3 HammerHitPos = new Vector3(0f, 0f, 0.27f);
    public static readonly Vector3 DrumTipPos = new Vector3(0f, 0f, 0.345f);

    [MenuItem("Tools/Whack-A-Mole/Rebuild Tool Models")]
    public static void RebuildInScene()
    {
        GameObject toolRoot = GameObject.Find("ToolRoot");
        if (toolRoot == null)
        {
            EditorUtility.DisplayDialog("두더지 게임", "ToolRoot를 찾지 못했습니다. 먼저 Build Scene으로 장면을 만드세요.", "확인");
            return;
        }

        Transform hammer = toolRoot.transform.Find("HammerTool");
        Transform wood = toolRoot.transform.Find("WoodTool");
        if (hammer != null) BuildHammer(hammer);
        if (wood != null) BuildDrumStick(wood);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(toolRoot.scene);
    }

    // ───────────────────────── 쇠 망치 ─────────────────────────
    // 고무 손잡이 + 나무 자루 + 양면 원형 타격면을 가진 강철 머리 (해머/클럽 해머 형태)
    public static void BuildHammer(Transform root)
    {
        ClearVisuals(root);

        Material grip = Mat("HammerGrip", new Color(0.70f, 0.12f, 0.10f), 0f, 0.35f);
        Material gripDark = Mat("HammerGripDark", new Color(0.08f, 0.08f, 0.09f), 0f, 0.30f);
        Material shaft = Mat("WoodLight", new Color(0.85f, 0.68f, 0.45f), 0f, 0.45f);
        Material steel = Mat("Steel", new Color(0.62f, 0.64f, 0.68f), 0.9f, 0.70f);
        Material steelDark = Mat("SteelDark", new Color(0.22f, 0.23f, 0.26f), 0.85f, 0.55f);
        Material steelFace = Mat("SteelPolished", new Color(0.85f, 0.86f, 0.88f), 1f, 0.90f);

        GameObject vis = Group(root, "Visual");
        Transform v = vis.transform;

        // 손잡이 끝 마개
        Prim(PrimitiveType.Sphere, "GripCap", v, new Vector3(0f, 0f, -0.005f), new Vector3(0.042f, 0.042f, 0.02f), gripDark);
        // 고무 그립 (손 쪽) + 홈 무늬
        Cyl("Grip", v, -0.005f, 0.115f, 0.038f, grip);
        for (int i = 0; i < 4; i++)
            Cyl($"GripRing_{i}", v, 0.015f + i * 0.025f, 0.021f + i * 0.025f, 0.040f, gripDark);
        Cyl("GripCollar", v, 0.115f, 0.125f, 0.034f, gripDark);
        // 나무 자루 (머리 쪽으로 살짝 가늘어짐)
        Cyl("Shaft", v, 0.125f, 0.215f, 0.028f, shaft);
        Cyl("ShaftNeck", v, 0.215f, 0.255f, 0.024f, shaft);
        // 머리 아래 쇠 고리
        Cyl("Ferrule", v, 0.235f, 0.25f, 0.030f, steelDark);

        // 머리: 로컬 Y축(위아래)으로 길게 놓인 강철 몸통, 양 끝이 타격면
        Vector3 c = HammerHitPos;
        Prim(PrimitiveType.Cube, "HeadBody", v, c, new Vector3(0.042f, 0.075f, 0.042f), steel);
        Prim(PrimitiveType.Cube, "HeadCheekL", v, c + new Vector3(-0.022f, 0f, 0f), new Vector3(0.004f, 0.05f, 0.03f), steelDark);
        Prim(PrimitiveType.Cube, "HeadCheekR", v, c + new Vector3(0.022f, 0f, 0f), new Vector3(0.004f, 0.05f, 0.03f), steelDark);
        // 자루가 머리를 관통한 모습 + 쐐기
        Cyl("Eye", v, c.z - 0.022f, c.z + 0.024f, 0.022f, shaft);
        Prim(PrimitiveType.Cube, "Wedge", v, c + new Vector3(0f, 0f, 0.0235f), new Vector3(0.004f, 0.018f, 0.002f), steelDark);
        // 양쪽 타격면 (목 → 넓은 면 → 모따기)
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "Bottom" : "Top";
            YCyl($"Neck{side}", v, c + new Vector3(0f, s * 0.042f, 0f), 0.046f, 0.012f, steel);
            YCyl($"Face{side}", v, c + new Vector3(0f, s * 0.054f, 0f), 0.054f, 0.014f, steel);
            YCyl($"FaceBevel{side}", v, c + new Vector3(0f, s * 0.0625f, 0f), 0.046f, 0.004f, steelFace);
        }

        PlaceHitPoint(root, HammerHitPos);
    }

    // ───────────────────────── 드럼 스틱 ─────────────────────────
    // 끝으로 갈수록 가늘어지는 나무 스틱 + 도토리형 팁, 손잡이 쪽엔 그립 테이프
    public static void BuildDrumStick(Transform root)
    {
        ClearVisuals(root);

        Material wood = Mat("DrumWood", new Color(0.90f, 0.76f, 0.55f), 0f, 0.55f);
        Material tape = Mat("DrumGripTape", new Color(0.10f, 0.10f, 0.11f), 0f, 0.25f);
        Material tip = Mat("DrumTip", new Color(0.96f, 0.95f, 0.92f), 0f, 0.75f);

        GameObject vis = Group(root, "Visual");
        Transform v = vis.transform;

        // 실제 드럼 스틱(약 40 cm, 지름 1.5 cm)을 손 위치 기준 1/4 지점을 잡은 모양으로 배치
        Prim(PrimitiveType.Sphere, "Butt", v, new Vector3(0f, 0f, -0.075f), new Vector3(0.017f, 0.017f, 0.012f), wood);
        Cyl("Shaft", v, -0.075f, 0.19f, 0.017f, wood);
        Cyl("GripTape", v, -0.045f, 0.055f, 0.0185f, tape);
        Cyl("Taper1", v, 0.19f, 0.25f, 0.0145f, wood);
        Cyl("Taper2", v, 0.25f, 0.295f, 0.0115f, wood);
        Cyl("Taper3", v, 0.295f, 0.325f, 0.0085f, wood);
        Cyl("Neck", v, 0.325f, 0.335f, 0.0065f, wood);
        // 도토리형 나일론 팁
        Prim(PrimitiveType.Sphere, "Tip", v, DrumTipPos, new Vector3(0.016f, 0.016f, 0.024f), tip);

        PlaceHitPoint(root, DrumTipPos);
    }

    // ───────────────────────── 공통 도우미 ─────────────────────────

    // HitPoint(판정)는 남기고 나머지 겉모양 자식은 모두 지움
    static void ClearVisuals(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform ch = root.GetChild(i);
            if (ch.name == "HitPoint") continue;
            Undo.DestroyObjectImmediate(ch.gameObject);
        }
    }

    static void PlaceHitPoint(Transform root, Vector3 localPos)
    {
        Transform hp = root.Find("HitPoint");
        if (hp == null) return;
        Undo.RecordObject(hp, "Move HitPoint");
        hp.localPosition = localPos;
        hp.SetAsLastSibling();
    }

    static GameObject Group(Transform parent, string name)
    {
        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, "Build tool model");
        g.transform.SetParent(parent, false);
        return g;
    }

    // Z축 방향 원기둥 (z0 ~ z1, 지름 d)
    static GameObject Cyl(string name, Transform parent, float z0, float z1, float d, Material m)
    {
        GameObject g = Prim(PrimitiveType.Cylinder, name, parent,
            new Vector3(0f, 0f, (z0 + z1) * 0.5f), new Vector3(d, (z1 - z0) * 0.5f, d), m);
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return g;
    }

    // Y축 방향 원기둥 (중심 c, 지름 d, 높이 h)
    static GameObject YCyl(string name, Transform parent, Vector3 c, float d, float h, Material m)
    {
        return Prim(PrimitiveType.Cylinder, name, parent, c, new Vector3(d, h * 0.5f, d), m);
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(g, "Build tool model");
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = localScale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off; // 손에 든 작은 도구라 그림자는 끔 (성능)
        Collider col = g.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col); // 판정은 HitPoint만 사용
        return g;
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/WhackAMole")) AssetDatabase.CreateFolder("Assets", "WhackAMole");
            AssetDatabase.CreateFolder("Assets/WhackAMole", "Materials");
        }
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            Shader s = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            m = new Material(s);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }
}

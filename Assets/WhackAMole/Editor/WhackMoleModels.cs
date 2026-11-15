// 두더지 모델(배경 두더지 · 탁자 두더지 · 사격 표적)을 한 가지 디자인으로 만들어 주는 에디터 스크립트
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Restyle Game Moles
//  - 탁자 두더지 9마리: 배경에서 지켜보는 두더지와 같은 상반신 모양
//  - 사격 표적 9마리: 벽 구멍 속에서 얼굴만 빼꼼 내미는 머리 모양 (앞발로 구멍 가장자리를 잡음)
//  - 판정용 콜라이더와 Mole 스크립트 설정은 유지·조정되므로 게임 로직은 그대로 동작합니다.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackMoleModels
{
    const string MatDir = "Assets/WhackAMole/Materials/Env";
    public static readonly Color FurColor = new Color(0.36f, 0.25f, 0.18f);

    static Material fur, skin, nose, black, white;
    static Material badSkin, badDark, badChest, badEye, scar;

    [MenuItem("Tools/Whack-A-Mole/Restyle Game Moles")]
    public static void RestyleMenu()
    {
        WhackGameManager gm = Object.FindFirstObjectByType<WhackGameManager>();
        if (gm == null)
        {
            EditorUtility.DisplayDialog("두더지 게임", "GameManager를 찾지 못했습니다.", "확인");
            return;
        }
        RestyleGameMoles(gm);
    }

    public static void RestyleGameMoles(WhackGameManager gm)
    {
        if (gm.tableMoles != null)
            foreach (Mole m in gm.tableMoles) if (m != null) RestyleTableMole(m);
        if (gm.gunTargets != null)
            foreach (Mole m in gm.gunTargets) if (m != null) RestyleTarget(m);
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
    }

    // 탁자 두더지: 구멍(지름 12 cm)에서 상반신이 올라옴. 몸통 폭 10 cm
    static void RestyleTableMole(Mole m)
    {
        Transform body = PrepareBody(m);
        CapsuleCollider col = body.gameObject.AddComponent<CapsuleCollider>();
        col.direction = 1; col.radius = 0.05f; col.height = 0.14f; col.center = Vector3.zero;

        // 상반신을 플레이어(-Z) 쪽으로 향하게. 귀여운 두더지(드럼 스틱용) + 초록 우락부락 두더지(쇠 망치용)
        Transform normal = BuildFigure(body, "Figure", new Vector3(0f, -0.056f, 0f), 0.2f, Quaternion.Euler(0f, 180f, 0f), false);
        Transform bad = BuildBadFigure(body, "BadFigure", new Vector3(0f, -0.056f, 0f), 0.2f, Quaternion.Euler(0f, 180f, 0f), false);
        bad.gameObject.SetActive(false);

        Undo.RecordObject(m, "Restyle mole");
        m.normalVisual = normal.gameObject;
        m.badVisual = bad.gameObject;
        m.downHeight = -0.1f;   // 탁자 윗면 아래로 완전히 숨음
        m.upHeight = 0.04f;     // 몸 아래쪽은 구멍 속, 앞발이 구멍 가장자리에 걸침
        m.normalColor = FurColor;
        EditorUtility.SetDirty(m);
    }

    // 사격 표적: 벽 속에서 구멍 쪽(로컬 Y = 플레이어 방향)으로 얼굴을 내밂
    static void RestyleTarget(Mole m)
    {
        Transform body = PrepareBody(m);
        SphereCollider col = body.gameObject.AddComponent<SphereCollider>();
        col.radius = 0.14f; col.center = Vector3.zero;

        // Target의 로컬 Y = 플레이어 쪽, 로컬 Z = 위쪽 → 얼굴(+Z)이 로컬 Y, 머리 위(+Y)가 로컬 Z를 향하도록
        BuildPeekHead(body, "Head", Vector3.zero, 0.56f, Quaternion.LookRotation(Vector3.up, Vector3.forward));

        Undo.RecordObject(m, "Restyle target");
        m.downHeight = -0.2f;  // 벽 속으로 완전히 숨음
        m.upHeight = 0.0f;     // 머리 절반(얼굴)이 구멍 밖으로 빼꼼
        m.speed = 1.2f;
        m.normalColor = FurColor;
        EditorUtility.SetDirty(m);
    }

    // Body를 기본 도형(캡슐) 대신 빈 오브젝트로 바꾸고 기존 겉모양·콜라이더 제거
    static Transform PrepareBody(Mole m)
    {
        Transform body = m.body != null ? m.body : m.transform.Find("Body");
        Undo.RecordObject(body, "Restyle mole body");
        body.localScale = Vector3.one;
        body.localRotation = Quaternion.identity;
        for (int i = body.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(body.GetChild(i).gameObject);
        foreach (Collider c in body.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
        MeshRenderer mr = body.GetComponent<MeshRenderer>(); if (mr) Undo.DestroyObjectImmediate(mr);
        MeshFilter mf = body.GetComponent<MeshFilter>(); if (mf) Undo.DestroyObjectImmediate(mf);
        return body;
    }

    // ───────────────────────── 디자인 ─────────────────────────

    // 상반신 두더지 (몸통 + 배 + 눈 + 코 + 앞니 + 앞발). 기준: 바닥 = y 0, 얼굴 = +Z
    public static Transform BuildFigure(Transform parent, string name, Vector3 pos, float scale, Quaternion rot, bool shadows = true)
    {
        LoadMaterials();
        Transform m = Group(parent, name, pos, rot, scale);
        Prim(PrimitiveType.Capsule, "Body", m, new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.32f, 0.45f), fur, shadows);
        Prim(PrimitiveType.Sphere, "Belly", m, new Vector3(0f, 0.22f, 0.15f), new Vector3(0.34f, 0.36f, 0.2f), skin, false);
        foreach (float x in new[] { -0.09f, 0.09f })
        {
            Prim(PrimitiveType.Sphere, "Eye", m, new Vector3(x, 0.44f, 0.19f), Vector3.one * 0.07f, black, false);
            Prim(PrimitiveType.Sphere, "EyeShine", m, new Vector3(x + 0.012f, 0.455f, 0.222f), Vector3.one * 0.022f, white, false);
            Prim(PrimitiveType.Sphere, "Paw", m, new Vector3(x * 2.1f, 0.12f, 0.2f), new Vector3(0.14f, 0.07f, 0.12f), skin, false);
        }
        Prim(PrimitiveType.Sphere, "Nose", m, new Vector3(0f, 0.37f, 0.25f), new Vector3(0.11f, 0.08f, 0.09f), nose, false);
        Prim(PrimitiveType.Cube, "Teeth", m, new Vector3(0f, 0.3f, 0.225f), new Vector3(0.05f, 0.04f, 0.01f), white, false);
        return m;
    }

    // 초록 우락부락 두더지 (쇠 망치용). 같은 크기 틀(바닥 = y 0, 얼굴 = +Z)에 넓은 어깨, 성난 눈썹, 빨간 눈, 송곳니, 흉터, 주먹, 머리 가시
    public static Transform BuildBadFigure(Transform parent, string name, Vector3 pos, float scale, Quaternion rot, bool shadows = true)
    {
        LoadMaterials();
        Transform m = Group(parent, name, pos, rot, scale);
        Prim(PrimitiveType.Capsule, "Body", m, new Vector3(0f, 0.25f, 0f), new Vector3(0.56f, 0.33f, 0.5f), badSkin, shadows);
        Prim(PrimitiveType.Sphere, "Chest", m, new Vector3(0f, 0.19f, 0.16f), new Vector3(0.38f, 0.32f, 0.22f), badChest, false);
        Prim(PrimitiveType.Cube, "Belt", m, new Vector3(0f, 0.08f, 0.19f), new Vector3(0.46f, 0.05f, 0.16f), badDark, false);
        // 머리 위 가시
        for (int i = -1; i <= 1; i++)
        {
            var spike = PrimT(PrimitiveType.Cube, "Spike", m, new Vector3(i * 0.1f, 0.57f - Mathf.Abs(i) * 0.03f, -0.02f), Vector3.one * 0.07f, badDark, false);
            spike.localRotation = Quaternion.Euler(45f, 0f, 45f + i * 12f);
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            // 떡 벌어진 어깨와 큰 주먹
            Prim(PrimitiveType.Sphere, "Shoulder", m, new Vector3(sx * 0.21f, 0.33f, 0f), new Vector3(0.22f, 0.2f, 0.22f), badSkin, false);
            Prim(PrimitiveType.Sphere, "Fist", m, new Vector3(sx * 0.2f, 0.12f, 0.2f), new Vector3(0.17f, 0.13f, 0.15f), badDark, false);
            // 빨간 눈 (흰자 + 빨간 눈동자) - 귀여운 두더지보다 작고 가늘게
            Prim(PrimitiveType.Sphere, "Eye", m, new Vector3(sx * 0.085f, 0.43f, 0.195f), new Vector3(0.075f, 0.055f, 0.05f), white, false);
            Prim(PrimitiveType.Sphere, "Pupil", m, new Vector3(sx * 0.08f, 0.428f, 0.222f), Vector3.one * 0.032f, badEye, false);
            // 성난 V자 눈썹 (안쪽이 낮음)
            var brow = PrimT(PrimitiveType.Cube, "Brow", m, new Vector3(sx * 0.085f, 0.475f, 0.215f), new Vector3(0.13f, 0.035f, 0.04f), badDark, false);
            brow.localRotation = Quaternion.Euler(0f, 0f, sx * 22f);
            // 아래로 삐죽 나온 송곳니
            var fang = PrimT(PrimitiveType.Cube, "Fang", m, new Vector3(sx * 0.045f, 0.275f, 0.24f), new Vector3(0.022f, 0.04f, 0.012f), white, false);
            fang.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        Prim(PrimitiveType.Sphere, "Nose", m, new Vector3(0f, 0.37f, 0.255f), new Vector3(0.13f, 0.09f, 0.1f), badDark, false);
        Prim(PrimitiveType.Cube, "Mouth", m, new Vector3(0f, 0.3f, 0.238f), new Vector3(0.15f, 0.022f, 0.01f), black, false);
        // 얼굴 흉터 (사선 + 꿰맨 자국)
        var s1 = PrimT(PrimitiveType.Cube, "Scar", m, new Vector3(-0.14f, 0.42f, 0.2f), new Vector3(0.014f, 0.11f, 0.01f), scar, false);
        s1.localRotation = Quaternion.Euler(0f, -30f, 28f);
        for (int i = -1; i <= 1; i++)
        {
            var st = PrimT(PrimitiveType.Cube, "Stitch", m, new Vector3(-0.14f + i * 0.022f, 0.42f - i * 0.04f, 0.202f), new Vector3(0.04f, 0.008f, 0.008f), scar, false);
            st.localRotation = Quaternion.Euler(0f, -30f, 28f);
        }
        return m;
    }

    // 구멍에서 빼꼼 내미는 머리 (같은 디자인의 얼굴 + 귀 + 구멍 가장자리를 잡은 앞발). 기준: 머리 중심 = 원점, 얼굴 = +Z
    public static Transform BuildPeekHead(Transform parent, string name, Vector3 pos, float scale, Quaternion rot)
    {
        LoadMaterials();
        Transform h = Group(parent, name, pos, rot, scale);
        Prim(PrimitiveType.Sphere, "Skull", h, Vector3.zero, new Vector3(0.5f, 0.46f, 0.48f), fur, false);
        Prim(PrimitiveType.Sphere, "Muzzle", h, new Vector3(0f, -0.07f, 0.17f), new Vector3(0.28f, 0.2f, 0.18f), skin, false);
        foreach (float x in new[] { -0.09f, 0.09f })
        {
            Prim(PrimitiveType.Sphere, "Ear", h, new Vector3(x * 2.2f, 0.17f, -0.02f), new Vector3(0.12f, 0.12f, 0.06f), fur, false);
            Prim(PrimitiveType.Sphere, "Eye", h, new Vector3(x, 0.06f, 0.2f), Vector3.one * 0.07f, black, false);
            Prim(PrimitiveType.Sphere, "EyeShine", h, new Vector3(x + 0.012f, 0.075f, 0.232f), Vector3.one * 0.022f, white, false);
            // 앞발: 구멍 아래쪽 가장자리를 잡은 모양
            Prim(PrimitiveType.Sphere, "Paw", h, new Vector3(x * 1.6f, -0.23f, 0.05f), new Vector3(0.13f, 0.07f, 0.11f), skin, false);
        }
        Prim(PrimitiveType.Sphere, "Nose", h, new Vector3(0f, -0.02f, 0.27f), new Vector3(0.11f, 0.08f, 0.09f), nose, false);
        Prim(PrimitiveType.Cube, "Teeth", h, new Vector3(0f, -0.11f, 0.25f), new Vector3(0.05f, 0.04f, 0.01f), white, false);
        return h;
    }

    // ───────────────────────── 도우미 ─────────────────────────
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

    static void Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows)
        => PrimT(type, name, parent, pos, scale, mat, shadows);

    static Transform PrimT(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        Collider c = g.GetComponent<Collider>();
        if (c != null) Object.DestroyImmediate(c);
        return g.transform;
    }

    static void LoadMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/WhackAMole/Materials")) AssetDatabase.CreateFolder("Assets/WhackAMole", "Materials");
            AssetDatabase.CreateFolder("Assets/WhackAMole/Materials", "Env");
        }
        fur = Mat("MoleFur", FurColor, 0.2f);
        skin = Mat("MoleSkin", new Color(0.93f, 0.68f, 0.62f), 0.3f);
        nose = Mat("PetalPink", new Color(0.97f, 0.55f, 0.72f), 0.3f);
        black = Mat("Black", new Color(0.05f, 0.05f, 0.06f), 0.8f);
        white = Mat("White", new Color(0.96f, 0.95f, 0.92f), 0.4f);
        // 초록 우락부락 두더지 (MoleBad* 이름은 Mole.cs가 맞았을 때 빨갛게 바꿀 부위로 인식)
        badSkin = Mat("MoleBadSkin", new Color(0.33f, 0.55f, 0.20f), 0.25f);
        badDark = Mat("MoleBadDark", new Color(0.14f, 0.26f, 0.10f), 0.2f);
        badChest = Mat("BadChest", new Color(0.62f, 0.68f, 0.38f), 0.2f);
        scar = Mat("BadScar", new Color(0.78f, 0.45f, 0.45f), 0.3f);
        badEye = Mat("BadEye", new Color(0.95f, 0.1f, 0.05f), 0.8f);
        if (badEye.HasProperty("_EmissionColor") && !badEye.IsKeywordEnabled("_EMISSION"))
        {
            badEye.EnableKeyword("_EMISSION");
            badEye.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f) * 1.5f);
            EditorUtility.SetDirty(badEye);
        }
    }

    static Material Mat(string name, Color color, float smoothness)
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            Shader s = rp != null && rp.defaultMaterial != null ? rp.defaultMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }
}

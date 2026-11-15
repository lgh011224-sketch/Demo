// 게임 시작 연출용 글자(3 2 1 START! 카운트다운, 게임 중 시간·점수 표시)를 장면에 만들고 GameManager에 연결
// 사용법: 상단 메뉴 Tools > Whack-A-Mole > Setup Start Countdown  (다시 누르면 지우고 새로 만듦)
// 폰트: Assets/WhackAMole/Fonts/LuckiestGuy-Regular.ttf (Google Fonts, Apache License 2.0)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WhackStartFx
{
    const string FontPath = "Assets/WhackAMole/Fonts/LuckiestGuy-Regular.ttf";

    [MenuItem("Tools/Whack-A-Mole/Setup Start Countdown")]
    public static void Setup()
    {
        WhackGameManager gm = Object.FindFirstObjectByType<WhackGameManager>();
        if (gm == null)
        {
            EditorUtility.DisplayDialog("두더지 게임", "GameManager를 찾지 못했습니다. 먼저 Build Scene으로 장면을 만드세요.", "확인");
            return;
        }
        Transform root = gm.transform.parent; // WhackAMole
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        foreach (string n in new[] { "Countdown", "PlayHud" })
        {
            Transform old = root.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        // 3 2 1 START! : 점수판 자리(눈높이, 탁자 너머)에 크게
        TextMesh countdown = MakeText(root, "Countdown", font, new Vector3(0f, 1.6f, 1.12f), Quaternion.identity,
            128, 0.022f, new Color(1f, 0.92f, 0.2f), "3");
        // 카툰 느낌의 그림자 (오른쪽 아래로 살짝 밀린 진한 글자)
        TextMesh shadow = MakeText(countdown.transform, "Shadow", font, new Vector3(0.014f, -0.014f, 0.005f), Quaternion.identity,
            128, 0.022f, new Color(0.25f, 0.08f, 0.05f, 0.85f), "3");
        shadow.transform.localScale = Vector3.one;
        countdown.gameObject.SetActive(false);

        // 게임 중 시간·점수: 탁자 뒤쪽, 플레이어를 향해 살짝 기울임 (점수판이 숨겨진 동안 사용)
        TextMesh hud = MakeText(root, "PlayHud", font, new Vector3(0f, 0.97f, 0.9f), Quaternion.Euler(25f, 0f, 0f),
            64, 0.0055f, new Color(1f, 0.9f, 0.3f), "TIME 60    HIT 0    MISS 0");
        hud.gameObject.SetActive(false);

        SetupBoardLegend(gm, font);

        Undo.RecordObject(gm, "Wire start countdown");
        gm.statusBoard = gm.statusText != null ? gm.statusText.transform.parent : root.Find("StatusBoard");
        gm.countdownText = countdown;
        gm.playHudText = hud;
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
    }

    // 시작 화면 점수판: 크게 키우고 양옆에 두더지 모형 + "어떤 도구로 잡는지" 설명을 붙임
    //   왼쪽: 귀여운 두더지 = DRUM STICK,  오른쪽: 초록 우락부락 두더지 = HAMMER
    //   가운데 글자(statusText)에는 GameManager가 조작 안내(A / 왼손 트리거)를 씀
    public static void SetupBoardLegend(WhackGameManager gm, Font font)
    {
        if (gm.statusText == null) return;
        Transform board = gm.statusText.transform.parent;
        Transform backdrop = board.Find("Backdrop");
        if (backdrop != null)
        {
            Undo.RecordObject(backdrop, "Resize board");
            // 부스 기둥(x ±0.62) 안쪽에 들어오는 폭
            backdrop.localPosition = new Vector3(0f, -0.02f, 0.01f);
            backdrop.localScale = new Vector3(1.16f, 0.6f, 1f);
            Renderer br = backdrop.GetComponent<Renderer>();
            Undo.RecordObject(br, "Board material");
            br.sharedMaterial = BoardMaterial(false);
        }
        Undo.RecordObject(gm, "Board fade material");
        gm.boardFadeMaterial = BoardMaterial(true);
        Undo.RecordObject(gm.statusText.transform, "Move status text");
        gm.statusText.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        gm.statusText.characterSize = 0.0042f;

        Transform old = board.Find("Legend");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        GameObject legend = new GameObject("Legend");
        Undo.RegisterCreatedObjectUndo(legend, "Create legend");
        legend.transform.SetParent(board, false);

        // 두더지 모형 (플레이어 쪽 -Z를 보도록 180도)
        Quaternion face = Quaternion.Euler(0f, 180f, 0f);
        WhackMoleModels.BuildFigure(legend.transform, "CuteMole", new Vector3(-0.44f, -0.31f, -0.04f), 0.3f, face, false);
        WhackMoleModels.BuildBadFigure(legend.transform, "BadMole", new Vector3(0.44f, -0.31f, -0.04f), 0.3f, face, false);

        MakeText(legend.transform, "CuteCaption", font, new Vector3(-0.44f, 0.16f, -0.01f), Quaternion.identity,
            64, 0.0045f, new Color(1f, 0.8f, 0.6f), "CUTE MOLE\n= DRUM STICK");
        MakeText(legend.transform, "BadCaption", font, new Vector3(0.44f, 0.16f, -0.01f), Quaternion.identity,
            64, 0.0045f, new Color(0.6f, 0.95f, 0.4f), "GREEN BRUTE\n= HAMMER");

        // 이번 판 결과: 점수판 아래쪽 가운데, 노란 사각 테두리 안에 노란 글자로 강조 (두더지 모형 사이)
        var box = new GameObject("ResultBox");
        Undo.RegisterCreatedObjectUndo(box, "Result box");
        box.transform.SetParent(legend.transform, false);
        box.transform.localPosition = new Vector3(0f, -0.185f, -0.012f);
        var yellow = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        const string yPath = "Assets/WhackAMole/Materials/ResultFrameYellow.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(yPath);
        if (existing != null) yellow = existing; else AssetDatabase.CreateAsset(yellow, yPath);
        yellow.SetColor("_BaseColor", new Color(1f, 0.8f, 0.15f));
        EditorUtility.SetDirty(yellow);
        float bw = 0.6f, bh = 0.085f, th = 0.006f;
        void Bar(string n, Vector3 p, Vector2 s)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = n;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(box.transform, false);
            q.transform.localPosition = p;
            q.transform.localScale = new Vector3(s.x, s.y, 1f);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = yellow; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Bar("Top", new Vector3(0f, bh * 0.5f, 0f), new Vector2(bw + th, th));
        Bar("Bottom", new Vector3(0f, -bh * 0.5f, 0f), new Vector2(bw + th, th));
        Bar("Left", new Vector3(-bw * 0.5f, 0f, 0f), new Vector2(th, bh + th));
        Bar("Right", new Vector3(bw * 0.5f, 0f, 0f), new Vector2(th, bh + th));
        MakeText(box.transform, "Label", font, new Vector3(0f, bh * 0.5f + 0.018f, 0f), Quaternion.identity,
            64, 0.0028f, new Color(1f, 0.8f, 0.15f), "THIS ROUND");
        var rt = MakeText(box.transform, "ResultText", font, new Vector3(0f, 0f, 0f), Quaternion.identity,
            64, 0.0034f, new Color(1f, 0.88f, 0.3f), "HIT 0    MISS 0    WRONG TOOL 0    (0%)");
        box.SetActive(false);
        Undo.RecordObject(gm, "Wire result box");
        gm.resultBox = box;
        gm.resultText = rt;
        EditorUtility.SetDirty(board.gameObject);
    }

    // 점수판 배경 머티리얼 (URP Unlit)
    //  - 평소: 불투명 (BoardBackdrop.mat) → 글자·두더지 모형이 흐려지거나 뒤가 비치지 않음
    //  - 페이드 중에만: 반투명 (BoardBackdropFade.mat)을 GameManager가 잠깐 바꿔 끼움
    static Material BoardMaterial(bool transparent)
    {
        string path = transparent ? "Assets/WhackAMole/Materials/BoardBackdropFade.mat" : "Assets/WhackAMole/Materials/BoardBackdrop.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, path);
        }
        var BM = UnityEngine.Rendering.BlendMode.One;
        m.SetFloat("_Surface", transparent ? 1f : 0f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
        m.SetFloat("_SrcBlend", (float)(transparent ? UnityEngine.Rendering.BlendMode.SrcAlpha : BM));
        m.SetFloat("_DstBlend", (float)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
        m.SetFloat("_SrcBlendAlpha", (float)BM);
        m.SetFloat("_DstBlendAlpha", (float)(transparent ? UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : UnityEngine.Rendering.BlendMode.Zero));
        m.SetFloat("_ZWrite", transparent ? 0f : 1f);
        if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)(transparent ? UnityEngine.Rendering.RenderQueue.Transparent : UnityEngine.Rendering.RenderQueue.Geometry);
        m.SetColor("_BaseColor", new Color(0.07f, 0.05f, 0.04f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static TextMesh MakeText(Transform parent, string name, Font font, Vector3 localPos, Quaternion localRot, int size, float charSize, Color color, string text)
    {
        GameObject g = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(g, "Create " + name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localRotation = localRot;
        TextMesh tm = g.AddComponent<TextMesh>();
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        }
        if (font != null)
        {
            tm.font = font;
            g.GetComponent<MeshRenderer>().sharedMaterial = TextMat.For(font); // 깊이 검사하는 글자 머티리얼
        }
        tm.fontSize = size;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.text = text;
        return tm;
    }
}

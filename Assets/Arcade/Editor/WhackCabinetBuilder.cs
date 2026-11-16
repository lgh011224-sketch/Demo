// 반실사 두더지 잡기 오락기 (줄무늬 기둥 부스 + 탁자를 대신함)
//   아래 캐비닛(허리 높이): 인쇄 아트 상판, 구멍마다 고무 링, 검은 비닐 쿠션 테두리, 앞면 아트 + 동전 투입구 문 + 발판
//   뒤 패널: 점수판(StatusBoard)을 크롬 테두리로 감싸고, 스피커 그릴, 모서리 LED 줄
//   위 간판: "두더지 잡기" 라이트박스 + 도는 전구
// 게임 위치(구멍 줄 z 0.55, 높이 0.75)와 점수판 위치는 그대로입니다.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class WhackCabinetBuilder
{
    const float DeckY = 0.75f;          // 구멍 높이 (두더지가 올라오는 면)
    const float FrontZ = 0.36f, BackZ = 1.14f, HalfW = 0.68f;   // 폭 = 위 전광판 블럭(1.36 m)과 같게

    [MenuItem("Tools/Arcade/Rebuild Whack-A-Mole Cabinet")]
    static void Menu() => Debug.Log("[Arcade] " + Build());

    public static string Build()
    {
        var gm = Object.FindAnyObjectByType<WhackGameManager>(FindObjectsInactive.Include);
        if (gm == null) return "WhackGameManager 없음";
        Transform root = gm.transform.parent;
        var old = root.Find("WhackCabinet");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var booth = GameObject.Find("WhackBooth");
        if (booth != null) Object.DestroyImmediate(booth);                 // 예전 줄무늬 기둥 부스
        var table = root.Find("Table");
        if (table != null && table.GetComponent<Renderer>()) table.GetComponent<Renderer>().enabled = false;   // 판정용으로만 남김
        foreach (Transform c in root)
            if (c.name.StartsWith("Hole_") && c.GetComponent<Renderer>()) c.GetComponent<Renderer>().enabled = false;   // 흙구멍 대신 고무 링

        var cab = ArcadeBuilder.Group(root, "WhackCabinet", Vector3.zero);
        var body = ArcadeBuilder.Mat("WhackBody", new Color(0.72f, 0.05f, 0.1f), 0.85f, 0f);
        var chrome = ArcadeBuilder.Mat("Chrome", new Color(0.85f, 0.86f, 0.9f), 0.9f, 1f);
        var vinyl = ArcadeBuilder.Mat("BlackVinyl", new Color(0.05f, 0.05f, 0.055f), 0.5f);
        var rubber = ArcadeBuilder.Mat("Rubber", new Color(0.04f, 0.04f, 0.045f), 0.28f);
        var holeDark = ArcadeBuilder.Mat("HoleDark", new Color(0.005f, 0.005f, 0.007f), 0.1f);
        var dark = ArcadeBuilder.Mat("CabinetDark", new Color(0.06f, 0.06f, 0.08f), 0.5f);

        // ── 아래 캐비닛 ──
        float depth = BackZ - FrontZ, cz = (FrontZ + BackZ) * 0.5f;
        ArcadeBuilder.RBox(cab, "Lower", new Vector3(0f, (DeckY - 0.02f) * 0.5f, cz), new Vector3(HalfW * 2f, DeckY - 0.02f, depth), body, true, 0.03f);
        foreach (float sx in new[] { -1f, 1f })
            ArcadeBuilder.Box(cab, "SideArt", new Vector3(sx * (HalfW + 0.002f), 0.38f, cz), new Vector3(0.004f, 0.56f, depth - 0.1f), ArcadeDetails.ArtMat(10, new Color(1f, 0.55f, 0.1f), new Color(0.6f, 0.1f, 0.55f)), false);
        // 상판 (인쇄 아트) + 쿠션 테두리
        var deck = ArcadeDetails.ArtMat(11, new Color(0.15f, 0.65f, 0.35f), new Color(0.05f, 0.3f, 0.45f));
        ArcadeBuilder.RBox(cab, "Deck", new Vector3(0f, DeckY - 0.012f, cz), new Vector3(HalfW * 2f - 0.02f, 0.024f, depth - 0.02f), deck, true, 0.008f);
        ArcadeBuilder.RBox(cab, "BumperFront", new Vector3(0f, DeckY + 0.01f, FrontZ + 0.03f), new Vector3(HalfW * 2f + 0.04f, 0.075f, 0.075f), vinyl, false, 0.034f);
        foreach (float sx in new[] { -1f, 1f })
            ArcadeBuilder.RBox(cab, "BumperSide", new Vector3(sx * (HalfW - 0.01f), DeckY + 0.01f, cz), new Vector3(0.07f, 0.07f, depth), vinyl, false, 0.032f);
        // 구멍: 검은 구멍 + 고무 링 (두더지 위치에 맞춤)
        var ring = ArcadeDetails.Torus(0.072f, 0.013f);
        foreach (Mole m in gm.tableMoles)
        {
            if (m == null) continue;
            Vector3 p = m.transform.position;
            var hole = ArcadeBuilder.Cyl(cab, "HoleDark", new Vector3(p.x, DeckY + 0.001f, p.z), new Vector3(0.13f, 0.001f, 0.13f), holeDark);
            ArcadeDetails.MeshObj(cab, "RubberRing", ring, rubber, new Vector3(p.x, DeckY + 0.006f, p.z));
        }
        // 앞면 (플레이어 쪽 = -Z): 아트 판 + 동전 문 + 발판. 앞을 +Z로 쓰도록 180° 돌린 그룹
        var front = ArcadeBuilder.Group(cab, "Front", new Vector3(0f, 0f, FrontZ - 0.002f));
        front.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ArcadeBuilder.Box(front, "FrontArt", new Vector3(0.12f, 0.46f, 0f), new Vector3(0.82f, 0.42f, 0.004f), ArcadeDetails.ArtMat(12, new Color(1f, 0.85f, 0.2f), new Color(0.9f, 0.2f, 0.35f)), false);
        ArcadeBuilder.Text(front, "WHACK-A-MOLE", new Vector3(0.12f, 0.56f, 0.004f), 0.0085f, new Color(0.25f, 0.05f, 0.1f), true);
        ArcadeBuilder.Text(front, "HIT THE MOLES - HIGH SCORE WINS", new Vector3(0.12f, 0.43f, 0.004f), 0.0042f, Color.white, true);
        ArcadeDetails.CoinDoor(front, new Vector3(-0.46f, 0.4f, 0.006f), 0.2f, 0.3f);
        ArcadeBuilder.Box(front, "KickPlate", new Vector3(0f, 0.05f, 0.006f), new Vector3(HalfW * 2f, 0.09f, 0.012f), ArcadeBuilder.Mat("KickMetal", new Color(0.3f, 0.31f, 0.34f), 0.7f, 0.9f, null, ArcadeBuilder.PH("metal_plate_02", "diff")), false);
        ArcadeDetails.CornerTrims(cab, new Vector3(0f, (DeckY - 0.04f) * 0.5f, cz), new Vector3(HalfW * 2f, DeckY - 0.04f, depth), chrome);

        // ── 뒤 패널 (점수판 주위) ──
        var board = gm.statusBoard;
        float boardZ = board != null ? board.position.z : 1.1f, boardY = board != null ? board.position.y : 1.6f;
        float upZ = boardZ + 0.12f;
        ArcadeBuilder.RBox(cab, "Upright", new Vector3(0f, (DeckY + 2.16f) * 0.5f, upZ), new Vector3(1.36f, 2.16f - DeckY, 0.2f), body, true, 0.035f);
        ArcadeBuilder.Box(cab, "UprightFace", new Vector3(0f, (DeckY + 2.16f) * 0.5f, upZ - 0.101f), new Vector3(1.28f, 2.16f - DeckY - 0.06f, 0.004f), dark, false);
        // 점수판 크롬 테두리
        float bw = 1.22f, bh = 0.68f, fz = boardZ + 0.004f;
        ArcadeBuilder.RBox(cab, "BezelT", new Vector3(0f, boardY + bh * 0.5f - 0.03f, fz), new Vector3(bw + 0.04f, 0.03f, 0.03f), chrome, false, 0.012f);
        ArcadeBuilder.RBox(cab, "BezelB", new Vector3(0f, boardY - bh * 0.5f - 0.03f, fz), new Vector3(bw + 0.04f, 0.03f, 0.03f), chrome, false, 0.012f);
        foreach (float sx in new[] { -1f, 1f })
            ArcadeBuilder.RBox(cab, "BezelS", new Vector3(sx * bw * 0.5f, boardY - 0.03f, fz), new Vector3(0.03f, bh + 0.03f, 0.03f), chrome, false, 0.012f);
        // 스피커 그릴
        foreach (float sx in new[] { -1f, 1f })
        {
            ArcadeBuilder.RBox(cab, "SpeakerRim", new Vector3(sx * 0.42f, 1.02f, upZ - 0.105f), new Vector3(0.26f, 0.2f, 0.012f), chrome, false, 0.02f);
            ArcadeBuilder.Box(cab, "Speaker", new Vector3(sx * 0.42f, 1.02f, upZ - 0.112f), new Vector3(0.23f, 0.17f, 0.004f), ArcadeDetails.GrilleMat(), false);
        }
        // 모서리 LED 줄
        // 고전 오락기처럼 옆면 테두리에 노란 T-몰딩
        var tmold = ArcadeBuilder.Mat("TMolding", new Color(1f, 0.78f, 0.1f), 0.8f);
        foreach (float sx in new[] { -1f, 1f })
        {
            ArcadeBuilder.RBox(cab, "TMoldFront", new Vector3(sx * 0.685f, (DeckY + 2.16f) * 0.5f, upZ - 0.1f), new Vector3(0.018f, 2.16f - DeckY, 0.022f), tmold, false, 0.008f);
            ArcadeBuilder.RBox(cab, "TMoldBack", new Vector3(sx * 0.685f, (DeckY + 2.16f) * 0.5f, upZ + 0.1f), new Vector3(0.018f, 2.16f - DeckY, 0.022f), tmold, false, 0.008f);
            ArcadeBuilder.RBox(cab, "TMoldLow", new Vector3(sx * (HalfW + 0.01f), DeckY * 0.5f, FrontZ + 0.01f), new Vector3(0.022f, DeckY, 0.022f), tmold, false, 0.008f);
        }
        // 뒤 패널 옆면 아트 (보라·분홍 고전 사이드 아트)
        foreach (float sx in new[] { -1f, 1f })
            ArcadeBuilder.Box(cab, "UprightArt", new Vector3(sx * 0.682f, (DeckY + 2.16f) * 0.5f, upZ), new Vector3(0.004f, 2.16f - DeckY - 0.12f, 0.16f), ArcadeDetails.ArtMat(13, new Color(0.55f, 0.15f, 0.85f), new Color(1f, 0.3f, 0.6f)), false);

        // ── 위 간판 ──
        float hy = 2.36f;
        ArcadeBuilder.RBox(cab, "Header", new Vector3(0f, hy, upZ - 0.04f), new Vector3(1.46f, 0.4f, 0.32f), body, false, 0.04f);
        // 80년대 미국 오락기 마키: 뒤에서 빛나는 아크릴 간판 + 검은 테두리
        var marquee = GraffitiBaker.RetroMarquee("Whack", "WHACK-A-MOLE", "★  CLASSIC ARCADE  ★", "INSERT COIN  -  1 PLAYER");
        var mface = GameObject.CreatePrimitive(PrimitiveType.Quad);
        mface.name = "Marquee"; Object.DestroyImmediate(mface.GetComponent<Collider>());
        mface.transform.SetParent(cab, false);
        mface.transform.localPosition = new Vector3(0f, hy, upZ - 0.203f);
        mface.transform.localScale = new Vector3(1.34f, 1.34f * 350f / 1600f, 1f);
        mface.GetComponent<Renderer>().sharedMaterial = marquee;
        var frame = ArcadeBuilder.Mat("MarqueeFrame", new Color(0.02f, 0.02f, 0.025f), 0.6f, 0.3f);
        float mh = 1.34f * 350f / 1600f;
        ArcadeBuilder.Box(cab, "MFrameT", new Vector3(0f, hy + mh * 0.5f + 0.012f, upZ - 0.205f), new Vector3(1.4f, 0.024f, 0.02f), frame, false);
        ArcadeBuilder.Box(cab, "MFrameB", new Vector3(0f, hy - mh * 0.5f - 0.012f, upZ - 0.205f), new Vector3(1.4f, 0.024f, 0.02f), frame, false);
        // 마키 불빛이 상판을 비추는 느낌의 은은한 조명
        var ml = new GameObject("MarqueeGlow"); ml.transform.SetParent(cab, false); ml.transform.position = new Vector3(0f, hy, upZ - 0.45f);
        var mll = ml.AddComponent<Light>(); mll.type = LightType.Point; mll.range = 1.6f; mll.intensity = 1.2f; mll.color = new Color(1f, 0.6f, 0.75f); mll.shadows = LightShadows.None;
        // 간판 조명 (상판을 밝힘)
        var lg = new GameObject("DeckLight");
        lg.transform.SetParent(cab, false);
        lg.transform.position = new Vector3(0f, 2.05f, 0.75f);
        lg.transform.rotation = Quaternion.Euler(80f, 180f, 0f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Spot; l.spotAngle = 85f; l.range = 3f; l.intensity = 2.5f; l.color = new Color(0.92f, 0.85f, 1f); l.shadows = LightShadows.Soft;

        Displays(gm, cab, upZ);
        foreach (var r in cab.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = r.name == "Bulb" ? ShadowCastingMode.Off : r.shadowCastingMode;
        ArcadeDetails.SetStatic(cab);
        EditorUtility.SetDirty(gm);
        return $"두더지 오락기: 렌더러 {cab.GetComponentsInChildren<Renderer>().Length}개";
    }

    // ── 화면 두 개 ──
    //  위 전광판: "WHACK-A-MOLE" 타이틀 그림 + 두더지 두 마리 (설명 글 없음)
    //  상판 LED 화면(구멍 뒤 초록 부분): 큰 점수 · 짧은 안내 · 3·2·1 → 고개를 숙이고 쳐도 보임
    static void Displays(WhackGameManager gm, Transform cab, float upZ)
    {
        var board = gm.statusBoard;
        var backdrop = board != null ? board.Find("Backdrop") : null;
        var legend = board != null ? board.Find("Legend") : null;
        if (legend != null) legend.gameObject.SetActive(false);              // 예전 설명·결과 상자
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/LuckiestGuy-Regular.ttf");
        var fontMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Text/Text3D_LuckiestGuy-Regular.mat");

        // 위 전광판 타이틀
        if (backdrop != null)
        {
            var title = GraffitiBaker.RetroMarquee("WhackTitle", "WHACK-A-MOLE", "★  ARCADE CLASSIC  ★", "INSERT COIN", 1200, 600);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "TitleScreen"; Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(cab, false);
            q.transform.position = backdrop.position + Vector3.back * 0.006f;
            q.transform.rotation = backdrop.rotation;
            q.transform.localScale = new Vector3(backdrop.lossyScale.x - 0.04f, backdrop.lossyScale.y - 0.04f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = title;
            Vector3 c = backdrop.position;
            WhackMoleModels.BuildFigure(cab, "TitleCuteMole", new Vector3(c.x - 0.45f, c.y - 0.3f, c.z - 0.05f), 0.32f, Quaternion.Euler(0f, 180f, 0f), false);
            WhackMoleModels.BuildBadFigure(cab, "TitleBadMole", new Vector3(c.x + 0.45f, c.y - 0.3f, c.z - 0.05f), 0.32f, Quaternion.Euler(0f, 180f, 0f), false);
        }

        // 상판 LED 화면
        float y = DeckY + 0.003f, cz = 0.885f, w = 1.18f, d = 0.4f;
        ArcadeBuilder.RBox(cab, "DeckScreen", new Vector3(0f, DeckY + 0.001f, cz), new Vector3(w, 0.006f, d), ArcadeBuilder.Mat("DeckScreenLCD", new Color(0.01f, 0.012f, 0.02f), 0.85f), false, 0.002f);
        var frame = ArcadeBuilder.Neon("DeckScreenFrame", new Color(0.3f, 1f, 0.55f), 4f);
        ArcadeBuilder.Tube(cab, "DSFrameF", new Vector3(-w * 0.5f, y, cz - d * 0.5f), new Vector3(w * 0.5f, y, cz - d * 0.5f), 0.006f, frame);
        ArcadeBuilder.Tube(cab, "DSFrameB", new Vector3(-w * 0.5f, y, cz + d * 0.5f), new Vector3(w * 0.5f, y, cz + d * 0.5f), 0.006f, frame);
        ArcadeBuilder.Tube(cab, "DSFrameL", new Vector3(-w * 0.5f, y, cz - d * 0.5f), new Vector3(-w * 0.5f, y, cz + d * 0.5f), 0.006f, frame);
        ArcadeBuilder.Tube(cab, "DSFrameR", new Vector3(w * 0.5f, y, cz - d * 0.5f), new Vector3(w * 0.5f, y, cz + d * 0.5f), 0.006f, frame);
        Quaternion flat = Quaternion.Euler(90f, 0f, 0f);   // 위를 보고 눕힌 글자 (플레이어 쪽에서 읽힘)
        void Place(TextMesh t, float z, float charSize)
        {
            if (t == null) return;
            t.transform.SetParent(gm.transform.parent, true);
            t.transform.position = new Vector3(0f, y + 0.002f, z);
            t.transform.rotation = flat;
            t.transform.localScale = Vector3.one;
            if (font != null) { t.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = fontMat; }
            t.fontSize = 64; t.characterSize = charSize; t.fontStyle = FontStyle.Normal;
            t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            foreach (var child in t.GetComponentsInChildren<TextMesh>(true))   // 그림자 글자도 같은 폰트
                if (child != t && font != null) { child.font = font; child.GetComponent<MeshRenderer>().sharedMaterial = fontMat; }
            EditorUtility.SetDirty(t); EditorUtility.SetDirty(t.transform);
        }
        Place(gm.playHudText, cz + 0.055f, 0.034f);     // 큰 점수
        Place(gm.statusText, cz - 0.125f, 0.011f);      // 짧은 안내 한 줄
        if (gm.countdownText != null)
        {
            float cs = gm.countdownText.characterSize; int fs = gm.countdownText.fontSize;
            Place(gm.countdownText, cz, cs);
            gm.countdownText.fontSize = fs;
        }
        gm.screenSize = new Vector2(w - 0.06f, d - 0.04f);
        if (gm.statusText != null) gm.statusText.gameObject.SetActive(true);
        EditorUtility.SetDirty(gm);
    }
}

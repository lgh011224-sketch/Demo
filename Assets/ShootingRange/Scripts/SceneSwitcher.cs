// 씬 전환 (두더지 잡기 ↔ 사격장)
//   왼손 Y 버튼        : 확인 창 열기 ("사격장으로 가시겠습니까?")
//   확인 창에서 Y 다시 : 예 → 다른 씬으로 이동
//   확인 창에서 X      : 아니오 → 창 닫기 (아무것도 안 누르면 autoCloseSeconds 뒤 자동으로 닫힘)
//   키보드: F9 = Y,  Esc = X
// 두 씬에 하나씩 두고 targetScene / targetLabel에 상대 씬을 적습니다. 두 씬 모두 빌드 목록에 있어야 합니다.
// 확인 창은 실행 중에 머리 앞에 만들어지므로 씬에 미리 배치할 UI는 없습니다.
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SceneSwitcher : MonoBehaviour
{
    public string targetScene = "ShootingRange";
    [Tooltip("확인 창에 표시할 이동할 곳 이름 (예: 사격장, 두더지 잡기)")]
    public string targetLabel = "SHOOTING GALLERY";
    [Tooltip("한글이 들어 있는 폰트 (Noto Sans KR)")]
    public Font font;
    public float autoCloseSeconds = 8f;
    public float distance = 0.85f;

    [Header("예전 버전 호환 (사용 안 함)")]
    public TextMesh progressText;

    GameObject dialog;
    float openedAt;
    bool prevY, prevX, loading;
    Vector3 dialogBaseScale;

    void Start()
    {
        if (progressText != null) progressText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (loading) return;
        bool y = RightHandFollower.GetLeftButton(UnityEngine.XR.CommonUsages.secondaryButton);
        bool x = RightHandFollower.GetLeftButton(UnityEngine.XR.CommonUsages.primaryButton);
        bool keyY = false, keyX = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) { keyY = Keyboard.current.f9Key.wasPressedThisFrame; keyX = Keyboard.current.escapeKey.wasPressedThisFrame; }
#endif
        bool pressY = (y && !prevY) || keyY, pressX = (x && !prevX) || keyX;
        prevY = y; prevX = x;

        if (dialog == null || !dialog.activeSelf)
        {
            if (pressY) Open();
            return;
        }
        if (pressY) { Confirm(); return; }
        if (pressX || Time.time - openedAt > autoCloseSeconds) { Close(); return; }

        // 나타날 때 살짝 커지는 연출
        float k = Mathf.Clamp01((Time.time - openedAt) / 0.15f);
        dialog.transform.localScale = dialogBaseScale * Mathf.Lerp(0.85f, 1f, 1f - (1f - k) * (1f - k));
    }

    void Open()
    {
        if (dialog == null) Build();
        // 지금 보고 있는 방향 앞, 눈높이에 (위아래 기울기는 무시)
        Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            dialog.transform.position = cam.transform.position + fwd * distance + Vector3.down * 0.05f;
            dialog.transform.rotation = Quaternion.LookRotation(fwd);
        }
        dialog.SetActive(true);
        openedAt = Time.time;
        RightHandFollower.Vibrate(0.2f, 0.03f);
    }

    void Close()
    {
        if (dialog != null) dialog.SetActive(false);
    }

    void Confirm()
    {
        loading = true;
        if (dialog != null)
        {
            var tms = dialog.GetComponentsInChildren<TextMesh>();
            if (tms.Length > 0) tms[0].text = $"Loading {targetLabel}...";
        }
        SceneManager.LoadScene(targetScene);
    }

    // ───────── 확인 창 만들기 (머리 앞 작은 판 + 글자) ─────────
    void Build()
    {
        dialog = new GameObject("SceneSwitchDialog");
        dialogBaseScale = Vector3.one;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        Material Mat(Color c) { var m = new Material(unlit); m.SetColor("_BaseColor", c); return m; }

        Quad("Border", new Vector3(0f, 0f, 0.002f), new Vector2(0.64f, 0.29f), Mat(new Color(0.95f, 0.75f, 0.15f)));
        Quad("Panel", new Vector3(0f, 0f, 0f), new Vector2(0.62f, 0.27f), Mat(new Color(0.05f, 0.06f, 0.08f)));

        Font f = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var textShader = Shader.Find("WhackAMole/Text3D");
        Material textMat = textShader != null ? new Material(textShader) { mainTexture = f.material.mainTexture } : f.material;

        Text("Question", $"Go to {targetLabel}?", new Vector3(0f, 0.045f, -0.003f), 0.0042f, Color.white, f, textMat);
        Text("Answers", "<color=#F2C12E>[Y]</color> Yes          <color=#9AA4B2>[X]</color> No", new Vector3(0f, -0.06f, -0.003f), 0.0032f, new Color(0.85f, 0.88f, 0.92f), f, textMat);
        dialog.SetActive(false);
    }

    void Quad(string name, Vector3 pos, Vector2 size, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(dialog.transform, false);
        q.transform.localPosition = pos;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Text(string name, string text, Vector3 pos, float size, Color color, Font f, Material mat)
    {
        var g = new GameObject(name);
        g.transform.SetParent(dialog.transform, false);
        g.transform.localPosition = pos;
        var tm = g.AddComponent<TextMesh>();
        tm.font = f;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        tm.richText = true;
        tm.fontSize = 64; tm.characterSize = size; tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.text = text;
    }

    // 한글 마지막 글자에 받침이 있으면 "으로", 없으면 "로" (ㄹ 받침도 "로")
    static bool HasFinalConsonant(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        char c = s[s.Length - 1];
        if (c < 0xAC00 || c > 0xD7A3) return false;
        int jong = (c - 0xAC00) % 28;
        return jong != 0 && jong != 8; // 8 = ㄹ
    }
}

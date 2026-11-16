// 센서(하우징 가속도·자이로) 버전으로 두 씬을 바꾸는 에디터 메뉴
//   Tools > Sensor > Setup Whack-A-Mole (1 row x 5)  : 구멍을 플레이어 앞 좌우 한 줄 5개로, 도구를 센서로 움직이게
//   Tools > Sensor > Setup Shooting Range             : 권총 몸체는 시점 고정, 총구만 센서로
//   Tools > Sensor > Use Controller (undo sensor)     : 지금 열린 씬을 컨트롤러 방식으로 되돌림
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImuSetup
{
    public const float RowZ = 0.55f;
    public const float Spacing = 0.16f;

    [MenuItem("Tools/Sensor/Setup Whack-A-Mole (1 row x 5)")]
    static void SetupWhackMenu() => Debug.Log("[Sensor] " + SetupWhack());

    public static string SetupWhack()
    {
        var gm = Object.FindAnyObjectByType<WhackGameManager>(FindObjectsInactive.Include);
        if (gm == null) return "WhackGameManager 없음";
        Transform root = gm.transform.parent;

        // 1) 두더지 5마리를 좌우 한 줄로 (남는 두더지·구멍은 삭제)
        var all = gm.tableMoles.Where(m => m != null).ToList();
        string[] keep = { "Mole_00", "Mole_10", "Mole_11", "Mole_12", "Mole_02" };   // 이미 한 줄이면 이름이 Mole_0..4
        var row = new List<Mole>();
        if (all.Count == 5) row = all.OrderBy(m => m.transform.position.x).ToList();
        else
        {
            foreach (string n in keep) { var m = all.FirstOrDefault(x => x.name == n); if (m != null) row.Add(m); }
            foreach (var m in all) if (!row.Contains(m) && row.Count < 5) row.Add(m);
        }
        foreach (var m in all)
            if (!row.Contains(m))
            {
                var hole = root.Find(m.name.Replace("Mole_", "Hole_"));
                if (hole != null) Undo.DestroyObjectImmediate(hole.gameObject);
                Undo.DestroyObjectImmediate(m.gameObject);
            }
        float topY = row[0].transform.position.y;
        for (int i = 0; i < row.Count; i++)
        {
            var m = row[i];
            var hole = root.Find(m.name.Replace("Mole_", "Hole_"));
            Vector3 p = new Vector3((i - 2) * Spacing, topY, RowZ);
            Undo.RecordObject(m.transform, "row");
            m.transform.position = p;
            m.name = "Mole_" + i;
            if (hole != null)
            {
                Undo.RecordObject(hole, "row");
                hole.position = new Vector3(p.x, hole.position.y, p.z);
                hole.name = "Hole_" + i;
            }
        }
        Undo.RecordObject(gm, "row");
        gm.tableMoles = row.ToArray();

        // 2) 탁자를 좌우로 길게, 앞뒤는 좁게
        var table = root.Find("Table");
        if (table != null)
        {
            Undo.RecordObject(table, "table");
            table.position = new Vector3(0f, table.position.y, RowZ);
            table.localScale = new Vector3(Spacing * 4f + 0.24f, table.localScale.y, 0.34f);
        }

        // 3) 센서 입력 + 도구 구동
        gm.useModule = true;                       // 충돌 판정 끄고 아두이노 연결 유지
        EditorUtility.SetDirty(gm);
        var imu = SetupArduino(gm.arduino != null ? gm.arduino.gameObject : null);
        var toolRoot = GameObject.Find("ToolRoot");
        if (toolRoot != null)
        {
            var drv = toolRoot.GetComponent<ImuToolDriver>();
            if (drv == null) drv = Undo.AddComponent<ImuToolDriver>(toolRoot);
            drv.imu = imu; drv.game = gm; drv.enabled = true;
            EditorUtility.SetDirty(drv);
        }
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        return $"두더지 {row.Count}마리 한 줄 (x {-2 * Spacing:0.00}~{2 * Spacing:0.00}, z {RowZ}), 탁자 {(table != null ? table.localScale.ToString() : "없음")}, 센서 {(imu != null ? "OK" : "없음")}";
    }

    [MenuItem("Tools/Sensor/Setup Shooting Range")]
    static void SetupRangeMenu() => Debug.Log("[Sensor] " + SetupRange());

    public static string SetupRange()
    {
        MonoBehaviour mgr = Object.FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);
        if (mgr == null) mgr = Object.FindAnyObjectByType<ShootingRangeManager>(FindObjectsInactive.Include);
        if (mgr == null) return "사격 게임 매니저 없음";
        var serial = Object.FindAnyObjectByType<ArduinoSerial>(FindObjectsInactive.Include);
        var imu = SetupArduino(serial != null ? serial.gameObject : null);
        var toolRoot = GameObject.Find("ToolRoot");
        var pistol = Object.FindAnyObjectByType<RangePistol>(FindObjectsInactive.Include);
        if (toolRoot == null || pistol == null) return "ToolRoot / 권총 없음";
        var drv = toolRoot.GetComponent<ImuGunDriver>();
        if (drv == null) drv = Undo.AddComponent<ImuGunDriver>(toolRoot);
        drv.imu = imu;
        drv.muzzle = pistol.muzzle;
        drv.head = Camera.main != null ? Camera.main.transform : null;
        drv.enabled = true;
        EditorUtility.SetDirty(drv);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        return "사격장 센서 설정 완료 (muzzle=" + (pistol.muzzle ? pistol.muzzle.name : "null") + ")";
    }

    [MenuItem("Tools/Sensor/Use Controller (undo sensor)")]
    public static void UseController()
    {
        foreach (var d in Object.FindObjectsByType<ImuToolDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(d, "c"); d.enabled = false; }
        foreach (var d in Object.FindObjectsByType<ImuGunDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Undo.RecordObject(d, "c"); d.enabled = false; }
        var gm = Object.FindAnyObjectByType<WhackGameManager>(FindObjectsInactive.Include);
        if (gm != null) { Undo.RecordObject(gm, "c"); gm.useModule = false; }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    // 머리 앞(시야 가운데 약간 아래)에 자세 맞추기 안내 글자 + 어두운 판
    public static TextMesh AddPrompt(ImuInput imu)
    {
        var cam = GameObject.Find("XR Origin (VR)")?.GetComponentInChildren<Camera>(true);
        if (cam == null || imu == null) return null;
        var old = cam.transform.Find("ImuPrompt");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var g = new GameObject("ImuPrompt");
        Undo.RegisterCreatedObjectUndo(g, "prompt");
        g.transform.SetParent(cam.transform, false);
        g.transform.localPosition = new Vector3(0f, -0.05f, 0.7f);
        var tm = g.AddComponent<TextMesh>();
        tm.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/WhackAMole/Fonts/NotoSansKR.ttf");
        g.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Text/Text3D_HUD_NotoSansKR.mat");
        tm.fontSize = 64; tm.characterSize = 0.0055f; tm.fontStyle = FontStyle.Bold; tm.color = Color.white;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.text = "SENSOR SETUP";
        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Panel";
        Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.SetParent(g.transform, false);
        panel.transform.localPosition = new Vector3(0f, 0f, 0.004f);
        panel.transform.localScale = new Vector3(0.62f, 0.25f, 1f);
        var pr = panel.GetComponent<MeshRenderer>();
        pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/WhackAMole/Materials/Text/HudPanel.mat");
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        g.SetActive(false);
        imu.promptText = tm;
        EditorUtility.SetDirty(imu);
        return tm;
    }

    static ImuInput SetupArduino(GameObject go)
    {
        if (go == null) return null;
        var serial = go.GetComponent<ArduinoSerial>();
        if (serial != null)
        {
            Undo.RecordObject(serial, "serial");
            serial.baudRate = 115200;
            serial.quietPrefix = "I ";
            serial.connectOnStart = true;
            serial.enabled = true;
            EditorUtility.SetDirty(serial);
        }
        var imu = go.GetComponent<ImuInput>();
        if (imu == null) imu = Undo.AddComponent<ImuInput>(go);
        imu.arduino = serial;
        EditorUtility.SetDirty(imu);
        AddPrompt(imu);
        return imu;
    }
}

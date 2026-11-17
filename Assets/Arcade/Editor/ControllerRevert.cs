// 오락실 VR 버전을 다시 "Quest 컨트롤러" 방식으로 되돌리는 에디터 메뉴 (배경·오락기·화면·사격 게임 방식은 그대로)
//   Tools > Controller > Revert Both Scenes To Controller
//   - 두더지: 예전처럼 3×3 구멍 9개 + 컨트롤러로 직접 쳐서 판정 (센서 끔). 오락기는 구멍 배치에 맞게 뒤로 길어짐
//   - 사격: 오락실 사격 게임 그대로, 총만 오른손 컨트롤러로 (센서 끔). 총구 레이저는 유지
// 배치 모드: Unity.exe -batchmode -quit -projectPath . -executeMethod ControllerRevert.RunBatch
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ControllerRevert
{
    const string WhackScene = "Assets/Scenes/WhackAMole.unity";
    const string RangeScene = "Assets/Scenes/ShootingRange.unity";
    // 예전 컨트롤러 버전과 같은 간격 (좌우 0.16 m, 앞뒤 0.15 m). 앞 줄은 지금 오락기 앞 쿠션에 걸리지 않는 z 0.55
    const float SpacingX = 0.16f, SpacingZ = 0.15f, FrontRowZ = 0.55f;

    [MenuItem("Tools/Controller/Revert Both Scenes To Controller")]
    static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Debug.Log("[Controller] " + RunAll());
    }

    public static void RunBatch()
    {
        try { Debug.Log("[Controller] " + RunAll()); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static string RunAll()
    {
        var s1 = EditorSceneManager.OpenScene(WhackScene, OpenSceneMode.Single);
        string r1 = Whack();
        EditorSceneManager.SaveScene(s1);
        var s2 = EditorSceneManager.OpenScene(RangeScene, OpenSceneMode.Single);
        string r2 = Range();
        EditorSceneManager.SaveScene(s2);
        AssetDatabase.SaveAssets();
        return r1 + " | " + r2;
    }

    static void SensorOff()
    {
        foreach (var d in Object.FindObjectsByType<ImuToolDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { d.enabled = false; EditorUtility.SetDirty(d); }
        foreach (var d in Object.FindObjectsByType<ImuGunDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { d.enabled = false; EditorUtility.SetDirty(d); }
        foreach (var imu in Object.FindObjectsByType<ImuInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            imu.enabled = false;                                   // 센서 자세 맞추기 안내·마우스 시뮬레이션 안 뜸
            if (imu.promptText != null) imu.promptText.gameObject.SetActive(false);
            EditorUtility.SetDirty(imu);
        }
        foreach (var f in Object.FindObjectsByType<RightHandFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { f.enabled = true; EditorUtility.SetDirty(f); }
    }

    static string Whack()
    {
        var gm = Object.FindAnyObjectByType<WhackGameManager>(FindObjectsInactive.Include);
        if (gm == null) return "WhackGameManager 없음";
        Transform root = gm.transform.parent;
        SensorOff();
        gm.useModule = false;                                      // 컨트롤러 버전: 도구가 두더지에 닿으면 판정

        // ── 3×3 구멍 9개 ──
        var moles = gm.tableMoles.Where(m => m != null).OrderBy(m => m.transform.position.x).ToList();
        if (moles.Count == 0) return "두더지 없음";
        var holes = moles.Select(m => root.Find(m.name.Replace("Mole_", "Hole_"))).ToList();
        Mole protoMole = moles[0]; Transform protoHole = holes[0];
        while (moles.Count < 9)
        {
            var m = Object.Instantiate(protoMole.gameObject, protoMole.transform.parent).GetComponent<Mole>();
            moles.Add(m);
            holes.Add(protoHole != null ? Object.Instantiate(protoHole.gameObject, protoHole.parent).transform : null);
        }
        while (moles.Count > 9)
        {
            int i = moles.Count - 1;
            if (holes[i] != null) Object.DestroyImmediate(holes[i].gameObject);
            Object.DestroyImmediate(moles[i].gameObject);
            moles.RemoveAt(i); holes.RemoveAt(i);
        }
        float topY = protoMole.transform.position.y;
        var ordered = new Mole[9];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                int i = r * 3 + c;
                var p = new Vector3((c - 1) * SpacingX, topY, FrontRowZ + r * SpacingZ);
                moles[i].transform.position = p;
                moles[i].name = $"Mole_{r}{c}";
                moles[i].transform.SetSiblingIndex(protoMole.transform.GetSiblingIndex());
                if (holes[i] != null)
                {
                    holes[i].position = new Vector3(p.x, holes[i].position.y, p.z);
                    holes[i].name = $"Hole_{r}{c}";
                }
                ordered[i] = moles[i];
            }
        gm.tableMoles = ordered;

        // 판정용 탁자 (보이지 않음) – 예전 크기로 구멍 가운데에
        var table = root.Find("Table");
        if (table != null)
        {
            table.position = new Vector3(0f, table.position.y, FrontRowZ + SpacingZ);
            table.localScale = new Vector3(0.6f, table.localScale.y, 0.5f);
        }
        EditorUtility.SetDirty(gm);

        // ── 오락기 다시 만들기: 상판 LED 화면·점수판이 맨 뒤 구멍 뒤로 밀려 기계가 길어짐 ──
        string cab = WhackCabinetBuilder.Build();
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        return $"두더지: 컨트롤러 + 3×3 (z {FrontRowZ:0.00}~{FrontRowZ + 2 * SpacingZ:0.00}), 점수판 z {(gm.statusBoard ? gm.statusBoard.position.z : 0f):0.00}, {cab}";
    }

    static string Range()
    {
        SensorOff();
        var pistol = Object.FindAnyObjectByType<RangePistol>(FindObjectsInactive.Include);
        var laser = Object.FindAnyObjectByType<LaserSight>(FindObjectsInactive.Include);
        if (laser != null)
        {
            laser.gameObject.SetActive(true); laser.enabled = true;
            if (laser.muzzle == null && pistol != null) laser.muzzle = pistol.muzzle;
            EditorUtility.SetDirty(laser);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return $"사격: 컨트롤러 조준, 레이저 {(laser != null ? "유지" : "없음")}";
    }
}

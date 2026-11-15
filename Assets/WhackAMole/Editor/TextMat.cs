// 3D 글자용 머티리얼 (폰트마다 하나) – 깊이 검사를 하는 WhackAMole/Text3D 셰이더 사용
// 메뉴 Tools > Whack-A-Mole > Fix 3D Text Depth : 열린 씬의 모든 TextMesh에 적용
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TextMat
{
    const string Dir = "Assets/WhackAMole/Materials/Text";

    public static Material For(Font font)
    {
        if (font == null) return null;
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/WhackAMole/Materials", "Text");
        string path = $"{Dir}/Text3D_{font.name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("WhackAMole/Text3D"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.mainTexture = font.material.mainTexture;
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Tools/Whack-A-Mole/Fix 3D Text Depth")]
    public static int ApplyToOpenScene()
    {
        int n = 0;
        foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var mat = For(tm.font);
            if (mat == null) continue;
            var r = tm.GetComponent<MeshRenderer>();
            if (r.sharedMaterial == mat) continue;
            Undo.RecordObject(r, "Text material");
            r.sharedMaterial = mat;
            n++;
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return n;
    }
}

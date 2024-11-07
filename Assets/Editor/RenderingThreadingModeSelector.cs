using System.Collections;
using System.Collections.Generic;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;

/// <summary>    
/// RenderingThreadingModeの切り替えをMenuから行う為のClassです。
/// "Tools/RenderingThreadingMode" からモードを切り替える事が出来ます。
/// 
/// RenderingThreadingModeは PlayerSettings.MTRendering、PlayerSettings.graphicsJobs、PlayerSettings.graphicsJobsModeを組み合わせて指定します。
/// 関しては下記のURLを参照して下さい。
/// https://docs.unity3d.com/ScriptReference/Rendering.RenderingThreadingMode.html
/// https://learn.unity.com/tutorial/optimizing-graphics-in-unity#5c7f8528edbc2a002053b5ad
/// </summary>
public class RenderingThreadingModeSelector
{
    /// <summary>
    /// RenderingThreadingMode To Menu Path
    /// </summary>
    static Dictionary<UnityEngine.Rendering.RenderingThreadingMode, string> mRenderingThreadingModePaths = new Dictionary<UnityEngine.Rendering.RenderingThreadingMode, string>()
    {
        {UnityEngine.Rendering.RenderingThreadingMode.Direct,"Tools/RenderingThreadingMode/Direct" },
        {UnityEngine.Rendering.RenderingThreadingMode.MultiThreaded,"Tools/RenderingThreadingMode/MultiThreaded" },
        {UnityEngine.Rendering.RenderingThreadingMode.LegacyJobified,"Tools/RenderingThreadingMode/LegacyJobified" },
        {UnityEngine.Rendering.RenderingThreadingMode.NativeGraphicsJobs,"Tools/RenderingThreadingMode/NativeGraphicsJobs" },
        {UnityEngine.Rendering.RenderingThreadingMode.NativeGraphicsJobsWithoutRenderThread,"Tools/RenderingThreadingMode/NativeGraphicsJobsWithoutRenderThread" },
    };


    /// <summary>
    /// Unity エディターを読み込んだ時に実行する処理
    /// </summary>
    [InitializeOnLoadMethod]
    static void InitializeOnLoad()
    {
        EditorApplication.update += RenderingThreadingModeChecker;
    }


    /// <summary>
    /// Menuにチェックマークを付ける処理
    /// </summary>
    /// <param name="renderingThreadingMode">チェックを付けたい項目</param>
    static void SetMenu(UnityEngine.Rendering.RenderingThreadingMode renderingThreadingMode)
    {
        foreach (var key in mRenderingThreadingModePaths.Keys)
        {
            Menu.SetChecked(mRenderingThreadingModePaths[key], key == renderingThreadingMode);
        }
    }


    /// <summary>
    /// UnityEditorから定期的に実行される処理
    /// </summary>
    static void RenderingThreadingModeChecker()
    {
        //
        // 外部で変更される可能性がある為、ポーリングして変更を監視して、メニューに反映させています。
        //
        if ((PlayerSettings.MTRendering == false) &&
            (PlayerSettings.graphicsJobs == false))
        {
            SetMenu(UnityEngine.Rendering.RenderingThreadingMode.Direct);
        }
        else if ((PlayerSettings.MTRendering == true) &&
                    (PlayerSettings.graphicsJobs == false))
        {
            SetMenu(UnityEngine.Rendering.RenderingThreadingMode.MultiThreaded);
        }
        else if ((PlayerSettings.MTRendering == true) &&
                    (PlayerSettings.graphicsJobs == true))
        {
            if (PlayerSettings.graphicsJobMode == GraphicsJobMode.Legacy)
            {
                SetMenu(UnityEngine.Rendering.RenderingThreadingMode.LegacyJobified);
            }
            else
            {
                SetMenu(UnityEngine.Rendering.RenderingThreadingMode.NativeGraphicsJobs);
            }
        }
        else
        {
            SetMenu(UnityEngine.Rendering.RenderingThreadingMode.NativeGraphicsJobsWithoutRenderThread);
        }
    }


    /// <summary>
    /// Directが選択された時の処理
    /// </summary>
    [MenuItem("Tools/RenderingThreadingMode/Direct", false)]
    public static void Direct()
    {
        PlayerSettings.MTRendering = false;
        PlayerSettings.graphicsJobs = false;
    }


    /// <summary>
    /// Directが選択可能であるか否か
    /// </summary>
    /// <returns>true:可能 false:不可</returns>
    [MenuItem("Tools/RenderingThreadingMode/Direct", true)]
    public static bool IsDirect()
    {
        return true;
    }


    /// <summary>
    /// MultiThreadedが選択された時の処理
    /// </summary>
    [MenuItem("Tools/RenderingThreadingMode/MultiThreaded", false)]
    public static void MultiThreaded()
    {
        PlayerSettings.MTRendering = true;
        PlayerSettings.graphicsJobs = false;
    }


    /// <summary>
    /// MultiThreadedが選択可能であるか否か
    /// </summary>
    /// <returns>true:可能 false:不可能</returns>
    [MenuItem("Tools/RenderingThreadingMode/MultiThreaded", true)]
    public static bool IsThreaded()
    {
        return true;
    }


    /// <summary>
    /// LegacyJobifieldが選択された時の処理
    /// </summary>
    [MenuItem("Tools/RenderingThreadingMode/LegacyJobified", false)]
    public static void LegacyJobifield()
    {
        PlayerSettings.MTRendering = true;
        PlayerSettings.graphicsJobs = true;
        PlayerSettings.graphicsJobMode = GraphicsJobMode.Legacy;
    }


    /// <summary>
    /// LegacyJobifiedが選択可能であるか否か
    /// </summary>
    /// <returns></returns>
    [MenuItem("Tools/RenderingThreadingMode/LegacyJobified", true)]
    public static bool IsLegacyJobifield()
    {
        // Androidは選択不可
        if(EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            return false;
        }           
        return true;
    }


    /// <summary>
    /// NativeGraphicsJobsが選択された時の処理
    /// </summary>
    [MenuItem("Tools/RenderingThreadingMode/NativeGraphicsJobs", false)]
    public static void NativeGraphicsJobs()
    {
        PlayerSettings.MTRendering = true;
        PlayerSettings.graphicsJobs = true;
        PlayerSettings.graphicsJobMode = GraphicsJobMode.Native;
    }


    /// <summary>
    /// NativeGraphicsJobsが選択であるか否か
    /// </summary>
    /// <returns></returns>
    [MenuItem("Tools/RenderingThreadingMode/NativeGraphicsJobs", true)]
    public static bool IsNativeGraphicsJobs()
    {
        return true;
    }


    /// <summary>
    /// NativeGraphicsJobsWithoutRenderThreadが選択された時の処理
    /// </summary>
    [MenuItem("Tools/RenderingThreadingMode/NativeGraphicsJobsWithoutRenderThread", false)]
    public static void NativeGraphicsJobsWithoutRenderThread()
    {
        PlayerSettings.MTRendering = false;
        PlayerSettings.graphicsJobs = true;
    }


    /// <summary>
    /// NativeGraphicsJobsWithoutRenderThreadが選択可能であるか否か
    /// </summary>
    /// <returns>true:可能 false:不可能</returns>
    [MenuItem("Tools/RenderingThreadingMode/NativeGraphicsJobsWithoutRenderThread", true)]
    public static bool IsNativeGraphicsJobsWithoutRenderThread()
    {
        return true;
    }
}
#endif

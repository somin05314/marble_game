#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class StageProgressTools
{
    // StageProgressManager의 기본 파일명과 맞춰줘야 함
    const string DefaultFileName = "stage_progress.json";

    static string SavePath => Path.Combine(Application.persistentDataPath, DefaultFileName);
    static string BackupPath => SavePath + ".bak";
    static string TempPath => SavePath + ".tmp";

    [MenuItem("Tools/Stage Progress/Reset (Delete Save Files)...", priority = 10)]
    public static void ResetByDeletingFiles()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Reset Stage Progress",
            "스테이지 해금/클리어 진행도를 초기화합니다.\n\n" +
            "- stage_progress.json\n- stage_progress.json.bak\n- stage_progress.json.tmp\n\n" +
            "파일을 삭제하고, 실행 중이면 메모리 진행도도 초기화합니다.\n\n계속할까요?",
            "초기화", "취소"
        );

        if (!ok) return;

        int deleted = 0;
        deleted += TryDelete(SavePath);
        deleted += TryDelete(BackupPath);
        deleted += TryDelete(TempPath);

        // 플레이 중이면 인메모리도 같이 초기화
        if (Application.isPlaying && StageProgressManager.I != null)
        {
            StageProgressManager.I.ClearAllProgress();
        }

        EditorUtility.DisplayDialog(
            "Reset Stage Progress",
            $"완료! 삭제된 파일: {deleted}\n경로: {Application.persistentDataPath}",
            "OK"
        );
    }

    [MenuItem("Tools/Stage Progress/Open Save Folder", priority = 11)]
    public static void OpenSaveFolder()
    {
        // 폴더가 없으면 생성
        Directory.CreateDirectory(Application.persistentDataPath);
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [MenuItem("Tools/Stage Progress/Print Save Paths", priority = 12)]
    public static void PrintPaths()
    {
        Debug.Log($"[StageProgressTools] persistentDataPath: {Application.persistentDataPath}");
        Debug.Log($"[StageProgressTools] SavePath: {SavePath}");
        Debug.Log($"[StageProgressTools] BackupPath: {BackupPath}");
        Debug.Log($"[StageProgressTools] TempPath: {TempPath}");
    }

    static int TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            File.Delete(path);
            return 1;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[StageProgressTools] Failed to delete: {path}\n{e}");
            return 0;
        }
    }
}
#endif
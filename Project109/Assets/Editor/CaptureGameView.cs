using System;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class GameViewScreenshot
{
    [MenuItem("Tools/Capture GameView")]
    [Shortcut("Tools/Capture GameView", KeyCode.F12)]
    public static void Capture()
    {
        string folder = "Screenshots";
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"GameView_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        ScreenCapture.CaptureScreenshot(path);

        // 에디트 모드에서도 즉시 파일이 기록되도록 GameView 강제 갱신
        var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
        if (gameView != null)
        {
            gameView.Repaint();
        }

        Debug.Log($"Saved screenshot: {Path.GetFullPath(path)}");
    }
}
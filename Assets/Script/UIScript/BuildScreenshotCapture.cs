using System;
using System.IO;
using UnityEngine;

public class BuildScreenshotCapture : MonoBehaviour
{
    [Header("Hotkey")]
    [SerializeField] KeyCode captureKey = KeyCode.F12;

    [Header("Capture")]
    [SerializeField] int superSize = 1;
    [SerializeField] bool useTimestamp = true;
    [SerializeField] string filePrefix = "screenshot";

    [Header("Folder")]
    [SerializeField] string folderName = "Screenshots";

    void Update()
    {
        if (Input.GetKeyDown(captureKey))
        {
            Capture();
        }
    }

    public void Capture()
    {
        string folderPath = Path.Combine(Application.dataPath, "..", folderName);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        string fileName;
        if (useTimestamp)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            fileName = $"{filePrefix}_{timestamp}.png";
        }
        else
        {
            fileName = $"{filePrefix}.png";
        }

        string fullPath = Path.Combine(folderPath, fileName);

        ScreenCapture.CaptureScreenshot(fullPath, Mathf.Max(1, superSize));

        Debug.Log($"[Screenshot] Saved: {fullPath}");
    }
}
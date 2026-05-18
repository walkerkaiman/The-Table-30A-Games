using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Captures the current Wang Tiles viewport to a PNG inside the served
/// <c>StreamingAssets/WebApp/snapshots/</c> folder. Returns a LAN URL that any
/// phone (or QR scanner) can hit to download the image.
///
/// Reuses the existing in-project <see cref="QRCodeEncoder"/> on the consumer
/// side; this class only does the file-capture + URL-building step.
/// </summary>
public static class WangTilesSnapshot
{
    private const string SnapshotFolder = "snapshots";

    /// <summary>
    /// Render the given camera to a PNG and save it under StreamingAssets/WebApp/snapshots/.
    /// Returns the public LAN URL (e.g. http://192.168.0.10:7777/snapshots/wang_20260518_094210.png).
    /// Returns null on failure.
    /// </summary>
    public static string Capture(Camera cam, int width, int height)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            GameLog.Warn("WangTilesSnapshot: no camera available — skipping capture.");
            return null;
        }

        RenderTexture rt = null;
        Texture2D tex = null;
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;

        try
        {
            rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();

            string dir = Path.Combine(Application.streamingAssetsPath, "WebApp", SnapshotFolder);
            Directory.CreateDirectory(dir);
            string fileName = $"wang_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, png);

            string ip = NetworkManager.Instance != null ? NetworkManager.Instance.LocalIP : "127.0.0.1";
            int port = NetworkManager.Instance != null ? NetworkManager.Instance.Port : 7777;
            string url = $"http://{ip}:{port}/{SnapshotFolder}/{fileName}";

            GameLog.Game($"Wang Tiles snapshot saved: {path}");
            GameLog.Game($"Wang Tiles snapshot URL:   {url}");
            return url;
        }
        catch (Exception ex)
        {
            GameLog.Error($"WangTilesSnapshot.Capture failed: {ex}");
            return null;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }
}

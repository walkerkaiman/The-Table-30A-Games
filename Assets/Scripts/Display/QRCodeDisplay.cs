using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates two QR codes at startup and sets them on child Image components:
///   "WiFi QR"    — scans to auto-join the venue WiFi network
///   "Browser QR" — scans to open the game URL in the phone's browser
///
/// Place this on a Canvas GameObject. Create two child GameObjects named
/// exactly "WiFi QR" and "Browser QR", each with an Image component.
/// The script generates the QR textures dynamically from NetworkManager's
/// IP/port and your WiFi credentials.
/// </summary>
public class QRCodeDisplay : MonoBehaviour
{
    [Header("WiFi Network")]
    [Tooltip("The WiFi network name (SSID) players should connect to.")]
    [SerializeField] private string wifiSSID = "MyNetwork";

    [Tooltip("The WiFi password. Leave empty for open networks.")]
    [SerializeField] private string wifiPassword = "";

    [Tooltip("Authentication type: WPA, WEP, or nopass (open network).")]
    [SerializeField] private string wifiAuthType = "WPA";

    [Header("QR Image Targets")]
    [Tooltip("Drag the Image that should display the WiFi QR code.")]
    public Image wifiQRImage;

    [Tooltip("Drag the Image that should display the browser URL QR code.")]
    public Image browserQRImage;

    [Header("QR Appearance")]
    [SerializeField] private int pixelsPerModule = 10;
    [SerializeField] private Color darkColor = Color.black;
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField] private int quietZone = 4;

    private void Start()
    {
        string ip = NetworkManager.Instance != null ? NetworkManager.Instance.LocalIP : "127.0.0.1";
        int port = NetworkManager.Instance != null ? NetworkManager.Instance.Port : 7777;

        string wifiPayload = BuildWifiPayload();
        string urlPayload = $"http://{ip}:{port}";

        ApplyQR(wifiQRImage, "WiFi QR", wifiPayload);
        ApplyQR(browserQRImage, "Browser QR", urlPayload);

        GameLog.Game($"QR codes generated — WiFi: \"{wifiSSID}\", URL: {urlPayload}");
    }

    private void ApplyQR(Image img, string label, string payload)
    {
        if (img == null)
        {
            Debug.LogWarning($"QRCodeDisplay: {label} Image is not assigned in the Inspector.");
            return;
        }

        bool[,] matrix = QRCodeEncoder.Encode(payload);
        Texture2D tex = QRCodeEncoder.ToTexture(matrix, pixelsPerModule, darkColor, lightColor, quietZone);
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        img.preserveAspect = true;
    }

    private string BuildWifiPayload()
    {
        string auth = string.IsNullOrEmpty(wifiPassword) ? "nopass" : wifiAuthType;
        string ssid = EscapeWifi(wifiSSID);
        string pass = EscapeWifi(wifiPassword);
        return $"WIFI:T:{auth};S:{ssid};P:{pass};;";
    }

    /// <summary>
    /// Escape special characters per the WiFi QR code spec.
    /// </summary>
    private static string EscapeWifi(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace(":", "\\:");
    }
}

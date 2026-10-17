using System.Security.Cryptography;
using System.Text;

namespace MarwanQuTech.WebApi.Common;

/// <summary>
/// Anonymous visitor fingerprint for view de-duplication: SHA-256 of IP + User-Agent + the current UTC day.
/// No raw IP is stored anywhere, and the daily salt means keys can't be linked across days.
/// </summary>
public static class VisitorKey
{
    public static string From(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var agent = http.Request.Headers.UserAgent.ToString();
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{ip}|{agent}|{day}"));
        return Convert.ToHexString(hash, 0, 16);
    }
}

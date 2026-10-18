using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BongoCatNotice.Utilities
{
    /// <summary>
    /// 轻量版本检查：只查 GitHub Releases 的最新 tag，与当前版本比较。
    /// 任何异常都静默返回 null —— 检查更新失败绝不能影响正常使用。
    /// </summary>
    public static class UpdateChecker
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(4) };

        public static async Task<string?> GetLatestTagAsync(CancellationToken ct = default)
        {
            try
            {
                string api = GlobalHelper.Url.Replace("https://github.com/", "https://api.github.com/repos/") + "/releases/latest";

                using HttpRequestMessage request = new(HttpMethod.Get, api);
                request.Headers.UserAgent.ParseAdd("BongoCatNotice-UpdateChecker");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");

                using HttpResponseMessage response = await Http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                return doc.RootElement.TryGetProperty("tag_name", out JsonElement tag) ? tag.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>latest 是否比 current 新（都解析失败时视为不新）</summary>
        public static bool IsNewer(string? latest, string current)
        {
            Version? l = ParseVersion(latest);
            Version? c = ParseVersion(current);
            return l is not null && c is not null && l > c;
        }

        private static Version? ParseVersion(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string s = text.Trim().TrimStart('v', 'V');

            // 去掉预发布后缀：0.1.0.4-beta / 0.1.0.4+build
            int cut = s.IndexOfAny(['-', '+']);
            if (cut > 0)
                s = s[..cut];

            return Version.TryParse(s, out Version? v) ? v : null;
        }
    }
}

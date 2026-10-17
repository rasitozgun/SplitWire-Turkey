using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Resolves the correct linux asset from a GitHub repository's latest release
/// via the public API, keeping us independent of changing asset file names.
/// </summary>
public static class GitHubReleaseHelper
{
    public static async Task<string> GetLatestAssetUrlAsync(
        HttpClient http, string repo, string assetPattern, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        request.Headers.UserAgent.ParseAdd("SplitWire-Turkey");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        // Minimal dependency-free JSON scan: find all "browser_download_url" values.
        var urls = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();
        if (urls.Count == 0)
        {
            throw new InvalidOperationException($"No release assets found for {repo}.");
        }

        var match = urls.FirstOrDefault(u => Regex.IsMatch(u, assetPattern, RegexOptions.IgnoreCase));
        return match ?? throw new InvalidOperationException(
            $"No asset matching '{assetPattern}' in latest release of {repo}. Available: {string.Join(", ", urls)}");
    }
}

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LogService = SplitWireTurkey.Linux.Services.LogService;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Resolves the correct linux asset from a GitHub repository's latest release
/// via the public API, keeping us independent of changing asset file names.
/// </summary>
public static class GitHubReleaseHelper
{
    // Known-good asset URLs used when api.github.com answers 403 rate-limit
    // (60 req/h per IP unauthenticated) or is unreachable. Values verified
    // against the real releases at the time of the Linux build.
    private static readonly Dictionary<string, string> KnownGoodAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ViRb3/wgcf"] = "https://github.com/ViRb3/wgcf/releases/download/v2.3.1/wgcf_2.3.1_linux_amd64",
        ["hufrea/byedpi"] = "https://github.com/hufrea/byedpi/releases/download/v0.17.3/byedpi-17.3-x86_64.tar.gz",
        ["bol-van/zapret"] = "https://github.com/bol-van/zapret/releases/download/v72.13/zapret-v72.13.tar.gz",
    };

    // Cache file survives across runs: {URL} last resolved per repo.
    private static string CacheFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SplitWire-Turkey", "github-assets.json");

    public static async Task<string> GetLatestAssetUrlAsync(
        HttpClient http, string repo, string assetPattern, CancellationToken ct = default, string? fallbackUrl = null)
    {
        string? url = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd("SplitWire-Turkey");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                // Minimal dependency-free JSON scan: find all "browser_download_url" values.
                var urls = Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"")
                    .Select(m => m.Groups[1].Value)
                    .ToList();
                url = urls.FirstOrDefault(u => Regex.IsMatch(u, assetPattern, RegexOptions.IgnoreCase));
                if (url is not null)
                {
                    WriteCache(repo, url);
                    return url;
                }
                throw new InvalidOperationException(
                    $"No asset matching '{assetPattern}' in latest release of {repo}. Available: {string.Join(", ", urls)}");
            }
            LogService.Warn("Release", $"GitHub API returned {response.StatusCode} for {repo}; using cached/fallback asset.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.Warn("Release", $"GitHub API request failed for {repo}: {ex.Message}");
        }

        // Fallbacks: write-through cache first, then known-good pin.
        url = ReadCache(repo);
        if (url is not null) return url;
        var known = fallbackUrl ?? (KnownGoodAssets.TryGetValue(repo, out var k) ? k : null);
        if (known is not null)
        {
            LogService.Info("Release", $"Using pinned asset for {repo}: {known}");
            return known;
        }
        throw new InvalidOperationException($"Cannot resolve release asset for {repo} (rate limit or no cache).");
    }

    private static string? ReadCache(string repo)
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var json = JsonDocument.Parse(File.ReadAllText(CacheFile));
            if (json.RootElement.TryGetProperty(repo, out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        catch { }
        return null;
    }

    private static void WriteCache(string repo, string url)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFile)!);
            var root = File.Exists(CacheFile)
                ? JsonDocument.Parse(File.ReadAllText(CacheFile)).RootElement.Clone()
                : JsonNode.Parse("{}");
            (root as JsonObject)![repo] = url;
            File.WriteAllText(CacheFile, root!.ToString());
        }
        catch { }
    }
}

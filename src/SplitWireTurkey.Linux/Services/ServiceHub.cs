using System.Net.Http;

namespace SplitWireTurkey.Linux.Services;

/// <summary>App-wide singleton services shared by the pages.</summary>
public static class ServiceHub
{
    public static HttpClient HttpClient { get; } = new HttpClient();
    public static ProcessRunner Runner { get; } = ProcessRunner.Instance;
}

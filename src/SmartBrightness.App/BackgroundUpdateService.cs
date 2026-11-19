using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBrightness.App;

internal sealed class BackgroundUpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/Maxhm007/SmartBrightness-AI/releases/latest");
    private static readonly HttpClient Client = CreateClient();
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly Version CurrentVersion = typeof(BackgroundUpdateService).Assembly.GetName().Version ?? new Version(0, 0);
    private readonly Action<string> _startInstaller;
    private int _updateStarted;

    public BackgroundUpdateService(Action<string> startInstaller) => _startInstaller = startInstaller;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndInstallAsync(cancellationToken);
                await Task.Delay(CheckInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (TaskCanceledException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (HttpRequestException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (IOException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (JsonException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                await DelayBeforeRetryAsync(cancellationToken);
            }
        }
    }

    private async Task CheckAndInstallAsync(CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(LatestReleaseUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return;

        await using var json = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(json, cancellationToken: cancellationToken);
        if (release is null || !TryParseVersion(release.TagName, out var releaseVersion) || releaseVersion <= CurrentVersion) return;

        var expectedName = $"SmartBrightnessAI-Setup-{releaseVersion}.exe";
        var asset = release.Assets.FirstOrDefault(item => string.Equals(item.Name, expectedName, StringComparison.Ordinal));
        if (asset is null || !TryGetSha256(asset.Digest, out var expectedHash) || !Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps) return;

        var updateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartBrightnessAI", "updates");
        Directory.CreateDirectory(updateDirectory);
        var installerPath = Path.Combine(updateDirectory, expectedName);
        using (var installerResponse = await Client.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            if (!installerResponse.IsSuccessStatusCode) return;
            await using var source = await installerResponse.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await source.CopyToAsync(destination, cancellationToken);
        }

        await using (var installer = File.OpenRead(installerPath))
        {
            var actualHash = await SHA256.HashDataAsync(installer, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                File.Delete(installerPath);
                return;
            }
        }

        if (Interlocked.Exchange(ref _updateStarted, 1) != 0) return;
        try
        {
            _startInstaller(installerPath);
        }
        catch
        {
            Interlocked.Exchange(ref _updateStarted, 0);
            throw;
        }
    }

    private static async Task DelayBeforeRetryAsync(CancellationToken cancellationToken) =>
        await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);

    private static bool TryParseVersion(string? tag, out Version version)
    {
        var normalized = tag is not null && tag.StartsWith('v') ? tag[1..] : tag;
        if (Version.TryParse(normalized, out var parsed))
        {
            version = parsed;
            return true;
        }
        version = new Version(0, 0);
        return false;
    }

    private static bool TryGetSha256(string? digest, out byte[] hash)
    {
        hash = [];
        const string prefix = "sha256:";
        if (digest?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) != true) return false;
        try
        {
            hash = Convert.FromHexString(digest[prefix.Length..]);
            return hash.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SmartBrightnessAI", CurrentVersion.ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; init; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }
}

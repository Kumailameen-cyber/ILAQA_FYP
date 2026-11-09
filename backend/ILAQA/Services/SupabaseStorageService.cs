using System.Net.Http.Headers;

namespace ILAQA.Services;

// Stores profile pictures in the Supabase Storage bucket "profile-pictures".
// Each runner's files live under "<userId>/".
public sealed class SupabaseStorageService(
    HttpClient httpClient, IConfiguration configuration, ILogger<SupabaseStorageService> logger)
{
    private const string BucketName = "profile-pictures";

    // Uploads the image and returns its public URL.
    public async Task<string> UploadProfilePictureAsync(Stream imageStream, string contentType, string userId)
    {
        var (url, key) = GetStorageConfiguration();

        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new ArgumentException("Unsupported image type.")
        };

        var filePath = $"{userId}/{Guid.NewGuid():N}{extension}";

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/storage/v1/object/{BucketName}/{filePath}");
        AddAuthentication(request, key);
        request.Content = new StreamContent(imageStream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Supabase upload failed: {(int)response.StatusCode}");

        return $"{url}/storage/v1/object/public/{BucketName}/{filePath}";
    }

    // Best-effort delete of an old picture. A failure is only logged: the
    // profile change has already been saved and must not be undone.
    public async Task TryDeleteProfilePictureAsync(string? imageUrl, string userId)
    {
        var filePath = GetOwnedFilePath(imageUrl, userId);
        if (filePath == null)
            return;

        var (url, key) = GetStorageConfiguration();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{url}/storage/v1/object/{BucketName}/{filePath}");
        AddAuthentication(request, key);

        try
        {
            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Supabase deletion failed ({Status}) for {FilePath}.", (int)response.StatusCode, filePath);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Supabase deletion failed for {FilePath}.", filePath);
        }
    }

    private (string Url, string Key) GetStorageConfiguration()
    {
        var url = configuration["Supabase:Url"];
        var key = configuration["Supabase:SecretKey"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Supabase Storage is not configured.");

        return (url.TrimEnd('/'), key);
    }

    private static void AddAuthentication(HttpRequestMessage request, string secretKey)
    {
        request.Headers.Add("apikey", secretKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
    }

    // The storage path of a saved URL, but only for a single file in this
    // runner's own folder of our bucket (never another runner's image).
    private string? GetOwnedFilePath(string? imageUrl, string userId)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        var prefix = $"{GetStorageConfiguration().Url}/storage/v1/object/public/{BucketName}/";
        if (!imageUrl.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var filePath = imageUrl[prefix.Length..];
        if (!filePath.StartsWith($"{userId}/", StringComparison.Ordinal))
            return null;

        var fileName = filePath[(userId.Length + 1)..];
        var isSingleFileName = !string.IsNullOrWhiteSpace(fileName) &&
                               fileName.IndexOfAny(['/', '\\', '?', '#']) < 0 &&
                               fileName is not "." and not "..";

        return isSingleFileName ? filePath : null;
    }
}

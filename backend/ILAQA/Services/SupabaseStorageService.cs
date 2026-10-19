
using System.Net.Http.Headers;

namespace ILAQA.Services
{
    public class SupabaseStorageService
    {
        private const string BucketName = "profile-pictures";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SupabaseStorageService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        // Reusable configuration helper
        private (string Url, string Key) GetStorageConfiguration()
        {
            var url = _configuration["Supabase:Url"];
            var key = _configuration["Supabase:SecretKey"];

            if (string.IsNullOrWhiteSpace(url) ||
                string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "Supabase Storage is not configured.");
            }

            return (url.TrimEnd('/'), key);
        }

        // Reusable authentication helper
        private void AddStorageAuthentication(
            HttpRequestMessage request,
            string secretKey)
        {
            request.Headers.Add("apikey", secretKey);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);
        }

        // Reusable helper for generating public URLs
        private string GetPublicUrl(string filePath)
        {
            var (url, _) = GetStorageConfiguration();

            return $"{url}/storage/v1/object/public/{BucketName}/{filePath}";
        }

        // Existing upload functionality
        public async Task<string> UploadProfilePictureAsync(
            Stream imageStream,
            string contentType,
            string userId)
        {
            var (url, key) = GetStorageConfiguration();

            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => throw new ArgumentException(
                    "Unsupported image type.")
            };

            var filePath =
                $"{userId}/{Guid.NewGuid():N}{extension}";

            var uploadUrl =
                $"{url}/storage/v1/object/{BucketName}/{filePath}";

            using var request = new HttpRequestMessage(
                HttpMethod.Post, uploadUrl);

            AddStorageAuthentication(request, key);

            request.Content = new StreamContent(imageStream);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue(contentType);

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Supabase upload failed: {(int)response.StatusCode}");
            }

            return GetPublicUrl(filePath);
        }

        // Extract the storage path from a previously saved URL.
        // Only accept images belonging to this Supabase project and bucket.
        private string? GetProfilePicturePath(
            string? imageUrl,
            string userId)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            var (url, _) = GetStorageConfiguration();

            var prefix =
                $"{url}/storage/v1/object/public/{BucketName}/";

            if (!imageUrl.StartsWith(
                prefix, StringComparison.Ordinal))
            {
                return null;
            }

            var filePath = imageUrl.Substring(prefix.Length);

            // Prevent deletion of another user's images.
            if (!filePath.StartsWith(
                $"{userId}/", StringComparison.Ordinal))
            {
                return null;
            }

            var fileName = filePath.Substring(userId.Length + 1);

            // Only allow a single filename, not nested paths.
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.Contains('/') ||
                fileName.Contains('\\') ||
                fileName.Contains('?') ||
                fileName.Contains('#') ||
                fileName == "." ||
                fileName == "..")
            {
                return null;
            }

            return filePath;
        }

        // Reusable deletion function
        public async Task DeleteProfilePictureAsync(
            string? imageUrl,
            string userId)
        {
            var filePath =
                GetProfilePicturePath(imageUrl, userId);

            if (filePath == null)
                return;

            var (url, key) = GetStorageConfiguration();

            var deleteUrl =
                $"{url}/storage/v1/object/{BucketName}/{filePath}";

            using var request = new HttpRequestMessage(
                HttpMethod.Delete, deleteUrl);

            AddStorageAuthentication(request, key);

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Supabase deletion failed: {(int)response.StatusCode}");
            }
        }
    }
}

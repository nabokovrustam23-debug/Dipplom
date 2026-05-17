namespace BarbershopCrm.Web.Services;

public interface IImageUploadService
{
    /// <summary>
    /// Validates and saves the uploaded file under wwwroot/uploads/{folder}/.
    /// Returns the public URL (e.g. "/uploads/services/abcd.png") or null if file is null/empty.
    /// Throws InvalidOperationException with user-friendly message on validation failure.
    /// </summary>
    Task<string?> SaveAsync(IFormFile? file, string folder, CancellationToken ct);

    void Delete(string? relativeUrl);
}

public sealed class LocalImageUploadService : IImageUploadService
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];
    private const long MaxBytes = 4 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalImageUploadService> _logger;

    public LocalImageUploadService(IWebHostEnvironment env, ILogger<LocalImageUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string?> SaveAsync(IFormFile? file, string folder, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;

        if (file.Length > MaxBytes)
            throw new InvalidOperationException($"Файл слишком большой (>4 МБ).");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"Неподдерживаемый формат. Разрешены: {string.Join(", ", AllowedExtensions)}.");

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Файл не является изображением (некорректный MIME-тип).");

        var safeFolder = SanitizeFolder(folder);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var contentRoot = _env.ContentRootPath ?? Directory.GetCurrentDirectory();
        var webRoot = _env.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", safeFolder);

        _logger.LogInformation("WebRootPath={WebRoot}, ContentRoot={Content}, dir={Dir}",
            _env.WebRootPath, _env.ContentRootPath, dir);

        // Validate the directory path is writable
        try
        {
            Directory.CreateDirectory(dir);
            _logger.LogInformation("Directory created/exists: {Dir}", dir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create directory {Dir}", dir);
            throw new InvalidOperationException($"Не удалось создать папку для загрузки: {ex.Message}");
        }

        var fullPath = Path.Combine(dir, fileName);
        _logger.LogInformation("Full path: {Path}", fullPath);

        try
        {
            // Write to a memory stream first, then to disk
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            _logger.LogInformation("File copied to memory: {Size} bytes", ms.Length);

            ms.Position = 0;
            await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await ms.CopyToAsync(fs, ct);
            }
            _logger.LogInformation("File written to disk: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write file {Path}", fullPath);
            throw new InvalidOperationException($"Не удалось сохранить файл: {ex.Message}");
        }

        return $"/uploads/{safeFolder}/{fileName}";
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl)) return;
        if (!relativeUrl.StartsWith("/uploads/", StringComparison.Ordinal)) return;

        try
        {
            var contentRoot = _env.ContentRootPath ?? Directory.GetCurrentDirectory();
            var webRoot = _env.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
            var fullPath = Path.Combine(webRoot, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete uploaded image '{Url}'", relativeUrl);
        }
    }

    private static string SanitizeFolder(string folder)
    {
        var clean = new string(folder.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return string.IsNullOrEmpty(clean) ? "misc" : clean.ToLowerInvariant();
    }
}

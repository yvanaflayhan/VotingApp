namespace Voting_app.Services
{
    public interface IImageService
    {
        Task<string?> ValidateAsync(IFormFile file); // an error message, or null if it is fine
        Task<string> SaveAsync(IFormFile file, string folder); // returns the public path
        void Delete(string? path);
    }

    public class ImageService : IImageService
    {
        private const long MaxBytes = 2 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly IWebHostEnvironment _env;

        public ImageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string?> ValidateAsync(IFormFile file)
        {
            if (file.Length > MaxBytes)
                return "The photo must be 2 MB or smaller.";

            return await IsRealImageAsync(file) ? null : "Please upload a JPG, PNG or WEBP image.";
        }

        public async Task<string> SaveAsync(IFormFile file, string folder)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{ext}"; // never trust the original name
            var directory = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(directory);

            await using var stream = new FileStream(
                Path.Combine(directory, fileName),
                FileMode.Create
            );
            await file.CopyToAsync(stream);

            return $"/uploads/{folder}/{fileName}";
        }

        public void Delete(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var fullPath = Path.Combine(
                _env.WebRootPath,
                path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)
            );

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }

        // Checks the extension AND the first bytes of the file (its real format)
        private static async Task<bool> IsRealImageAsync(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return false;

            var h = new byte[12];
            await using var stream = file.OpenReadStream();
            if (await stream.ReadAsync(h, 0, h.Length) < h.Length)
                return false;

            var jpeg = h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
            var png = h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
            var webp =
                h[0] == 'R'
                && h[1] == 'I'
                && h[2] == 'F'
                && h[3] == 'F'
                && h[8] == 'W'
                && h[9] == 'E'
                && h[10] == 'B'
                && h[11] == 'P';

            return jpeg || png || webp;
        }
    }
}

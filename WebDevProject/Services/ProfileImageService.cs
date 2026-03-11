namespace WebDevProject.Services
{
    public class ProfileImageService
    {
        private readonly IWebHostEnvironment _environment;

        public ProfileImageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<(bool Success, string? ImageUrl, string? ErrorMessage)> SaveProfileImageAsync(IFormFile? profileImage, string userId, string? existingImageUrl)
        {
            if (profileImage == null || profileImage.Length <= 0)
            {
                return (true, existingImageUrl, null);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(profileImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return (false, existingImageUrl, "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
            }

            if (profileImage.Length > 5 * 1024 * 1024)
            {
                return (false, existingImageUrl, "Image file size must not exceed 5MB.");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (!string.IsNullOrWhiteSpace(existingImageUrl))
            {
                var oldRelativePath = existingImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldImagePath = Path.Combine(_environment.WebRootPath, oldRelativePath);
                if (System.IO.File.Exists(oldImagePath))
                {
                    try
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete old profile image {oldImagePath}: {ex.Message}");
                    }
                }
            }

            var uniqueFileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await profileImage.CopyToAsync(fileStream);
            }

            return (true, $"/uploads/profiles/{uniqueFileName}", null);
        }
    }
}

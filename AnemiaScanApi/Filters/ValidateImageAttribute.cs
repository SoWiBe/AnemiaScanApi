using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AnemiaScanApi.Filters;

/// <summary>
/// Validates image file uploads (size, format, MIME type)
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ValidateImageAttribute : ActionFilterAttribute
{
    private readonly long _maxFileSizeInBytes;
    private readonly string[] _allowedExtensions;
    private readonly string[] _allowedMimeTypes;
    private readonly string _fileParameterName;

    /// <summary>
    /// Initialize image validation attribute
    /// </summary>
    /// <param name="maxFileSizeMB">Maximum file size in megabytes (default: 10 MB)</param>
    /// <param name="fileParameterName">Name of the file parameter to validate (default: "image")</param>
    public ValidateImageAttribute(int maxFileSizeMB = 10, string fileParameterName = "image")
    {
        _maxFileSizeInBytes = maxFileSizeMB * 1024 * 1024;
        _fileParameterName = fileParameterName;
        
        // Allowed image extensions
        _allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        
        // Allowed MIME types
        _allowedMimeTypes = new[]
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/bmp",
            "image/gif",
            "image/webp"
        };
    }

    /// <summary>
    /// Custom constructor with specific allowed extensions
    /// </summary>
    /// <param name="maxFileSizeMB">Maximum file size in megabytes</param>
    /// <param name="allowedExtensions">Allowed file extensions (e.g., ".jpg", ".png")</param>
    /// <param name="fileParameterName">Name of the file parameter to validate</param>
    public ValidateImageAttribute(int maxFileSizeMB, string[] allowedExtensions, string fileParameterName = "image")
    {
        _maxFileSizeInBytes = maxFileSizeMB * 1024 * 1024;
        _allowedExtensions = allowedExtensions.Select(ext => ext.ToLowerInvariant()).ToArray();
        _fileParameterName = fileParameterName;
        
        // Map extensions to MIME types
        _allowedMimeTypes = allowedExtensions.Select(MapExtensionToMimeType).ToArray();
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Try to get the file from action arguments
        if (!context.ActionArguments.TryGetValue(_fileParameterName, out var fileObj))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = $"No file parameter '{_fileParameterName}' found in request",
                field = _fileParameterName
            });
            return;
        }

        // Check if it's an IFormFile
        if (fileObj is not IFormFile file)
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = $"Parameter '{_fileParameterName}' must be a file",
                field = _fileParameterName
            });
            return;
        }

        // Validate file is not null or empty
        if (file == null || file.Length == 0)
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "No file uploaded or file is empty",
                field = _fileParameterName
            });
            return;
        }

        // Validate file size
        if (file.Length > _maxFileSizeInBytes)
        {
            var maxSizeMB = _maxFileSizeInBytes / (1024.0 * 1024.0);
            var actualSizeMB = file.Length / (1024.0 * 1024.0);
            
            context.Result = new BadRequestObjectResult(new
            {
                error = $"File size exceeds maximum allowed size of {maxSizeMB:F2} MB",
                field = _fileParameterName,
                maxSizeMB = Math.Round(maxSizeMB, 2),
                actualSizeMB = Math.Round(actualSizeMB, 2)
            });
            return;
        }

        // Validate file extension
        var fileExtension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(fileExtension) || !_allowedExtensions.Contains(fileExtension))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "Invalid file format",
                field = _fileParameterName,
                allowedFormats = _allowedExtensions,
                receivedFormat = fileExtension ?? "unknown"
            });
            return;
        }

        // Validate MIME type
        var contentType = file.ContentType?.ToLowerInvariant();
        
        if (string.IsNullOrEmpty(contentType) || !_allowedMimeTypes.Contains(contentType))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "Invalid file content type",
                field = _fileParameterName,
                allowedContentTypes = _allowedMimeTypes,
                receivedContentType = contentType ?? "unknown"
            });
            return;
        }

        // Additional validation: Check file signature (magic numbers)
        if (!IsValidImageSignature(file))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "File appears to be corrupted or is not a valid image",
                field = _fileParameterName
            });
            return;
        }

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Validates image file signature (magic numbers) to ensure it's a real image
    /// </summary>
    private bool IsValidImageSignature(IFormFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[8];
            stream.Read(header, 0, header.Length);

            // Check for common image file signatures
            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                return true;

            // GIF: 47 49 46 38
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                return true;

            // BMP: 42 4D
            if (header[0] == 0x42 && header[1] == 0x4D)
                return true;

            // WebP: 52 49 46 46 ... 57 45 42 50
            if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
            {
                // Additional check for WEBP at offset 8
                stream.Seek(8, SeekOrigin.Begin);
                var webpCheck = new byte[4];
                stream.Read(webpCheck, 0, 4);
                if (webpCheck[0] == 0x57 && webpCheck[1] == 0x45 && webpCheck[2] == 0x42 && webpCheck[3] == 0x50)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Maps file extension to MIME type
    /// </summary>
    private string MapExtensionToMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
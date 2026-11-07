using SkillBridge.Domain.Common;

namespace SkillBridge.Application.Applications;

public static class CvFileValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".docx"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/octet-stream",
        "application/msword"
    };

    public static void Validate(string fileName, long length, Stream stream, string? contentType)
    {
        if (length <= 0)
        {
            throw new BusinessRuleException(
                "validation.empty_file",
                "Empty file",
                "The uploaded CV file is empty.");
        }

        if (length > MaxFileSizeBytes)
        {
            throw new BusinessRuleException(
                "validation.file_too_large",
                "File too large",
                "CV file size cannot exceed 5 MB.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException(
                "validation.invalid_file_type",
                "Invalid file type",
                "Only PDF and DOCX files are allowed.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedContentTypes.Contains(contentType))
        {
            throw new BusinessRuleException(
                "validation.invalid_file_type",
                "Invalid file format",
                "Only PDF and DOCX files are allowed.");
        }

        // Validate magic numbers / file signatures
        ValidateMagicBytes(extension.ToLowerInvariant(), stream);
    }

    private static void ValidateMagicBytes(string extension, Stream stream)
    {
        if (!stream.CanSeek)
            return;

        var originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            var header = new byte[4];
            var bytesRead = stream.Read(header, 0, 4);

            if (bytesRead < 4)
            {
                throw new BusinessRuleException(
                    "validation.invalid_file_type",
                    "Invalid file",
                    "The uploaded file is not a valid document.");
            }

            if (extension == ".pdf")
            {
                // PDF magic number: %PDF (0x25, 0x50, 0x44, 0x46)
                if (header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
                {
                    throw new BusinessRuleException(
                        "validation.invalid_file_type",
                        "Invalid PDF",
                        "The uploaded file does not match a valid PDF document structure.");
                }
            }
            else if (extension == ".docx")
            {
                // DOCX is a zip archive starting with PK\x03\x04 (0x50, 0x4B, 0x03, 0x04)
                if (header[0] != 0x50 || header[1] != 0x4B || header[2] != 0x03 || header[3] != 0x04)
                {
                    throw new BusinessRuleException(
                        "validation.invalid_file_type",
                        "Invalid DOCX",
                        "The uploaded file does not match a valid Word (DOCX) document structure.");
                }
            }
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }
}


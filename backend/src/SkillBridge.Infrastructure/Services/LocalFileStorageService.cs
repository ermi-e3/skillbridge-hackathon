using Microsoft.Extensions.Configuration;
using SkillBridge.Application.Common.Interfaces;

namespace SkillBridge.Infrastructure.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;

    public LocalFileStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["FileStorage:CvUploadDirectory"];
        _storageDirectory = !string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.Combine(AppContext.BaseDirectory, "uploads", "cvs");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SaveFileAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var destinationPath = Path.Combine(_storageDirectory, uniqueFileName);

        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return uniqueFileName;
    }

    public Task<Stream?> GetFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_storageDirectory, safeFileName);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_storageDirectory, safeFileName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string fileName)
    {
        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.Combine(_storageDirectory, safeFileName);
        return File.Exists(fullPath);
    }
}


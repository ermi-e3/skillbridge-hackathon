namespace SkillBridge.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream stream, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> GetFileAsync(string fileName, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileName, CancellationToken cancellationToken = default);
    bool FileExists(string fileName);
}


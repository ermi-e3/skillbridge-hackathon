using SkillBridge.Application.Applications;

namespace SkillBridge.Application.CandidateProfiles;

public interface ICandidateProfileService
{
    Task<CandidateProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<CandidateProfileResponse> UpdateMyProfileAsync(
        UpdateCandidateProfileRequest request, CancellationToken cancellationToken = default);

    Task<CandidateProfileResponse> UploadDefaultCvAsync(
        Stream cvStream, string originalFileName, string contentType, long fileSizeBytes, CancellationToken cancellationToken = default);

    Task<CvDownloadResult> GetDefaultCvFileAsync(CancellationToken cancellationToken = default);

    Task RemoveDefaultCvAsync(CancellationToken cancellationToken = default);
}

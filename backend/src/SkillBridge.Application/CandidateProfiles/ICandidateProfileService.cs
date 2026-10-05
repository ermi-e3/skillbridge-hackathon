namespace SkillBridge.Application.CandidateProfiles;

public interface ICandidateProfileService
{
    Task<CandidateProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<CandidateProfileResponse> UpdateMyProfileAsync(
        UpdateCandidateProfileRequest request, CancellationToken cancellationToken = default);
}

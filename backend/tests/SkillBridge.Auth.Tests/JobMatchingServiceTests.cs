using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Enums;
using Xunit;

namespace SkillBridge.Auth.Tests;

public sealed class JobMatchingServiceTests
{
    private readonly JobMatchingService _sut = new();

    [Fact]
    public void CalculateMatch_PerfectMatch_Returns100Percent()
    {
        // Arrange
        var jobCriteria = new JobMatchCriteria(
            JobId: 1,
            Title: "Senior .NET Developer",
            Location: "San Francisco, CA",
            JobType: JobType.FullTime,
            WorkArrangement: WorkArrangement.Hybrid,
            ExperienceLevel: ExperienceLevel.Senior,
            MinExperienceYears: 5,
            Skills: new List<JobSkillRequirement>
            {
                new(1, "C#", IsRequired: true, MinProficiency: SkillProficiency.Advanced),
                new(2, ".NET Core", IsRequired: true, MinProficiency: SkillProficiency.Advanced),
                new(3, "Docker", IsRequired: false, MinProficiency: SkillProficiency.Intermediate)
            });

        var candidate = new CandidateMatchProfile(
            UserId: "cand-1",
            Location: "San Francisco, CA",
            ExperienceLevel: ExperienceLevel.Senior,
            YearsOfExperience: 6,
            PreferredWorkArrangement: WorkArrangement.Hybrid,
            PreferredJobType: JobType.FullTime,
            Skills: new List<CandidateSkillProfile>
            {
                new(1, "C#", SkillProficiency.Advanced, 6),
                new(2, ".NET Core", SkillProficiency.Expert, 6),
                new(3, "Docker", SkillProficiency.Intermediate, 3)
            },
            ExperienceHistory: new List<CandidateExperienceSummary>
            {
                new("Senior Software Engineer", "TechCorp", 36),
                new("Software Engineer", "Acme Inc", 36)
            });

        // Act
        var result = _sut.CalculateMatch(jobCriteria, candidate);

        // Assert
        Assert.Equal(100, result.Percent);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(50, result.Breakdown.RequiredSkillsScore);
        Assert.Equal(15, result.Breakdown.PreferredSkillsScore);
        Assert.Equal(15, result.Breakdown.ExperienceScore);
        Assert.Equal(10, result.Breakdown.WorkArrangementScore);
        Assert.Equal(5, result.Breakdown.LocationScore);
        Assert.Equal(5, result.Breakdown.JobTypeScore);
        Assert.Equal(2, result.MatchedSkills.Count);
        Assert.Empty(result.MissingSkills);
        Assert.Single(result.MatchedPreferredSkills!);
        Assert.Empty(result.MissingPreferredSkills!);
    }

    [Fact]
    public void CalculateMatch_LowerProficiency_ReducesRequiredScore()
    {
        // Arrange: Job requires Expert proficiency, candidate is Beginner (2 levels below -> 0.70x)
        var jobCriteria = new JobMatchCriteria(
            JobId: 1,
            Title: "Backend Lead",
            Location: null,
            JobType: JobType.FullTime,
            WorkArrangement: WorkArrangement.Remote,
            ExperienceLevel: ExperienceLevel.Lead,
            MinExperienceYears: 8,
            Skills: new List<JobSkillRequirement>
            {
                new(1, "C#", IsRequired: true, MinProficiency: SkillProficiency.Expert)
            });

        var candidate = new CandidateMatchProfile(
            UserId: "cand-2",
            Location: "Remote",
            ExperienceLevel: ExperienceLevel.Lead,
            YearsOfExperience: 8,
            PreferredWorkArrangement: WorkArrangement.Remote,
            PreferredJobType: JobType.FullTime,
            Skills: new List<CandidateSkillProfile>
            {
                new(1, "C#", SkillProficiency.Beginner, 1)
            });

        // Act
        var result = _sut.CalculateMatch(jobCriteria, candidate);

        // Assert: Job has no preferred skills, so required skills max is 65.
        // With 0.7 multiplier: 65 * 0.7 = 45.5 rounded to 46.
        Assert.NotNull(result.Breakdown);
        Assert.True(result.Breakdown.RequiredSkillsScore < 65);
        Assert.Equal(46, result.Breakdown.RequiredSkillsScore);
    }

    [Fact]
    public void CalculateMatch_WorkArrangement_RemoteJobIsFullyCompatibleForAnyPreference()
    {
        // Arrange: Remote job with candidate preferring OnSite
        var jobCriteria = new JobMatchCriteria(
            JobId: 1,
            Title: "Cloud Engineer",
            Location: "New York",
            JobType: JobType.FullTime,
            WorkArrangement: WorkArrangement.Remote,
            ExperienceLevel: ExperienceLevel.MidLevel,
            MinExperienceYears: 3,
            Skills: new List<JobSkillRequirement>
            {
                new(1, "AWS", IsRequired: true, MinProficiency: SkillProficiency.Intermediate)
            });

        var candidate = new CandidateMatchProfile(
            UserId: "cand-3",
            Location: "Los Angeles",
            ExperienceLevel: ExperienceLevel.MidLevel,
            YearsOfExperience: 3,
            PreferredWorkArrangement: WorkArrangement.OnSite,
            PreferredJobType: JobType.FullTime,
            Skills: new List<CandidateSkillProfile>
            {
                new(1, "AWS", SkillProficiency.Intermediate, 3)
            });

        // Act
        var result = _sut.CalculateMatch(jobCriteria, candidate);

        // Assert
        Assert.NotNull(result.Breakdown);
        Assert.Equal(10, result.Breakdown.WorkArrangementScore);
        Assert.Equal(5, result.Breakdown.LocationScore); // Remote jobs give 100% location compatibility
    }

    [Fact]
    public void CalculateMatch_ZeroRequiredSkillsMatched_CapsScoreAt25Percent()
    {
        // Arrange: Job requires 3 skills, candidate has none of them but matches everything else
        var jobCriteria = new JobMatchCriteria(
            JobId: 1,
            Title: "Go Developer",
            Location: "Austin, TX",
            JobType: JobType.FullTime,
            WorkArrangement: WorkArrangement.OnSite,
            ExperienceLevel: ExperienceLevel.Senior,
            MinExperienceYears: 5,
            Skills: new List<JobSkillRequirement>
            {
                new(1, "Go", IsRequired: true, MinProficiency: SkillProficiency.Advanced),
                new(2, "Kubernetes", IsRequired: true, MinProficiency: SkillProficiency.Advanced),
                new(3, "gRPC", IsRequired: true, MinProficiency: SkillProficiency.Intermediate)
            });

        var candidate = new CandidateMatchProfile(
            UserId: "cand-4",
            Location: "Austin, TX",
            ExperienceLevel: ExperienceLevel.Senior,
            YearsOfExperience: 6,
            PreferredWorkArrangement: WorkArrangement.OnSite,
            PreferredJobType: JobType.FullTime,
            Skills: new List<CandidateSkillProfile>
            {
                new(99, "PHP", SkillProficiency.Advanced, 6)
            });

        // Act
        var result = _sut.CalculateMatch(jobCriteria, candidate);

        // Assert: Cap rule triggers
        Assert.True(result.Percent <= 25);
        Assert.NotNull(result.Breakdown);
        Assert.Equal(0, result.Breakdown.RequiredSkillsScore);
        Assert.Equal(3, result.MissingSkills.Count);
    }

    [Fact]
    public void CalculateMatch_NullCandidateProfile_ReturnsLowScoreSafely()
    {
        // Arrange
        var jobCriteria = new JobMatchCriteria(
            JobId: 1,
            Title: "Frontend Developer",
            Location: "Berlin",
            JobType: JobType.FullTime,
            WorkArrangement: WorkArrangement.Hybrid,
            ExperienceLevel: ExperienceLevel.Junior,
            MinExperienceYears: 1,
            Skills: new List<JobSkillRequirement>
            {
                new(1, "React", IsRequired: true, MinProficiency: SkillProficiency.Intermediate)
            });

        var emptyCandidate = new CandidateMatchProfile(
            UserId: "cand-none",
            Location: null,
            ExperienceLevel: ExperienceLevel.EntryLevel,
            YearsOfExperience: 0,
            PreferredWorkArrangement: WorkArrangement.Remote,
            PreferredJobType: JobType.FullTime,
            Skills: new List<CandidateSkillProfile>());

        // Act
        var result = _sut.CalculateMatch(jobCriteria, emptyCandidate);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Percent < 30);
        Assert.Single(result.MissingSkills);
        Assert.Empty(result.MatchedSkills);
    }

    [Fact]
    public void CalculateMatch_CandidateWithIncompleteProfile_ReturnsZeroPercent()
    {
        var job = new SkillBridge.Domain.Entities.Job(
            employerId: "emp-1",
            title: "Frontend Developer",
            description: "Build UI",
            location: "Berlin",
            requiredSkillIds: new[] { 1 },
            createdAt: DateTime.UtcNow,
            jobType: JobType.FullTime,
            workArrangement: WorkArrangement.Hybrid,
            experienceLevel: ExperienceLevel.Junior,
            minExperienceYears: 1);

        var newProfile = new SkillBridge.Domain.Entities.CandidateProfile("cand-new"); // Newly registered candidate with no defaults

        var result = _sut.CalculateMatch(job, newProfile);

        Assert.NotNull(result);
        Assert.Equal(0, result.Percent);
        Assert.Equal("Profile Incomplete", result.Breakdown?.ExperienceCompatibility);
    }
}

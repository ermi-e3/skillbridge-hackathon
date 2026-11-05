using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class ConcurrentInteractionsIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Candidate_and_Employer_simultaneous_sessions_and_concurrent_interactions_reflect_consistently()
    {
        // 1. Concurrent Registration & Login of both Candidate and Employer
        var employerEmail = $"employer-{Guid.NewGuid():N}@concurrent.test";
        var candidateEmail = $"candidate-{Guid.NewGuid():N}@concurrent.test";
        const string password = "TestPassword123!";

        using var employerClient = fixture.Factory.CreateClient();
        using var candidateClient = fixture.Factory.CreateClient();

        var employerRegisterTask = employerClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = employerEmail,
            password,
            fullName = "Abebe Employer",
            role = Roles.Employer,
            companyName = "Concurrent Tech Solutions"
        });

        var candidateRegisterTask = candidateClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidateEmail,
            password,
            fullName = "Almaz Candidate",
            role = Roles.Candidate
        });

        var registerResponses = await Task.WhenAll(employerRegisterTask, candidateRegisterTask);
        Assert.Equal(HttpStatusCode.OK, registerResponses[0].StatusCode);
        Assert.Equal(HttpStatusCode.OK, registerResponses[1].StatusCode);

        // Simultaneous Login
        var employerLoginTask = employerClient.PostAsJsonAsync("/api/auth/login", new { email = employerEmail, password });
        var candidateLoginTask = candidateClient.PostAsJsonAsync("/api/auth/login", new { email = candidateEmail, password });

        var loginResponses = await Task.WhenAll(employerLoginTask, candidateLoginTask);
        Assert.Equal(HttpStatusCode.OK, loginResponses[0].StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponses[1].StatusCode);

        var employerAuth = await loginResponses[0].Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var candidateAuth = await loginResponses[1].Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        Assert.NotNull(employerAuth?.AccessToken);
        Assert.NotNull(candidateAuth?.AccessToken);

        employerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", employerAuth.AccessToken);
        candidateClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", candidateAuth.AccessToken);

        // 2. Concurrent Actions: Candidate updates profile while Employer posts a job opening
        var candidateUpdateProfileTask = candidateClient.PutAsJsonAsync("/api/candidates/me/profile", new
        {
            headline = "Full Stack .NET & Angular Developer",
            bio = "Building scalable cloud solutions in Ethiopia",
            location = "Addis Ababa",
            experienceLevel = ExperienceLevel.MidLevel,
            yearsOfExperience = 3,
            preferredWorkArrangement = WorkArrangement.Hybrid,
            preferredJobType = JobType.FullTime,
            skillIds = new[] { 1, 3 }, // ASP.NET Core, C#
            preferredSkillIds = new[] { 2 } // Angular
        });

        var employerCreateJobTask = employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title = "Senior Full-Stack Engineer",
            description = "We are seeking a high-performing engineer to join our team in Addis Ababa.",
            location = "Addis Ababa",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Hybrid,
            experienceLevel = ExperienceLevel.MidLevel,
            minExperienceYears = 2,
            requiredSkillIds = new[] { 1, 3 }, // ASP.NET Core, C#
            preferredSkillIds = new[] { 2 } // Angular
        });

        var phase1Responses = await Task.WhenAll(candidateUpdateProfileTask, employerCreateJobTask);
        Assert.Equal(HttpStatusCode.OK, phase1Responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.Created, phase1Responses[1].StatusCode);

        var createdJob = await phase1Responses[1].Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(createdJob);
        var jobId = createdJob.Id;

        // 3. Concurrent Verification:
        // - Candidate discovers the new job in the Catalog/Dashboard and views Job Details
        // - Employer checks their jobs list
        var candidateGetJobsTask = candidateClient.GetFromJsonAsync<PagedJobsResponse>("/api/jobs", JsonOptions);
        var candidateGetJobDetailTask = candidateClient.GetFromJsonAsync<JobDto>($"/api/jobs/{jobId}", JsonOptions);
        var employerGetJobsTask = employerClient.GetFromJsonAsync<List<EmployerJobDto>>("/api/employer/jobs", JsonOptions);

        await Task.WhenAll(candidateGetJobsTask, candidateGetJobDetailTask, employerGetJobsTask);

        var candidateJobs = await candidateGetJobsTask;
        var candidateJobDetail = await candidateGetJobDetailTask;
        var employerJobs = await employerGetJobsTask;

        // Candidate sees job with match score and not yet applied
        Assert.NotNull(candidateJobs);
        var jobInCatalog = candidateJobs.Items.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(jobInCatalog);
        Assert.Equal("Concurrent Tech Solutions", jobInCatalog.CompanyName);
        Assert.Equal(false, jobInCatalog.HasApplied);
        Assert.Null(jobInCatalog.ApplicationStatus);
        Assert.True(jobInCatalog.MyMatchPercent >= 80);

        Assert.NotNull(candidateJobDetail);
        Assert.Equal(false, candidateJobDetail.HasApplied);
        Assert.Null(candidateJobDetail.ApplicationStatus);
        Assert.Equal(JobType.FullTime, candidateJobDetail.JobType);
        Assert.Equal(WorkArrangement.Hybrid, candidateJobDetail.WorkArrangement);

        // Employer sees the job with 0 applicants
        Assert.NotNull(employerJobs);
        var employerJob = employerJobs.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(employerJob);
        Assert.Equal(0, employerJob.ApplicantCounts.Total);
        Assert.Equal(0, employerJob.ApplicantCounts.Received);
        Assert.Equal(0, employerJob.ApplicantCounts.Withdrawn);
        Assert.Equal(JobType.FullTime, employerJob.JobType);
        Assert.Equal(WorkArrangement.Hybrid, employerJob.WorkArrangement);

        // 4. Candidate Applies to the Job
        var applyResponse = await candidateClient.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new ApplyRequest(
            "I have 3 years experience building .NET and Angular systems."
        ));
        Assert.Equal(HttpStatusCode.Created, applyResponse.StatusCode);
        var application = await applyResponse.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(application);
        var applicationId = application.Id;
        Assert.Equal("Received", application.Status);

        // 5. Concurrent Bidirectional Verification after Application:
        // - Candidate checks Job Detail, Dashboard, and My Applications
        // - Employer checks Employer Jobs and Job Applicants list
        var candidateCheckDetailTask = candidateClient.GetFromJsonAsync<JobDto>($"/api/jobs/{jobId}", JsonOptions);
        var candidateCheckAppsTask = candidateClient.GetFromJsonAsync<List<ApplicationDto>>("/api/candidates/me/applications", JsonOptions);
        var candidateCheckCatalogTask = candidateClient.GetFromJsonAsync<PagedJobsResponse>("/api/jobs", JsonOptions);
        var employerCheckJobsTask = employerClient.GetFromJsonAsync<List<EmployerJobDto>>("/api/employer/jobs", JsonOptions);
        var employerCheckApplicantsTask = employerClient.GetFromJsonAsync<JobApplicantsDto>($"/api/jobs/{jobId}/applications", JsonOptions);

        await Task.WhenAll(
            candidateCheckDetailTask,
            candidateCheckAppsTask,
            candidateCheckCatalogTask,
            employerCheckJobsTask,
            employerCheckApplicantsTask);

        // Candidate verification
        var updatedJobDetail = await candidateCheckDetailTask;
        Assert.NotNull(updatedJobDetail);
        Assert.True(updatedJobDetail.HasApplied);
        Assert.Equal("Received", updatedJobDetail.ApplicationStatus);
        Assert.Equal(applicationId, updatedJobDetail.ApplicationId);

        var candidateApps = await candidateCheckAppsTask;
        Assert.NotNull(candidateApps);
        var myApp = candidateApps.FirstOrDefault(a => a.Id == applicationId);
        Assert.NotNull(myApp);
        Assert.Equal("Received", myApp.Status);

        var catalogAfterApply = await candidateCheckCatalogTask;
        var catalogJobAfterApply = catalogAfterApply?.Items.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(catalogJobAfterApply);
        Assert.True(catalogJobAfterApply.HasApplied);
        Assert.Equal("Received", catalogJobAfterApply.ApplicationStatus);

        // Employer verification
        var employerJobsAfterApply = await employerCheckJobsTask;
        var empJobAfterApply = employerJobsAfterApply?.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(empJobAfterApply);
        Assert.Equal(1, empJobAfterApply.ApplicantCounts.Total);
        Assert.Equal(1, empJobAfterApply.ApplicantCounts.Received);
        Assert.Equal(0, empJobAfterApply.ApplicantCounts.Withdrawn);

        var applicantsDto = await employerCheckApplicantsTask;
        Assert.NotNull(applicantsDto);
        Assert.Single(applicantsDto.Applicants);
        var applicantItem = applicantsDto.Applicants[0];
        Assert.Equal(applicationId, applicantItem.ApplicationId);
        Assert.Equal("Received", applicantItem.Status);
        Assert.Equal("Almaz Candidate", applicantItem.Candidate.FullName);
        Assert.Equal("I have 3 years experience building .NET and Angular systems.", applicantItem.CoverNote);

        // 6. Candidate Withdraws Application
        var withdrawResponse = await candidateClient.PatchAsync($"/api/applications/{applicationId}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawResponse.StatusCode);
        var withdrawnApp = await withdrawResponse.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(withdrawnApp);
        Assert.Equal("Withdrawn", withdrawnApp.Status);

        // 7. Concurrent Verification after Withdrawal:
        // - Candidate views Job Detail, Catalog, and My Applications -> All reflect "Withdrawn"
        // - Employer views Employer Jobs and Applicants -> All reflect "Withdrawn"
        var candidatePostWithdrawDetailTask = candidateClient.GetFromJsonAsync<JobDto>($"/api/jobs/{jobId}", JsonOptions);
        var candidatePostWithdrawAppsTask = candidateClient.GetFromJsonAsync<List<ApplicationDto>>("/api/candidates/me/applications", JsonOptions);
        var candidatePostWithdrawCatalogTask = candidateClient.GetFromJsonAsync<PagedJobsResponse>("/api/jobs", JsonOptions);
        var employerPostWithdrawJobsTask = employerClient.GetFromJsonAsync<List<EmployerJobDto>>("/api/employer/jobs", JsonOptions);
        var employerPostWithdrawApplicantsTask = employerClient.GetFromJsonAsync<JobApplicantsDto>($"/api/jobs/{jobId}/applications", JsonOptions);

        await Task.WhenAll(
            candidatePostWithdrawDetailTask,
            candidatePostWithdrawAppsTask,
            candidatePostWithdrawCatalogTask,
            employerPostWithdrawJobsTask,
            employerPostWithdrawApplicantsTask);

        // Candidate verification:
        var postWithdrawDetail = await candidatePostWithdrawDetailTask;
        Assert.NotNull(postWithdrawDetail);
        Assert.True(postWithdrawDetail.HasApplied);
        Assert.Equal("Withdrawn", postWithdrawDetail.ApplicationStatus);

        var postWithdrawApps = await candidatePostWithdrawAppsTask;
        var myWithdrawnApp = postWithdrawApps?.FirstOrDefault(a => a.Id == applicationId);
        Assert.NotNull(myWithdrawnApp);
        Assert.Equal("Withdrawn", myWithdrawnApp.Status);

        var postWithdrawCatalog = await candidatePostWithdrawCatalogTask;
        var myWithdrawnCatalogJob = postWithdrawCatalog?.Items.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(myWithdrawnCatalogJob);
        Assert.True(myWithdrawnCatalogJob.HasApplied);
        Assert.Equal("Withdrawn", myWithdrawnCatalogJob.ApplicationStatus);

        // Employer verification:
        var employerJobsPostWithdraw = await employerPostWithdrawJobsTask;
        var empJobPostWithdraw = employerJobsPostWithdraw?.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(empJobPostWithdraw);
        Assert.Equal(1, empJobPostWithdraw.ApplicantCounts.Total);
        Assert.Equal(0, empJobPostWithdraw.ApplicantCounts.Received);
        Assert.Equal(1, empJobPostWithdraw.ApplicantCounts.Withdrawn); // Withdrawn count incremented

        var applicantsPostWithdraw = await employerPostWithdrawApplicantsTask;
        Assert.NotNull(applicantsPostWithdraw);
        Assert.Single(applicantsPostWithdraw.Applicants);
        Assert.Equal("Withdrawn", applicantsPostWithdraw.Applicants[0].Status);

        // 8. Multi-candidate concurrent lifecycle: Candidate 2 applies & Employer shortlists
        var candidate2Email = $"candidate2-{Guid.NewGuid():N}@concurrent.test";
        using var candidate2Client = fixture.Factory.CreateClient();

        var reg2 = await candidate2Client.PostAsJsonAsync("/api/auth/register", new
        {
            email = candidate2Email,
            password,
            fullName = "Dawit SecondCandidate",
            role = Roles.Candidate
        });
        Assert.Equal(HttpStatusCode.OK, reg2.StatusCode);
        var auth2 = await reg2.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        candidate2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.AccessToken);

        // Candidate 2 sets profile skills
        var profile2Response = await candidate2Client.PutAsJsonAsync("/api/candidates/me/profile", new
        {
            headline = "Junior .NET Developer",
            location = "Addis Ababa",
            skillIds = new[] { 1 } // ASP.NET Core
        });
        Assert.Equal(HttpStatusCode.OK, profile2Response.StatusCode);

        // Candidate 2 applies
        var apply2Response = await candidate2Client.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new ApplyRequest(
            "Hello, I am Candidate 2 applying to this position."
        ));
        Assert.Equal(HttpStatusCode.Created, apply2Response.StatusCode);
        var app2 = await apply2Response.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app2);

        // Employer shortlists Candidate 2
        var shortlistResponse = await employerClient.PatchAsJsonAsync($"/api/applications/{app2.Id}/status", new ChangeApplicationStatusRequest("Shortlisted"));
        Assert.Equal(HttpStatusCode.OK, shortlistResponse.StatusCode);

        // Concurrently verify Candidate 2 and Employer views
        var candidate2AppsTask = candidate2Client.GetFromJsonAsync<List<ApplicationDto>>("/api/candidates/me/applications", JsonOptions);
        var finalEmployerJobsTask = employerClient.GetFromJsonAsync<List<EmployerJobDto>>("/api/employer/jobs", JsonOptions);
        var finalApplicantsTask = employerClient.GetFromJsonAsync<JobApplicantsDto>($"/api/jobs/{jobId}/applications", JsonOptions);

        await Task.WhenAll(candidate2AppsTask, finalEmployerJobsTask, finalApplicantsTask);

        var candidate2Apps = await candidate2AppsTask;
        var myApp2 = candidate2Apps?.FirstOrDefault(a => a.Id == app2.Id);
        Assert.NotNull(myApp2);
        Assert.Equal("Shortlisted", myApp2.Status);

        var finalEmpJobs = await finalEmployerJobsTask;
        var finalJob = finalEmpJobs?.FirstOrDefault(j => j.Id == jobId);
        Assert.NotNull(finalJob);
        Assert.Equal(2, finalJob.ApplicantCounts.Total);
        Assert.Equal(0, finalJob.ApplicantCounts.Received);
        Assert.Equal(1, finalJob.ApplicantCounts.Shortlisted);
        Assert.Equal(1, finalJob.ApplicantCounts.Withdrawn);

        var finalApplicants = await finalApplicantsTask;
        Assert.NotNull(finalApplicants);
        Assert.Equal(2, finalApplicants.Applicants.Count);
        Assert.Contains(finalApplicants.Applicants, a => a.ApplicationId == applicationId && a.Status == "Withdrawn");
        Assert.Contains(finalApplicants.Applicants, a => a.ApplicationId == app2.Id && a.Status == "Shortlisted");
    }

    private sealed record AuthResponse(string UserId, string AccessToken, string? Role);
}

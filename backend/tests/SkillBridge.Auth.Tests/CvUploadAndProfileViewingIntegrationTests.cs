using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using SkillBridge.Application.Applications;
using SkillBridge.Application.Auth;
using SkillBridge.Application.CandidateProfiles;
using SkillBridge.Application.Jobs;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Enums;
using Xunit;

namespace SkillBridge.Auth.Tests;

[Collection(AuthCollection.Name)]
public sealed class CvUploadAndProfileViewingIntegrationTests(AuthFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static byte[] CreateFakePdfBytes(string content = "Sample PDF test content")
    {
        var header = "%PDF-1.4\n";
        var body = $"1 0 obj\n<< /Title ({content}) >>\nendobj\n";
        var footer = "%%EOF";
        return Encoding.UTF8.GetBytes(header + body + footer);
    }

    private static byte[] CreateFakeDocxBytes(string content = "Sample DOCX test content")
    {
        // PK\x03\x04 header
        var header = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var body = Encoding.UTF8.GetBytes(content);
        var result = new byte[header.Length + body.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(body, 0, result, header.Length, body.Length);
        return result;
    }

    private static MultipartFormDataContent CreateMultipartForm(string? coverNote, string fileName, byte[] fileBytes, string contentType)
    {
        var form = new MultipartFormDataContent();
        if (!string.IsNullOrEmpty(coverNote))
        {
            form.Add(new StringContent(coverNote), "coverNote");
        }
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "cv", fileName);
        return form;
    }

    private async Task<(HttpClient client, string email)> CreateEmployerAsync(string company = "Acme Corp")
    {
        var client = fixture.Factory.CreateClient();
        var email = $"emp-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName = "Employer User",
            role = Roles.Employer,
            companyName = company
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, email);
    }

    private async Task<(HttpClient client, string email, string userId)> CreateCandidateAsync(string fullName = "Candidate User")
    {
        var client = fixture.Factory.CreateClient();
        var email = $"cand-{Guid.NewGuid():N}@test.com";
        const string password = "TestPassword123!";

        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            fullName,
            role = Roles.Candidate
        });

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Populate candidate profile with skills and experience
        await client.PutAsJsonAsync("/api/candidates/me/profile", new
        {
            headline = "Full Stack .NET & Angular Developer",
            bio = "5 years developing enterprise SaaS platforms.",
            skillIds = new[] { 1, 2 }, // ASP.NET Core, Angular
            location = "London, UK",
            experienceLevel = ExperienceLevel.Senior,
            yearsOfExperience = 5,
            preferredWorkArrangement = WorkArrangement.Remote,
            preferredJobType = JobType.FullTime,
            experienceHistory = new[]
            {
                new
                {
                    title = "Senior Software Engineer",
                    company = "Tech Innovations Ltd",
                    location = "London",
                    startDate = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    endDate = (DateTime?)null,
                    isCurrent = true,
                    description = "Lead backend engineer building microservices."
                }
            }
        });

        return (client, email, auth!.UserId);
    }

    private async Task<JobDto> CreateJobAsync(HttpClient employerClient, string title = "Senior Backend Engineer")
    {
        var res = await employerClient.PostAsJsonAsync("/api/jobs", new
        {
            title,
            description = "Develop high scalability APIs in .NET 10.",
            location = "Remote",
            jobType = JobType.FullTime,
            workArrangement = WorkArrangement.Remote,
            experienceLevel = ExperienceLevel.Senior,
            minExperienceYears = 3,
            requiredSkillIds = new[] { 1 } // ASP.NET Core
        });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var job = await res.Content.ReadFromJsonAsync<JobDto>(JsonOptions);
        Assert.NotNull(job);
        return job;
    }

    [Fact]
    public async Task Candidate_can_apply_with_valid_pdf_cv()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        var pdfBytes = CreateFakePdfBytes("Curriculum Vitae of Jane Doe");
        using var form = CreateMultipartForm("I have 5 years experience with .NET", "jane-doe-cv.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);

        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);
        Assert.True(app.HasCv);
        Assert.Equal("jane-doe-cv.pdf", app.CvOriginalFileName);
        Assert.NotNull(app.CvFileSizeBytes);
        Assert.True(app.CvFileSizeBytes > 0);
        Assert.NotNull(app.CvUploadedAt);
        Assert.Equal($"/api/applications/{app.Id}/cv", app.CvDownloadUrl);
    }

    [Fact]
    public async Task Candidate_can_apply_with_valid_docx_cv()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        var docxBytes = CreateFakeDocxBytes("Resume content in docx");
        using var form = CreateMultipartForm("Check out my DOCX resume", "resume.docx", docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);

        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);
        Assert.True(app.HasCv);
        Assert.Equal("resume.docx", app.CvOriginalFileName);
        Assert.Equal($"/api/applications/{app.Id}/cv", app.CvDownloadUrl);
    }

    [Fact]
    public async Task Candidate_cannot_apply_with_invalid_extension()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        var bytes = Encoding.UTF8.GetBytes("echo malware");
        using var form = CreateMultipartForm("My script", "script.exe", bytes, "application/octet-stream");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_apply_with_oversized_file()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        // 5.5 MB fake PDF (starts with %PDF)
        var oversizedBytes = new byte[5500000];
        var pdfHeader = Encoding.UTF8.GetBytes("%PDF-1.4\n");
        Buffer.BlockCopy(pdfHeader, 0, oversizedBytes, 0, pdfHeader.Length);

        using var form = CreateMultipartForm("Too large file", "large.pdf", oversizedBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_apply_with_spoofed_pdf_signature()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        // Named .pdf but contents are plain text, not starting with %PDF
        var fakeBytes = Encoding.UTF8.GetBytes("Not really a PDF file at all!");
        using var form = CreateMultipartForm("Spoofed", "fake.pdf", fakeBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
    }

    [Fact]
    public async Task Employer_who_owns_job_can_download_applicant_cv()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();
        var pdfBytes = CreateFakePdfBytes("Jane Doe CV Content 12345");
        using var form = CreateMultipartForm("Cover note", "jane.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        // Employer downloads CV
        var cvRes = await empClient.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.OK, cvRes.StatusCode);
        Assert.Equal("application/pdf", cvRes.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await cvRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, downloadedBytes);
    }

    [Fact]
    public async Task Non_owner_employer_cannot_download_applicant_cv()
    {
        var (ownerClient, _) = await CreateEmployerAsync("Owner Corp");
        var job = await CreateJobAsync(ownerClient);

        var (otherEmpClient, _) = await CreateEmployerAsync("Competitor Corp");

        var (candClient, _, _) = await CreateCandidateAsync();
        var pdfBytes = CreateFakePdfBytes("Private Candidate Info");
        using var form = CreateMultipartForm("Cover note", "private.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        // Competitor tries to download CV -> 403 Forbidden
        var cvRes = await otherEmpClient.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.Forbidden, cvRes.StatusCode);
    }

    [Fact]
    public async Task Candidate_who_applied_can_download_their_own_cv_but_other_candidates_cannot()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient1, _, _) = await CreateCandidateAsync("Candidate 1");
        var (candClient2, _, _) = await CreateCandidateAsync("Candidate 2");

        var pdfBytes = CreateFakePdfBytes("Candidate 1 CV");
        using var form = CreateMultipartForm("Note", "cand1.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient1.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        // Candidate 1 downloads own CV -> 200 OK
        var ownCvRes = await candClient1.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.OK, ownCvRes.StatusCode);

        // Candidate 2 tries to download Candidate 1's CV -> 403 Forbidden
        var otherCvRes = await candClient2.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.Forbidden, otherCvRes.StatusCode);
    }

    [Fact]
    public async Task Employer_who_owns_job_can_view_full_applicant_profile()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, candUserId) = await CreateCandidateAsync("Alice Engineer");
        var pdfBytes = CreateFakePdfBytes("Alice CV content");
        using var form = CreateMultipartForm("Hello from Alice", "alice.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        // Owner views applicant profile via job endpoint
        var profileRes = await empClient.GetAsync($"/api/jobs/{job.Id}/applicants/{candUserId}/profile");
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);

        var profile = await profileRes.Content.ReadFromJsonAsync<ApplicantProfileDto>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal(candUserId, profile.CandidateId);
        Assert.Equal("Alice Engineer", profile.FullName);
        Assert.Equal("Full Stack .NET & Angular Developer", profile.Headline);
        Assert.Equal("London, UK", profile.Location);
        Assert.NotEmpty(profile.Skills);
        Assert.NotEmpty(profile.ExperienceHistory);
        Assert.Equal("Senior Software Engineer", profile.ExperienceHistory[0].Title);
        Assert.Equal("Tech Innovations Ltd", profile.ExperienceHistory[0].Company);
        Assert.True(profile.HasCv);
        Assert.Equal("alice.pdf", profile.CvOriginalFileName);
        Assert.Equal($"/api/applications/{app.Id}/cv", profile.CvDownloadUrl);

        // Also test the application-based endpoint: /api/applications/{id}/candidate-profile
        var appProfileRes = await empClient.GetAsync($"/api/applications/{app.Id}/candidate-profile");
        Assert.Equal(HttpStatusCode.OK, appProfileRes.StatusCode);
        var appProfile = await appProfileRes.Content.ReadFromJsonAsync<ApplicantProfileDto>(JsonOptions);
        Assert.NotNull(appProfile);
        Assert.Equal(candUserId, appProfile.CandidateId);
    }

    [Fact]
    public async Task Non_owner_employer_cannot_view_applicant_profile()
    {
        var (ownerClient, _) = await CreateEmployerAsync("Owner Corp");
        var job = await CreateJobAsync(ownerClient);

        var (otherEmpClient, _) = await CreateEmployerAsync("Other Corp");

        var (candClient, _, candUserId) = await CreateCandidateAsync("Bob Developer");
        var pdfBytes = CreateFakePdfBytes("Bob CV");
        using var form = CreateMultipartForm("Bob's Note", "bob.pdf", pdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", form);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);

        // Other employer attempts to view Bob's profile on Owner's job -> 403 Forbidden
        var profileRes = await otherEmpClient.GetAsync($"/api/jobs/{job.Id}/applicants/{candUserId}/profile");
        Assert.Equal(HttpStatusCode.Forbidden, profileRes.StatusCode);

        var appProfileRes = await otherEmpClient.GetAsync($"/api/applications/{app.Id}/candidate-profile");
        Assert.Equal(HttpStatusCode.Forbidden, appProfileRes.StatusCode);
    }

    [Fact]
    public async Task Candidate_cannot_apply_without_cv()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync();

        // Attempt JSON apply without CV and without profile default CV -> 400 Bad Request
        var applyRes = await candClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "Applying without CV"
        });

        Assert.Equal(HttpStatusCode.BadRequest, applyRes.StatusCode);
        var raw = await applyRes.Content.ReadAsStringAsync();
        Assert.Contains("applications.cv_required", raw);
        var problem = await applyRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("valid CV", problem.Detail ?? problem.Title ?? "");
    }

    [Fact]
    public async Task Candidate_can_upload_preview_and_delete_default_cv_in_profile()
    {
        var (candClient, _, _) = await CreateCandidateAsync("David Profile");

        // 1. Initially candidate profile has no default CV
        var profileRes = await candClient.GetFromJsonAsync<CandidateProfileResponse>("/api/candidates/me/profile", JsonOptions);
        Assert.NotNull(profileRes);
        Assert.False(profileRes.HasDefaultCv);
        Assert.Null(profileRes.DefaultCvOriginalFileName);

        // 2. Upload default CV
        var pdfBytes = CreateFakePdfBytes("David Default CV Content");
        using var form = CreateMultipartForm(null, "david-default.pdf", pdfBytes, "application/pdf");
        var uploadRes = await candClient.PostAsync("/api/candidates/me/profile/cv", form);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);

        var updatedProfile = await uploadRes.Content.ReadFromJsonAsync<CandidateProfileResponse>(JsonOptions);
        Assert.NotNull(updatedProfile);
        Assert.True(updatedProfile.HasDefaultCv);
        Assert.Equal("david-default.pdf", updatedProfile.DefaultCvOriginalFileName);
        Assert.NotNull(updatedProfile.DefaultCvFileSizeBytes);
        Assert.True(updatedProfile.DefaultCvFileSizeBytes > 0);

        // 3. Download / Preview default CV
        var cvRes = await candClient.GetAsync("/api/candidates/me/profile/cv");
        Assert.Equal(HttpStatusCode.OK, cvRes.StatusCode);
        Assert.Equal("application/pdf", cvRes.Content.Headers.ContentType?.MediaType);
        var downloadedBytes = await cvRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, downloadedBytes);

        // 4. Delete default CV
        var deleteRes = await candClient.DeleteAsync("/api/candidates/me/profile/cv");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // 5. Verify default CV is removed
        var profileAfterDelete = await candClient.GetFromJsonAsync<CandidateProfileResponse>("/api/candidates/me/profile", JsonOptions);
        Assert.NotNull(profileAfterDelete);
        Assert.False(profileAfterDelete.HasDefaultCv);
        Assert.Null(profileAfterDelete.DefaultCvOriginalFileName);
    }

    [Fact]
    public async Task Candidate_can_apply_using_default_cv_from_profile()
    {
        var (empClient, _) = await CreateEmployerAsync("Hiring Manager Corp");
        var job = await CreateJobAsync(empClient, "Full Stack Lead");

        var (candClient, _, _) = await CreateCandidateAsync("Emma Default");

        // Upload default CV to Emma's profile
        var pdfBytes = CreateFakePdfBytes("Emma's Master Profile Resume");
        using var form = CreateMultipartForm(null, "emma-master-resume.pdf", pdfBytes, "application/pdf");
        var uploadRes = await candClient.PostAsync("/api/candidates/me/profile/cv", form);
        Assert.Equal(HttpStatusCode.OK, uploadRes.StatusCode);

        // Apply using Option 1: useDefaultCv = true
        var applyRes = await candClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new
        {
            coverNote = "Applying using my profile default CV",
            useDefaultCv = true
        });

        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);
        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);
        Assert.True(app.HasCv);
        Assert.Equal("emma-master-resume.pdf", app.CvOriginalFileName);
        Assert.NotNull(app.CvFileSizeBytes);

        // Verify employer who owns the job can download the application CV
        var cvRes = await empClient.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.OK, cvRes.StatusCode);
        var downloadedBytes = await cvRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, downloadedBytes);
    }

    [Fact]
    public async Task Candidate_can_apply_using_different_cv_even_when_profile_has_default_cv()
    {
        var (empClient, _) = await CreateEmployerAsync();
        var job = await CreateJobAsync(empClient);

        var (candClient, _, _) = await CreateCandidateAsync("Frank Options");

        // Upload default CV to Frank's profile
        var defaultPdfBytes = CreateFakePdfBytes("Frank Default CV");
        using var defaultForm = CreateMultipartForm(null, "frank-default.pdf", defaultPdfBytes, "application/pdf");
        await candClient.PostAsync("/api/candidates/me/profile/cv", defaultForm);

        // Apply using Option 2: upload a DIFFERENT CV specifically for this application
        var tailoredPdfBytes = CreateFakePdfBytes("Frank Tailored CV for this specific job");
        using var applyForm = CreateMultipartForm("Tailored application", "frank-tailored.pdf", tailoredPdfBytes, "application/pdf");

        var applyRes = await candClient.PostAsync($"/api/jobs/{job.Id}/applications", applyForm);
        Assert.Equal(HttpStatusCode.Created, applyRes.StatusCode);

        var app = await applyRes.Content.ReadFromJsonAsync<ApplicationDto>(JsonOptions);
        Assert.NotNull(app);
        Assert.True(app.HasCv);
        Assert.Equal("frank-tailored.pdf", app.CvOriginalFileName);

        // Verify the tailored CV was stored and downloaded, not the default one
        var cvRes = await empClient.GetAsync($"/api/applications/{app.Id}/cv");
        Assert.Equal(HttpStatusCode.OK, cvRes.StatusCode);
        var downloadedBytes = await cvRes.Content.ReadAsByteArrayAsync();
        Assert.Equal(tailoredPdfBytes, downloadedBytes);
    }
}

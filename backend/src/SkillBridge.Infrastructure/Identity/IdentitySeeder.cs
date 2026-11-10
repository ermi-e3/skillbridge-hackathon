using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkillBridge.Application.Auth;
using SkillBridge.Application.Common.Interfaces;
using SkillBridge.Domain.Common;
using SkillBridge.Domain.Entities;
using SkillBridge.Domain.Enums;
using SkillBridge.Infrastructure.Persistence;
using SkillBridge.Infrastructure.Persistence.Configurations;

namespace SkillBridge.Infrastructure.Identity;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IIdentityService identityService,
    AppDbContext dbContext)
{
    public async Task SeedAsync(bool seedDemoUsers, CancellationToken cancellationToken = default)
    {
        await EnsureRoleAsync(Roles.Candidate, cancellationToken);
        await EnsureRoleAsync(Roles.Employer, cancellationToken);

        if (!seedDemoUsers)
            return;

        await EnsureDemoUserAsync(
            new RegisterRequest("admin@hackathon.local", "Admin123!", "Demo Employer", Roles.Employer, "SkillBridge Enterprise Partners"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("health@hackathon.local", "Health123!", "Dr. Almaz Belay", Roles.Employer, "St. Paul General Hospital & Health Network"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("finance@hackathon.local", "Finance123!", "Dawit Haile", Roles.Employer, "Abyssinia Capital & Financial Group"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("edu@hackathon.local", "Education123!", "Rahel Tadesse", Roles.Employer, "Addis International Academy & Institutes"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("eng@hackathon.local", "Engineer123!", "Eng. Michael Girma", Roles.Employer, "Apex Engineering & Infrastructure Corp"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("hospitality@hackathon.local", "Hospitality123!", "Hanna Solomon", Roles.Employer, "Grand Palace Hotel & Hospitality Group"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("marketing@hackathon.local", "Marketing123!", "Elias Bekele", Roles.Employer, "Horizon Creative Media & Marketing"),
            cancellationToken);

        await EnsureDemoUserAsync(
            new RegisterRequest("user@hackathon.local", "User123!", "Demo Candidate", Roles.Candidate),
            cancellationToken);

        await EnsureSkillsAsync(cancellationToken);
        await SeedDemoDataAsync(cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await roleManager.RoleExistsAsync(roleName))
            return;

        var role = new IdentityRole(roleName);
        try
        {
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
                throw new InvalidOperationException("The required identity roles could not be initialized.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "RoleNameIndex" })
        {
            // Another API instance may have seeded this role after our existence check.
            dbContext.Entry(role).State = EntityState.Detached;
            if (!await roleManager.RoleExistsAsync(roleName))
                throw new InvalidOperationException("The required identity roles could not be initialized.");
        }
    }

    private async Task EnsureDemoUserAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            try
            {
                await identityService.RegisterAsync(request, cancellationToken);
                return;
            }
            catch (BusinessRuleException exception) when (exception.Code == "auth.email_taken")
            {
                // Registration is atomic; a concurrent seeder may already have completed it.
                user = await userManager.FindByEmailAsync(request.Email);
            }
        }

        if (user is null)
            throw new InvalidOperationException("A demo account could not be initialized.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(role => role != request.Role) || roles.Count > 1)
            throw new InvalidOperationException("A demo account has an unexpected role.");

        if (roles.Count == 0)
        {
            var assignment = await userManager.AddToRoleAsync(user, request.Role);
            if (!assignment.Succeeded)
                throw new InvalidOperationException("A demo account role could not be initialized.");
        }

        var hasCandidateProfile = await dbContext.CandidateProfiles
            .AnyAsync(profile => profile.UserId == user.Id, cancellationToken);
        if (request.Role == Roles.Candidate && !hasCandidateProfile)
            dbContext.CandidateProfiles.Add(new CandidateProfile(user.Id));
        else if (request.Role == Roles.Employer && hasCandidateProfile)
            throw new InvalidOperationException("The employer demo account has an unexpected candidate profile.");

        var updateUser = false;
        if (string.IsNullOrWhiteSpace(user.FullName))
        {
            user.FullName = request.FullName;
            updateUser = true;
        }
        if (request.Role == Roles.Employer && string.IsNullOrWhiteSpace(user.CompanyName))
        {
            user.CompanyName = request.CompanyName;
            updateUser = true;
        }
        if (!user.LockoutEnabled)
        {
            user.LockoutEnabled = true;
            updateUser = true;
        }
        if (updateUser && !(await userManager.UpdateAsync(user)).Succeeded)
            throw new InvalidOperationException("A demo account could not be initialized.");

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureSkillsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingSkillNames = await dbContext.Skills
            .Select(s => s.NormalizedName)
            .ToHashSetAsync(cancellationToken);

        var newSkills = new List<Skill>();
        for (var i = 0; i < SkillConfiguration.SeedNames.Length; i++)
        {
            var id = i + 1;
            var name = SkillConfiguration.SeedNames[i];
            var normalized = name.ToUpperInvariant();

            if (!existingSkillNames.Contains(normalized))
            {
                newSkills.Add(new Skill(id, name));
            }
        }

        if (newSkills.Count > 0)
        {
            dbContext.Skills.AddRange(newSkills);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adminUser = await userManager.FindByEmailAsync("admin@hackathon.local");
        var healthUser = await userManager.FindByEmailAsync("health@hackathon.local");
        var financeUser = await userManager.FindByEmailAsync("finance@hackathon.local");
        var eduUser = await userManager.FindByEmailAsync("edu@hackathon.local");
        var engUser = await userManager.FindByEmailAsync("eng@hackathon.local");
        var hospitalityUser = await userManager.FindByEmailAsync("hospitality@hackathon.local");
        var marketingUser = await userManager.FindByEmailAsync("marketing@hackathon.local");
        var candidateUser = await userManager.FindByEmailAsync("user@hackathon.local");

        if (adminUser is null)
            return;

        var existingTitlesList = await dbContext.Jobs
            .Select(j => j.Title)
            .ToListAsync(cancellationToken);
        var existingTitles = new HashSet<string>(existingTitlesList, StringComparer.OrdinalIgnoreCase);

        var isDiverseCatalogSeeded =
            existingTitles.Contains("Registered Nurse (Inpatient Care)") ||
            existingTitles.Contains("Senior Financial Analyst") ||
            existingTitles.Contains("High School STEM Educator") ||
            existingTitles.Contains("Civil Site Engineer");

        if (!isDiverseCatalogSeeded)
        {
            // Upgrade legacy tech-only jobs if they have no candidate applications yet
            var legacyTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Junior Backend Developer",
                "Full-Stack Software Engineer",
                "Frontend Angular Specialist"
            };
            var legacyJobs = await dbContext.Jobs
                .Include(j => j.Applications)
                .Where(j => legacyTitles.Contains(j.Title) && j.Applications.Count == 0)
                .ToListAsync(cancellationToken);

            if (legacyJobs.Count > 0)
            {
                dbContext.Jobs.RemoveRange(legacyJobs);
                await dbContext.SaveChangesAsync(cancellationToken);
                foreach (var removed in legacyJobs)
                    existingTitles.Remove(removed.Title);
            }

            var now = DateTime.UtcNow;
            var jobs = new List<Job>
            {
                // 1. Healthcare: Registered Nurse
                new Job(
                    healthUser?.Id ?? adminUser.Id,
                    "Registered Nurse (Inpatient Care)",
                    "Provide compassionate, evidence-based nursing care to patients within our medical-surgical and intensive care units. Conduct clinical assessments, monitor vital signs, administer prescribed medications, and coordinate patient care plans.",
                    "Addis Ababa",
                    [41, 42, 45, 48], // Patient Care, Clinical Assessment, First Aid & BLS, Infection Control
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 2,
                    preferredSkillIds: [43, 44, 49], // Pharmacology, Medical Records (EHR), Nursing
                    deadline: now.AddDays(14),
                    category: "Healthcare & Medical"),

                // 2. Healthcare: Medical Laboratory Scientist
                new Job(
                    healthUser?.Id ?? adminUser.Id,
                    "Medical Laboratory Scientist",
                    "Perform diagnostic laboratory testing, quality assurance protocols, and clinical specimen evaluations in hematology, microbiology, and clinical chemistry to support healthcare decision-making.",
                    "Addis Ababa",
                    [51, 42, 48], // Medical Laboratory, Clinical Assessment, Infection Control
                    now.AddDays(-3),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [92, 50], // Quality Control, Health Education
                    deadline: now.AddDays(21),
                    category: "Healthcare & Medical"),

                // 3. Finance: Senior Financial Analyst
                new Job(
                    financeUser?.Id ?? adminUser.Id,
                    "Senior Financial Analyst",
                    "Lead corporate financial modeling, annual budgeting cycles, variance analyses, and capital investment appraisals. Prepare executive board decks and advise leadership on strategic risk mitigation.",
                    "Addis Ababa (Hybrid)",
                    [56, 57, 164, 62], // Financial Analysis, Financial Modeling, Microsoft Excel, Budgeting & Forecasting
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [65, 60], // Risk Management, IFRS Compliance
                    deadline: now.AddDays(18),
                    category: "Finance & Accounting"),

                // 4. Finance: Accountant & Tax Associate
                new Job(
                    financeUser?.Id ?? adminUser.Id,
                    "Accountant & Tax Associate",
                    "Manage daily general ledger reconciliations, payroll processing, accounts payable/receivable, and statutory tax compliance filings including VAT and withholding taxes.",
                    "Addis Ababa",
                    [58, 61, 59, 56], // Bookkeeping, Tax Compliance, QuickBooks, Financial Analysis
                    now.AddDays(-4),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 2,
                    preferredSkillIds: [64, 63], // Payroll Management, Auditing
                    deadline: now.AddDays(10),
                    category: "Finance & Accounting"),

                // 5. Education: High School STEM Educator
                new Job(
                    eduUser?.Id ?? adminUser.Id,
                    "High School STEM Educator",
                    "Deliver inspiring science and mathematics instruction to secondary school students. Design interactive lesson plans, maintain positive classroom discipline, and track student academic achievement.",
                    "Addis Ababa",
                    [72, 73, 71, 75], // Classroom Management, Lesson Planning, Curriculum Development, Student Assessment
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [74, 77], // Educational Technology, Mentoring
                    deadline: now.AddDays(25),
                    category: "Education & Training"),

                // 6. Education: Instructional Designer & E-Learning Coordinator
                new Job(
                    eduUser?.Id ?? adminUser.Id,
                    "Instructional Designer & E-Learning Coordinator",
                    "Develop engaging blended-learning courseware, interactive digital modules, and professional development workshops for faculty and students across our learning management system.",
                    "Remote",
                    [76, 74, 71], // Instructional Design, Educational Technology, Curriculum Development
                    now.AddDays(-1),
                    JobType.Contract,
                    WorkArrangement.Remote,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 2,
                    preferredSkillIds: [81, 168], // E-Learning, Technical Writing
                    deadline: now.AddDays(12),
                    category: "Education & Training"),

                // 7. Engineering: Civil Site Engineer
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Civil Site Engineer",
                    "Supervise structural construction operations, enforce site health and safety standards, review architectural drawings, and ensure structural compliance with engineering specifications.",
                    "Hawassa",
                    [87, 86, 94, 95], // Civil Engineering, AutoCAD, Site Supervision, Safety Compliance
                    now.AddDays(-5),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [91, 88], // Project Estimation, Structural Analysis
                    deadline: now.AddDays(15),
                    category: "Engineering & Architecture"),

                // 8. Engineering: Electrical Systems Maintenance Technician
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Electrical Systems Maintenance Technician",
                    "Inspect, troubleshoot, and maintain industrial electrical distribution panels, backup generators, lighting grids, and commercial wiring systems in accordance with safety codes.",
                    "Dire Dawa",
                    [89, 150, 153, 154], // Electrical Systems, Electrical Installation, Facilities Maintenance, Workplace Safety (OSHA)
                    now.AddDays(-3),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 2,
                    preferredSkillIds: [92], // Quality Control
                    deadline: now.AddDays(8),
                    category: "Logistics & Skilled Trades"),

                // 9. Business: Operations & HR Generalist
                new Job(
                    adminUser.Id,
                    "Operations & HR Generalist",
                    "Oversee day-to-day organizational operations, talent recruitment pipelines, employee onboarding, office administration, and workplace compliance policies.",
                    "Addis Ababa (Hybrid)",
                    [104, 101, 107, 106], // Human Resources (HR), Operations Management, Talent Acquisition, Office Administration
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [108, 109], // Performance Management, Contract Negotiation
                    deadline: now.AddDays(20),
                    category: "Business & Operations"),

                // 10. Business: Project Manager (Strategic Initiatives)
                new Job(
                    adminUser.Id,
                    "Project Manager (Strategic Initiatives)",
                    "Lead cross-functional project lifecycles from initiation to delivery. Define project scopes, monitor milestones, manage stakeholder communication, and drive operational efficiency.",
                    "Addis Ababa (Hybrid)",
                    [105, 110, 102, 103], // Project Management, Agile & Scrum, Strategic Planning, Business Analysis
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [112, 111], // Change Management, Vendor Management
                    deadline: now.AddDays(28),
                    category: "Business & Operations"),

                // 11. Marketing: Digital Marketing Specialist
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Digital Marketing Specialist",
                    "Drive brand awareness and qualified customer acquisition across social media, search engine optimization, content marketing, and targeted digital campaign funnels.",
                    "Remote",
                    [116, 117, 119, 120], // Digital Marketing, Content Strategy, Social Media Marketing, Copywriting
                    now.AddDays(-3),
                    JobType.FullTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.Junior,
                    minExperienceYears: 2,
                    preferredSkillIds: [118, 125], // SEO, Brand Development
                    deadline: now.AddDays(16),
                    category: "Sales & Marketing"),

                // 12. Sales: B2B Corporate Sales Executive
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "B2B Corporate Sales Executive",
                    "Identify prospective enterprise clients, manage end-to-end sales pipelines, deliver consultative product presentations, and negotiate commercial partnerships.",
                    "Addis Ababa",
                    [123, 122, 124, 131], // B2B Sales, Sales Management, CRM Systems, Customer Relationship Management
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [126, 109], // Market Research, Contract Negotiation
                    deadline: now.AddDays(19),
                    category: "Sales & Marketing"),

                // 13. Hospitality: Front Desk & Guest Experience Supervisor
                new Job(
                    hospitalityUser?.Id ?? adminUser.Id,
                    "Front Desk & Guest Experience Supervisor",
                    "Oversee front office reception operations, manage VIP guest arrivals, coordinate room reservations, and resolve customer service inquiries to deliver 5-star hospitality.",
                    "Addis Ababa",
                    [135, 133, 132, 137], // Guest Relations, Front Desk Operations, Conflict Resolution, Reservation Systems
                    now.AddDays(-4),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 2,
                    preferredSkillIds: [140, 138], // Customer Retention, Hospitality Management
                    deadline: now.AddDays(11),
                    category: "Hospitality & Customer Service"),

                // 14. Customer Service: Customer Support Team Lead
                new Job(
                    adminUser.Id,
                    "Customer Support Team Lead",
                    "Lead a high-performing omnichannel customer care team. Manage support ticket escalation queues, coach representatives, and maintain customer satisfaction (CSAT) ratings.",
                    "Remote",
                    [131, 136, 132], // Customer Relationship Management, Call Center Operations, Conflict Resolution
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [140, 157], // Customer Retention, Quality Assurance
                    deadline: now.AddDays(22),
                    category: "Hospitality & Customer Service"),

                // 15. Technology: Full-Stack Software Engineer
                new Job(
                    adminUser.Id,
                    "Full-Stack Software Engineer",
                    "Architect, develop, and maintain responsive web applications and secure backend microservices. Collaborate with multidisciplinary teams to build scalable digital services.",
                    "Remote",
                    [1, 2, 3, 4, 5], // ASP.NET Core, Angular, C#, TypeScript, PostgreSQL
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [12, 13, 39], // Docker, Git, CI/CD
                    deadline: now.AddDays(24),
                    category: "Technology & Software"),

                // 16. Data & Analytics: Data Analyst & Business Intelligence Specialist
                new Job(
                    financeUser?.Id ?? adminUser.Id,
                    "Data Analyst & BI Specialist",
                    "Transform multi-source organizational datasets into dynamic Power BI dashboards, automated SQL reports, and actionable business intelligence for executive decision-making.",
                    "Addis Ababa (Hybrid)",
                    [161, 163, 11, 164], // Data Analysis, Power BI, SQL Server, Microsoft Excel
                    now.AddDays(-3),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [21, 170], // Python, Statistical Modeling
                    deadline: now.AddDays(17),
                    category: "Data & Analytics")
            };

            var jobsToInsert = jobs
                .Where(j => !existingTitles.Contains(j.Title))
                .ToList();

            if (jobsToInsert.Count > 0)
            {
                dbContext.Jobs.AddRange(jobsToInsert);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        // Backfill categories for any existing seeded jobs that predate the Category column
        var defaultCategoryByTitle = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Registered Nurse (Inpatient Care)"] = "Healthcare & Medical",
            ["Medical Laboratory Scientist"] = "Healthcare & Medical",
            ["Senior Financial Analyst"] = "Finance & Accounting",
            ["Accountant & Tax Associate"] = "Finance & Accounting",
            ["High School STEM Educator"] = "Education & Training",
            ["Instructional Designer & E-Learning Coordinator"] = "Education & Training",
            ["Civil Site Engineer"] = "Engineering & Architecture",
            ["Electrical Systems Maintenance Technician"] = "Logistics & Skilled Trades",
            ["Operations & HR Generalist"] = "Business & Operations",
            ["Project Manager (Strategic Initiatives)"] = "Business & Operations",
            ["Digital Marketing Specialist"] = "Sales & Marketing",
            ["B2B Corporate Sales Executive"] = "Sales & Marketing",
            ["Front Desk & Guest Experience Supervisor"] = "Hospitality & Customer Service",
            ["Customer Support Team Lead"] = "Hospitality & Customer Service",
            ["Full-Stack Software Engineer"] = "Technology & Software",
            ["Data Analyst & BI Specialist"] = "Data & Analytics"
        };

        var uncategorizedJobs = await dbContext.Jobs
            .Where(j => j.Category == null || j.Category == "")
            .ToListAsync(cancellationToken);

        if (uncategorizedJobs.Count > 0)
        {
            var updatedAny = false;
            foreach (var job in uncategorizedJobs)
            {
                if (defaultCategoryByTitle.TryGetValue(job.Title, out var cat))
                {
                    job.SetCategory(cat);
                    updatedAny = true;
                }
            }
            if (updatedAny)
                await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (candidateUser is not null)
        {
            var profile = await dbContext.CandidateProfiles
                .Include(p => p.Skills)
                .FirstOrDefaultAsync(p => p.UserId == candidateUser.Id, cancellationToken);

            if (profile is not null && !profile.HasSkills)
            {
                profile.Update(
                    "Project Coordinator & Operations Specialist",
                    "Results-oriented professional with experience in project planning, operational workflow optimization, cross-functional collaboration, and business analysis.",
                    "https://github.com/demo-candidate",
                    [105, 101, 103, 164], // Project Management, Operations Management, Business Analysis, Microsoft Excel
                    DateTime.UtcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}

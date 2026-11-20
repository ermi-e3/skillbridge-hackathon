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
            new RegisterRequest("hr@hackathon.local", "HrAdmin123!", "Sara Mengistu", Roles.Employer, "Ethio Talent & HR Solutions"),
            cancellationToken);
        await EnsureDemoUserAsync(
            new RegisterRequest("legal@hackathon.local", "Legal123!", "Abebe Tadesse, Esq.", Roles.Employer, "Addis Legal Advisors & Compliance Corp"),
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
        var hrUser = await userManager.FindByEmailAsync("hr@hackathon.local");
        var legalUser = await userManager.FindByEmailAsync("legal@hackathon.local");
        var candidateUser = await userManager.FindByEmailAsync("user@hackathon.local");

        if (adminUser is null)
            return;

        var existingTitlesList = await dbContext.Jobs
            .Select(j => j.Title)
            .ToListAsync(cancellationToken);
        var existingTitles = new HashSet<string>(existingTitlesList, StringComparer.OrdinalIgnoreCase);

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
                    category: "Data & Analytics"),

                // ==========================================
                // Department 1: Engineering & Architecture
                // ==========================================
                // 17. Lead Structural Architect & Building Systems Director (Lead, Hybrid, Full-time)
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Lead Structural Architect & Building Systems Director",
                    "Lead architectural design development, structural BIM modeling, and multi-disciplinary engineering coordination for landmark commercial high-rises and civic infrastructure projects across Addis Ababa. Direct project teams through schematic design, municipal permitting, and structural integrity audits.\n\n**Key Responsibilities:**\n- Champion comprehensive structural analysis and Revit/BIM architectural modeling across all project phases.\n- Coordinate closely with civil, MEP, and geotechnical engineering leads to resolve design clashes.\n- Review contractor shop drawings and conduct structural milestone compliance inspections.\n- Mentor intermediate architects and present architectural concepts to municipal review boards.\n\n**Requirements:**\n- Bachelor's or Master's degree in Architecture, Structural Engineering, or related discipline.\n- 8+ years of proven architectural leadership delivering complex urban commercial buildings.\n- Proficiency in Revit, AutoCAD, BIM Modeling, and Ethiopian Building Codes.\n\n**Compensation:** 85,000 – 130,000 ETB / mo",
                    "Addis Ababa (Bole Medhanialem)",
                    [88, 86, 93, 100], // Structural Analysis, AutoCAD, Revit, BIM Modeling
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Lead,
                    minExperienceYears: 8,
                    preferredSkillIds: [97, 91, 95], // Construction Management, Project Estimation, Safety Compliance
                    deadline: now.AddDays(28),
                    category: "Engineering & Architecture"),

                // 18. Senior Geotechnical & Civil Infrastructure Consultant (Senior, Remote, Contract)
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Senior Geotechnical & Civil Infrastructure Consultant",
                    "Deliver high-level technical advisory services, soil stabilization assessments, foundation design evaluations, and remote terrain modeling for inter-regional road and bridge infrastructure corridors. Review environmental impact assessments and draft engineering sign-off reports.\n\n**Key Responsibilities:**\n- Review remote borehole logs, geophysical survey datasets, and slope stability computations.\n- Formulate technical specifications for deep foundation pilings and bridge abutment reinforcement.\n- Conduct peer reviews of civil engineering design packages and environmental mitigation plans.\n- Advise public works stakeholders on geotechnical risks and construction staging strategies.\n\n**Requirements:**\n- 6+ years of consulting experience in geotechnical analysis and highway/bridge infrastructure.\n- Advanced competence in Civil Engineering, Structural Analysis, and Surveying methodologies.\n- Professional engineering licensure with demonstrated advisory track record.\n\n**Compensation:** 70,000 – 105,000 ETB / mo",
                    "Remote",
                    [87, 88, 98], // Civil Engineering, Structural Analysis, Surveying
                    now.AddDays(-3),
                    JobType.Contract,
                    WorkArrangement.Remote,
                    ExperienceLevel.Senior,
                    minExperienceYears: 6,
                    preferredSkillIds: [91, 95, 168], // Project Estimation, Safety Compliance, Technical Writing
                    deadline: now.AddDays(21),
                    category: "Engineering & Architecture"),

                // 19. HVAC & Building Energy Systems Retrofit Engineer (Mid-Level, Hybrid, Part-time)
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "HVAC & Building Energy Systems Retrofit Engineer",
                    "Perform building energy audits, airflow modeling, and HVAC equipment overhaul schedules for commercial real estate and manufacturing plants. Coordinate with facility engineers 20 hours per week to optimize thermodynamic efficiency.\n\n**Key Responsibilities:**\n- Conduct thermal load calculations, ductwork sizing, and chilled water distribution analyses.\n- Specify energy-efficient rooftop HVAC units, ventilation heat-recovery units, and digital dampers.\n- Monitor indoor air quality metrics and formulate preventative maintenance schedules.\n- Prepare building energy compliance documentation and return-on-investment retrofit decks.\n\n**Requirements:**\n- 3+ years in commercial HVAC design, MEP systems, or building thermodynamic consulting.\n- Hands-on experience with HVAC Design, Mechanical Design, and Facilities Maintenance.\n- Strong analytical problem-solving and client technical presentation abilities.\n\n**Compensation:** 22,000 – 34,000 ETB / mo",
                    "Dire Dawa (Hybrid)",
                    [96, 90, 153], // HVAC Design, Mechanical Design, Facilities Maintenance
                    now.AddDays(-4),
                    JobType.PartTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [92, 159], // Quality Control, Preventative Maintenance
                    deadline: now.AddDays(18),
                    category: "Engineering & Architecture"),

                // 20. Junior MEP Installation & Site Supervision Engineer (Junior, On-site, Full-time)
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Junior MEP Installation & Site Supervision Engineer",
                    "Oversee on-site mechanical, electrical, and plumbing (MEP) installations, conduct equipment load calculations, and supervise electrical distribution switchgear and piping hookups at Hawassa Industrial Park.\n\n**Key Responsibilities:**\n- Supervise daily field execution of MEP sub-contractors against approved engineering drawings.\n- Inspect conduit runs, cable trays, plumbing risers, and drainage lines for quality assurance.\n- Prepare daily engineering site progress logs, material inspection reports, and punch lists.\n- Enforce strict adherence to occupational workplace safety and PPE guidelines.\n\n**Requirements:**\n- Degree in Mechanical or Electrical Engineering with 1+ years of active field site experience.\n- Familiarity with Mechanical Design, Electrical Systems, AutoCAD, and Quality Control.\n- Strong communication skills in multi-disciplinary jobsite environments.\n\n**Compensation:** 26,000 – 38,000 ETB / mo",
                    "Hawassa",
                    [90, 89, 86], // Mechanical Design, Electrical Systems, AutoCAD
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [94, 95, 92], // Site Supervision, Safety Compliance, Quality Control
                    deadline: now.AddDays(14),
                    category: "Engineering & Architecture"),

                // 21. Architectural Drafting & 3D Visualization Intern (Entry Level, On-site, Internship)
                new Job(
                    engUser?.Id ?? adminUser.Id,
                    "Architectural Drafting & 3D Visualization Intern",
                    "Assist senior architects in converting conceptual hand sketches into precision AutoCAD floor plans, Revit BIM models, and photorealistic 3D interior renderings for client presentations in Addis Ababa.\n\n**Key Responsibilities:**\n- Draft 2D floor plans, elevations, and architectural sectional cuts under senior supervision.\n- Build digital 3D models and apply lighting, textures, and material shaders in Revit and Figma.\n- Organize architectural drawing sets, scale prints, and maintain digital design catalogs.\n- Attend project site walk-throughs to verify existing dimensional conditions.\n\n**Requirements:**\n- Recent graduate or final-year student in Architecture, Interior Design, or Urban Planning.\n- Foundational proficiency in AutoCAD, Revit, and digital design tools.\n- Eager to learn, detail-oriented eye for aesthetics, and passion for contemporary architecture.\n\n**Compensation:** 12,000 – 18,000 ETB / mo",
                    "Addis Ababa (Kazanchis)",
                    [86, 93], // AutoCAD, Revit
                    now.AddDays(-2),
                    JobType.Internship,
                    WorkArrangement.OnSite,
                    ExperienceLevel.EntryLevel,
                    minExperienceYears: 0,
                    preferredSkillIds: [37, 100], // Figma, BIM Modeling
                    deadline: now.AddDays(25),
                    category: "Engineering & Architecture"),

                // ==========================================
                // Department 2: Human Resources
                // ==========================================
                // 22. Head of People Operations & Organizational Culture (Lead, Hybrid, Full-time)
                new Job(
                    hrUser?.Id ?? adminUser.Id,
                    "Head of People Operations & Organizational Culture",
                    "Architect organizational people strategy, compensation benchmarking, leadership succession frameworks, and employee retention programs for an expanding multinational enterprise. Champion workplace equity, employee wellness, and high-performance cultural transformations.\n\n**Key Responsibilities:**\n- Define multi-year people strategy aligned with corporate growth targets and organizational values.\n- Lead performance appraisal modernization, competency frameworks, and management coaching.\n- Design competitive total rewards, wellness initiatives, and staff development programs.\n- Advise executive leadership on organizational restructuring, labor law, and risk management.\n\n**Requirements:**\n- 7+ years of progressive HR leadership experience in mid-to-large scale corporate environments.\n- Expertise in Human Resources (HR), Strategic Planning, Performance Management, and Change Management.\n- High emotional intelligence, ethical leadership, and stellar stakeholder diplomacy.\n\n**Compensation:** 80,000 – 120,000 ETB / mo",
                    "Addis Ababa (Kazanchis)",
                    [104, 102, 108, 112], // Human Resources (HR), Strategic Planning, Performance Management, Change Management
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Lead,
                    minExperienceYears: 7,
                    preferredSkillIds: [107, 64, 109], // Talent Acquisition, Payroll Management, Contract Negotiation
                    deadline: now.AddDays(30),
                    category: "Human Resources"),

                // 23. Employee Relations & Organizational Development Consultant (Senior, Hybrid, Contract)
                new Job(
                    hrUser?.Id ?? adminUser.Id,
                    "Employee Relations & Organizational Development Consultant",
                    "Lead a 12-month organizational restructuring initiative, workplace mediation protocols, labor policy harmonizations, and cultural engagement strategies across regional business units.\n\n**Key Responsibilities:**\n- Formulate modernized employee handbooks, disciplinary matrices, and grievance arbitration policies.\n- Facilitate executive change management workshops and departmental alignment forums.\n- Conduct confidential workplace climate evaluations and recommend actionable culture initiatives.\n- Coach line managers on progressive disciplinary procedures and constructive feedback delivery.\n\n**Requirements:**\n- 5+ years specializing in employee relations, workplace dispute mediation, or organizational development.\n- Mastery in Human Resources (HR), Conflict Resolution, Change Management, and Contract Negotiation.\n- In-depth understanding of Ethiopian labor proclamations and international workplace conventions.\n\n**Compensation:** 55,000 – 80,000 ETB / mo",
                    "Addis Ababa (Hybrid)",
                    [104, 132, 112, 109], // Human Resources (HR), Conflict Resolution, Change Management, Contract Negotiation
                    now.AddDays(-3),
                    JobType.Contract,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [102, 108], // Strategic Planning, Performance Management
                    deadline: now.AddDays(22),
                    category: "Human Resources"),

                // 24. Talent Acquisition Partner & Technical Sourcer (Mid-Level, Remote, Full-time)
                new Job(
                    hrUser?.Id ?? adminUser.Id,
                    "Talent Acquisition Partner & Technical Sourcer",
                    "Drive full-lifecycle technical and corporate recruitment across East Africa. Source passive engineering and leadership talent, conduct structured competency-based behavioral interviews, and optimize applicant tracking funnel metrics.\n\n**Key Responsibilities:**\n- Partner with department heads to draft calibrated job scorecards and candidate assessment rubrics.\n- Build outbound candidate sourcing pipelines across LinkedIn, GitHub, and professional networks.\n- Conduct structured preliminary behavioral screenings and salary expectation alignments.\n- Oversee offer negotiation, reference verifications, and smooth candidate pre-boarding handoffs.\n\n**Requirements:**\n- 3+ years of end-to-end recruitment or talent sourcing experience.\n- Core proficiency in Talent Acquisition, Human Resources (HR), and Market Research.\n- Data-driven mindset with experience tracking time-to-hire, offer acceptance, and pipeline velocity.\n\n**Compensation:** 40,000 – 62,000 ETB / mo",
                    "Remote",
                    [107, 104, 126], // Talent Acquisition, Human Resources (HR), Market Research
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [108, 102, 106], // Performance Management, Strategic Planning, Office Administration
                    deadline: now.AddDays(20),
                    category: "Human Resources"),

                // 25. HR Operations & Payroll Specialist (Junior, On-site, Full-time)
                new Job(
                    hrUser?.Id ?? adminUser.Id,
                    "HR Operations & Payroll Specialist",
                    "Process monthly staff payroll, manage statutory pension and tax withholding submissions, maintain digital employee master files, and coordinate employee medical insurance enrollments with utmost confidentiality.\n\n**Key Responsibilities:**\n- Execute end-to-end monthly payroll computations, overtime tracking, and statutory tax deductions.\n- Administer employee leave balances, attendance logs, and digital personnel files.\n- Coordinate onboarding documentation, ID badging, and healthcare insurance enrollments.\n- Provide prompt first-line support to staff inquiries regarding benefits and employment verifications.\n\n**Requirements:**\n- 1-2 years of proven experience in HR administration, payroll support, or general accounting.\n- Proficient in Payroll Management, Human Resources (HR), Office Administration, and Microsoft Excel.\n- High discretion, numerical precision, and strict adherence to data privacy ethics.\n\n**Compensation:** 28,000 – 42,000 ETB / mo",
                    "Addis Ababa (Kirkos)",
                    [64, 104, 106, 164], // Payroll Management, Human Resources (HR), Office Administration, Microsoft Excel
                    now.AddDays(-4),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [61, 58], // Tax Compliance, Bookkeeping
                    deadline: now.AddDays(15),
                    category: "Human Resources"),

                // 26. HR & People Operations Intern (Entry Level, On-site, Internship)
                new Job(
                    hrUser?.Id ?? adminUser.Id,
                    "HR & People Operations Intern",
                    "Support human resources coordinators in screening candidate CVs, scheduling candidate panel interviews, organizing employee onboarding welcome packets, and maintaining digital HR records in Addis Ababa.\n\n**Key Responsibilities:**\n- Review inbound candidate applications against job requirements and update applicant tracking statuses.\n- Coordinate interview logistics, calendar invites, and reception greeting for on-site candidates.\n- Assist with new-hire orientation logistics, handbook distribution, and file compliance audits.\n- Support internal staff wellness events, birthday acknowledgments, and team-building logistics.\n\n**Requirements:**\n- Recent graduate in Human Resource Management, Business Administration, or Psychology.\n- Solid working knowledge of Microsoft Excel and basic Office Administration principles.\n- Friendly interpersonal demeanor, strong written English and Amharic communication skills.\n\n**Compensation:** 10,000 – 16,000 ETB / mo",
                    "Addis Ababa (Bole)",
                    [104, 106], // Human Resources (HR), Office Administration
                    now.AddDays(-1),
                    JobType.Internship,
                    WorkArrangement.OnSite,
                    ExperienceLevel.EntryLevel,
                    minExperienceYears: 0,
                    preferredSkillIds: [164, 115], // Microsoft Excel, Event Planning
                    deadline: now.AddDays(26),
                    category: "Human Resources"),

                // ==========================================
                // Department 3: Legal & Compliance
                // ==========================================
                // 27. Chief Compliance Officer & Corporate Legal Counsel (Lead, Hybrid, Full-time)
                new Job(
                    legalUser?.Id ?? adminUser.Id,
                    "Chief Compliance Officer & Corporate Legal Counsel",
                    "Serve as principal legal advisor to the executive board. Oversee enterprise governance, commercial contracting, regulatory compliance with Ethiopian trade and financial sector authorities, cross-border joint ventures, and litigation risk mitigation.\n\n**Key Responsibilities:**\n- Direct corporate legal governance, board secretarial filings, and statutory compliance audits.\n- Lead high-stakes commercial negotiations, cross-border technology licensing, and M&A transactions.\n- Oversee regulatory relationships with ministries, investment commissions, and tax authorities.\n- Formulate enterprise compliance frameworks covering anti-corruption, AML, and whistleblower policies.\n\n**Requirements:**\n- LLB / LLM degree and active license to practice law with 8+ years of high-level corporate counsel experience.\n- Deep mastery in Contract Negotiation, Risk Management, Corporate Finance, and Internal Audit.\n- Track record handling complex commercial dispute resolution and regulatory affairs.\n\n**Compensation:** 90,000 – 140,000 ETB / mo",
                    "Addis Ababa (Bole Road)",
                    [109, 65, 66, 69], // Contract Negotiation, Risk Management, Corporate Finance, Internal Audit
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Lead,
                    minExperienceYears: 8,
                    preferredSkillIds: [60, 102, 168], // IFRS Compliance, Strategic Planning, Technical Writing
                    deadline: now.AddDays(28),
                    category: "Legal & Compliance"),

                // 28. Data Protection & Cybersecurity Compliance Auditor (Senior, Remote, Contract)
                new Job(
                    legalUser?.Id ?? adminUser.Id,
                    "Data Protection & Cybersecurity Compliance Auditor",
                    "Conduct rigorous institutional data privacy audits, information security policy reviews, and regulatory gap assessments in accordance with national cybersecurity directives and international data protection standards.\n\n**Key Responsibilities:**\n- Audit enterprise cloud architecture, customer data flows, and access controls for regulatory compliance.\n- Draft data processing agreements, privacy impact assessments, and incident notification playbooks.\n- Evaluate vendor third-party security postures and cloud service provider certifications.\n- Deliver executive compliance audit reports with prioritized risk remediation matrices.\n\n**Requirements:**\n- 5+ years of experience auditing IT systems, data governance, or information security compliance.\n- Strong expertise in Cybersecurity, Internal Audit, and Risk Management.\n- Familiarity with cloud platforms (AWS, Azure) and international privacy frameworks (GDPR, ISO 27001).\n\n**Compensation:** 65,000 – 95,000 ETB / mo",
                    "Remote",
                    [165, 69, 65], // Cybersecurity, Internal Audit, Risk Management
                    now.AddDays(-3),
                    JobType.Contract,
                    WorkArrangement.Remote,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [161, 95, 168], // Data Analysis, Safety Compliance, Technical Writing
                    deadline: now.AddDays(24),
                    category: "Legal & Compliance"),

                // 29. Commercial Contracts & Regulatory Associate (Mid-Level, On-site, Full-time)
                new Job(
                    legalUser?.Id ?? adminUser.Id,
                    "Commercial Contracts & Regulatory Associate",
                    "Draft, review, and negotiate enterprise master service agreements, vendor procurement contracts, non-disclosure agreements, and commercial leases. Liaise with government licensing ministries and trademark offices.\n\n**Key Responsibilities:**\n- Review and redline commercial sales agreements, supplier SLAs, and partnership MOUs.\n- Liaise with the Ministry of Trade, Investment Commission, and tax bureaus for corporate permits.\n- Maintain the enterprise contract lifecycle repository and monitor renewal/termination milestones.\n- Provide day-to-day legal operational counsel to procurement and sales teams.\n\n**Requirements:**\n- LLB degree with 3+ years in commercial law, corporate in-house counsel, or reputable law firm.\n- Solid command of Contract Negotiation, Tax Compliance, and Auditing fundamentals.\n- Exceptional drafting clarity, attention to detail, and commercial business acumen.\n\n**Compensation:** 45,000 – 68,000 ETB / mo",
                    "Addis Ababa (Piazza)",
                    [109, 61, 63], // Contract Negotiation, Tax Compliance, Auditing
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [106, 168, 65], // Office Administration, Technical Writing, Risk Management
                    deadline: now.AddDays(19),
                    category: "Legal & Compliance"),

                // 30. Junior AML & KYC Compliance Analyst (Junior, On-site, Full-time)
                new Job(
                    legalUser?.Id ?? adminUser.Id,
                    "Junior AML & KYC Compliance Analyst",
                    "Review customer onboarding files, investigate suspicious transaction alerts, and verify corporate ownership structures under National Bank of Ethiopia AML/CFT guidelines. Prepare weekly suspicious activity compliance reports.\n\n**Key Responsibilities:**\n- Perform enhanced due diligence (EDD) and sanctions screenings on high-risk institutional clients.\n- Analyze automated transaction monitoring flags for potential money laundering or fraud indicators.\n- Compile well-substantiated case investigation dossiers and escalate material risks.\n- Assist in updating customer risk rating methodologies and internal AML training modules.\n\n**Requirements:**\n- 1-2 years experience in bank compliance, internal audit, fraud prevention, or financial analysis.\n- Proficiency in Risk Management, Auditing, and Microsoft Excel.\n- Sharp investigative curiosity, analytical rigor, and unwavering commitment to integrity.\n\n**Compensation:** 30,000 – 45,000 ETB / mo",
                    "Addis Ababa (Financial District)",
                    [65, 63, 164], // Risk Management, Auditing, Microsoft Excel
                    now.AddDays(-4),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [161, 69], // Data Analysis, Internal Audit
                    deadline: now.AddDays(16),
                    category: "Legal & Compliance"),

                // 31. Legal Operations & Document Review Clerk (Entry Level, Hybrid, Part-time)
                new Job(
                    legalUser?.Id ?? adminUser.Id,
                    "Legal Operations & Document Review Clerk",
                    "Assist legal attorneys in indexing court filings, conducting statutory legal precedent research, proofreading case briefs, and maintaining physical and cloud-based case libraries 20 hours per week.\n\n**Key Responsibilities:**\n- Organize and catalog trial exhibits, corporate resolutions, and legal correspondences.\n- Conduct online legal research on court registries, official gazettes, and ministerial directives.\n- Proofread legal documents, verify statutory citations, and prepare court hearing bundles.\n- Manage calendar deadlines for court appearances, statute of limitations, and filings.\n\n**Requirements:**\n- Law student or recent LLB graduate seeking practical corporate legal experience.\n- Competence in Office Administration, Technical Writing, and Microsoft Excel.\n- Meticulous document formatting skills, organizational diligence, and prompt responsiveness.\n\n**Compensation:** 15,000 – 22,000 ETB / mo",
                    "Addis Ababa (Arat Kilo)",
                    [106, 168], // Office Administration, Technical Writing
                    now.AddDays(-2),
                    JobType.PartTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.EntryLevel,
                    minExperienceYears: 0,
                    preferredSkillIds: [109, 164], // Contract Negotiation, Microsoft Excel
                    deadline: now.AddDays(23),
                    category: "Legal & Compliance"),

                // ==========================================
                // Department 4: Creative & Design
                // ==========================================
                // 32. Principal Design Systems Architect & UI/UX Director (Lead, Remote, Full-time)
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Principal Design Systems Architect & UI/UX Director",
                    "Establish enterprise UI design systems, token architectures, and responsive micro-interactions across web and mobile ecosystems. Lead cross-functional design sprints, conduct usability testing labs, and bridge visual design with engineering component libraries.\n\n**Key Responsibilities:**\n- Direct multi-platform design systems (tokens, typography, color semantics, components) in Figma.\n- Partner with frontend engineers to ensure high-fidelity pixel perfection across web and mobile.\n- Conduct qualitative candidate and employer user testing, journey mapping, and conversion audits.\n- Mentor product designers, lead quarterly design critiques, and uphold WCAG accessibility compliance.\n\n**Requirements:**\n- 6+ years designing complex SaaS products, design systems, and responsive web platforms.\n- Advanced expertise in UI/UX Design, Figma, CSS, and Tailwind CSS.\n- Working knowledge of frontend frameworks (Angular, HTML) and design token tooling.\n\n**Compensation:** 75,000 – 115,000 ETB / mo",
                    "Remote",
                    [166, 37, 9, 10], // UI/UX Design, Figma, CSS, Tailwind CSS
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.Lead,
                    minExperienceYears: 6,
                    preferredSkillIds: [8, 4, 2, 125], // HTML, TypeScript, Angular, Brand Development
                    deadline: now.AddDays(27),
                    category: "Creative & Design"),

                // 33. Senior 3D Visualizer & Environmental Spatial Designer (Senior, Hybrid, Contract)
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Senior 3D Visualizer & Environmental Spatial Designer",
                    "Craft immersive 3D architectural visualizations, experiential retail staging designs, and photorealistic spatial renderings for major hospitality, commercial real estate, and trade expo exhibitions.\n\n**Key Responsibilities:**\n- Model highly detailed 3D spatial environments, event pavilions, and retail showroom interiors.\n- Create high-resolution lighting, photorealistic textures, and architectural camera animation fly-throughs.\n- Collaborate with marketing teams to translate brand identities into three-dimensional physical spaces.\n- Deliver optimized asset packages for print banners, digital walk-throughs, and fabrication builders.\n\n**Requirements:**\n- 5+ years of professional 3D spatial design, commercial architectural visualization, or exhibit staging.\n- Mastery in Graphic Design, AutoCAD, SolidWorks, or equivalent 3D modeling suites.\n- Outstanding portfolio demonstrating architectural scale, lighting balance, and spatial imagination.\n\n**Compensation:** 60,000 – 90,000 ETB / mo",
                    "Addis Ababa (Hybrid)",
                    [167, 86, 99], // Graphic Design, AutoCAD, SolidWorks
                    now.AddDays(-3),
                    JobType.Contract,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [93, 37], // Revit, Figma
                    deadline: now.AddDays(21),
                    category: "Creative & Design"),

                // 34. Brand Identity & Visual Communications Designer (Mid-Level, Hybrid, Full-time)
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Brand Identity & Visual Communications Designer",
                    "Create evocative visual brand identities, vector illustrations, promotional motion graphics, typography guidelines, and marketing collateral for high-visibility commercial product launches and digital media campaigns.\n\n**Key Responsibilities:**\n- Design comprehensive brand guidelines, iconography systems, and typography style books.\n- Produce promotional digital banners, physical billboard artwork, packaging designs, and brochures.\n- Collaborate with content strategists to translate campaign messaging into striking visuals.\n- Maintain organized creative asset repositories and ensure rigorous brand consistency.\n\n**Requirements:**\n- 3+ years in visual design, brand studio, or advertising agency.\n- High proficiency in Graphic Design, Brand Development, and Figma.\n- Compelling visual portfolio featuring logo designs, identity systems, and print/digital collateral.\n\n**Compensation:** 38,000 – 58,000 ETB / mo",
                    "Addis Ababa (Old Airport)",
                    [167, 125, 37], // Graphic Design, Brand Development, Figma
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [128, 117], // Advertising, Content Strategy
                    deadline: now.AddDays(17),
                    category: "Creative & Design"),

                // 35. Junior Creative Copywriter & Social Content Creator (Junior, Remote, Part-time)
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Junior Creative Copywriter & Social Content Creator",
                    "Produce witty, viral bilingual social media content, brand storytelling narratives, and micro-copy for consumer campaigns across TikTok, LinkedIn, and Instagram. Collaborate 20 hours per week with digital marketing leads.\n\n**Key Responsibilities:**\n- Write punchy marketing copy for social posts, newsletter blasts, and advertising creatives.\n- Brainstorm creative campaign hooks, meme formats, and short-form video scripts.\n- Adapt brand tone of voice across diverse audience segments in English and Amharic.\n- Track post engagement analytics and iterate copy formats based on performance data.\n\n**Requirements:**\n- 1+ years writing creative copy, managing social media channels, or editorial journalism.\n- Proficient in Copywriting, Social Media Marketing, and Content Strategy.\n- Exceptional bilingual creative writing flair with a sharp instinct for modern digital trends.\n\n**Compensation:** 18,000 – 26,000 ETB / mo",
                    "Remote",
                    [120, 119, 117], // Copywriting, Social Media Marketing, Content Strategy
                    now.AddDays(-4),
                    JobType.PartTime,
                    WorkArrangement.Remote,
                    ExperienceLevel.Junior,
                    minExperienceYears: 1,
                    preferredSkillIds: [167, 118], // Graphic Design, SEO
                    deadline: now.AddDays(15),
                    category: "Creative & Design"),

                // 36. Visual Design & Multimedia Production Intern (Entry Level, On-site, Internship)
                new Job(
                    marketingUser?.Id ?? adminUser.Id,
                    "Visual Design & Multimedia Production Intern",
                    "Support creative directors in asset exporting, banner resizing, iconography design, presentation deck styling, and basic video post-production editing for social media platforms in Addis Ababa.\n\n**Key Responsibilities:**\n- Resize and crop visual assets for multiple social media formats and display advertising dimensions.\n- Assist senior designers with layout cleanups, vector tracing, and presentation slide formatting.\n- Edit basic video snippets, add captions, and export animated GIFs for digital campaigns.\n- Catalog creative production assets and assist in photo and video shoot logistics.\n\n**Requirements:**\n- Degree or diploma in Graphic Design, Multimedia Arts, or Visual Communications.\n- Hands-on skills in Graphic Design and Figma.\n- Strong eye for color harmony, visual hierarchy, and enthusiasm to learn modern studio workflows.\n\n**Compensation:** 12,000 – 18,000 ETB / mo",
                    "Addis Ababa (Meskel Flower)",
                    [167, 37], // Graphic Design, Figma
                    now.AddDays(-1),
                    JobType.Internship,
                    WorkArrangement.OnSite,
                    ExperienceLevel.EntryLevel,
                    minExperienceYears: 0,
                    preferredSkillIds: [166, 119], // UI/UX Design, Social Media Marketing
                    deadline: now.AddDays(24),
                    category: "Creative & Design"),

                // ==========================================
                // Department 5: Other / General
                // ==========================================
                // 37. Director of Facilities, Physical Plant & Campus Safety (Lead, On-site, Full-time)
                new Job(
                    adminUser.Id,
                    "Director of Facilities, Physical Plant & Campus Safety",
                    "Direct complete physical infrastructure operations, building automation systems, vendor service-level contracts, security protocols, and preventative maintenance programs across a 15-building corporate campus.\n\n**Key Responsibilities:**\n- Formulate strategic capital expenditure plans for physical infrastructure and power resilience.\n- Direct plant engineering teams, HVAC technicians, electrical contractors, and janitorial crews.\n- Ensure 100% compliance with OSHA workplace safety standards and municipal fire codes.\n- Negotiate multi-million ETB master facility service agreements and manage service SLAs.\n\n**Requirements:**\n- 8+ years leading large-scale corporate facilities, physical plant operations, or campus engineering.\n- Proven mastery in Facilities Maintenance, Operations Management, Workplace Safety (OSHA), and Preventative Maintenance.\n- Strong leadership presence, crisis management capability, and budgetary discipline.\n\n**Compensation:** 70,000 – 100,000 ETB / mo",
                    "Addis Ababa (Gotera)",
                    [153, 101, 154, 159], // Facilities Maintenance, Operations Management, Workplace Safety (OSHA), Preventative Maintenance
                    now.AddDays(-2),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Lead,
                    minExperienceYears: 8,
                    preferredSkillIds: [111, 91, 152], // Vendor Management, Project Estimation, HVAC Maintenance
                    deadline: now.AddDays(29),
                    category: "Other / General"),

                // 38. Supply Chain Logistics & Warehouse Operations Lead (Senior, On-site, Full-time)
                new Job(
                    adminUser.Id,
                    "Supply Chain Logistics & Warehouse Operations Lead",
                    "Oversee freight forwarding, customs clearance coordination, automated barcoding inventory controls, and cross-docking operations at the inland container terminal. Supervise logistics supervisors and forklift operators.\n\n**Key Responsibilities:**\n- Manage inbound and outbound inventory flows across 25,000 sq. meters of warehouse space.\n- Coordinate multi-modal freight transport from Djibouti port to Modjo Dry Port hub.\n- Implement real-time barcode tracking to achieve 99.8% inventory accuracy and minimize shrinkage.\n- Supervise warehouse safety standards, equipment inspections, and logistics shift scheduling.\n\n**Requirements:**\n- 5+ years managing distribution centers, dry ports, or industrial supply chain hubs.\n- Deep capability in Supply Chain Management, Logistics Planning, Warehouse Operations, and Inventory Control.\n- Strong leadership track record managing frontline blue-collar logistics teams.\n\n**Compensation:** 48,000 – 72,000 ETB / mo",
                    "Modjo Dry Port & Logistics Hub",
                    [146, 148, 156, 147], // Supply Chain Management, Logistics Planning, Warehouse Operations, Inventory Control
                    now.AddDays(-3),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.Senior,
                    minExperienceYears: 5,
                    preferredSkillIds: [160, 149, 155], // Fleet Management, Procurement, Heavy Equipment Operation
                    deadline: now.AddDays(20),
                    category: "Other / General"),

                // 39. Field Health, Safety & Environmental (HSE) Specialist (Mid-Level, On-site, Full-time)
                new Job(
                    adminUser.Id,
                    "Field Health, Safety & Environmental (HSE) Specialist",
                    "Conduct regular on-site safety audits, hazard assessments, toolbox talks, incident root-cause investigations, and environmental waste management drills across regional manufacturing plants in Bahir Dar.\n\n**Key Responsibilities:**\n- Perform comprehensive job hazard analyses (JHA) and safety compliance inspections.\n- Lead daily safety toolbox briefings and train field operators on proper PPE usage.\n- Investigate workplace incidents, document near-misses, and enforce preventive safety actions.\n- Ensure proper hazardous waste storage, effluent monitoring, and environmental compliance.\n\n**Requirements:**\n- 3+ years in industrial safety, environmental health, or manufacturing quality assurance.\n- Certified competencies in Workplace Safety (OSHA), Safety Compliance, and Quality Assurance.\n- Strong field presence, firm adherence to safety standards, and clear instructional delivery.\n\n**Compensation:** 36,000 – 52,000 ETB / mo",
                    "Bahir Dar",
                    [154, 95, 157], // Workplace Safety (OSHA), Safety Compliance, Quality Assurance
                    now.AddDays(-1),
                    JobType.FullTime,
                    WorkArrangement.OnSite,
                    ExperienceLevel.MidLevel,
                    minExperienceYears: 3,
                    preferredSkillIds: [45, 55], // First Aid & BLS, Emergency Response
                    deadline: now.AddDays(18),
                    category: "Other / General"),

                // 40. Strategic Procurement & Vendor Sourcing Coordinator (Junior, Hybrid, Contract)
                new Job(
                    adminUser.Id,
                    "Strategic Procurement & Vendor Sourcing Coordinator",
                    "Solicit and evaluate vendor RFPs, negotiate competitive pricing terms, track supplier delivery timelines, and maintain supplier quality scorecards during a 1-year capital expansion project in Addis Ababa.\n\n**Key Responsibilities:**\n- Issue requests for quotation (RFQs), analyze vendor proposals, and prepare cost comparison sheets.\n- Negotiate payment terms, volume discounts, and delivery warranties with domestic suppliers.\n- Track purchase order fulfillment lifecycles and resolve invoice and delivery discrepancies.\n- Maintain the approved vendor registry and evaluate supplier on-time delivery scorecards.\n\n**Requirements:**\n- 2+ years experience in purchasing, vendor relations, or supply chain administration.\n- Strong skills in Procurement, Contract Negotiation, and Vendor Management.\n- Analytical negotiation acumen with high proficiency in Microsoft Excel.\n\n**Compensation:** 26,000 – 40,000 ETB / mo",
                    "Addis Ababa (Bole)",
                    [149, 109, 111], // Procurement, Contract Negotiation, Vendor Management
                    now.AddDays(-4),
                    JobType.Contract,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.Junior,
                    minExperienceYears: 2,
                    preferredSkillIds: [164, 146], // Microsoft Excel, Supply Chain Management
                    deadline: now.AddDays(16),
                    category: "Other / General"),

                // 41. Corporate Events & General Operations Intern (Entry Level, Hybrid, Internship)
                new Job(
                    adminUser.Id,
                    "Corporate Events & General Operations Intern",
                    "Assist organizational operations teams with executive calendar coordination, visitor hospitality, procurement requisition tracking, conference room AV setup, and logistics for annual corporate galas in Addis Ababa.\n\n**Key Responsibilities:**\n- Assist in coordinating company-wide town halls, executive meetings, and stakeholder banquets.\n- Manage reception visitor sign-ins, meeting room calendar bookings, and incoming package logistics.\n- Assist procurement officers in cataloging office supply inventories and submitting purchase requests.\n- Coordinate meeting room audio-visual equipment and take structured operational meeting minutes.\n\n**Requirements:**\n- Final-year student or recent graduate in Management, Communications, or Hospitality.\n- Demonstrated foundational skills in Office Administration and Event Planning.\n- Energetic team player with proactive problem-solving attitude and strong computer skills.\n\n**Compensation:** 12,000 – 17,000 ETB / mo",
                    "Addis Ababa (Sarbet)",
                    [106, 115], // Office Administration, Event Planning
                    now.AddDays(-2),
                    JobType.Internship,
                    WorkArrangement.Hybrid,
                    ExperienceLevel.EntryLevel,
                    minExperienceYears: 0,
                    preferredSkillIds: [114, 164], // Executive Support, Microsoft Excel
                    deadline: now.AddDays(25),
                    category: "Other / General")
            };

            var jobsToInsert = jobs
                .Where(j => !existingTitles.Contains(j.Title))
                .ToList();

            if (jobsToInsert.Count > 0)
            {
                dbContext.Jobs.AddRange(jobsToInsert);
                await dbContext.SaveChangesAsync(cancellationToken);
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
            ["Data Analyst & BI Specialist"] = "Data & Analytics",

            // Engineering & Architecture
            ["Lead Structural Architect & Building Systems Director"] = "Engineering & Architecture",
            ["Senior Geotechnical & Civil Infrastructure Consultant"] = "Engineering & Architecture",
            ["HVAC & Building Energy Systems Retrofit Engineer"] = "Engineering & Architecture",
            ["Junior MEP Installation & Site Supervision Engineer"] = "Engineering & Architecture",
            ["Architectural Drafting & 3D Visualization Intern"] = "Engineering & Architecture",

            // Human Resources
            ["Head of People Operations & Organizational Culture"] = "Human Resources",
            ["Employee Relations & Organizational Development Consultant"] = "Human Resources",
            ["Talent Acquisition Partner & Technical Sourcer"] = "Human Resources",
            ["HR Operations & Payroll Specialist"] = "Human Resources",
            ["HR & People Operations Intern"] = "Human Resources",

            // Legal & Compliance
            ["Chief Compliance Officer & Corporate Legal Counsel"] = "Legal & Compliance",
            ["Data Protection & Cybersecurity Compliance Auditor"] = "Legal & Compliance",
            ["Commercial Contracts & Regulatory Associate"] = "Legal & Compliance",
            ["Junior AML & KYC Compliance Analyst"] = "Legal & Compliance",
            ["Legal Operations & Document Review Clerk"] = "Legal & Compliance",

            // Creative & Design
            ["Principal Design Systems Architect & UI/UX Director"] = "Creative & Design",
            ["Senior 3D Visualizer & Environmental Spatial Designer"] = "Creative & Design",
            ["Brand Identity & Visual Communications Designer"] = "Creative & Design",
            ["Junior Creative Copywriter & Social Content Creator"] = "Creative & Design",
            ["Visual Design & Multimedia Production Intern"] = "Creative & Design",

            // Other / General
            ["Director of Facilities, Physical Plant & Campus Safety"] = "Other / General",
            ["Supply Chain Logistics & Warehouse Operations Lead"] = "Other / General",
            ["Field Health, Safety & Environmental (HSE) Specialist"] = "Other / General",
            ["Strategic Procurement & Vendor Sourcing Coordinator"] = "Other / General",
            ["Corporate Events & General Operations Intern"] = "Other / General"
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

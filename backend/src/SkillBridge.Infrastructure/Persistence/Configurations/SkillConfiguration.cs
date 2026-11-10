using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkillBridge.Domain.Entities;

namespace SkillBridge.Infrastructure.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    // Ids 1..170 spanning diverse professions across Healthcare, Finance, Education, Engineering,
    // Business, Marketing, Hospitality, Trades, Operations, and Technology.
    // Ids 1..40 preserve the original software & web skill vocabulary for test compatibility.
    public static readonly string[] SeedNames =
    [
        // 1..40: Technology & Software Development
        "ASP.NET Core", "Angular", "C#", "TypeScript", "PostgreSQL",
        "Entity Framework Core", "JavaScript", "HTML", "CSS", "Tailwind CSS",
        "SQL Server", "Docker", "Git", "Linux", "REST APIs",
        "SignalR", "React", "Vue.js", "Node.js", "Express.js",
        "Python", "Django", "Flask", "Java", "Spring Boot",
        "Kotlin", "Flutter", "Dart", "Swift", "PHP",
        "Laravel", "MySQL", "MongoDB", "Redis", "Azure",
        "AWS", "Figma", "Unit Testing", "CI/CD", "Go",

        // 41..55: Healthcare & Medical Sciences
        "Patient Care", "Clinical Assessment", "Pharmacology", "Medical Records (EHR)", "First Aid & BLS",
        "Triage", "Phlebotomy", "Infection Control", "Nursing", "Health Education",
        "Medical Laboratory", "Patient Counseling", "Vital Signs Monitoring", "Public Health", "Emergency Response",

        // 56..70: Finance, Banking & Accounting
        "Financial Analysis", "Financial Modeling", "Bookkeeping", "QuickBooks", "IFRS Compliance",
        "Tax Compliance", "Budgeting & Forecasting", "Auditing", "Payroll Management", "Risk Management",
        "Corporate Finance", "Investment Analysis", "Accounts Payable/Receivable", "Internal Audit", "Cost Accounting",

        // 71..85: Education, Teaching & Training
        "Curriculum Development", "Classroom Management", "Lesson Planning", "Educational Technology", "Student Assessment",
        "Instructional Design", "Academic Advising", "Special Education", "Pedagogy", "Adult Education",
        "E-Learning", "Workshop Facilitation", "Educational Leadership", "Tutoring", "Course Evaluation",

        // 86..100: Engineering, Architecture & Construction
        "AutoCAD", "Civil Engineering", "Structural Analysis", "Electrical Systems", "Mechanical Design",
        "Project Estimation", "Quality Control", "Revit", "Site Supervision", "Safety Compliance",
        "HVAC Design", "Construction Management", "Surveying", "SolidWorks", "BIM Modeling",

        // 101..115: Business, HR & Operations Management
        "Operations Management", "Strategic Planning", "Business Analysis", "Human Resources (HR)", "Project Management",
        "Office Administration", "Talent Acquisition", "Performance Management", "Contract Negotiation", "Agile & Scrum",
        "Vendor Management", "Change Management", "Process Optimization", "Executive Support", "Event Planning",

        // 116..130: Marketing, Sales & Communications
        "Digital Marketing", "Content Strategy", "SEO", "Social Media Marketing", "Copywriting",
        "Public Relations", "Sales Management", "B2B Sales", "CRM Systems", "Brand Development",
        "Market Research", "Email Marketing", "Advertising", "Lead Generation", "Direct Sales",

        // 131..145: Hospitality, Tourism & Customer Service
        "Customer Relationship Management", "Conflict Resolution", "Front Desk Operations", "Food & Beverage Service", "Guest Relations",
        "Call Center Operations", "Reservation Systems", "Hospitality Management", "Event Coordination", "Customer Retention",
        "Concierge Services", "Menu Planning", "Housekeeping Management", "Help Desk Support", "Client Onboarding",

        // 146..160: Skilled Trades, Logistics & Supply Chain
        "Supply Chain Management", "Inventory Control", "Logistics Planning", "Procurement", "Electrical Installation",
        "Plumbing", "HVAC Maintenance", "Facilities Maintenance", "Workplace Safety (OSHA)", "Heavy Equipment Operation",
        "Warehouse Operations", "Quality Assurance", "Welding", "Preventative Maintenance", "Fleet Management",

        // 161..170: Data, Analytics & General Professional
        "Data Analysis", "Machine Learning", "Power BI", "Microsoft Excel", "Cybersecurity",
        "UI/UX Design", "Graphic Design", "Technical Writing", "Tableau", "Statistical Modeling"
    ];

    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);

        // Seeded rows use explicit ids; start the sequence above them.
        builder.Property(s => s.Id).UseIdentityByDefaultColumn().HasIdentityOptions(startValue: 1000);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(50);
        builder.Property(s => s.NormalizedName).IsRequired().HasMaxLength(50);

        builder.HasIndex(s => s.NormalizedName).IsUnique();

        builder.HasData(SeedNames.Select((name, i) => new Skill(i + 1, name)));
    }
}

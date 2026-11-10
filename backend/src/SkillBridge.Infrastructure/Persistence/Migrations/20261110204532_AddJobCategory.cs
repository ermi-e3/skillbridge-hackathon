using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                for (var i = 40; i < SkillBridge.Infrastructure.Persistence.Configurations.SkillConfiguration.SeedNames.Length; i++)
                {
                    var id = i + 1;
                    var name = SkillBridge.Infrastructure.Persistence.Configurations.SkillConfiguration.SeedNames[i].Replace("'", "''");
                    var norm = name.ToUpperInvariant();
                    migrationBuilder.Sql(
                        $"INSERT INTO \"Skills\" (\"Id\", \"Name\", \"NormalizedName\") VALUES ({id}, '{name}', '{norm}') ON CONFLICT DO NOTHING;");
                }
            }
            else
            {
            migrationBuilder.InsertData(
                table: "Skills",
                columns: new[] { "Id", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 41, "Patient Care", "PATIENT CARE" },
                    { 42, "Clinical Assessment", "CLINICAL ASSESSMENT" },
                    { 43, "Pharmacology", "PHARMACOLOGY" },
                    { 44, "Medical Records (EHR)", "MEDICAL RECORDS (EHR)" },
                    { 45, "First Aid & BLS", "FIRST AID & BLS" },
                    { 46, "Triage", "TRIAGE" },
                    { 47, "Phlebotomy", "PHLEBOTOMY" },
                    { 48, "Infection Control", "INFECTION CONTROL" },
                    { 49, "Nursing", "NURSING" },
                    { 50, "Health Education", "HEALTH EDUCATION" },
                    { 51, "Medical Laboratory", "MEDICAL LABORATORY" },
                    { 52, "Patient Counseling", "PATIENT COUNSELING" },
                    { 53, "Vital Signs Monitoring", "VITAL SIGNS MONITORING" },
                    { 54, "Public Health", "PUBLIC HEALTH" },
                    { 55, "Emergency Response", "EMERGENCY RESPONSE" },
                    { 56, "Financial Analysis", "FINANCIAL ANALYSIS" },
                    { 57, "Financial Modeling", "FINANCIAL MODELING" },
                    { 58, "Bookkeeping", "BOOKKEEPING" },
                    { 59, "QuickBooks", "QUICKBOOKS" },
                    { 60, "IFRS Compliance", "IFRS COMPLIANCE" },
                    { 61, "Tax Compliance", "TAX COMPLIANCE" },
                    { 62, "Budgeting & Forecasting", "BUDGETING & FORECASTING" },
                    { 63, "Auditing", "AUDITING" },
                    { 64, "Payroll Management", "PAYROLL MANAGEMENT" },
                    { 65, "Risk Management", "RISK MANAGEMENT" },
                    { 66, "Corporate Finance", "CORPORATE FINANCE" },
                    { 67, "Investment Analysis", "INVESTMENT ANALYSIS" },
                    { 68, "Accounts Payable/Receivable", "ACCOUNTS PAYABLE/RECEIVABLE" },
                    { 69, "Internal Audit", "INTERNAL AUDIT" },
                    { 70, "Cost Accounting", "COST ACCOUNTING" },
                    { 71, "Curriculum Development", "CURRICULUM DEVELOPMENT" },
                    { 72, "Classroom Management", "CLASSROOM MANAGEMENT" },
                    { 73, "Lesson Planning", "LESSON PLANNING" },
                    { 74, "Educational Technology", "EDUCATIONAL TECHNOLOGY" },
                    { 75, "Student Assessment", "STUDENT ASSESSMENT" },
                    { 76, "Instructional Design", "INSTRUCTIONAL DESIGN" },
                    { 77, "Academic Advising", "ACADEMIC ADVISING" },
                    { 78, "Special Education", "SPECIAL EDUCATION" },
                    { 79, "Pedagogy", "PEDAGOGY" },
                    { 80, "Adult Education", "ADULT EDUCATION" },
                    { 81, "E-Learning", "E-LEARNING" },
                    { 82, "Workshop Facilitation", "WORKSHOP FACILITATION" },
                    { 83, "Educational Leadership", "EDUCATIONAL LEADERSHIP" },
                    { 84, "Tutoring", "TUTORING" },
                    { 85, "Course Evaluation", "COURSE EVALUATION" },
                    { 86, "AutoCAD", "AUTOCAD" },
                    { 87, "Civil Engineering", "CIVIL ENGINEERING" },
                    { 88, "Structural Analysis", "STRUCTURAL ANALYSIS" },
                    { 89, "Electrical Systems", "ELECTRICAL SYSTEMS" },
                    { 90, "Mechanical Design", "MECHANICAL DESIGN" },
                    { 91, "Project Estimation", "PROJECT ESTIMATION" },
                    { 92, "Quality Control", "QUALITY CONTROL" },
                    { 93, "Revit", "REVIT" },
                    { 94, "Site Supervision", "SITE SUPERVISION" },
                    { 95, "Safety Compliance", "SAFETY COMPLIANCE" },
                    { 96, "HVAC Design", "HVAC DESIGN" },
                    { 97, "Construction Management", "CONSTRUCTION MANAGEMENT" },
                    { 98, "Surveying", "SURVEYING" },
                    { 99, "SolidWorks", "SOLIDWORKS" },
                    { 100, "BIM Modeling", "BIM MODELING" },
                    { 101, "Operations Management", "OPERATIONS MANAGEMENT" },
                    { 102, "Strategic Planning", "STRATEGIC PLANNING" },
                    { 103, "Business Analysis", "BUSINESS ANALYSIS" },
                    { 104, "Human Resources (HR)", "HUMAN RESOURCES (HR)" },
                    { 105, "Project Management", "PROJECT MANAGEMENT" },
                    { 106, "Office Administration", "OFFICE ADMINISTRATION" },
                    { 107, "Talent Acquisition", "TALENT ACQUISITION" },
                    { 108, "Performance Management", "PERFORMANCE MANAGEMENT" },
                    { 109, "Contract Negotiation", "CONTRACT NEGOTIATION" },
                    { 110, "Agile & Scrum", "AGILE & SCRUM" },
                    { 111, "Vendor Management", "VENDOR MANAGEMENT" },
                    { 112, "Change Management", "CHANGE MANAGEMENT" },
                    { 113, "Process Optimization", "PROCESS OPTIMIZATION" },
                    { 114, "Executive Support", "EXECUTIVE SUPPORT" },
                    { 115, "Event Planning", "EVENT PLANNING" },
                    { 116, "Digital Marketing", "DIGITAL MARKETING" },
                    { 117, "Content Strategy", "CONTENT STRATEGY" },
                    { 118, "SEO", "SEO" },
                    { 119, "Social Media Marketing", "SOCIAL MEDIA MARKETING" },
                    { 120, "Copywriting", "COPYWRITING" },
                    { 121, "Public Relations", "PUBLIC RELATIONS" },
                    { 122, "Sales Management", "SALES MANAGEMENT" },
                    { 123, "B2B Sales", "B2B SALES" },
                    { 124, "CRM Systems", "CRM SYSTEMS" },
                    { 125, "Brand Development", "BRAND DEVELOPMENT" },
                    { 126, "Market Research", "MARKET RESEARCH" },
                    { 127, "Email Marketing", "EMAIL MARKETING" },
                    { 128, "Advertising", "ADVERTISING" },
                    { 129, "Lead Generation", "LEAD GENERATION" },
                    { 130, "Direct Sales", "DIRECT SALES" },
                    { 131, "Customer Relationship Management", "CUSTOMER RELATIONSHIP MANAGEMENT" },
                    { 132, "Conflict Resolution", "CONFLICT RESOLUTION" },
                    { 133, "Front Desk Operations", "FRONT DESK OPERATIONS" },
                    { 134, "Food & Beverage Service", "FOOD & BEVERAGE SERVICE" },
                    { 135, "Guest Relations", "GUEST RELATIONS" },
                    { 136, "Call Center Operations", "CALL CENTER OPERATIONS" },
                    { 137, "Reservation Systems", "RESERVATION SYSTEMS" },
                    { 138, "Hospitality Management", "HOSPITALITY MANAGEMENT" },
                    { 139, "Event Coordination", "EVENT COORDINATION" },
                    { 140, "Customer Retention", "CUSTOMER RETENTION" },
                    { 141, "Concierge Services", "CONCIERGE SERVICES" },
                    { 142, "Menu Planning", "MENU PLANNING" },
                    { 143, "Housekeeping Management", "HOUSEKEEPING MANAGEMENT" },
                    { 144, "Help Desk Support", "HELP DESK SUPPORT" },
                    { 145, "Client Onboarding", "CLIENT ONBOARDING" },
                    { 146, "Supply Chain Management", "SUPPLY CHAIN MANAGEMENT" },
                    { 147, "Inventory Control", "INVENTORY CONTROL" },
                    { 148, "Logistics Planning", "LOGISTICS PLANNING" },
                    { 149, "Procurement", "PROCUREMENT" },
                    { 150, "Electrical Installation", "ELECTRICAL INSTALLATION" },
                    { 151, "Plumbing", "PLUMBING" },
                    { 152, "HVAC Maintenance", "HVAC MAINTENANCE" },
                    { 153, "Facilities Maintenance", "FACILITIES MAINTENANCE" },
                    { 154, "Workplace Safety (OSHA)", "WORKPLACE SAFETY (OSHA)" },
                    { 155, "Heavy Equipment Operation", "HEAVY EQUIPMENT OPERATION" },
                    { 156, "Warehouse Operations", "WAREHOUSE OPERATIONS" },
                    { 157, "Quality Assurance", "QUALITY ASSURANCE" },
                    { 158, "Welding", "WELDING" },
                    { 159, "Preventative Maintenance", "PREVENTATIVE MAINTENANCE" },
                    { 160, "Fleet Management", "FLEET MANAGEMENT" },
                    { 161, "Data Analysis", "DATA ANALYSIS" },
                    { 162, "Machine Learning", "MACHINE LEARNING" },
                    { 163, "Power BI", "POWER BI" },
                    { 164, "Microsoft Excel", "MICROSOFT EXCEL" },
                    { 165, "Cybersecurity", "CYBERSECURITY" },
                    { 166, "UI/UX Design", "UI/UX DESIGN" },
                    { 167, "Graphic Design", "GRAPHIC DESIGN" },
                    { 168, "Technical Writing", "TECHNICAL WRITING" },
                    { 169, "Tableau", "TABLEAU" },
                    { 170, "Statistical Modeling", "STATISTICAL MODELING" }
                });

            }

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_IsOpen_Category",
                table: "Jobs",
                columns: new[] { "IsOpen", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_IsOpen_Category",
                table: "Jobs");

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "Skills",
                keyColumn: "Id",
                keyValue: 170);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Jobs");
        }
    }
}

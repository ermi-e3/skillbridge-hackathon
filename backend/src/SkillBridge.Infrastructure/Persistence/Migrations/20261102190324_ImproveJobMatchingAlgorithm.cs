using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImproveJobMatchingAlgorithm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "JobSkills",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MinProficiency",
                table: "JobSkills",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceLevel",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "MinExperienceYears",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkArrangement",
                table: "Jobs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Proficiency",
                table: "CandidateSkills",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "CandidateSkills",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceLevel",
                table: "CandidateProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "CandidateProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreferredJobType",
                table: "CandidateProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PreferredWorkArrangement",
                table: "CandidateProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "CandidateProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CandidateExperiences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CandidateId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Company = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateExperiences_CandidateProfiles_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CandidateProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobSkills_JobId_IsRequired",
                table: "JobSkills",
                columns: new[] { "JobId", "IsRequired" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_IsOpen_ExperienceLevel",
                table: "Jobs",
                columns: new[] { "IsOpen", "ExperienceLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_IsOpen_JobType",
                table: "Jobs",
                columns: new[] { "IsOpen", "JobType" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_IsOpen_WorkArrangement",
                table: "Jobs",
                columns: new[] { "IsOpen", "WorkArrangement" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateExperiences_CandidateId",
                table: "CandidateExperiences",
                column: "CandidateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateExperiences");

            migrationBuilder.DropIndex(
                name: "IX_JobSkills_JobId_IsRequired",
                table: "JobSkills");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_IsOpen_ExperienceLevel",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_IsOpen_JobType",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_IsOpen_WorkArrangement",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "JobSkills");

            migrationBuilder.DropColumn(
                name: "MinProficiency",
                table: "JobSkills");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "MinExperienceYears",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WorkArrangement",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Proficiency",
                table: "CandidateSkills");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "CandidateSkills");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredJobType",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredWorkArrangement",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "CandidateProfiles");
        }
    }
}

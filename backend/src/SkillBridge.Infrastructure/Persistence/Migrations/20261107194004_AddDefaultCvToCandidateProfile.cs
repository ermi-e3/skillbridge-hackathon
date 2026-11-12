using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCvToCandidateProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCvContentType",
                table: "CandidateProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCvFileName",
                table: "CandidateProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DefaultCvFileSizeBytes",
                table: "CandidateProfiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultCvOriginalFileName",
                table: "CandidateProfiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DefaultCvUploadedAt",
                table: "CandidateProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCvContentType",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCvFileName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCvFileSizeBytes",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCvOriginalFileName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultCvUploadedAt",
                table: "CandidateProfiles");
        }
    }
}

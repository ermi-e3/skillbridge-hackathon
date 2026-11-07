using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCvFieldsToApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvContentType",
                table: "Applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvFileName",
                table: "Applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CvFileSizeBytes",
                table: "Applications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvOriginalFileName",
                table: "Applications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CvUploadedAt",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvContentType",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CvFileName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CvFileSizeBytes",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CvOriginalFileName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CvUploadedAt",
                table: "Applications");
        }
    }
}

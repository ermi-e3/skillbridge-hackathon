using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceGitHubUrlWithLinkedInUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GitHubUrl",
                table: "CandidateProfiles",
                newName: "LinkedInUrl");

            migrationBuilder.Sql(@"
                UPDATE ""CandidateProfiles""
                SET ""LinkedInUrl"" = REPLACE(""LinkedInUrl"", 'https://github.com/', 'https://www.linkedin.com/in/')
                WHERE ""LinkedInUrl"" LIKE '%github.com%';

                UPDATE ""CandidateProfiles""
                SET ""LinkedInUrl"" = 'https://www.linkedin.com/in/demo-candidate'
                WHERE ""LinkedInUrl"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""CandidateProfiles""
                SET ""LinkedInUrl"" = REPLACE(""LinkedInUrl"", 'https://www.linkedin.com/in/', 'https://github.com/')
                WHERE ""LinkedInUrl"" LIKE '%linkedin.com%';
            ");

            migrationBuilder.RenameColumn(
                name: "LinkedInUrl",
                table: "CandidateProfiles",
                newName: "GitHubUrl");
        }
    }
}


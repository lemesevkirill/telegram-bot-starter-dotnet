using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotTemplate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobsPollingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_Attempts_Id",
                table: "Jobs",
                columns: new[] { "Status", "Attempts", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_Status_Attempts_Id",
                table: "Jobs");
        }
    }
}

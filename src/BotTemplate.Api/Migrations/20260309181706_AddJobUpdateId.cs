using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotTemplate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobUpdateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "UpdateId",
                table: "Jobs",
                type: "bigint",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UpdateId",
                table: "Jobs",
                column: "UpdateId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_UpdateId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "UpdateId",
                table: "Jobs");
        }
    }
}

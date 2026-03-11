using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotTemplate.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddJobExecutionOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionOptions",
                table: "Jobs",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionOptions",
                table: "Jobs");
        }
    }
}

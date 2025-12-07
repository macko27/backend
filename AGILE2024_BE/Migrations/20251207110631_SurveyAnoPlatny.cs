using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class SurveyAnoPlatny : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "anoPlatny",
                table: "Surveys",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anoPlatny",
                table: "Surveys");
        }
    }
}

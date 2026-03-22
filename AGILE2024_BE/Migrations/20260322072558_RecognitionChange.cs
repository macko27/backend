using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class RecognitionChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "anoPlatny",
                table: "Recognitions");

            migrationBuilder.DropColumn(
                name: "state",
                table: "Recognitions");

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "RecognitionRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "anoPlatny",
                table: "RecognitionRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "RecognitionRecipients");

            migrationBuilder.DropColumn(
                name: "anoPlatny",
                table: "RecognitionRecipients");

            migrationBuilder.AddColumn<int>(
                name: "anoPlatny",
                table: "Recognitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "state",
                table: "Recognitions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class changeAnswerSurveyChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyOptions_SurveyAnswers_SurveyAnswerId",
                table: "SurveyOptions");

            migrationBuilder.DropIndex(
                name: "IX_SurveyOptions_SurveyAnswerId",
                table: "SurveyOptions");

            migrationBuilder.DropColumn(
                name: "SurveyAnswerId",
                table: "SurveyOptions");

            migrationBuilder.CreateTable(
                name: "SurveyOptionAnswer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SurveyAnswerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OptionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyOptionAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyOptionAnswer_SurveyAnswers_SurveyAnswerId",
                        column: x => x.SurveyAnswerId,
                        principalTable: "SurveyAnswers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptionAnswer_SurveyAnswerId",
                table: "SurveyOptionAnswer",
                column: "SurveyAnswerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyOptionAnswer");

            migrationBuilder.AddColumn<Guid>(
                name: "SurveyAnswerId",
                table: "SurveyOptions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyOptions_SurveyAnswerId",
                table: "SurveyOptions",
                column: "SurveyAnswerId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyOptions_SurveyAnswers_SurveyAnswerId",
                table: "SurveyOptions",
                column: "SurveyAnswerId",
                principalTable: "SurveyAnswers",
                principalColumn: "Id");
        }
    }
}

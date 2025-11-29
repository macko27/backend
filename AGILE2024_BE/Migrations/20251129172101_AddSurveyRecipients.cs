using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyOptions_Surveys_SurveyId",
                table: "SurveyOptions");

            migrationBuilder.RenameColumn(
                name: "question",
                table: "Surveys",
                newName: "SurveyType");

            migrationBuilder.RenameColumn(
                name: "answer",
                table: "SurveyOptions",
                newName: "Answer");

            migrationBuilder.RenameColumn(
                name: "SurveyId",
                table: "SurveyOptions",
                newName: "QuestionId");

            migrationBuilder.RenameIndex(
                name: "IX_SurveyOptions_SurveyId",
                table: "SurveyOptions",
                newName: "IX_SurveyOptions_QuestionId");

            migrationBuilder.AddColumn<DateTime>(
                name: "start",
                table: "Surveys",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "SurveyQuestion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    question = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SurveyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SurveyQuestion_Surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "Surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestion_SurveyId",
                table: "SurveyQuestion",
                column: "SurveyId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyOptions_SurveyQuestion_QuestionId",
                table: "SurveyOptions",
                column: "QuestionId",
                principalTable: "SurveyQuestion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyOptions_SurveyQuestion_QuestionId",
                table: "SurveyOptions");

            migrationBuilder.DropTable(
                name: "SurveyQuestion");

            migrationBuilder.DropColumn(
                name: "start",
                table: "Surveys");

            migrationBuilder.RenameColumn(
                name: "SurveyType",
                table: "Surveys",
                newName: "question");

            migrationBuilder.RenameColumn(
                name: "Answer",
                table: "SurveyOptions",
                newName: "answer");

            migrationBuilder.RenameColumn(
                name: "QuestionId",
                table: "SurveyOptions",
                newName: "SurveyId");

            migrationBuilder.RenameIndex(
                name: "IX_SurveyOptions_QuestionId",
                table: "SurveyOptions",
                newName: "IX_SurveyOptions_SurveyId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyOptions_Surveys_SurveyId",
                table: "SurveyOptions",
                column: "SurveyId",
                principalTable: "Surveys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class hlasovanieAnkety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyAnswers_Surveys_SurveyId",
                table: "SurveyAnswers");

            migrationBuilder.DropColumn(
                name: "answer",
                table: "SurveyAnswers");

            migrationBuilder.RenameColumn(
                name: "SurveyId",
                table: "SurveyAnswers",
                newName: "SurveyQuestionId");

            migrationBuilder.RenameIndex(
                name: "IX_SurveyAnswers_SurveyId",
                table: "SurveyAnswers",
                newName: "IX_SurveyAnswers_SurveyQuestionId");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeCardId",
                table: "SurveyAnswers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyAnswers_EmployeeCardId",
                table: "SurveyAnswers",
                column: "EmployeeCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyAnswers_EmployeeCards_EmployeeCardId",
                table: "SurveyAnswers",
                column: "EmployeeCardId",
                principalTable: "EmployeeCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyAnswers_SurveyQuestion_SurveyQuestionId",
                table: "SurveyAnswers",
                column: "SurveyQuestionId",
                principalTable: "SurveyQuestion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyAnswers_EmployeeCards_EmployeeCardId",
                table: "SurveyAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_SurveyAnswers_SurveyQuestion_SurveyQuestionId",
                table: "SurveyAnswers");

            migrationBuilder.DropIndex(
                name: "IX_SurveyAnswers_EmployeeCardId",
                table: "SurveyAnswers");

            migrationBuilder.DropColumn(
                name: "EmployeeCardId",
                table: "SurveyAnswers");

            migrationBuilder.RenameColumn(
                name: "SurveyQuestionId",
                table: "SurveyAnswers",
                newName: "SurveyId");

            migrationBuilder.RenameIndex(
                name: "IX_SurveyAnswers_SurveyQuestionId",
                table: "SurveyAnswers",
                newName: "IX_SurveyAnswers_SurveyId");

            migrationBuilder.AddColumn<string>(
                name: "answer",
                table: "SurveyAnswers",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyAnswers_Surveys_SurveyId",
                table: "SurveyAnswers",
                column: "SurveyId",
                principalTable: "Surveys",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

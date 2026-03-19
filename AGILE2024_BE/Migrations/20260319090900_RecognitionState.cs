using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class RecognitionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "state",
                table: "Recognitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionRecipients_EmployeeCardId",
                table: "RecognitionRecipients",
                column: "EmployeeCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecognitionRecipients_EmployeeCards_EmployeeCardId",
                table: "RecognitionRecipients",
                column: "EmployeeCardId",
                principalTable: "EmployeeCards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecognitionRecipients_EmployeeCards_EmployeeCardId",
                table: "RecognitionRecipients");

            migrationBuilder.DropIndex(
                name: "IX_RecognitionRecipients_EmployeeCardId",
                table: "RecognitionRecipients");

            migrationBuilder.DropColumn(
                name: "state",
                table: "Recognitions");
        }
    }
}

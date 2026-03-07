using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AGILE2024_BE.Migrations
{
    /// <inheritdoc />
    public partial class RecognitionAdd2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recognitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Predmet = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Text = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Odmena = table.Column<int>(type: "int", nullable: false),
                    EmployeeCardId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DateIn = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    anoPlatny = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recognitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recognitions_EmployeeCards_EmployeeCardId",
                        column: x => x.EmployeeCardId,
                        principalTable: "EmployeeCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecognitionRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecognitionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeCardId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecognitionRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecognitionRecipients_Recognitions_RecognitionId",
                        column: x => x.RecognitionId,
                        principalTable: "Recognitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RecognitionRecipients_RecognitionId",
                table: "RecognitionRecipients",
                column: "RecognitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Recognitions_EmployeeCardId",
                table: "Recognitions",
                column: "EmployeeCardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecognitionRecipients");

            migrationBuilder.DropTable(
                name: "Recognitions");
        }
    }
}

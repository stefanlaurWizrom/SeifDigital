using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteFisierToNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteFisier",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserNote_Id = table.Column<long>(type: "bigint", nullable: false),
                    UserFile_Id = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteFisier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteFisier_UserFile_UserFile_Id",
                        column: x => x.UserFile_Id,
                        principalSchema: "dbo",
                        principalTable: "UserFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NoteFisier_UserNote_UserNote_Id",
                        column: x => x.UserNote_Id,
                        principalSchema: "dbo",
                        principalTable: "UserNote",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteFisier_UserFile_Id",
                schema: "dbo",
                table: "NoteFisier",
                column: "UserFile_Id");

            migrationBuilder.CreateIndex(
                name: "IX_NoteFisier_UserNote_Id",
                schema: "dbo",
                table: "NoteFisier",
                column: "UserNote_Id");

            migrationBuilder.CreateIndex(
                name: "IX_NoteFisier_UserNote_Id_UserFile_Id",
                schema: "dbo",
                table: "NoteFisier",
                columns: new[] { "UserNote_Id", "UserFile_Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteFisier",
                schema: "dbo");
        }
    }
}

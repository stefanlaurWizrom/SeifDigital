using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleFileTypesSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileCategory",
                schema: "dbo",
                table: "UserFile",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "InformatieFisier",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InformatieSensibila_Id = table.Column<int>(type: "int", nullable: false),
                    UserFile_Id = table.Column<long>(type: "bigint", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformatieFisier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformatieFisier_InformatiiSensibile_InformatieSensibila_Id",
                        column: x => x.InformatieSensibila_Id,
                        principalSchema: "dbo",
                        principalTable: "InformatiiSensibile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InformatieFisier_UserFile_UserFile_Id",
                        column: x => x.UserFile_Id,
                        principalSchema: "dbo",
                        principalTable: "UserFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InformatieFisier_InformatieSensibila_Id",
                schema: "dbo",
                table: "InformatieFisier",
                column: "InformatieSensibila_Id");

            migrationBuilder.CreateIndex(
                name: "IX_InformatieFisier_InformatieSensibila_Id_UserFile_Id",
                schema: "dbo",
                table: "InformatieFisier",
                columns: new[] { "InformatieSensibila_Id", "UserFile_Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InformatieFisier_UserFile_Id",
                schema: "dbo",
                table: "InformatieFisier",
                column: "UserFile_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InformatieFisier",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "FileCategory",
                schema: "dbo",
                table: "UserFile");
        }
    }
}

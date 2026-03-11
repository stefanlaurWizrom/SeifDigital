using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToMultipleImageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InformatiiSensibile_UserFile_RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile");

            migrationBuilder.DropIndex(
                name: "IX_InformatiiSensibile_RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile");

            migrationBuilder.DropColumn(
                name: "RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                schema: "dbo",
                table: "UserFile",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            // StoredRelativePath deja există din migrația anterioară

            migrationBuilder.CreateTable(
                name: "InformatieImagine",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InformatieSensibila_Id = table.Column<int>(type: "int", nullable: false),
                    UserFile_Id = table.Column<long>(type: "bigint", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformatieImagine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformatieImagine_InformatiiSensibile_InformatieSensibila_Id",
                        column: x => x.InformatieSensibila_Id,
                        principalSchema: "dbo",
                        principalTable: "InformatiiSensibile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InformatieImagine_UserFile_UserFile_Id",
                        column: x => x.UserFile_Id,
                        principalSchema: "dbo",
                        principalTable: "UserFile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InformatieImagine_InformatieSensibila_Id",
                schema: "dbo",
                table: "InformatieImagine",
                column: "InformatieSensibila_Id");

            migrationBuilder.CreateIndex(
                name: "IX_InformatieImagine_InformatieSensibila_Id_UserFile_Id",
                schema: "dbo",
                table: "InformatieImagine",
                columns: new[] { "InformatieSensibila_Id", "UserFile_Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InformatieImagine_UserFile_Id",
                schema: "dbo",
                table: "InformatieImagine",
                column: "UserFile_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InformatieImagine",
                schema: "dbo");

            // Nu eliminam StoredRelativePath deoarece va fi necesar și mai târziu

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                schema: "dbo",
                table: "UserFile",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InformatiiSensibile_RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile",
                column: "RelatedImageFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_InformatiiSensibile_UserFile_RelatedImageFileId",
                schema: "dbo",
                table: "InformatiiSensibile",
                column: "RelatedImageFileId",
                principalSchema: "dbo",
                principalTable: "UserFile",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

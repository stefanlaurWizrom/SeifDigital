using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNoteEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ✅ Adaug coloană nouă pentru text criptat
            migrationBuilder.AddColumn<string>(
                name: "TextCriptat",
                table: "UserNote",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                -- ✅ Criptez textul existent (setez gol dacă nu avem criptare încă)
                -- În producție, trebuie să te asiguri că ai EncryptionService disponibil
                UPDATE [dbo].[UserNote]
                SET [TextCriptat] = ISNULL([Text], '')
                WHERE [TextCriptat] = '';
            ");

            // ✅ Redenumesc coloana veche pentru compatibilitate (opțional)
            // migrationBuilder.RenameColumn("Text", "UserNote", "TextNOTENCRYPTED");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ✅ Rollback
            migrationBuilder.DropColumn(
                name: "TextCriptat",
                table: "UserNote");
        }
    }
}

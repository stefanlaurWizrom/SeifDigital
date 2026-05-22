using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginServerVerificationFieldsToManagedCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginCertificateIssuer",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginCertificateExpiryDate",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginCertificateSubject",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginDaysUntilExpiry",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginServerIP",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginServerPort",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "int",
                nullable: false,
                defaultValue: 443);

            migrationBuilder.AddColumn<bool>(
                name: "IsCertificateMismatch",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationMethod",
                schema: "dbo",
                table: "ManagedCertificates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginCertificateIssuer",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "OriginCertificateExpiryDate",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "OriginCertificateSubject",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "OriginDaysUntilExpiry",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "OriginServerIP",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "OriginServerPort",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "IsCertificateMismatch",
                schema: "dbo",
                table: "ManagedCertificates");

            migrationBuilder.DropColumn(
                name: "VerificationMethod",
                schema: "dbo",
                table: "ManagedCertificates");
        }
    }
}

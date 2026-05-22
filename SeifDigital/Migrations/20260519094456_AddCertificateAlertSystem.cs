using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeifDigital.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateAlertSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Table and indexes already created via SQL scripts
            // This migration is here as a placeholder for EF Core tracking
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateAlertLog",
                schema: "dbo");
        }
    }
}

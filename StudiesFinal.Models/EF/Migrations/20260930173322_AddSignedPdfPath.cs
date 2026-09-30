using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudiesFinal.Models.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddSignedPdfPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignedPdfPath",
                table: "Studies",
                type: "TEXT",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SignedPdfPath",
                table: "Studies");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudiesFinal.Models.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddLastNameToPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Patients",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Patients");
        }
    }
}

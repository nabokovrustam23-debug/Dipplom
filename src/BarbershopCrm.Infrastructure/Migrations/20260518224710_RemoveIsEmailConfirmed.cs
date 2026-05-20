using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsEmailConfirmed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_IsEmailConfirmed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsEmailConfirmed",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailConfirmed",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_IsEmailConfirmed",
                table: "Users",
                sql: "IsEmailConfirmed IN (0,1)");
        }
    }
}

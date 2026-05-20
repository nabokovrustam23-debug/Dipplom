using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBranchCoordinatesAndUserTokenCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTokens_Purpose",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Branches");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTokens_Purpose",
                table: "UserTokens",
                sql: "Purpose IN ('PasswordReset')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserTokens_Purpose",
                table: "UserTokens");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserTokens",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "(datetime('now'))");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Branches",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Branches",
                type: "REAL",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "BranchId",
                keyValue: 1,
                columns: new[] { "Latitude", "Longitude" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "BranchId",
                keyValue: 2,
                columns: new[] { "Latitude", "Longitude" },
                values: new object[] { null, null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserTokens_Purpose",
                table: "UserTokens",
                sql: "Purpose IN ('EmailVerification','PasswordReset')");
        }
    }
}

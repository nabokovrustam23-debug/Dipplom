using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyFieldsToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscountPercent",
                table: "Bookings",
                type: "NUMERIC",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LoyaltyDiscountReason",
                table: "Bookings",
                type: "TEXT",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_LoyaltyDiscount",
                table: "Bookings",
                sql: "LoyaltyDiscountPercent >= 0 AND LoyaltyDiscountPercent <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_LoyaltyDiscount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountPercent",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountReason",
                table: "Bookings");
        }
    }
}

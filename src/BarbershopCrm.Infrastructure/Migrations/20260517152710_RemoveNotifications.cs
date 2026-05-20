using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipientPersonaId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedBookingId = table.Column<int>(type: "INTEGER", nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "(datetime('now'))"),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Pending"),
                    Subject = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.CheckConstraint("CK_Notifications_Channel", "Channel IN ('Email','Sms','InApp')");
                    table.CheckConstraint("CK_Notifications_Status", "Status IN ('Pending','Sent','Failed')");
                    table.ForeignKey(
                        name: "FK_Notifications_Bookings_RelatedBookingId",
                        column: x => x.RelatedBookingId,
                        principalTable: "Bookings",
                        principalColumn: "BookingId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Notifications_Persona_RecipientPersonaId",
                        column: x => x.RecipientPersonaId,
                        principalTable: "Persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientPersonaId",
                table: "Notifications",
                column: "RecipientPersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RelatedBookingId",
                table: "Notifications",
                column: "RelatedBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Status_Created",
                table: "Notifications",
                columns: new[] { "Status", "CreatedAt" });
        }
    }
}

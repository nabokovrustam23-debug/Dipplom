using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BarbershopCrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveConsentLogTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentLog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentLog",
                columns: table => new
                {
                    ConsentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PersonaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "(datetime('now'))"),
                    ConsentType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentLog", x => x.ConsentId);
                    table.CheckConstraint("CK_ConsentLog_Type", "ConsentType IN ('PersonalData','Marketing')");
                    table.ForeignKey(
                        name: "FK_ConsentLog_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentLog_PersonaId",
                table: "ConsentLog",
                column: "PersonaId");
        }
    }
}

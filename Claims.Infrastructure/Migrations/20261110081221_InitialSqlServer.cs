using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Entity = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HttpRequestType = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoverAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoverId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HttpRequestType = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Covers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Premium = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Covers", x => x.Id);
                    table.CheckConstraint("CK_Covers_Dates", "[EndDate] > [StartDate]");
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    CoverId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Created = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DamageCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.CheckConstraint("CK_Claims_DamageCost", "CAST([DamageCost] AS decimal(18, 2)) BETWEEN 0 AND 100000");
                    table.ForeignKey(
                        name: "FK_Claims_Covers_CoverId",
                        column: x => x.CoverId,
                        principalTable: "Covers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditOutbox_Created",
                table: "AuditOutbox",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAudits_EventId",
                table: "ClaimAudits",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CoverId",
                table: "Claims",
                column: "CoverId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverAudits_EventId",
                table: "CoverAudits",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditOutbox");

            migrationBuilder.DropTable(
                name: "ClaimAudits");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "CoverAudits");

            migrationBuilder.DropTable(
                name: "Covers");
        }
    }
}

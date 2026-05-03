using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF migration: inline column arrays are clearer than cached statics.

namespace B2.Infrastructure.Migrations;

/// <inheritdoc />
public partial class BaselineSchema0001 : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ChangesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyHeaders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    OnDutyOfficerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TotalVehicles = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSummaryComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyHeaders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Declarations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeclarationNumber = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    ProcessorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GoodsName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    YardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EnteredByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    KvgsConfirmedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    KvgsConfirmedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Declarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResponseStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseContentType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => new { x.Key, x.RequestHash });
                });

            migrationBuilder.CreateTable(
                name: "Processors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedLoginWindowStartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Yards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Yards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeclarationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlateNumber = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    PlateLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true, computedColumnSql: "substr(\"PlateNumber\", -4)", stored: true),
                    TrailerOrContainerNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EntryTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExitMarked = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExitTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExitConfirmedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    EnteredByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Declarations_DeclarationId",
                        column: x => x.DeclarationId,
                        principalTable: "Declarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ActorAt",
                table: "AuditLogs",
                columns: new[] { "ActorUserId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityIdAt",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyHeaders_Date",
                table: "DailyHeaders",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_BusinessDate",
                table: "Declarations",
                column: "BusinessDate");

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_DeclarationNumber",
                table: "Declarations",
                column: "DeclarationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_ProcessorId",
                table: "Declarations",
                column: "ProcessorId");

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_Status",
                table: "Declarations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_YardId",
                table: "Declarations",
                column: "YardId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_CreatedAt",
                table: "IdempotencyKeys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Processors_FullName",
                table: "Processors",
                column: "FullName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicle_PlateEntry",
                table: "Vehicles",
                columns: new[] { "PlateNumber", "EntryTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DeclarationId",
                table: "Vehicles",
                column: "DeclarationId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_EntryTime",
                table: "Vehicles",
                column: "EntryTime");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ExitMarked",
                table: "Vehicles",
                column: "ExitMarked");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PlateLast4",
                table: "Vehicles",
                column: "PlateLast4");

            migrationBuilder.CreateIndex(
                name: "IX_Yards_Code",
                table: "Yards",
                column: "Code",
                unique: true);

            var seedTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var adminId = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var processorId = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var yardId = new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc");

            migrationBuilder.InsertData(
                table: "Processors",
                columns: new[] { "Id", "FullName", "IsActive", "CreatedAt" },
                values: new object[] { processorId, "Nguyen Van Nam (seed)", true, seedTime });

            migrationBuilder.InsertData(
                table: "Yards",
                columns: new[] { "Id", "Code", "Name", "IsActive", "CreatedAt" },
                values: new object[] { yardId, "H1", "Bai Huu Nghi (seed)", true, seedTime });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id", "Username", "PasswordHash", "FullName", "Role", "IsActive", "MustChangePassword",
                    "FailedLoginCount", "FailedLoginWindowStartedAt", "LockedUntil", "LastLoginAt", "CreatedAt",
                },
                values: new object[]
                {
                    adminId,
                    "admin",
                    "$2a$10$DC08sVNrW.UQgIG3Yspllu6ugYvMu8VPWtkLPYROYKs.er.3h.eWy",
                    "System Administrator",
                    "Admin",
                    true,
                    true,
                    0,
                    null,
                    null,
                    null,
                    seedTime,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DailyHeaders");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropTable(
                name: "Processors");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Yards");

            migrationBuilder.DropTable(
                name: "Declarations");
        }
}

#pragma warning restore CA1861


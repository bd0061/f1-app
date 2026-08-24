using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace F1Ticketing.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Code = table.Column<string>(
                        type: "character varying(3)",
                        maxLength: 3,
                        nullable: false
                    ),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AdditionalInformation = table.Column<string>(type: "text", nullable: false),
                    DiscountUntil = table.Column<DateOnly>(type: "date", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationCode = table.Column<string>(type: "text", nullable: false),
                    PromoCode = table.Column<string>(type: "text", nullable: false),
                    PromoCodeUsed = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    Address1 = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    TotalPrice = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "RaceDays",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BasePrice = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: false
                    ),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaceDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaceDays_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "SeatingZones",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Characteristics = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Surcharge = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatingZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatingZones_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "PaddockPasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    PitLane = table.Column<bool>(type: "boolean", nullable: false),
                    Food = table.Column<bool>(type: "boolean", nullable: false),
                    Drinks = table.Column<bool>(type: "boolean", nullable: false),
                    TotalPrice = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaddockPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaddockPasses_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TicketDays",
                columns: table => new
                {
                    Id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceDayId = table.Column<int>(type: "integer", nullable: false),
                    SeatingZoneId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(
                        type: "numeric(12,2)",
                        precision: 12,
                        scale: 2,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketDays_RaceDays_RaceDayId",
                        column: x => x.RaceDayId,
                        principalTable: "RaceDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TicketDays_SeatingZones_SeatingZoneId",
                        column: x => x.SeatingZoneId,
                        principalTable: "SeatingZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_TicketDays_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_PaddockPasses_TicketId",
                table: "PaddockPasses",
                column: "TicketId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_RaceDays_RaceId_Date",
                table: "RaceDays",
                columns: new[] { "RaceId", "Date" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_SeatingZones_RaceId_Name",
                table: "SeatingZones",
                columns: new[] { "RaceId", "Name" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_TicketDays_RaceDayId",
                table: "TicketDays",
                column: "RaceDayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TicketDays_SeatingZoneId",
                table: "TicketDays",
                column: "SeatingZoneId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TicketDays_TicketId_RaceDayId",
                table: "TicketDays",
                columns: new[] { "TicketId", "RaceDayId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CurrencyId",
                table: "Tickets",
                column: "CurrencyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_PromoCode",
                table: "Tickets",
                column: "PromoCode",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RegistrationCode",
                table: "Tickets",
                column: "RegistrationCode",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PaddockPasses");

            migrationBuilder.DropTable(name: "TicketDays");

            migrationBuilder.DropTable(name: "RaceDays");

            migrationBuilder.DropTable(name: "SeatingZones");

            migrationBuilder.DropTable(name: "Tickets");

            migrationBuilder.DropTable(name: "Races");

            migrationBuilder.DropTable(name: "Currencies");
        }
    }
}

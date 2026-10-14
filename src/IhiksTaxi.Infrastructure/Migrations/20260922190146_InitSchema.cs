using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IhiksTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // БД из ЛР1 уже содержит driver/vehicle/passenger/trip,
            // поэтому миграция добавляет только новые сущности курсового.

            migrationBuilder.CreateTable(
                name: "tariff",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_cost = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    rate_per_km = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    rate_per_minute = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    demand_factor_default = table.Column<decimal>(type: "numeric(4,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trip_status",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rating",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    driver_id = table.Column<int>(type: "integer", nullable: false),
                    trip_id = table.Column<int>(type: "integer", nullable: true),
                    rate = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rating", x => x.id);
                    table.CheckConstraint("CK_rating_rate", "rate >= 1 AND rate <= 5");
                    table.ForeignKey(
                        name: "FK_rating_driver",
                        column: x => x.driver_id,
                        principalTable: "driver",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_rating_trip",
                        column: x => x.trip_id,
                        principalTable: "trip",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // справочник тарифов (классы совпадают со значениями vehicle.class из ЛР1)
            migrationBuilder.Sql(@"
                INSERT INTO tariff (id, name, base_cost, rate_per_km, rate_per_minute, demand_factor_default) VALUES
                    (1, 'Эконом',   150.00, 20.00, 8.00, 1.00),
                    (2, 'Комфорт',  200.00, 25.00, 10.00, 1.00),
                    (3, 'Бизнес',   300.00, 35.00, 15.00, 1.30);");

            // статусы поездок
            migrationBuilder.Sql(@"
                INSERT INTO trip_status (id, name) VALUES
                    (1, 'Created'),
                    (2, 'SearchingDriver'),
                    (3, 'DriverAssigned'),
                    (4, 'DriverArriving'),
                    (5, 'PassengerPickedUp'),
                    (6, 'InProgress'),
                    (7, 'Completed'),
                    (8, 'Cancelled');");

            // счётчики для новых таблиц продолжаются после явных id
            migrationBuilder.Sql(@"
                SELECT pg_catalog.setval(pg_get_serial_sequence('tariff', 'id'), 3, true);
                SELECT pg_catalog.setval(pg_get_serial_sequence('trip_status', 'id'), 8, true);");

            // колонки пассажира для входа в приложение (пароль храним как хеш)
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "passenger",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "passenger",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // автомобилю — тариф соответственно классу
            migrationBuilder.AddColumn<int>(
                name: "tariff_id",
                table: "vehicle",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE vehicle SET tariff_id = t.id
                FROM tariff t
                WHERE vehicle.class = t.name;");

            // поездке — статус (уже существующие поездки считаем завершёнными)
            migrationBuilder.AlterColumn<int>(
                name: "driver_id",
                table: "trip",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "duration_min",
                table: "trip",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "trip_status_id",
                table: "trip",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.CreateIndex(
                name: "Passenger_IX_Email",
                table: "passenger",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Rating_IX_DriverId",
                table: "rating",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "Rating_IX_TripId",
                table: "rating",
                column: "trip_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Tariff_IX_Name",
                table: "tariff",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_trip_status_id",
                table: "trip",
                column: "trip_status_id");

            migrationBuilder.CreateIndex(
                name: "TripStatus_IX_Name",
                table: "trip_status",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tariff_id",
                table: "vehicle",
                column: "tariff_id");

            migrationBuilder.AddForeignKey(
                name: "FK_trip_trip_status",
                table: "trip",
                column: "trip_status_id",
                principalTable: "trip_status",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_vehicle_tariff",
                table: "vehicle",
                column: "tariff_id",
                principalTable: "tariff",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_trip_trip_status",
                table: "trip");

            migrationBuilder.DropForeignKey(
                name: "FK_vehicle_tariff",
                table: "vehicle");

            migrationBuilder.DropIndex(
                name: "IX_trip_trip_status_id",
                table: "trip");

            migrationBuilder.DropIndex(
                name: "IX_vehicle_tariff_id",
                table: "vehicle");

            migrationBuilder.DropIndex(
                name: "TripStatus_IX_Name",
                table: "trip_status");

            migrationBuilder.DropIndex(
                name: "Tariff_IX_Name",
                table: "tariff");

            migrationBuilder.DropIndex(
                name: "Rating_IX_TripId",
                table: "rating");

            migrationBuilder.DropIndex(
                name: "Rating_IX_DriverId",
                table: "rating");

            migrationBuilder.DropIndex(
                name: "Passenger_IX_Email",
                table: "passenger");

            migrationBuilder.DropColumn(
                name: "trip_status_id",
                table: "trip");

            migrationBuilder.DropColumn(
                name: "duration_min",
                table: "trip");

            migrationBuilder.DropColumn(
                name: "tariff_id",
                table: "vehicle");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "passenger");

            migrationBuilder.DropColumn(
                name: "email",
                table: "passenger");

            migrationBuilder.DropTable(
                name: "rating");

            migrationBuilder.DropTable(
                name: "trip_status");

            migrationBuilder.DropTable(
                name: "tariff");
        }
    }
}
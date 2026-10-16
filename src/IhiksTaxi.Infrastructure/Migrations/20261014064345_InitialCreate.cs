using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IhiksTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "driver",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    experience_years = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_driver", x => x.id);
                    table.CheckConstraint("CK_driver_experience", "experience_years >= 1 AND experience_years <= 60");
                });

            migrationBuilder.CreateTable(
                name: "passenger",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passenger", x => x.id);
                });

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
                name: "vehicle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    driver_id = table.Column<int>(type: "integer", nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    @class = table.Column<string>(name: "class", type: "character varying(20)", maxLength: 20, nullable: false),
                    plate = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    tariff_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicle_driver",
                        column: x => x.driver_id,
                        principalTable: "driver",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_vehicle_tariff",
                        column: x => x.tariff_id,
                        principalTable: "tariff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    passenger_id = table.Column<int>(type: "integer", nullable: false),
                    driver_id = table.Column<int>(type: "integer", nullable: true),
                    from_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    to_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    distance_km = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    trip_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    duration_min = table.Column<int>(type: "integer", nullable: true),
                    trip_status_id = table.Column<int>(type: "integer", nullable: false),
                    tariff_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip", x => x.id);
                    table.CheckConstraint("CK_trip_cost", "cost >= 0");
                    table.CheckConstraint("CK_trip_distance", "distance_km >= 0");
                    table.ForeignKey(
                        name: "FK_trip_driver",
                        column: x => x.driver_id,
                        principalTable: "driver",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_trip_passenger",
                        column: x => x.passenger_id,
                        principalTable: "passenger",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_trip_tariff",
                        column: x => x.tariff_id,
                        principalTable: "tariff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_trip_trip_status",
                        column: x => x.trip_status_id,
                        principalTable: "trip_status",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_trip_tariff_id",
                table: "trip",
                column: "tariff_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_trip_status_id",
                table: "trip",
                column: "trip_status_id");

            migrationBuilder.CreateIndex(
                name: "Trip_IX_DriverId",
                table: "trip",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "Trip_IX_PassengerId",
                table: "trip",
                column: "passenger_id");

            migrationBuilder.CreateIndex(
                name: "TripStatus_IX_Name",
                table: "trip_status",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_tariff_id",
                table: "vehicle",
                column: "tariff_id");

            migrationBuilder.CreateIndex(
                name: "Vehicle_IX_DriverId",
                table: "vehicle",
                column: "driver_id");

            migrationBuilder.Sql(@"
                INSERT INTO tariff (id, name, base_cost, rate_per_km, rate_per_minute, demand_factor_default) VALUES
                    (1, 'Эконом',  150.00, 20.00, 8.00, 1.00),
                    (2, 'Комфорт', 200.00, 25.00, 10.00, 1.00),
                    (3, 'Бизнес',  300.00, 35.00, 15.00, 1.30);
                SELECT setval(pg_get_serial_sequence('tariff', 'id'), 3, true);");

            migrationBuilder.Sql(@"
                INSERT INTO trip_status (id, name) VALUES
                    (1, 'Created'),
                    (2, 'SearchingDriver'),
                    (3, 'DriverAssigned'),
                    (4, 'DriverArriving'),
                    (5, 'PassengerPickedUp'),
                    (6, 'InProgress'),
                    (7, 'Completed'),
                    (8, 'Cancelled');
                SELECT setval(pg_get_serial_sequence('trip_status', 'id'), 8, true);");

            migrationBuilder.Sql(@"
                INSERT INTO driver (id, full_name, phone, experience_years, category) VALUES
                    (1, 'Алексей Смирнов',  '+7 (900) 111-22-33',  7, 'B'),
                    (2, 'Мария Иванова',    '+7 (900) 444-55-66',  3, 'B'),
                    (3, 'Дмитрий Кузнецов', '+7 (900) 777-88-99', 12, 'B'),
                    (4, 'Ольга Соколова',   '+7 (900) 555-66-77',  5, 'B'),
                    (5, 'Виктор Егоров',    '+7 (903) 321-54-76',  4, 'B');
                SELECT setval(pg_get_serial_sequence('driver', 'id'), 5, true);");

            migrationBuilder.Sql(@"
                INSERT INTO vehicle (id, driver_id, brand, model, class, plate, tariff_id) VALUES
                    (1, 1, 'Lada',    'Vesta',   'Эконом',  'A777BC 77', 1),
                    (2, 2, 'Kia',     'Rio',     'Комфорт', 'B555MK 77', 2),
                    (3, 3, 'Toyota',  'Camry',   'Бизнес',  'C111OP 77', 3),
                    (4, 4, 'Skoda',   'Octavia', 'Комфорт', 'K222XA 77', 2),
                    (5, 2, 'Renault', 'Logan',   'Эконом',  'M333CT 77', 1);
                SELECT setval(pg_get_serial_sequence('vehicle', 'id'), 5, true);");

            migrationBuilder.Sql(@"
                INSERT INTO passenger (id, full_name, phone, email) VALUES
                    (1, 'Игорь Соловьёв', '+7 (921) 100-20-30', 'solovyev@example.com'),
                    (2, 'Анна Петрова',   '+7 (921) 200-30-40', 'petrova@example.com'),
                    (3, 'Сергей Волков',  '+7 (921) 300-40-50', 'volkov@example.com'),
                    (4, 'Елена Морозова', '+7 (921) 400-50-60', 'morozova@example.com');
                SELECT setval(pg_get_serial_sequence('passenger', 'id'), 4, true);");

            migrationBuilder.Sql(@"
                INSERT INTO trip (id, passenger_id, driver_id, from_address, to_address, distance_km, cost, trip_date, duration_min, trip_status_id, tariff_id) VALUES
                    (1, 1, 1, 'ул. Ленина, 12',      'ТЦ «Галерея»',           6.40,  430.00, '2026-09-28 09:15:00', 19, 7, 1),
                    (2, 1, 3, 'аэропорт Пулково',    'гостиница «Астория»',   21.50, 2109.25, '2026-10-02 18:40:00', 38, 7, 3);
                SELECT setval(pg_get_serial_sequence('trip', 'id'), 2, true);

                INSERT INTO rating (id, driver_id, trip_id, rate) VALUES
                    (1, 1, 1, 5),
                    (2, 3, 2, 4);
                SELECT setval(pg_get_serial_sequence('rating', 'id'), 2, true);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rating");

            migrationBuilder.DropTable(
                name: "vehicle");

            migrationBuilder.DropTable(
                name: "trip");

            migrationBuilder.DropTable(
                name: "driver");

            migrationBuilder.DropTable(
                name: "passenger");

            migrationBuilder.DropTable(
                name: "tariff");

            migrationBuilder.DropTable(
                name: "trip_status");
        }
    }
}

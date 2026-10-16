using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IhiksTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStreetsAndPassengerPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone_normalized",
                table: "passenger",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_street_id",
                table: "driver",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "street",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_street", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "Passenger_IX_PhoneNormalized",
                table: "passenger",
                column: "phone_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_current_street_id",
                table: "driver",
                column: "current_street_id");

            migrationBuilder.CreateIndex(
                name: "Street_IX_Name",
                table: "street",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_driver_street",
                table: "driver",
                column: "current_street_id",
                principalTable: "street",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // справочник улиц Иванова взят из лабораторной работы №3 (db/StreetDirectory.sql).
            // идентификаторы 1..20 закреплены явно, чтобы совпадать с картой города из RouteCalculator.
            migrationBuilder.Sql(@"
                INSERT INTO street (id, name, latitude, longitude) VALUES
                    (1,  'пр-т Ленина',           56.993800, 40.977600),
                    (2,  'Шереметевский пр-т',    56.990600, 40.983100),
                    (3,  'ул. Ташкентская',       57.009800, 40.946300),
                    (4,  'ул. Куконковых',        57.008500, 40.942200),
                    (5,  'ул. Рабфаковская',      57.002500, 40.937000),
                    (6,  'пр-т Строителей',       57.003400, 40.958100),
                    (7,  'ул. Лежневская',        56.973900, 40.987200),
                    (8,  'Вокзальная площадь',    56.991200, 40.938500),
                    (9,  'Аэропорт (Южный)',      56.941200, 40.942300),
                    (10, 'ул. Станкостроителей',  57.001000, 40.981500),
                    (11, 'ул. Смирнова',          56.982100, 40.964400),
                    (12, 'ул. 10 Августа',        56.996200, 40.971300),
                    (13, 'ул. Сакко',             56.994800, 40.976000),
                    (14, 'ул. Багаева',           56.996800, 40.949800),
                    (15, 'ул. Парижской Коммуны', 56.988600, 40.969000),
                    (16, 'пр-т Текстильщиков',    56.983600, 40.951200),
                    (17, 'ул. Почтовая',          56.994700, 40.968900),
                    (18, 'ул. Крутицкая',         57.006500, 40.923000),
                    (19, 'ул. Суворова',          56.988100, 40.980700),
                    (20, 'ул. 40 лет Победы',     57.004300, 40.995000);
                SELECT setval(pg_get_serial_sequence('street', 'id'), 20, true);

                UPDATE driver SET current_street_id = 11 WHERE id = 1;
                UPDATE driver SET current_street_id = 3  WHERE id = 2;
                UPDATE driver SET current_street_id = 7  WHERE id = 3;
                UPDATE driver SET current_street_id = 2  WHERE id = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_driver_street",
                table: "driver");

            migrationBuilder.DropTable(
                name: "street");

            migrationBuilder.DropIndex(
                name: "Passenger_IX_PhoneNormalized",
                table: "passenger");

            migrationBuilder.DropIndex(
                name: "IX_driver_current_street_id",
                table: "driver");

            migrationBuilder.DropColumn(
                name: "phone_normalized",
                table: "passenger");

            migrationBuilder.DropColumn(
                name: "current_street_id",
                table: "driver");
        }
    }
}

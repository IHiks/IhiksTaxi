using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IhiksTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStreetCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // координаты справочника были выдуманы в ЛР3 и не совпадали с реальными улицами Иванова.
            // значения ниже взяты их открытых данных OpenStreetMap (проверено через геокодер Photon).
            migrationBuilder.Sql(@"
                UPDATE street SET latitude = 57.007628, longitude = 40.972188 WHERE id = 1;
                UPDATE street SET latitude = 57.011455, longitude = 40.987637 WHERE id = 2;
                UPDATE street SET latitude = 56.975166, longitude = 40.965442 WHERE id = 3;
                UPDATE street SET latitude = 56.971700, longitude = 41.012535 WHERE id = 4;
                UPDATE street SET latitude = 57.006918, longitude = 40.949740 WHERE id = 5;
                UPDATE street SET latitude = 56.969265, longitude = 41.012727 WHERE id = 6;
                UPDATE street SET latitude = 56.982410, longitude = 40.980367 WHERE id = 7;
                UPDATE street SET latitude = 57.017068, longitude = 40.979889 WHERE id = 8;
                UPDATE street SET latitude = 56.941138, longitude = 40.938973 WHERE id = 9;
                UPDATE street SET latitude = 56.975347, longitude = 40.922410 WHERE id = 10;
                UPDATE street SET latitude = 56.992800, longitude = 40.985865 WHERE id = 11;
                UPDATE street SET latitude = 56.993170, longitude = 40.993502 WHERE id = 12;
                UPDATE street SET latitude = 57.001196, longitude = 40.991031 WHERE id = 13;
                UPDATE street SET latitude = 56.992758, longitude = 40.973624 WHERE id = 14;
                UPDATE street SET latitude = 56.999292, longitude = 40.907707 WHERE id = 15;
                UPDATE street SET latitude = 56.958153, longitude = 40.981250 WHERE id = 16;
                UPDATE street SET latitude = 56.994871, longitude = 40.967087 WHERE id = 17;
                UPDATE street SET latitude = 56.998686, longitude = 40.976123 WHERE id = 18;
                UPDATE street SET latitude = 56.993707, longitude = 41.016269 WHERE id = 19;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // возвращаем прежние (ошибочные) значения справочника из ЛР3
            migrationBuilder.Sql(@"
                UPDATE street SET latitude = 56.993800, longitude = 40.977600 WHERE id = 1;
                UPDATE street SET latitude = 56.990600, longitude = 40.983100 WHERE id = 2;
                UPDATE street SET latitude = 57.009800, longitude = 40.946300 WHERE id = 3;
                UPDATE street SET latitude = 57.008500, longitude = 40.942200 WHERE id = 4;
                UPDATE street SET latitude = 57.002500, longitude = 40.937000 WHERE id = 5;
                UPDATE street SET latitude = 57.003400, longitude = 40.958100 WHERE id = 6;
                UPDATE street SET latitude = 56.973900, longitude = 40.987200 WHERE id = 7;
                UPDATE street SET latitude = 56.991200, longitude = 40.938500 WHERE id = 8;
                UPDATE street SET latitude = 56.941200, longitude = 40.942300 WHERE id = 9;
                UPDATE street SET latitude = 57.001000, longitude = 40.981500 WHERE id = 10;
                UPDATE street SET latitude = 56.982100, longitude = 40.964400 WHERE id = 11;
                UPDATE street SET latitude = 56.996200, longitude = 40.971300 WHERE id = 12;
                UPDATE street SET latitude = 56.994800, longitude = 40.976000 WHERE id = 13;
                UPDATE street SET latitude = 56.996800, longitude = 40.949800 WHERE id = 14;
                UPDATE street SET latitude = 56.988600, longitude = 40.969000 WHERE id = 15;
                UPDATE street SET latitude = 56.983600, longitude = 40.951200 WHERE id = 16;
                UPDATE street SET latitude = 56.994700, longitude = 40.968900 WHERE id = 17;
                UPDATE street SET latitude = 57.006500, longitude = 40.923000 WHERE id = 18;
                UPDATE street SET latitude = 56.988100, longitude = 40.980700 WHERE id = 19;");
        }
    }
}

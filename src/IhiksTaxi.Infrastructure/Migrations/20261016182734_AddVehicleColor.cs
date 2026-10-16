using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IhiksTaxi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "vehicle",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE vehicle SET color = 'Белый'       WHERE plate = 'A777BC 77';
                UPDATE vehicle SET color = 'Белый'       WHERE plate = 'B555MK 77';
                UPDATE vehicle SET color = 'Чёрный'      WHERE plate = 'C111OP 77';
                UPDATE vehicle SET color = 'Серый'       WHERE plate = 'K222XA 77';
                UPDATE vehicle SET color = 'Синий'       WHERE plate = 'M333CT 77';
                UPDATE vehicle SET color = 'Серебристый' WHERE color = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "vehicle");
        }
    }
}

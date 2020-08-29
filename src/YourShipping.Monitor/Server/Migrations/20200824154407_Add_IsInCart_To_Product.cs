using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_IsInCart_To_Product : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInCart",
                table: "Products",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsInCart",
                table: "Products");
        }
    }
}

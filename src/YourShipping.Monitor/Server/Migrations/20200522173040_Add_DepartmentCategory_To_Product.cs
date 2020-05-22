using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_DepartmentCategory_To_Product : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentCategory",
                table: "Products",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartmentCategory",
                table: "Products");
        }
    }
}

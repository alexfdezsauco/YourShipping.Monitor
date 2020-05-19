using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_Category_Property : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropColumn(
            //    name: "ProductsCount",
            //    table: "Stores");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Departments",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Departments");

            //migrationBuilder.AddColumn<int>(
            //    name: "ProductsCount",
            //    table: "Stores",
            //    type: "INTEGER",
            //    nullable: false,
            //    defaultValue: 0);
        }
    }
}

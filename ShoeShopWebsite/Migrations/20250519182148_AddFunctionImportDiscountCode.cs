using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionImportDiscountCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscountCodeID",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiscountCodeID",
                table: "Orders",
                column: "DiscountCodeID");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DiscountCodes_DiscountCodeID",
                table: "Orders",
                column: "DiscountCodeID",
                principalTable: "DiscountCodes",
                principalColumn: "DiscountCodeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DiscountCodes_DiscountCodeID",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_DiscountCodeID",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountCodeID",
                table: "Orders");
        }
    }
}

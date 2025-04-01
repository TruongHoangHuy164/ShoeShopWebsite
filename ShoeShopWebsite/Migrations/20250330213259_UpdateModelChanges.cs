using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColorID",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "ColorID",
                table: "OrderDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ColorID",
                table: "Orders",
                column: "ColorID");

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_ColorID",
                table: "OrderDetails",
                column: "ColorID");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_Colors_ColorID",
                table: "OrderDetails",
                column: "ColorID",
                principalTable: "Colors",
                principalColumn: "ColorID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
        name: "FK_Orders_Colors_ColorID",
        table: "Orders",
        column: "ColorID",
        principalTable: "Colors",
        principalColumn: "ColorID",
        onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_Colors_ColorID",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Colors_ColorID",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ColorID",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_ColorID",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "ColorID",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "ColorID",
                table: "OrderDetails",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}

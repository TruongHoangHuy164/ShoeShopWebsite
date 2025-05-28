using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductReviews2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_AspNetUsers_UserID",
                table: "ProductReviews");
            migrationBuilder.Sql("DELETE FROM ProductReviews WHERE UserID NOT IN (SELECT Id FROM AspNetUsers);");
            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Users",
                table: "ProductReviews",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_Users",
                table: "ProductReviews");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_AspNetUsers_UserID",
                table: "ProductReviews",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class FixReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ColorID",
                table: "ProductReviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SizeID",
                table: "ProductReviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_ColorID",
                table: "ProductReviews",
                column: "ColorID");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviews_SizeID",
                table: "ProductReviews",
                column: "SizeID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Colors_ColorID",
                table: "ProductReviews",
                column: "ColorID",
                principalTable: "Colors",
                principalColumn: "ColorID");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductReviews_Sizes_SizeID",
                table: "ProductReviews",
                column: "SizeID",
                principalTable: "Sizes",
                principalColumn: "SizeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_Colors_ColorID",
                table: "ProductReviews");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductReviews_Sizes_SizeID",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_ColorID",
                table: "ProductReviews");

            migrationBuilder.DropIndex(
                name: "IX_ProductReviews_SizeID",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "ColorID",
                table: "ProductReviews");

            migrationBuilder.DropColumn(
                name: "SizeID",
                table: "ProductReviews");
        }
    }
}

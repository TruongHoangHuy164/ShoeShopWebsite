using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShoeShopWebsite.Models
{
    public class NikeShopDbContext : IdentityDbContext<ApplicationUser>
    {
        public NikeShopDbContext(DbContextOptions<NikeShopDbContext> options) : base(options) { }

        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductSize> ProductSizes { get; set; }
        public DbSet<Color> Colors { get; set; }
        public DbSet<ProductColor> ProductColors { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Size> Sizes { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Wishlist> Wishlist { get; set; }

        public DbSet<Province> Provinces { get; set; }
        public DbSet<District> Districts { get; set; }
        public DbSet<Ward> Wards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình khóa chính
            modelBuilder.Entity<Province>()
                .HasKey(p => p.Code);

            modelBuilder.Entity<District>()
                .HasKey(d => d.Code);

            modelBuilder.Entity<Ward>()
                .HasKey(w => w.Code);

            // Cấu hình mối quan hệ Province - District
            modelBuilder.Entity<District>()
                .HasOne(d => d.Province)
                .WithMany(p => p.Districts)
                .HasForeignKey(d => d.ProvinceId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa District nếu Province bị xóa

            // Cấu hình mối quan hệ District - Ward
            modelBuilder.Entity<Ward>()
                .HasOne(w => w.District)
                .WithMany(d => d.Wards)
                .HasForeignKey(w => w.DistrictId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Ward nếu District bị xóa

            // Cấu hình cho OrderDetail (giữ nguyên)
            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Color)
                .WithMany()
                .HasForeignKey(od => od.ColorID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
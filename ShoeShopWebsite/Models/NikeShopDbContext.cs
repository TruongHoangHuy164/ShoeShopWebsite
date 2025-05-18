using Microsoft.AspNetCore.Identity;
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
        public DbSet<DiscountCode> DiscountCodes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<IdentityRole>().HasData(
    new IdentityRole
    {
        Id = "b1d1f35e-7d18-4a17-b88a-c8e1e8d9a210",
        Name = SD.Role_Admin,
        NormalizedName = SD.Role_Admin.ToUpper(),
        ConcurrencyStamp = "f21a49e8-5e1a-4234-9f92-0f430f0a1467"
    },
    new IdentityRole
    {
        Id = "dd76877a-4e07-4d84-a55f-c94a1d4f45a3",
        Name = SD.Role_Employee,
        NormalizedName = SD.Role_Employee.ToUpper(),
        ConcurrencyStamp = "cd17c4b9-11b9-41bc-9b72-6f4e0deee5a4"
    },
    new IdentityRole
    {
        Id = "3a6f9f49-309f-4e11-8f44-c75fcb0b9e89",
        Name = SD.Role_Customer,
        NormalizedName = SD.Role_Customer.ToUpper(),
        ConcurrencyStamp = "02a02f43-81f4-48dd-bb13-37db2347c18f"
    }
);

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
            modelBuilder.Entity<DiscountCode>()
           .Property(dc => dc.DiscountValue)
           .HasColumnType("decimal(18,2)");
            // Cấu hình cho DiscountCode
            modelBuilder.Entity<DiscountCode>()
                .Property(dc => dc.MinOrderValue)
                .HasColumnType("decimal(18,2)");
        }
    }
}
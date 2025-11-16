using System;
using System.Collections.Generic;
using AgriEcommerces_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriEcommerces_MVC.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<category> categories { get; set; }

    public virtual DbSet<historicalpricedatum> historicalpricedata { get; set; }

    public virtual DbSet<order> orders { get; set; }

    public virtual DbSet<orderdetail> orderdetails { get; set; }

    public virtual DbSet<priceprediction> pricepredictions { get; set; }

    public virtual DbSet<product> products { get; set; }

    public virtual DbSet<productimage> productimages { get; set; }

    public virtual DbSet<review> reviews { get; set; }

    public virtual DbSet<user> users { get; set; }

    public virtual DbSet<SellerRequest> sellerrequests { get; set; }

     public virtual DbSet<customer_address> customer_addresses { get; set; }

    public virtual DbSet<promotion> promotions { get; set; }
    public virtual DbSet<promotion_category> promotion_categories { get; set; }
    public virtual DbSet<promotion_product> promotion_products { get; set; }
    public virtual DbSet<promotion_farmer> promotion_farmers { get; set; }
    public virtual DbSet<promotion_usagehistory> promotion_usagehistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
   
        modelBuilder.Entity<category>(entity =>
        {
            entity.HasKey(e => e.categoryid).HasName("categories_pkey");

            entity.HasIndex(e => e.categoryname, "categories_name_key").IsUnique();

            entity.Property(e => e.categoryname).HasMaxLength(50);
            
        });

        modelBuilder.Entity<historicalpricedatum>(entity =>
        {
            entity.HasKey(e => e.dataid).HasName("historicalpricedata_pkey");

            entity.HasIndex(e => new { e.productid, e.datamonth }, "idx_histprice_product_month");

            entity.Property(e => e.actualprice).HasPrecision(10, 2);
            entity.Property(e => e.createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.rainfall).HasPrecision(5, 2);
            entity.Property(e => e.temperature).HasPrecision(5, 2);

            entity.HasOne(d => d.product).WithMany(p => p.historicalpricedata)
                .HasForeignKey(d => d.productid)
                .HasConstraintName("historicalpricedata_productid_fkey");
        });

        modelBuilder.Entity<order>(entity =>
        {
            entity.HasKey(e => e.orderid).HasName("orders_pkey");

            entity.HasIndex(e => e.customerid, "idx_orders_customer");

            entity.Property(e => e.orderdate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.Property(e => e.status).HasMaxLength(30);
            entity.Property(e => e.totalamount).HasPrecision(12, 2);

            entity.HasOne(d => d.customer).WithMany(p => p.orders)
                .HasForeignKey(d => d.customerid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orders_customerid_fkey");
        });

        modelBuilder.Entity<orderdetail>(entity =>
        {
            entity.HasKey(e => e.orderdetailid).HasName("orderdetails_pkey");

            entity.HasIndex(e => e.orderid, "idx_orderdetails_order");

            entity.Property(e => e.unitprice).HasPrecision(10, 2);

            entity.HasOne(d => d.order).WithMany(p => p.orderdetails)
                .HasForeignKey(d => d.orderid)
                .HasConstraintName("orderdetails_orderid_fkey");

            entity.HasOne(d => d.product).WithMany(p => p.orderdetails)
                .HasForeignKey(d => d.productid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdetails_productid_fkey");
        });

        modelBuilder.Entity<priceprediction>(entity =>
        {
            entity.HasKey(e => e.predictionid).HasName("pricepredictions_pkey");

            entity.HasIndex(e => e.predictedmonth, "idx_pred_month");

            entity.HasIndex(e => e.productid, "idx_pred_product");

            entity.Property(e => e.createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.predictedprice).HasPrecision(10, 2);
            entity.Property(e => e.rainfall).HasPrecision(5, 2);
            entity.Property(e => e.temperature).HasPrecision(5, 2);

            entity.HasOne(d => d.product).WithMany(p => p.pricepredictions)
                .HasForeignKey(d => d.productid)
                .HasConstraintName("pricepredictions_productid_fkey");
        });

        modelBuilder.Entity<product>(entity =>
        {
            entity.HasKey(e => e.productid).HasName("products_pkey");

            entity.HasIndex(e => e.categoryid, "idx_products_cat");

            entity.HasIndex(e => e.userid, "idx_products_user");

            entity.Property(e => e.createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.price).HasPrecision(10, 2);
            entity.Property(e => e.productname).HasMaxLength(100);
            entity.Property(e => e.quantityavailable).HasDefaultValue(0);
            entity.Property(e => e.unit).HasMaxLength(20);

            entity.HasOne(d => d.category).WithMany(p => p.products)
                .HasForeignKey(d => d.categoryid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("products_categoryid_fkey");

            entity.HasOne(d => d.user).WithMany(p => p.products)
                .HasForeignKey(d => d.userid)
                .HasConstraintName("products_userid_fkey");
        });

        modelBuilder.Entity<productimage>(entity =>
        {
            entity.HasKey(e => e.imageid).HasName("productimages_pkey");

            entity.Property(e => e.uploadedat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.product).WithMany(p => p.productimages)
                .HasForeignKey(d => d.productid)
                .HasConstraintName("productimages_productid_fkey");
        });

        modelBuilder.Entity<review>(entity =>
        {
            entity.HasKey(e => e.reviewid).HasName("reviews_pkey");

            entity.HasIndex(e => e.productid, "idx_reviews_product");

            entity.Property(e => e.createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.customer).WithMany(p => p.reviews)
                .HasForeignKey(d => d.customerid)
                .HasConstraintName("reviews_customerid_fkey");

            entity.HasOne(d => d.product).WithMany(p => p.reviews)
                .HasForeignKey(d => d.productid)
                .HasConstraintName("reviews_productid_fkey");
        });

        modelBuilder.Entity<user>(entity =>
        {
            entity.HasKey(e => e.userid).HasName("users_pkey");

            entity.HasIndex(e => e.email, "idx_users_email");

            entity.HasIndex(e => e.email, "users_email_key").IsUnique();
           
            entity.Property(e => e.createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.email).HasMaxLength(100);
            entity.Property(e => e.fullname).HasMaxLength(100);
            entity.Property(e => e.isapproved).HasDefaultValue(false);
            entity.Property(e => e.phonenumber).HasMaxLength(20);
            entity.Property(e => e.role).HasMaxLength(20);
            entity.Property(e => e.role).HasDefaultValue("Customer");
        });

        // ================================================
        // BẮT ĐẦU: CẤU HÌNH CÁC BẢNG KHUYẾN MÃI
        // ================================================

        modelBuilder.Entity<promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("promotions_pkey");

            // Ràng buộc UNIQUE cho cột 'code'
            entity.HasIndex(e => e.Code, "promotions_code_key").IsUnique();

            // Liên kết đến người tạo (user role 'Admin')
            entity.HasOne(d => d.CreatedBy) // Model: virtual user createdbyuser { get; set; }
                .WithMany(p => p.promotions) // Model: ICollection<promotion> promotions { get; set; }
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("promotions_createdbyuserid_fkey");
        });

        modelBuilder.Entity<promotion_category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("promotion_categories_pkey");

            // Ràng buộc UNIQUE để 1 danh mục chỉ được thêm 1 lần cho 1 khuyến mãi
            entity.HasIndex(e => new { e.PromotionId, e.CategoryId }, "idx_promo_cat_unique").IsUnique();

            // Liên kết đến promotion
            entity.HasOne(d => d.Promotion)
                .WithMany(p => p.PromotionCategories) // Model: ICollection<promotion_category> promotion_categories { get; set; }
                .HasForeignKey(d => d.PromotionId)
                .OnDelete(DeleteBehavior.Cascade) // Tự động xóa nếu promotion bị xóa
                .HasConstraintName("promo_cat_promotionid_fkey");

            // Liên kết đến category
            entity.HasOne(d => d.Category)
                .WithMany(p => p.promotion_categories) // Model: ICollection<promotion_category> promotion_categories { get; set; }
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("promo_cat_categoryid_fkey");
        });

        modelBuilder.Entity<promotion_farmer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("promotion_farmers_pkey");

            entity.HasIndex(e => new { e.PromotionId, e.FarmerId }, "idx_promo_farmer_unique").IsUnique();

            // Liên kết đến promotion
            entity.HasOne(d => d.Promotion)
                .WithMany(p => p.PromotionFarmers) // Model: ICollection<promotion_farmer> promotion_farmers { get; set; }
                .HasForeignKey(d => d.PromotionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("promo_farmer_promotionid_fkey");

            // Liên kết đến user (farmer)
            entity.HasOne(d => d.Farmer) // Model: virtual user farmer { get; set; }
                .WithMany(p => p.promotion_farmers) // Model: ICollection<promotion_farmer> promotion_farmers { get; set; }
                .HasForeignKey(d => d.FarmerId)
                .HasConstraintName("promo_farmer_farmerid_fkey");
        });

        modelBuilder.Entity<promotion_product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("promotion_products_pkey");

            entity.HasIndex(e => new { e.PromotionId, e.ProductId }, "idx_promo_product_unique").IsUnique();

            // Liên kết đến promotion
            entity.HasOne(d => d.Promotion)
                .WithMany(p => p.PromotionProducts) // Model: ICollection<promotion_product> promotion_products { get; set; }
                .HasForeignKey(d => d.PromotionId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("promo_product_promotionid_fkey");

            // Liên kết đến product
            entity.HasOne(d => d.Product)
                .WithMany(p => p.promotion_products) // Model: ICollection<promotion_product> promotion_products { get; set; }
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("promo_product_productid_fkey");
        });

        modelBuilder.Entity<promotion_usagehistory>(entity =>
        {
            entity.HasKey(e => e.UsageId).HasName("promotion_usagehistory_pkey");

            // UNIQUE: Một đơn hàng chỉ dùng 1 mã 1 lần
            entity.HasIndex(e => new { e.PromotionId, e.OrderId }, "idx_promo_usage_order_unique").IsUnique();
            // INDEX: Để tra cứu nhanh 1 user đã dùng 1 mã bao nhiêu lần
            entity.HasIndex(e => new { e.UserId, e.PromotionId }, "idx_promo_usage_user");

            // Liên kết đến promotion
            entity.HasOne(d => d.Promotion)
                .WithMany(p => p.PromotionUsageHistory) // Model: ICollection<promotion_usagehistory> promotion_usagehistories { get; set; }
                .HasForeignKey(d => d.PromotionId)
                .OnDelete(DeleteBehavior.SetNull) // Nếu xóa KM, giữ lại lịch sử
                .HasConstraintName("promo_usage_promotionid_fkey");

            // Liên kết đến user (customer)
            entity.HasOne(d => d.User)
                .WithMany(p => p.promotion_usagehistories) // Model: ICollection<promotion_usagehistory> promotion_usagehistories { get; set; }
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull) // Nếu xóa user, giữ lại lịch sử
                .HasConstraintName("promo_usage_userid_fkey");

            // Liên kết đến order
            entity.HasOne(d => d.Order)
                .WithMany(p => p.promotion_usagehistories) // Model: ICollection<promotion_usagehistory> promotion_usagehistories { get; set; }
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade) // Nếu xóa đơn hàng, xóa luôn lịch sử
                .HasConstraintName("promo_usage_orderid_fkey");
        });

        // ================================================
        // KẾT THÚC: CẤU HÌNH CÁC BẢNG KHUYẾN MÃI
        // ================================================

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

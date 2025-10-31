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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

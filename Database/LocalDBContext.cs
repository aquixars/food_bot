using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using fobot.Database.Models;

namespace fobot.Database;

public partial class LocalDBContext : DbContext
{
    public LocalDBContext()
    {
    }

    public LocalDBContext(DbContextOptions<LocalDBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Client> Clients { get; set; }

    public virtual DbSet<Dish> Dishes { get; set; }

    public virtual DbSet<DishType> DishTypes { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderLine> OrderLines { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.HasIndex(e => e.Id, "IX_Admins_id").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Bank)
                .IsRequired()
                .HasColumnName("bank");
            entity.Property(e => e.ClientId).HasColumnName("clientId");
            entity.Property(e => e.Initials)
                .IsRequired()
                .HasDefaultValue("")
                .HasColumnName("initials");
            entity.Property(e => e.IsActive).HasColumnName("isActive");
            entity.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasColumnName("phoneNumber");

            entity.HasOne(d => d.Client).WithMany(p => p.Admins)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasIndex(e => e.Id, "clientIdIndex");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ExternalId).HasColumnName("externalId");
            entity.Property(e => e.FirstName).HasColumnName("firstName");
            entity.Property(e => e.LastMessageCreated).HasColumnName("lastMessageCreated");
            entity.Property(e => e.LastName).HasColumnName("lastName");
            entity.Property(e => e.SystemName).HasColumnName("systemName");
            entity.Property(e => e.UserName).HasColumnName("userName");
        });

        modelBuilder.Entity<Dish>(entity =>
        {
            entity.HasIndex(e => e.Id, "dishIdIndex");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DishTypeId).HasColumnName("dishTypeId");
            entity.Property(e => e.IsGarnishIncluded).HasColumnName("isGarnishIncluded");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("name");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Sort).HasColumnName("sort");

            entity.HasOne(d => d.DishType).WithMany(p => p.Dishes).HasForeignKey(d => d.DishTypeId);
        });

        modelBuilder.Entity<DishType>(entity =>
        {
            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("name");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(e => e.Id, "IX_Orders_id").IsUnique();

            entity.HasIndex(e => e.Id, "orderIdIndex");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClientId).HasColumnName("clientId");
            entity.Property(e => e.Created)
                .IsRequired()
                .HasColumnName("created");
            entity.Property(e => e.CreatedInTicks).HasColumnName("createdInTicks");
            entity.Property(e => e.IsConfirmed).HasColumnName("isConfirmed");
            entity.Property(e => e.IsSend).HasColumnName("isSend");

            entity.HasOne(d => d.Client).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ChildDishId).HasColumnName("childDishId");
            entity.Property(e => e.DishId).HasColumnName("dishId");
            entity.Property(e => e.OrderId).HasColumnName("orderId");

            entity.HasOne(d => d.ChildDish).WithMany(p => p.OrderLineChildDishes).HasForeignKey(d => d.ChildDishId);

            entity.HasOne(d => d.Dish).WithMany(p => p.OrderLineDishes)
                .HasForeignKey(d => d.DishId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Order).WithMany(p => p.OrderLines)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RoomRum.API.Models;

namespace RoomRum.API.Data;

public partial class RoomRumDbContext : DbContext
{
    public RoomRumDbContext(
        DbContextOptions<RoomRumDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Listing> Listings { get; set; }

    public virtual DbSet<ListingImage> ListingImages { get; set; }

    public virtual DbSet<ListingVideo> ListingVideos { get; set; }

    public virtual DbSet<ListingReviewEvent> ListingReviewEvents { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<SchemaVersion> SchemaVersions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Appointments_Integrity");
                tb.HasTrigger("TR_Appointments_PreventHardDelete");
            });

            entity.HasIndex(
                    e => e.ListingId,
                    "UX_Appointments_OneConfirmedPerListing")
                .IsUnique()
                .HasFilter("([Status]=N'Confirmed')");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Appointments_CreatedAt");

            entity.Property(e => e.DurationMinutes)
                .HasDefaultValue(
                    (short)30,
                    "DF_Appointments_Duration");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.Property(e => e.ScheduledEndAt)
                .HasComputedColumnSql(
                    "(dateadd(minute,[DurationMinutes],[ScheduledAt]))",
                    true);

            entity.Property(e => e.Status)
                .HasDefaultValue(
                    "Pending",
                    "DF_Appointments_Status");

            // ====================================================
            // IMPORTANT:
            // One Listing -> Many Appointments.
            // The filtered unique index only means:
            // one Confirmed appointment per Listing.
            // It does NOT mean one Appointment per Listing.
            // ====================================================

            entity.HasOne(d => d.Listing)
                .WithMany(p => p.Appointments)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Appointments_Listing");

            entity.HasOne(d => d.Tenant)
                .WithMany(p => p.Appointments)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Appointments_Tenant");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Conversations_Integrity");
                tb.HasTrigger("TR_Conversations_PreventHardDelete");
            });

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Conversations_CreatedAt");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(d => d.Landlord)
                .WithMany(p => p.ConversationLandlords)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Conversations_Landlord");

            entity.HasOne(d => d.Listing)
                .WithMany(p => p.Conversations)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Conversations_Listing");

            entity.HasOne(d => d.Tenant)
                .WithMany(p => p.ConversationTenants)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Conversations_Tenant");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.ToTable(tb =>
                tb.HasTrigger("TR_Favorites_Integrity"));

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Favorites_CreatedAt");

            entity.HasOne(d => d.Listing)
                .WithMany(p => p.Favorites)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Favorites_Listing");

            entity.HasOne(d => d.Tenant)
                .WithMany(p => p.Favorites)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Favorites_Tenant");
        });

        modelBuilder.Entity<Listing>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Listings_Integrity");
                tb.HasTrigger("TR_Listings_PreventHardDelete");
                tb.HasTrigger("TR_Listings_ReviewIntegrity");
            });

            entity.HasIndex(
                    e => new
                    {
                        e.Status,
                        e.Latitude,
                        e.Longitude
                    },
                    "IX_Listings_Active_Map")
                .HasFilter(
                    "([Status] IN (N'Active', N'Reserved'))");

            entity.HasIndex(
                    e => new
                    {
                        e.ReviewStatus,
                        e.CreatedAt
                    },
                    "IX_Listings_ReviewStatus_CreatedAt")
                .IsDescending(false, true);

            entity.Property(e => e.Bathrooms)
                .HasDefaultValue(
                    (byte)1,
                    "DF_Listings_Bathrooms");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Listings_CreatedAt");

            entity.Property(e => e.HasPrivateBathroom)
                .HasDefaultValue(
                    true,
                    "DF_Listings_HasPrivateBathroom");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.Property(e => e.Status)
                .HasDefaultValue(
                    "Hidden",
                    "DF_Listings_Status");

            entity.Property(e => e.ReviewStatus)
                .HasDefaultValue(
                    "PendingReview",
                    "DF_Listings_ReviewStatus");

            entity.HasOne(d => d.Landlord)
                .WithMany(p => p.Listings)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Listings_Landlord");

            entity.HasOne(d => d.Reviewer)
                .WithMany(p => p.ReviewedListings)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Listings_ReviewedBy");
        });

        modelBuilder.Entity<ListingImage>(entity =>
        {
            entity.ToTable(tb =>
                tb.HasTrigger("TR_ListingImages_Integrity"));

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.SourceUrl)
                .HasMaxLength(500);

            entity.Property(e => e.ImageData)
                .HasColumnType("varbinary(max)");

            entity.Property(e => e.ContentType)
                .HasMaxLength(100);

            entity.HasIndex(
                    e => e.ListingId,
                    "UX_ListingImages_OnePrimary")
                .IsUnique()
                .HasFilter("([IsPrimary]=(1))");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_ListingImages_CreatedAt");

            // ====================================================
            // IMPORTANT:
            // One Listing -> Many ListingImages.
            // The unique filtered index above means:
            // one PRIMARY image per Listing.
            // It does NOT mean one image per Listing.
            // ====================================================

            entity.HasOne(d => d.Listing)
                .WithMany(p => p.ListingImages)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ListingImages_Listing");
        });

        modelBuilder.Entity<ListingVideo>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_ListingVideos_Integrity"));
            entity.HasIndex(e => e.ListingId).IsUnique();
            entity.Property(e => e.VideoData).HasColumnType("varbinary(max)");
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.HasOne(e => e.Listing).WithMany(l => l.ListingVideos)
                .HasForeignKey(e => e.ListingId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ListingVideos_Listing");
        });

        modelBuilder.Entity<ListingReviewEvent>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("TR_ListingReviewEvents_Integrity"));
            entity.HasIndex(e => new { e.ListingId, e.CreatedAt, e.ReviewEventId });
            entity.Property(e => e.EventType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.HasOne<Listing>().WithMany()
                .HasForeignKey(e => e.ListingId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ListingReviewEvents_Listing");
            entity.HasOne<User>().WithMany()
                .HasForeignKey(e => e.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ListingReviewEvents_Actor");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Messages_Integrity");
                tb.HasTrigger(
                    "TR_Messages_UpdateConversationLastMessage");
            });

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.Property(e => e.SentAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Messages_SentAt");

            entity.HasOne(d => d.Conversation)
                .WithMany(p => p.Messages)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Messages_Conversation");

            entity.HasOne(d => d.Sender)
                .WithMany(p => p.Messages)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Messages_Sender");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Payments_Integrity");
                tb.HasTrigger("TR_Payments_PreventHardDelete");
            });

            entity.HasIndex(
                    e => e.TransactionCode,
                    "UX_Payments_TransactionCode")
                .IsUnique()
                .HasFilter(
                    "([TransactionCode] IS NOT NULL)");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Payments_CreatedAt");

            entity.Property(e => e.PaymentPurpose)
                .HasDefaultValue(
                    "BookingDeposit",
                    "DF_Payments_Purpose");

            entity.Property(e => e.PaymentStatus)
                .HasDefaultValue(
                    "Pending",
                    "DF_Payments_Status");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // Payment is genuinely one-to-one with Appointment.
            entity.HasOne(d => d.Appointment)
                .WithOne(p => p.Payment)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Payments_Appointment");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable(tb =>
                tb.HasTrigger("TR_RefreshTokens_Integrity"));

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.2 truncates to whole seconds for the same reason as
                    // every other timestamp in this schema.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_RefreshTokens_CreatedAt");

            entity.HasOne(d => d.User)
                .WithMany(p => p.RefreshTokens)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_RefreshTokens_User");
        });

        modelBuilder.Entity<SchemaVersion>(entity =>
        {
            entity.Property(e => e.VersionId)
                .ValueGeneratedNever();

            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_SchemaVersion_AppliedAt");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable(tb =>
            {
                tb.HasTrigger("TR_Users_Integrity");
                tb.HasTrigger("TR_Users_PreventHardDelete");
            });

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql(
                    // v5.1 truncates to whole seconds instead of letting
                    // DATETIME2(0) round the value up into the next second.
                    "(DATEADD(SECOND, DATEDIFF(SECOND, CONVERT(DATETIME2(0), '20200101'), SYSUTCDATETIME()), CONVERT(DATETIME2(0), '20200101')))",
                    "DF_Users_CreatedAt");

            entity.Property(e => e.EmailNormalized)
                .HasComputedColumnSql(
                    "(upper(ltrim(rtrim([Email]))))",
                    true);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(
                    true,
                    "DF_Users_IsActive");

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(
        ModelBuilder modelBuilder);
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.DAL.Entities;

public partial class RoomRumDbContext : DbContext
{
    public RoomRumDbContext(DbContextOptions<RoomRumDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Amenity> Amenities { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Repairing> Repairings { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomImage> RoomImages { get; set; }

    public virtual DbSet<RoomRating> RoomRatings { get; set; }

    public virtual DbSet<RoomTenant> RoomTenants { get; set; }

    public virtual DbSet<RoomTenantHistory> RoomTenantHistories { get; set; }

    public virtual DbSet<Sharing> Sharings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public DbSet<UserRegistrationFile> UserRegistrationFiles => Set<UserRegistrationFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.HasKey(e => e.AmenityId);

            entity.ToTable("Amenity");
            entity.HasIndex(e => e.AmenityName, "UX_Amenity_Name").IsUnique();

            entity.Property(e => e.AmenityId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("amenity_id");
            entity.Property(e => e.AmenityName)
                .HasMaxLength(255)
                .HasColumnName("amenity_name");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId);

            entity.ToTable("Appointment", table => table.HasCheckConstraint(
                "CK_Appointment_Status", "[status] IN ('Pending', 'Confirmed', 'Rejected', 'Cancelled', 'Completed')"));

            entity.HasIndex(e => e.RoomId, "IX_Appointment_RoomID");

            entity.HasIndex(e => new { e.UserId, e.AppointmentDate, e.AppointmentTime }, "IX_Appointment_User_Date");

            entity.Property(e => e.AppointmentId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("appointment_id");
            entity.Property(e => e.AppointmentDate).HasColumnName("appointment_date");
            entity.Property(e => e.AppointmentTime).HasColumnName("appointment_time");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId);

            entity.ToTable("Conversation");

            entity.Property(e => e.ConversationId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("conversation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.FavoriteId);

            entity.ToTable("Favorite");

            entity.HasIndex(e => e.UserId, "IX_Favorite_UserID");
            entity.HasIndex(e => new { e.UserId, e.RoomId }, "UX_Favorite_User_Room").IsUnique();

            entity.Property(e => e.FavoriteId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("favorite_id");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.ToTable("Message");

            entity.HasIndex(e => e.ConversationId, "IX_Message_ConversationID");

            entity.Property(e => e.MessageId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("message_id");
            entity.Property(e => e.ChatAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("chat_at");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.Receiver).HasColumnName("receiver");
            entity.Property(e => e.Sender).HasColumnName("sender");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ReceiverNavigation).WithMany(p => p.MessageReceiverNavigations)
                .HasForeignKey(d => d.Receiver)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.SenderNavigation).WithMany(p => p.MessageSenderNavigations)
                .HasForeignKey(d => d.Sender)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Repairing>(entity =>
        {
            entity.HasKey(e => e.MaintenanceId);

            entity.ToTable("Repairing");

            entity.HasIndex(e => new { e.RoomId, e.Status }, "IX_Repairing_RoomID_Status");

            entity.Property(e => e.MaintenanceId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("maintenance_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ExpectingDate).HasColumnName("expecting_date");
            entity.Property(e => e.Priority)
                .HasMaxLength(50)
                .HasColumnName("priority");
            entity.Property(e => e.RepairItems).HasColumnName("repair_items");
            entity.Property(e => e.Response).HasColumnName("response");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.Repairings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.Repairings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId);

            entity.ToTable("Report");

            entity.Property(e => e.ReportId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("report_id");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReportDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("report_date");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.Reports)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.Reports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId);

            entity.ToTable("Role");
            entity.HasIndex(e => e.RoleName, "UX_Role_Name").IsUnique();

            entity.Property(e => e.RoleId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("role_id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId);

            entity.ToTable("Room", table =>
            {
                table.HasCheckConstraint("CK_Room_Status",
                    "[status] IS NOT NULL AND [status] IN ('Pending', 'Available', 'Rented', 'Hidden', 'Cancelled')");
                table.HasCheckConstraint("CK_Room_Coordinates",
                    "([latitude] IS NULL AND [longitude] IS NULL) OR ([latitude] BETWEEN -90 AND 90 AND [longitude] BETWEEN -180 AND 180)");
            });

            entity.HasIndex(e => new { e.Status, e.Type, e.Price }, "IX_Room_Filter");

            entity.HasIndex(e => new { e.Latitude, e.Longitude }, "IX_Room_Coordinates")
                .HasFilter("[latitude] IS NOT NULL AND [longitude] IS NOT NULL");

            entity.HasIndex(e => e.UserId, "IX_Room_UserID");

            entity.Property(e => e.RoomId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("room_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.Latitude).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasColumnName("longitude");
            entity.Property(e => e.Area)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("area");
            entity.Property(e => e.Deposit)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("deposit");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ElectricityFee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("electricity_fee");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Rule).HasColumnName("rule");
            entity.Property(e => e.ServiceFee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("service_fee");
            entity.Property(e => e.Status)
                .HasDefaultValue("Pending", "DF_Room_Status")
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired(false)
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WaterFee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("water_fee");

            entity.HasOne(d => d.User).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasMany(d => d.Amenities).WithMany(p => p.Rooms)
                .UsingEntity<Dictionary<string, object>>(
                    "RoomAmenity",
                    r => r.HasOne<Amenity>().WithMany()
                        .HasForeignKey("AmenityId")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    l => l.HasOne<Room>().WithMany()
                        .HasForeignKey("RoomId")
                        .OnDelete(DeleteBehavior.ClientSetNull),
                    j =>
                    {
                        j.HasKey("RoomId", "AmenityId");
                        j.ToTable("RoomAmenity");
                        j.HasIndex(new[] { "AmenityId" }, "IX_RoomAmenity_AmenityID");
                        j.IndexerProperty<Guid>("RoomId").HasColumnName("room_id");
                        j.IndexerProperty<Guid>("AmenityId").HasColumnName("amenity_id");
                    });
        });

        modelBuilder.Entity<RoomImage>(entity =>
        {
            entity.HasKey(e => e.RoomImageId);

            entity.ToTable("RoomImage");

            entity.HasIndex(e => e.RoomId, "IX_RoomImage_RoomID");

            entity.Property(e => e.RoomImageId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("room_image_id");
            entity.Property(e => e.ImageUrl)
                .IsUnicode(false)
                .HasColumnName("image_url");
            entity.Property(e => e.RoomId).HasColumnName("room_id");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomImages)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RoomRating>(entity =>
        {
            entity.HasKey(e => e.RatingId);

            entity.ToTable("RoomRating", table => table.HasCheckConstraint(
                "CK_RoomRating_Rating", "[rating] >= 1 AND [rating] <= 5"));

            entity.HasIndex(e => e.RoomId, "IX_RoomRating_RoomID");

            entity.HasAlternateKey(e => new { e.UserId, e.RoomId }).HasName("UQ_RoomRating_User_Room");

            entity.Property(e => e.RatingId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("rating_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.RatingDate)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("rating_date");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomRatings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.RoomRatings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.RoomTenant).WithMany(p => p.RoomRatings)
                .HasForeignKey(d => new { d.RoomId, d.UserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RoomRating_RoomTenant");
        });

        modelBuilder.Entity<RoomTenant>(entity =>
        {
            entity.HasKey(e => new { e.RoomId, e.UserId });

            entity.ToTable("RoomTenant", table =>
            {
                table.HasCheckConstraint("CK_RoomTenant_Status",
                    "[status] IN ('Pending', 'Active', 'Ended', 'Cancelled')");
                table.HasCheckConstraint("CK_RoomTenant_LeftAt",
                    "[left_at] IS NULL OR [left_at] >= [hired_at]");
            });

            entity.HasIndex(e => e.UserId, "IX_RoomTenant_UserID");

            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.HiredAt)
                .HasDefaultValueSql("(sysdatetime())")
                .HasColumnName("hired_at");
            entity.Property(e => e.LeftAt).HasColumnName("left_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Pending", "DF_RoomTenant_Status")
                .HasColumnName("status");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomTenants)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.RoomTenants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<RoomTenantHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK_RoomTenantHistory");
            entity.ToTable("RoomTenantHistory", table => table.HasCheckConstraint(
                "CK_RoomTenantHistory_Status", "[status] IN ('Ended', 'Cancelled')"));
            entity.HasIndex(e => new { e.RoomId, e.UserId, e.ArchivedAt },
                "IX_RoomTenantHistory_RoomUser_ArchivedAt")
                .IsDescending(false, false, true);
            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.HiredAt).HasColumnName("hired_at");
            entity.Property(e => e.LeftAt).HasColumnName("left_at");
            entity.Property(e => e.Status).HasColumnName("status")
                .HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.ArchivedAt).HasColumnName("archived_at")
                .HasDefaultValueSql("(sysdatetime())", "DF_RoomTenantHistory_ArchivedAt");
            entity.HasOne(e => e.RoomTenant).WithMany(e => e.History)
                .HasForeignKey(e => new { e.RoomId, e.UserId })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RoomTenantHistory_RoomTenant");
        });

        modelBuilder.Entity<Sharing>(entity =>
        {
            entity.HasKey(e => e.SharingId);

            entity.ToTable("Sharing");

            entity.HasIndex(e => e.RoomId, "IX_Sharing_RoomID");

            entity.Property(e => e.SharingId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("sharing_id");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.Introduction).HasColumnName("introduction");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.Rule).HasColumnName("rule");
            entity.Property(e => e.SharingFee)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("sharing_fee");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Room).WithMany(p => p.Sharings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User).WithMany(p => p.Sharings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.ToTable("User", table => table.HasCheckConstraint(
                "CK_User_Status", "[status] IN ('Inactive', 'Active')"));

            entity.HasIndex(e => e.RoleId, "IX_User_RoleID");

            entity.HasIndex(e => e.Email, "UX_User_Email_NotNull").IsUnique().HasFilter("[email] IS NOT NULL");

            entity.HasIndex(e => e.CitizenId, "UX_User_CitizenId_NotNull").IsUnique().HasFilter("[citizen_id] IS NOT NULL");

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("user_id");
            entity.Property(e => e.CitizenId)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("citizen_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Dob).HasColumnName("dob");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.Gender)
                .HasMaxLength(20)
                .HasColumnName("gender");
            entity.Property(e => e.ImageUrl)
                .IsUnicode(false)
                .HasColumnName("image_url");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("password");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("phone");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsRequired()
                .HasDefaultValue("Inactive", "DF_User_Status")
                .HasColumnName("status");

            entity.Property(e => e.EmailVerificationTokenHash)
                .HasColumnName("email_verification_token_hash")
                .HasColumnType("varbinary(32)");
            entity.Property(e => e.EmailVerificationExpiresAt).HasColumnName("email_verification_expires_at");
            entity.Property(e => e.EmailVerificationSentAt).HasColumnName("email_verification_sent_at");
            entity.Property(e => e.EmailVerifiedAt).HasColumnName("email_verified_at");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<UserRegistrationFile>(entity =>
        {
            entity.ToTable("UserRegistrationFile", table =>
            {
                table.HasCheckConstraint("CK_UserRegistrationFile_Category",
                    "[category] IN ('PropertyImage', 'IdentityDocument')");
                table.HasCheckConstraint("CK_UserRegistrationFile_Size",
                    "DATALENGTH([file_content]) BETWEEN 1 AND 5242880");
            });
            entity.HasKey(x => x.FileId).HasName("PK_UserRegistrationFile");
            entity.Property(x => x.FileId).ValueGeneratedNever().HasColumnName("file_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
            entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.Content).HasColumnName("file_content").HasColumnType("varbinary(max)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
            entity.HasIndex(x => x.UserId, "IX_UserRegistrationFile_UserId");
            entity.HasOne(x => x.User).WithMany(x => x.UserRegistrationFiles).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserRegistrationFile_User");
        });
    }
}

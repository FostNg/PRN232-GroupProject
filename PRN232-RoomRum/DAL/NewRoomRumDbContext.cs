using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.DAL;

public partial class NewRoomRumDbContext : DbContext
{
    public NewRoomRumDbContext(DbContextOptions<NewRoomRumDbContext> options)
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.HasKey(e => e.AmenityId).HasName("PK__Amenity__E908452D801ED2B6");

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
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__A50828FCBF3B7319");

            entity.ToTable("Appointment");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__room___6383C8BA");

            entity.HasOne(d => d.User).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Appointme__user___628FA481");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK__Conversa__311E7E9A2B35BC00");

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
            entity.HasKey(e => e.FavoriteId).HasName("PK__Favorite__46ACF4CB55E7CF11");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Favorite__room_i__72C60C4A");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Favorite__user_i__71D1E811");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__Message__0BBF6EE64B26A1F8");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Message__convers__1332DBDC");

            entity.HasOne(d => d.ReceiverNavigation).WithMany(p => p.MessageReceiverNavigations)
                .HasForeignKey(d => d.Receiver)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Message__receive__151B244E");

            entity.HasOne(d => d.SenderNavigation).WithMany(p => p.MessageSenderNavigations)
                .HasForeignKey(d => d.Sender)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Message__sender__14270015");
        });

        modelBuilder.Entity<Repairing>(entity =>
        {
            entity.HasKey(e => e.MaintenanceId).HasName("PK__Repairin__9D754BEAA1F718B1");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Repairing__room___6E01572D");

            entity.HasOne(d => d.User).WithMany(p => p.Repairings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Repairing__user___6D0D32F4");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__Report__779B7C5822A363EE");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Report__room_id__693CA210");

            entity.HasOne(d => d.User).WithMany(p => p.Reports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Report__user_id__68487DD7");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__760965CCCC366AE2");

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
            entity.HasKey(e => e.RoomId).HasName("PK__Room__19675A8ABF214A6A");

            entity.ToTable("Room");

            entity.HasIndex(e => new { e.Status, e.Type, e.Price }, "IX_Room_Filter");

            entity.HasIndex(e => e.UserId, "IX_Room_UserID");

            entity.Property(e => e.RoomId)
                .HasDefaultValueSql("(newsequentialid())")
                .HasColumnName("room_id");
            entity.Property(e => e.Address).HasColumnName("address");
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
                .HasMaxLength(50)
                .IsUnicode(false)
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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Room__user_id__571DF1D5");

            entity.HasMany(d => d.Amenities).WithMany(p => p.Rooms)
                .UsingEntity<Dictionary<string, object>>(
                    "RoomAmenity",
                    r => r.HasOne<Amenity>().WithMany()
                        .HasForeignKey("AmenityId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__RoomAmeni__ameni__06CD04F7"),
                    l => l.HasOne<Room>().WithMany()
                        .HasForeignKey("RoomId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK__RoomAmeni__room___05D8E0BE"),
                    j =>
                    {
                        j.HasKey("RoomId", "AmenityId").HasName("PK__RoomAmen__D7F7DED8FFA21B8F");
                        j.ToTable("RoomAmenity");
                        j.HasIndex(new[] { "AmenityId" }, "IX_RoomAmenity_AmenityID");
                        j.IndexerProperty<Guid>("RoomId").HasColumnName("room_id");
                        j.IndexerProperty<Guid>("AmenityId").HasColumnName("amenity_id");
                    });
        });

        modelBuilder.Entity<RoomImage>(entity =>
        {
            entity.HasKey(e => e.RoomImageId).HasName("PK__RoomImag__544C54BAD7E3F9C2");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RoomImage__room___0A9D95DB");
        });

        modelBuilder.Entity<RoomRating>(entity =>
        {
            entity.HasKey(e => e.RatingId).HasName("PK__RoomRati__D35B278BFC37D95F");

            entity.ToTable("RoomRating");

            entity.HasIndex(e => e.RoomId, "IX_RoomRating_RoomID");

            entity.HasIndex(e => new { e.UserId, e.RoomId }, "UQ_RoomRating_User_Room").IsUnique();

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RoomRatin__room___7A672E12");

            entity.HasOne(d => d.User).WithMany(p => p.RoomRatings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RoomRatin__user___797309D9");

            entity.HasOne(d => d.RoomTenant).WithMany(p => p.RoomRatings)
                .HasForeignKey(d => new { d.RoomId, d.UserId })
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RoomRating_RoomTenant");
        });

        modelBuilder.Entity<RoomTenant>(entity =>
        {
            entity.HasKey(e => new { e.RoomId, e.UserId }).HasName("PK__RoomTena__F2FCB9FA3CECE8B1");

            entity.ToTable("RoomTenant");

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
                .HasDefaultValue("Pending")
                .HasColumnName("status");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomTenants)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RoomTenan__room___00200768");

            entity.HasOne(d => d.User).WithMany(p => p.RoomTenants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__RoomTenan__user___01142BA1");
        });

        modelBuilder.Entity<RoomTenantHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK_RoomTenantHistory");
            entity.ToTable("RoomTenantHistory");
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
                .HasDefaultValueSql("(sysdatetime())");
            entity.HasOne(e => e.RoomTenant).WithMany(e => e.History)
                .HasForeignKey(e => new { e.RoomId, e.UserId })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RoomTenantHistory_RoomTenant");
        });

        modelBuilder.Entity<Sharing>(entity =>
        {
            entity.HasKey(e => e.SharingId).HasName("PK__Sharing__BB366F9154C94520");

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
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Sharing__room_id__5BE2A6F2");

            entity.HasOne(d => d.User).WithMany(p => p.Sharings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Sharing__user_id__5AEE82B9");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__B9BE370FB2983914");

            entity.ToTable("User");

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
                .HasColumnName("status");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__User__role_id__534D60F1");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

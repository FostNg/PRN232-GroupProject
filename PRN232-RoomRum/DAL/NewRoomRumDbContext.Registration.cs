using Microsoft.EntityFrameworkCore;
using RoomRum.DAL.Models;

namespace RoomRum.DAL;

public partial class NewRoomRumDbContext
{
    public DbSet<UserRegistrationFile> UserRegistrationFiles => Set<UserRegistrationFile>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.EmailVerificationTokenHash).HasColumnName("email_verification_token_hash")
                .HasColumnType("varbinary(32)");
            entity.Property(x => x.EmailVerificationExpiresAt).HasColumnName("email_verification_expires_at");
            entity.Property(x => x.EmailVerificationSentAt).HasColumnName("email_verification_sent_at");
            entity.Property(x => x.EmailVerifiedAt).HasColumnName("email_verified_at");
        });
        modelBuilder.Entity<UserRegistrationFile>(entity =>
        {
            entity.ToTable("UserRegistrationFile");
            entity.HasKey(x => x.FileId);
            entity.Property(x => x.FileId).HasColumnName("file_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(30).IsUnicode(false);
            entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255);
            entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.Content).HasColumnName("file_content");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

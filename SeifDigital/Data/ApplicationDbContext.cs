using Microsoft.EntityFrameworkCore;
using SeifDigital.Models;

namespace SeifDigital.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<InformatieSensibila> InformatiiSensibile { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

        public DbSet<UserNote> UserNotes { get; set; }
        public DbSet<UserFile> UserFiles { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }

        // Login user/parola
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<UserMessage> UserMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // InformatiiSensibile (dbo.InformatiiSensibile)
            // =========================
            modelBuilder.Entity<InformatieSensibila>(e =>
            {
                e.ToTable("InformatiiSensibile", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.OwnerKey).HasMaxLength(256);
                e.Property(x => x.NumeUtilizator).HasMaxLength(256);

                e.Property(x => x.TitluAplicatie).HasMaxLength(256);
                e.Property(x => x.UsernameSalvat).HasMaxLength(256);

                e.Property(x => x.DateCriptate);
                e.Property(x => x.DetaliiCriptate);
                e.Property(x => x.DetaliiTokens);

                e.HasIndex(x => x.OwnerKey);
            });

            // =========================
            // AuditLog (dbo.AuditLog)
            // =========================
            modelBuilder.Entity<AuditLog>(e =>
            {
                e.ToTable("AuditLog", "dbo");

                e.Property(x => x.EventTimeUtc).HasColumnType("datetime2(3)");
                e.Property(x => x.EventType).HasMaxLength(64);
                e.Property(x => x.ActorUser).HasMaxLength(256);
                e.Property(x => x.ActorSid).HasMaxLength(128);
                e.Property(x => x.TargetType).HasMaxLength(64);
                e.Property(x => x.TargetId).HasMaxLength(64);
                e.Property(x => x.Outcome).HasMaxLength(16);
                e.Property(x => x.Reason).HasMaxLength(256);
                e.Property(x => x.ClientIp).HasMaxLength(64);
                e.Property(x => x.UserAgent).HasMaxLength(512);
                e.Property(x => x.CorrelationId).HasMaxLength(64);
            });

            // =========================
            // AppSettings (dbo.AppSettings)
            // =========================
            modelBuilder.Entity<AppSetting>(e =>
            {
                e.ToTable("AppSettings", "dbo");
                e.HasKey(x => x.Key);

                e.Property(x => x.Key).HasMaxLength(128);
                e.Property(x => x.Value).HasMaxLength(1024);
                e.Property(x => x.UpdatedUtc).HasColumnType("datetime2(3)");
            });

            // =========================
            // UserNote (dbo.UserNote)
            // Text -> NoteText
            // =========================
            modelBuilder.Entity<UserNote>(e =>
            {
                e.ToTable("UserNote", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnType("bigint");

                e.Property(x => x.OwnerUser)
                    .HasColumnName("OwnerUser")
                    .HasMaxLength(256)
                    .IsRequired();

                e.Property(x => x.OwnerKey)
                    .HasColumnName("OwnerKey")
                    .HasMaxLength(256);

                // ✅ NOU: Title (dbo.UserNote.Title)
                e.Property(x => x.Title)
                    .HasColumnName("Title")
                    .HasMaxLength(255)
                    .IsRequired();

                e.Property(x => x.Text)
                    .HasColumnName("NoteText")
                    .HasMaxLength(255)
                    .IsRequired();

                e.Property(x => x.CreatedUtc).HasColumnType("datetime2(3)").IsRequired();
                e.Property(x => x.UpdatedUtc).HasColumnType("datetime2(3)").IsRequired();

                e.HasIndex(x => x.OwnerKey);
            });

            // =========================
            // UserFile (dbo.UserFile)
            // =========================
            modelBuilder.Entity<UserFile>(e =>
            {
                e.ToTable("UserFile", "dbo");
                e.HasKey(x => x.Id);
            });

            // =========================
            // UserProfile (dbo.UserProfile) - legacy/cache
            // =========================
            modelBuilder.Entity<UserProfile>(e =>
            {
                e.ToTable("UserProfile", "dbo");

                e.Property(x => x.DomainUser).HasMaxLength(256).IsRequired();
                e.Property(x => x.Email).HasMaxLength(256).IsRequired();
                e.Property(x => x.EmailSource).HasMaxLength(32).IsRequired();

                e.HasIndex(x => x.DomainUser).IsUnique();
            });

            // =========================
            // UserAccount (dbo.UserAccount) - LOGIN
            // =========================
            modelBuilder.Entity<UserAccount>(e =>
            {
                e.ToTable("UserAccount", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnType("bigint");

                e.Property(x => x.Email)
                    .HasMaxLength(256)
                    .IsRequired();

                e.HasIndex(x => x.Email).IsUnique();

                e.Property(x => x.PasswordHash)
                    .HasColumnType("varbinary(64)")
                    .IsRequired();

                e.Property(x => x.PasswordSalt)
                    .HasColumnType("varbinary(16)")
                    .IsRequired();

                e.Property(x => x.CreatedUtc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired();

                e.Property(x => x.UpdatedUtc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired();

                e.Property(x => x.IsActive)
                    .HasColumnType("bit")
                    .IsRequired();

                e.Property(x => x.IsAdmin)
                    .HasColumnType("bit")
                    .IsRequired();
            });

            // =========================
            // UserMessage (dbo.UserMessage) - inbox
            // =========================
            modelBuilder.Entity<UserMessage>(e =>
            {
                e.ToTable("UserMessage", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnType("bigint");

                e.Property(x => x.RecipientOwnerKey).HasMaxLength(256).IsRequired();
                e.Property(x => x.SenderEmail).HasMaxLength(256).IsRequired();
                e.Property(x => x.SourceType).HasMaxLength(20).IsRequired();

                e.Property(x => x.OriginalId).HasColumnType("bigint");

                e.Property(x => x.CreatedUtc).HasColumnType("datetime2(3)").IsRequired();
                e.Property(x => x.SavedUtc).HasColumnType("datetime2(3)");

                e.Property(x => x.TitluAplicatie).HasMaxLength(100);
                e.Property(x => x.UsernameSalvat).HasMaxLength(200);

                e.Property(x => x.NoteText).HasMaxLength(255);
                e.Property(x => x.Text).HasMaxLength(255);

                e.HasIndex(x => x.RecipientOwnerKey);
                e.HasIndex(x => x.SourceType);
            });
        }
    }
}

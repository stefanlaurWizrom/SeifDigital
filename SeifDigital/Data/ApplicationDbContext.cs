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
        public DbSet<NoteFisier> NoteFisieri { get; set; }  // ✅ NOU: Fișiere pentru Note
        public DbSet<UserFile> UserFiles { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<InformatieImagine> InformatiiImagini { get; set; } // ✅ Keep for backward compatibility
        public DbSet<InformatieFisier> InformatiiImagini_New { get; set; } // ✅ Nou: Generic files

        // Login user/parola
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<UserMessage> UserMessages { get; set; }

        // ✅ Certificate Management
        public DbSet<ManagedCertificate> ManagedCertificates { get; set; }
        public DbSet<CertificateAlertLog> CertificateAlertLogs { get; set; }

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
                e.Property(x => x.LastUpdatedUtc).HasColumnType("datetime2(3)");

                // ✅ ACTUALIZAT: Many-to-Many relație prin InformatieFisier (generic)
                e.HasMany(x => x.Fisieri)
                    .WithOne(x => x.InformatieSensibila)
                    .HasForeignKey(x => x.InformatieSensibila_Id)
                    .OnDelete(DeleteBehavior.Cascade);

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

                // ✅ NOU: Many-to-Many relație cu fișiere (NoteFisier)
                e.HasMany(x => x.Fisieri)
                    .WithOne(x => x.UserNote)
                    .HasForeignKey(x => x.UserNote_Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // NoteFisier (dbo.NoteFisier) - Junction table for Notes + Files
            // =========================
            modelBuilder.Entity<NoteFisier>(e =>
            {
                e.ToTable("NoteFisier", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.FileType).HasMaxLength(50).IsRequired();
                e.Property(x => x.CreatedUtc).HasColumnType("datetime2(3)");

                // Foreign keys
                e.HasOne(x => x.UserNote)
                    .WithMany(x => x.Fisieri)
                    .HasForeignKey(x => x.UserNote_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.UserFile)
                    .WithMany()
                    .HasForeignKey(x => x.UserFile_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                e.HasIndex(x => x.UserNote_Id);
                e.HasIndex(x => x.UserFile_Id);
                e.HasIndex(x => new { x.UserNote_Id, x.UserFile_Id }).IsUnique(false);
            });

            // =========================
            // UserFile (dbo.UserFile)
            // =========================
            modelBuilder.Entity<UserFile>(e =>
            {
                e.ToTable("UserFile", "dbo");
                e.HasKey(x => x.Id);

                // ✅ NOU: Many-to-Many relație cu InformatiiSensibile
                e.HasMany(x => x.InformatiiSensibile)
                    .WithOne(x => x.UserFile)
                    .HasForeignKey(x => x.UserFile_Id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // =========================
            // InformatieImagine (dbo.InformatieImagine) - Junction table
            // =========================
            modelBuilder.Entity<InformatieImagine>(e =>
            {
                e.ToTable("InformatieImagine", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.CreatedUtc).HasColumnType("datetime2(3)");

                // Indexes
                e.HasIndex(x => x.InformatieSensibila_Id);
                e.HasIndex(x => x.UserFile_Id);
                e.HasIndex(x => new { x.InformatieSensibila_Id, x.UserFile_Id }).IsUnique(false);
            });

            // =========================
            // InformatieFisier (dbo.InformatieFisier) - Generic files junction
            // =========================
            modelBuilder.Entity<InformatieFisier>(e =>
            {
                e.ToTable("InformatieFisier", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.FileType).HasMaxLength(50).IsRequired();
                e.Property(x => x.CreatedUtc).HasColumnType("datetime2(3)");

                // Foreign keys
                e.HasOne(x => x.InformatieSensibila)
                    .WithMany(x => x.Fisieri)
                    .HasForeignKey(x => x.InformatieSensibila_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.UserFile)
                    .WithMany(x => x.InformatiiSensibile)
                    .HasForeignKey(x => x.UserFile_Id)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes
                e.HasIndex(x => x.InformatieSensibila_Id);
                e.HasIndex(x => x.UserFile_Id);
                e.HasIndex(x => new { x.InformatieSensibila_Id, x.UserFile_Id }).IsUnique(false);
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

            // =========================
            // ManagedCertificate (dbo.ManagedCertificates)
            // =========================
            modelBuilder.Entity<ManagedCertificate>(e =>
            {
                e.ToTable("ManagedCertificates", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.Url)
                    .HasMaxLength(2048)
                    .IsRequired();

                e.Property(x => x.Status)
                    .HasMaxLength(50)
                    .IsRequired()
                    .HasDefaultValue("Unknown");

                e.Property(x => x.CertificateExpiryDate)
                    .HasColumnType("datetime2(3)");

                e.Property(x => x.LastCheckDate)
                    .HasColumnType("datetime2(3)");

                e.Property(x => x.CreatedDate)
                    .HasColumnType("datetime2(3)")
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                e.Property(x => x.DaysUntilExpiry);

                e.Property(x => x.CertificateSubject)
                    .HasMaxLength(512);

                e.Property(x => x.CertificateIssuer)
                    .HasMaxLength(512);

                e.Property(x => x.ErrorMessage)
                    .HasMaxLength(1024);

                // Origin server verification fields
                e.Property(x => x.OriginServerIP)
                    .HasMaxLength(45);  // IPv6 max length

                e.Property(x => x.OriginServerPort)
                    .HasDefaultValue(443);

                e.Property(x => x.OriginCertificateExpiryDate)
                    .HasColumnType("datetime2(3)");

                e.Property(x => x.OriginCertificateSubject)
                    .HasMaxLength(512);

                e.Property(x => x.OriginCertificateIssuer)
                    .HasMaxLength(512);

                e.Property(x => x.VerificationMethod)
                    .HasMaxLength(20);

                e.Property(x => x.IsCertificateMismatch)
                    .HasDefaultValue(false);

                e.Property(x => x.OriginDaysUntilExpiry);

                e.HasIndex(x => x.Url);
            });

            // =========================
            // CertificateAlertLog (dbo.CertificateAlertLog)
            // =========================
            modelBuilder.Entity<CertificateAlertLog>(e =>
            {
                e.ToTable("CertificateAlertLog", "dbo");
                e.HasKey(x => x.Id);

                e.Property(x => x.ManagedCertificateId)
                    .IsRequired();

                e.Property(x => x.CertificateUrl)
                    .HasMaxLength(2048)
                    .IsRequired();

                e.Property(x => x.DaysUntilExpiry)
                    .IsRequired();

                e.Property(x => x.AlertType)
                    .HasMaxLength(50)
                    .IsRequired();

                e.Property(x => x.EmailSentTo)
                    .HasMaxLength(256)
                    .IsRequired();

                e.Property(x => x.AlertSentDateUtc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired()
                    .HasDefaultValueSql("GETUTCDATE()");

                e.Property(x => x.EmailStatus)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("Pending");

                e.Property(x => x.ErrorMessage)
                    .HasMaxLength(1024);

                // Relationship to ManagedCertificate
                e.HasOne(x => x.ManagedCertificate)
                    .WithMany()
                    .HasForeignKey(x => x.ManagedCertificateId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Indexes for performance
                e.HasIndex(x => x.ManagedCertificateId);
                e.HasIndex(x => x.AlertSentDateUtc);
            });
        }
    }
}

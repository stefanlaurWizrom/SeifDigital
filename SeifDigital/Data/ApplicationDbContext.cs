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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =========================
            // AuditLog (EXISTENT - NU STRICĂM)
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
            //
            // IMPORTANT:
            //  - în DB ai OwnerUser, NoteText, CreatedUtc, UpdatedUtc + o coloană extra "Text" (NOT NULL)
            //  - noi mapăm proprietatea Text -> NoteText
            //  - și ignorăm coloana "Text" din DB ca să nu fie cerută de EF la INSERT/UPDATE
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

                e.Property(x => x.Text)
                    .HasColumnName("NoteText")
                    .HasMaxLength(255)
                    .IsRequired();

                e.Property(x => x.CreatedUtc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired();

                e.Property(x => x.UpdatedUtc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired();

                // există o coloană fizică "Text" în DB, dar NU există proprietate în model -> o ignorăm explicit
                // (EF nu o va include la insert/update)
                e.Ignore("_"); // no-op, doar ca să fie clar că nu mapăm coloana "Text"

                e.HasIndex(x => x.OwnerUser);
            });

            // =========================
            // UserFile (dbo.UserFile)
            // =========================
            modelBuilder.Entity<UserFile>(e =>
            {
                e.ToTable("UserFile", "dbo");
                e.HasKey(x => x.Id);
            });
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SeifDigital.Data;
using SeifDigital.Services;

namespace SeifDigital.Tools.PopulateDetaliiClear
{
    // Small console job to populate DetaliiClear by decrypting DetaliiCriptate.
    // Usage:
    //  - Add this project to the solution (or copy file into an existing console project).
    //  - Ensure the same connection string name ("DefaultConnection") exists in appsettings.json
    //  - Register EncryptionService in ConfigureServices below exactly as in your web app.
    //  - Run the tool on the server where it is safe to decrypt sensitive data.
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    // loads appsettings.json from the working directory - adjust path if necessary
                    cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    cfg.AddEnvironmentVariables();
                })
                .ConfigureServices((ctx, services) =>
                {
                    var configuration = ctx.Configuration;
                    var conn = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration.GetSection("ConnectionStrings")["DefaultConnection"];

                    // register your ApplicationDbContext exactly like the web app
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlServer(conn));

                    // Register EncryptionService the same way as in the web app.
                    // If EncryptionService requires constructor parameters, replicate its registration here.
                    services.AddSingleton<EncryptionService>();

                    // hosted service not needed; we'll run manual scope below.
                })
                .Build();

            try
            {
                using var scope = host.Services.CreateScope();
                var services = scope.ServiceProvider;

                var db = services.GetRequiredService<ApplicationDbContext>();
                var crypto = services.GetRequiredService<EncryptionService>();

                const int batchSize = 200;
                while (true)
                {
                    var rows = await db.InformatiiSensibile
                        .Where(x => x.DetaliiClear == null && x.DetaliiCriptate != null)
                        .OrderBy(x => x.Id)
                        .Take(batchSize)
                        .ToListAsync();

                    if (rows.Count == 0)
                    {
                        Console.WriteLine("No more rows to update. Done.");
                        break;
                    }

                    foreach (var row in rows)
                    {
                        try
                        {
                            // Decrypt and set DetaliiClear
                            var decrypted = crypto.Decrypt(row.DetaliiCriptate ?? "");
                            row.DetaliiClear = string.IsNullOrWhiteSpace(decrypted) ? null : decrypted;
                        }
                        catch (Exception ex)
                        {
                            // Log and skip problematic row - don't stop the whole batch.
                            Console.WriteLine($"Failed to decrypt Id={row.Id}: {ex.Message}");
                        }
                    }

                    await db.SaveChangesAsync();
                    Console.WriteLine($"Updated {rows.Count} rows (last id = {rows.Last().Id}).");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error: {ex}");
                return 1;
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
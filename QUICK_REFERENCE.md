# 🚀 SeifDigital Production Deployment - QUICK REFERENCE

## 📊 APPLICATION OVERVIEW

| Aspect | Details |
|--------|---------|
| **Framework** | ASP.NET Core 8.0 (Razor Pages) |
| **Database** | SQL Server 2019+ |
| **Authentication** | Custom (UserAccount/Password) + AD Support |
| **Web Server** | IIS 10.0+ |
| **Architecture** | Entity Framework Core with encrypted data |
| **Key Features** | Sensitive data encryption, Audit logs, 2FA support |

---

## ⚡ DEPLOYMENT IN 15 MINUTES (Quick Path)

### Step 1: Prerequisites (5 min)
```powershell
# Check .NET 8 installed
dotnet --version

# If not, download & install:
# https://dotnet.microsoft.com/download/dotnet/8.0
# Choose: Hosting Bundle for Windows x64
```

### Step 2: Create SQL Database (3 min)
```powershell
# Run SQL_Setup_Scripts.sql in SQL Server Management Studio
# This creates:
# - Database: SeifDate
# - Login: seifapp
# - All permissions configured
```

### Step 3: Create IIS Infrastructure (4 min)
```powershell
# Run as Administrator:
.\Deploy-SeifDigital.ps1 -ServerName ".\SQLEXPRESS" `
                         -DbPassword "YourStrongPassword" `
                         -Domain "seifdigital.yourdomain.com"

# This automatically:
# - Creates directories
# - Sets permissions
# - Creates App Pool
# - Creates Website
# - Publishes application
# - Runs migrations
```

### Step 4: Verify
```
Browse to: http://seifdigital.yourdomain.com
Expected: Home page with WizVault branding loads
```

---

## 📋 DETAILED DEPLOYMENT CHECKLIST

### PRE-DEPLOYMENT
- [ ] Windows Server 2019/2022 prepared
- [ ] Administrator access available
- [ ] .NET 8 Hosting Bundle downloaded
- [ ] SQL Server 2019+ installed
- [ ] Domain/hostname registered
- [ ] SSL certificate obtained (optional but recommended)

### PHASE 1: SQL SERVER (10-15 min)
```powershell
# Execute all scripts in SQL_Setup_Scripts.sql
# Verify in SQL Server Management Studio:
# - Database exists: SeifDate
# - User exists: seifapp
# - Permissions: db_owner role
# - Query Store: enabled

# Test connection:
sqlcmd -S .\SQLEXPRESS -U seifapp -P "PASSWORD" -Q "SELECT @@VERSION"
```

### PHASE 2: .NET HOSTING (5-10 min)
```powershell
# 1. Install Hosting Bundle (if not done)
#    Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# 2. Restart IIS
iisreset /restart

# 3. Verify ANCM installed
Get-Content "C:\Windows\System32\inetsrv\config\applicationHost.config" | `
    Select-String "AspNetCoreModule"
```

### PHASE 3: IIS SETUP (10-15 min)
```powershell
# Run automated script (handles all below):
.\Deploy-SeifDigital.ps1

# OR Manual setup:
# 1. Create App Pool: seifdigital
# 2. Create Website: SeifDigital (binding: port 80)
# 3. Create directories: C:\inetpub\wwwroot\seifdigital\{app,uploads,logs,cache,config}
# 4. Set NTFS permissions for "IIS AppPool\seifdigital"
```

### PHASE 4: APPLICATION DEPLOYMENT (5 min)
```powershell
# Publish from development machine OR
cd "D:\seifdigital\SeifDigital"
dotnet publish -c Release -o "C:\inetpub\wwwroot\seifdigital\app"

# Copy appsettings.Production.json to app folder
```

### PHASE 5: DATABASE MIGRATIONS (2-5 min)
```powershell
# From published app directory:
cd "C:\inetpub\wwwroot\seifdigital\app"
dotnet ef database update --configuration Release

# Verify tables created:
# - InformatiiSensibile
# - AuditLog
# - UserAccounts
# - UserNotes
# - UserFiles
# - UserProfiles
# - UserMessages
# - AppSettings
```

### PHASE 6: HTTPS/SSL (5-15 min)
```powershell
# 1. Obtain certificate (Let's Encrypt or commercial)
# 2. Import to Windows:
$cert = Import-PfxCertificate -FilePath "C:\certificates\seifdigital.pfx" `
    -CertStoreLocation Cert:\LocalMachine\My `
    -Password (ConvertTo-SecureString "PASSWORD" -AsPlainText -Force)

# 3. Create HTTPS binding in IIS (port 443)
# 4. Enable HTTP→HTTPS redirect (see IIS_HTTPS_SETUP.md)
```

### POST-DEPLOYMENT
- [ ] Test login at https://seifdigital.yourdomain.com
- [ ] Verify database connectivity
- [ ] Check logs for errors
- [ ] Configure automated backups
- [ ] Setup monitoring/alerts
- [ ] Create test users
- [ ] Document server configuration

---

## 🔑 IMPORTANT CONFIGURATION FILES

### appsettings.Production.json
**Location**: `C:\inetpub\wwwroot\seifdigital\app\appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\SQLEXPRESS;Database=SeifDate;User Id=seifapp;Password=YourPassword;TrustServerCertificate=True;Encrypt=True;"
  },
  "Crypto": {
    "MasterKeyBase64": "GENERATE_AND_KEEP_SECURE"
  },
  "Smtp": {
    "Host": "relay.wizrom.ro",
    "Port": 25,
    "From": "seifdigital@wizrom.ro"
  },
  "AllowedHosts": "seifdigital.yourdomain.com"
}
```

### web.config
**Location**: `C:\inetpub\wwwroot\seifdigital\app\web.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath=".\SeifDigital.exe" stdoutLogEnabled="false" />
  </system.webServer>
</configuration>
```

---

## 🔧 FREQUENTLY NEEDED COMMANDS

### SQL Server
```sql
-- Test database access
USE SeifDate;
SELECT COUNT(*) AS UserCount FROM dbo.UserAccounts;

-- View audit logs
SELECT TOP 20 * FROM dbo.AuditLog ORDER BY EventTimeUtc DESC;

-- Backup database
BACKUP DATABASE [SeifDate] 
TO DISK = N'D:\SQLBackups\SeifDate_Full.bak'
WITH INIT, STATS = 10, COMPRESSION;
```

### IIS Management
```powershell
# Restart app pool
Restart-WebAppPool -Name seifdigital

# Stop/Start website
Stop-Website -Name SeifDigital
Start-Website -Name SeifDigital

# Check app pool state
Get-WebAppPoolState -Name seifdigital

# View logs
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\u_ex*.log" -Tail 50
```

### Application Management
```powershell
# Restart application (IIS restart)
iisreset /restart

# Graceful stop (finish requests first)
iisreset /stop
# Then restart
iisreset /start

# Check process memory
Get-Process -Name w3wp | Format-Table Name, WorkingSet, ProcessorTime

# Deploy new version
cd "C:\inetpub\wwwroot\seifdigital\app"
dotnet publish -c Release -o . --force
```

---

## ⚠️ CRITICAL SECURITY ITEMS

- [ ] Change default SQL password (`TrebuieSaFiiParolaFORTE@123#SeifDigital`)
- [ ] Secure master encryption key (use Azure Key Vault in production)
- [ ] Enable HTTPS/SSL (not optional for production)
- [ ] Configure firewall (80, 443 only)
- [ ] Set strong Windows admin passwords
- [ ] Disable unnecessary Windows services
- [ ] Enable Windows Defender/Antivirus
- [ ] Configure automated SQL backups
- [ ] Setup application logging/monitoring
- [ ] Regular security patching (Windows Updates)
- [ ] Restrict database access to app only
- [ ] Enable audit logging (already configured in app)

---

## 📞 TROUBLESHOOTING MATRIX

| Issue | Cause | Solution |
|-------|-------|----------|
| "502 Bad Gateway" | App crash | Check app logs, restart pool |
| "Cannot connect to database" | Wrong connection string | Verify Server name, database, user, password |
| "AspNetCore module not found" | Hosting Bundle not installed | Reinstall .NET 8 Hosting Bundle |
| "Permission denied" | App pool user can't write | Set NTFS permissions on directories |
| "Slow performance" | Memory/CPU bottleneck | Monitor processes, increase app pool limits |
| "SSL certificate error" | Invalid cert or binding | Reinstall cert, create correct binding |
| "404 Not Found" | Wrong routing | Verify URL routing in application |
| "Session lost after logout" | Session cookie issue | Check cookie settings in Program.cs |

---

## 📊 PERFORMANCE BASELINES (TARGETS)

After deployment, verify:

| Metric | Expected | How to Check |
|--------|----------|--------------|
| Home page load time | < 500 ms | Browser F12 dev tools |
| Database response | < 100 ms | SQL Profiler / Logging |
| App pool memory | < 500 MB | Task Manager / PowerShell |
| CPU usage | < 50% | Task Manager / Performance Monitor |
| Request queue | < 10 | IIS Manager app pool details |
| Disk free space | > 20% | `Get-Volume` PowerShell |

---

## 📚 DOCUMENTATION PROVIDED

1. **DEPLOYMENT_GUIDE_RO.md** - Complete step-by-step guide (Romanian)
2. **Deploy-SeifDigital.ps1** - Automated deployment script
3. **SQL_Setup_Scripts.sql** - Database setup scripts
4. **IIS_HTTPS_SETUP.md** - Web server configuration guide
5. **This file** - Quick reference

---

## 🎯 NEXT STEPS AFTER DEPLOYMENT

1. **Monitor**: Setup Application Insights or similar
2. **Backup**: Configure daily SQL backups
3. **Updates**: Plan patching strategy for OS & .NET
4. **Security**: Regular security audits
5. **Scaling**: Plan for traffic growth
6. **Documentation**: Document any customizations
7. **Training**: Train ops team on management

---

## 📞 SUPPORT RESOURCES

- **Microsoft Docs**: https://learn.microsoft.com/aspnet/core
- **IIS Documentation**: https://learn.microsoft.com/iis
- **SQL Server Docs**: https://learn.microsoft.com/sql
- **ASP.NET Core Deployment**: https://learn.microsoft.com/aspnet/core/host-and-deploy
- **GitHub Repository**: https://github.com/stefanlaurWizrom/SeifDigital

---

**Prepared For**: SeifDigital Application
**Version**: .NET 8.0
**Last Updated**: 2025
**Status**: Production Ready ✅

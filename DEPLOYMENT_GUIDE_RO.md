# 📋 Ghid Complet Implementare SeifDigital pe Server de Producție

## 📌 Caracteristici Aplicație
- **Framework**: ASP.NET Core 8.0 (Razor Pages)
- **Bază de Date**: SQL Server
- **Autentificare**: Custom (UserAccount/Parola) + Optional Active Directory
- **Email**: SMTP (Wizrom)
- **Criptare**: Criptare end-to-end pentru date sensibile
- **Session Management**: Server-side sessions (30 min timeout)

---

## 🔧 PASUL 1: PREGĂTIRE SERVER (PRE-DEPLOYMENT)

### 1.1 Cerințe Hardware Minime
- **CPU**: 2 core
- **RAM**: 4 GB minim (8 GB recomandat)
- **Disk**: 50 GB SSD (pentru cache, logs, database)
- **OS**: Windows Server 2019 / 2022

### 1.2 Software Necesar
- ✅ .NET Runtime 8.0 (x64)
- ✅ .NET Desktop Runtime 8.0 (pentru administrative tools)
- ✅ SQL Server 2019+ (Standard edition minim)
- ✅ IIS 10.0+ (cu componente ASP.NET Core)
- ✅ Hosting Bundle .NET 8.0

**Descărcare Link**:
```
https://dotnet.microsoft.com/en-us/download/dotnet/8.0
- Download: Hosting Bundle (cuprinde Runtime + IIS Module)
```

---

## 💾 PASUL 2: SETUP SQL SERVER

### 2.1 Creare Bază de Date

**Opțiunea A: SQL Server Management Studio (SSMS)**

```sql
-- 1. Creare Database
CREATE DATABASE [SeifDate] 
CONTAINMENT = NONE 
ON PRIMARY 
( NAME = N'SeifDate', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate.mdf' , SIZE = 100MB , FILEGROWTH = 10% )
LOG ON 
( NAME = N'SeifDate_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate_log.ldf' , SIZE = 50MB , FILEGROWTH = 10%);

-- 2. Setare mode Recovery (Full pentru producție)
ALTER DATABASE [SeifDate] SET RECOVERY FULL;

-- 3. Creare Login SQL (pentru aplicație)
CREATE LOGIN [seifapp] WITH PASSWORD = 'TrebuieSaFiiParolaFORTE@123#SeifDigital';
ALTER LOGIN [seifapp] ENABLE;

-- 4. Creare User în baza de date
USE [SeifDate];
CREATE USER [seifapp] FOR LOGIN [seifapp];

-- 5. Alocare permisiuni
ALTER ROLE [db_owner] ADD MEMBER [seifapp];
-- ALTERNATIV (mai restrictiv):
-- GRANT CONTROL ON DATABASE::[SeifDate] TO [seifapp];

-- 6. Verificare conexiune
SELECT * FROM sys.databases WHERE name = 'SeifDate';
```

**Opțiunea B: PowerShell (Automation)**

```powershell
$sqlServer = ".\SQLEXPRESS"
$databaseName = "SeifDate"
$login = "seifapp"
$password = "TrebuieSaFiiParolaFORTE@123#SeifDigital"

# Conectare la SQL Server
[System.Reflection.Assembly]::LoadWithPartialName('Microsoft.SqlServer.SMO') | Out-Null
$srv = New-Object -TypeName Microsoft.SqlServer.Management.Smo.Server($sqlServer)

# Creare Database
$db = New-Object -TypeName Microsoft.SqlServer.Management.Smo.Database($srv, $databaseName)
$db.Create()
Write-Host "Database $databaseName created successfully"

# Setare Recovery Mode
$db.RecoveryModel = [Microsoft.SqlServer.Management.Smo.RecoveryModel]::Full
$db.Alter()

# Creare Login
$login_obj = New-Object -TypeName Microsoft.SqlServer.Management.Smo.Login($srv, $login)
$login_obj.LoginType = [Microsoft.SqlServer.Management.Smo.LoginType]::SqlLogin
$login_obj.Create($password)
Write-Host "Login $login created successfully"
```

### 2.2 Backup și Restore Strategie

```sql
-- Backup complet zilnic
BACKUP DATABASE [SeifDate] 
TO DISK = N'D:\SQLBackups\SeifDate_Full.bak'
WITH INIT, STATS = 10, COMPRESSION;

-- Backup tranzacții (orar)
BACKUP LOG [SeifDate] 
TO DISK = N'D:\SQLBackups\SeifDate_Log.trn'
WITH INIT, STATS = 10, COMPRESSION;
```

---

## 📁 PASUL 3: PREGĂTIRE FOLDERE ȘI PERMISIUNI

### 3.1 Structura Directoare pe Disk C: (sau alt disk)

```
C:\inetpub\wwwroot\
├── seifdigital/
│   ├── app/                 (fișiere aplicație)
│   ├── uploads/             (fișiere încărcate de utilizatori)
│   ├── logs/                (loguri aplicație)
│   ├── cache/               (cache temporar)
│   └── config/              (appsettings.json)
├── backups/                 (backup-uri)
└── certificates/            (SSL certificates)
```

### 3.2 PowerShell: Creare Structură

```powershell
$basePath = "C:\inetpub\wwwroot\seifdigital"
$folders = @("app", "uploads", "logs", "cache", "config")

foreach ($folder in $folders) {
    $path = Join-Path $basePath $folder
    if (!(Test-Path -Path $path)) {
        New-Item -Path $path -ItemType Directory -Force | Out-Null
        Write-Host "Created: $path"
    }
}
```

### 3.3 Setare Permisiuni NTFS

```powershell
# Identity pentru IIS App Pool
$iisUser = "IIS AppPool\seifdigital"

# Folderul root
icacls "C:\inetpub\wwwroot\seifdigital" /grant:r "$($iisUser):(OI)(CI)(M)" /t
icacls "C:\inetpub\wwwroot\seifdigital\uploads" /grant:r "$($iisUser):(OI)(CI)(F)" /t
icacls "C:\inetpub\wwwroot\seifdigital\logs" /grant:r "$($iisUser):(OI)(CI)(F)" /t
icacls "C:\inetpub\wwwroot\seifdigital\cache" /grant:r "$($iisUser):(OI)(CI)(F)" /t

Write-Host "Permissions set successfully"
```

---

## 🌐 PASUL 4: SETUP IIS (INTERNET INFORMATION SERVICES)

### 4.1 Instalare IIS cu Componente

**PowerShell (Administrator)**:

```powershell
# Instalare IIS cu role ASP.NET Core
Install-WindowsFeature -Name Web-Server, Web-Asp-Net, Web-Asp-Net45, Web-Mgmt-Tools

# Instalare specific pentru ASP.NET Core
Install-WindowsFeature -Name Web-AppInit, Web-Http-Errors, Web-Http-Logging, Web-Request-Monitor

# Restart dacă necesar
Restart-Computer -Force
```

### 4.2 Instalare .NET 8 Hosting Bundle

1. Descarcă: https://dotnet.microsoft.com/download/dotnet/8.0
2. Selectează "Hosting Bundle" (din .NET 8.0)
3. Rulează instalatorul cu Admin
4. **Restart IIS după instalare**

```powershell
# Restart IIS
iisreset /restart
```

### 4.3 Configurare Application Pool

```powershell
Import-Module WebAdministration

# Creare App Pool
$appPoolName = "seifdigital"
New-WebAppPool -Name $appPoolName

# Configurare
$appPool = Get-Item "IIS:\AppPools\$appPoolName"
$appPool.ProcessModel.IdentityType = "ApplicationPoolIdentity"
$appPool.ManagedRuntimeVersion = ""  # .NET Core (gol este corect!)
$appPool.Enable32BitAppOn64bit = $false
$appPool.ProcessModel.MaxProcesses = 4

# Setare timeout
$appPool.ProcessModel.IdleTimeout = [TimeSpan]::FromMinutes(20)

# Start App Pool
Start-WebAppPool -Name $appPoolName
Write-Host "App Pool $appPoolName created and started"
```

### 4.4 Creare Website în IIS

```powershell
# Variabile
$siteName = "SeifDigital"
$appPoolName = "seifdigital"
$physicalPath = "C:\inetpub\wwwroot\seifdigital\app"
$port = 80
$binding = "*:80:seifdigital.yourdomain.com"

# Creare Website
New-WebSite -Name $siteName -PhysicalPath $physicalPath -HostHeader "seifdigital.yourdomain.com" -Port $port -ApplicationPool $appPoolName

# Verifica
Get-Website -Name $siteName
```

**Alternativ (GUI IIS Manager)**:
1. Deschide **IIS Manager**
2. Clic dreapta pe **Sites** → **Add Website**
3. Completează:
   - **Site name**: SeifDigital
   - **Physical path**: C:\inetpub\wwwroot\seifdigital\app
   - **Binding Type**: http
   - **IP Address**: All Unassigned
   - **Port**: 80
   - **Host name**: seifdigital.yourdomain.com

### 4.5 Configurare Application (Virtual Directory)

```powershell
# Dacă vrei app la /seifdigital (subpath)
New-WebApplication -Name seifdigital `
    -Site "Default Web Site" `
    -PhysicalPath "C:\inetpub\wwwroot\seifdigital\app" `
    -ApplicationPool "seifdigital"
```

---

## 📦 PASUL 5: PUBLICARE APLICAȚIE

### 5.1 Build și Publish din Visual Studio

```powershell
# 1. Navigare la project
cd "D:\seifdigital"

# 2. Publish Release
dotnet publish -c Release -o "C:\inetpub\wwwroot\seifdigital\app"

# 3. Verifica fișierele
ls "C:\inetpub\wwwroot\seifdigital\app" | head -20
```

### 5.2 Alternative: Publish via CLI

```powershell
# Varianta Full (include dependencies)
dotnet publish -c Release --no-restore -o "C:\inetpub\wwwroot\seifdigital\app" -r win-x64 --self-contained

# Varianta Standard (necesită .NET Runtime)
dotnet publish -c Release -o "C:\inetpub\wwwroot\seifdigital\app"
```

---

## ⚙️ PASUL 6: CONFIGURARE APPSETTINGS.JSON

### 6.1 Creare appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=SERVERUL_TAU\\SQLEXPRESS;Database=SeifDate;User Id=seifapp;Password=TrebuieSaFiiParolaFORTE@123#SeifDigital;TrustServerCertificate=True;Encrypt=True;"
  },
  
  "Crypto": {
    "MasterKeyBase64": "TREBUIE_GENERAT_SI_SALVAT_IN_SIGURANTA"
  },

  "Smtp": {
    "Host": "relay.wizrom.ro",
    "Port": 25,
    "EnableSsl": false,
    "Username": "",
    "Password": "",
    "From": "seifdigital@wizrom.ro"
  },

  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },

  "AllowedHosts": "seifdigital.yourdomain.com",
  
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:5000"
      }
    }
  }
}
```

### 6.2 Generare Crypto Master Key Sigur

```csharp
// C# snippet pentru generare
using System;
using System.Security.Cryptography;
using System.Convert;

// Generare 32 bytes (256 bits) key
byte[] key = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(key);
}

string base64Key = Convert.ToBase64String(key);
Console.WriteLine("Master Key (Base64): " + base64Key);
// Salvează acest key într-un loc SIGUR (Secret Manager, Azure Key Vault)
```

**IMPORTANT**: 
- ❌ NU pune master key în appsettings.json pe producție
- ✅ Folosește **Azure Key Vault** sau **Secrets Manager**

---

## 🚀 PASUL 7: RULAREA MIGRAȚII DATABASE

### 7.1 Aplică Migrații

```powershell
# Navigare la project
cd "D:\seifdigital\SeifDigital"

# Applică migrații pe producție
dotnet ef database update --configuration Release --verbose

# Alternativ (dacă nu merge din CLI)
# - Pune fișierul publish-ed pe server
# - Rulează din publish folder:
#   dotnet SeifDigital.dll --migrate
```

### 7.2 Verify Database Structure

```sql
-- Verificare tabele create
USE [SeifDate];

SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME;

-- Output așteptat:
-- AppSettings
-- AuditLog
-- InformatiiImagini
-- InformatiiImagini_New
-- InformatiiSensibile
-- UserAccounts
-- UserFiles
-- UserMessages
-- UserNotes
-- UserProfiles
```

---

## 🔒 PASUL 8: SSL/HTTPS SETUP

### 8.1 Obține SSL Certificate

**Opțiuni**:
1. **Let's Encrypt** (gratuit) - Recomand
2. **GoDaddy / Sectigo** (plătit)
3. **Self-signed** (doar test - nu pentru producție!)

### 8.2 Configurare IIS cu Certificate

**PowerShell**:

```powershell
# 1. Importa certificate
$certPath = "C:\certificates\seifdigital.yourdomain.com.pfx"
$certPassword = ConvertTo-SecureString -String "PASSWORD_CERT" -Force -AsPlainText

Import-PfxCertificate -FilePath $certPath -CertStoreLocation Cert:\LocalMachine\My -Password $certPassword

# 2. Obține thumbprint
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object {$_.Subject -like "*seifdigital*"}
$thumbprint = $cert.Thumbprint
Write-Host "Certificate Thumbprint: $thumbprint"

# 3. Setare HTTPS binding în IIS
New-WebBinding -Name "SeifDigital" -Protocol https -Port 443 -HostHeader "seifdigital.yourdomain.com" -CertificateThumbprint $thumbprint -CertificateStoreName My -SslFlags 0

# 4. Redirecționare HTTP → HTTPS
Remove-WebBinding -Name "SeifDigital" -Protocol http -Port 80
New-WebBinding -Name "SeifDigital" -Protocol http -Port 80 -HostHeader "seifdigital.yourdomain.com"
```

### 8.3 Configurare URL Rewrite (HTTP → HTTPS)

În **web.config**:

```xml
<rewrite>
  <rules>
    <rule name="Redirect to HTTPS" stopProcessing="true">
      <match url="(.*)" />
      <conditions>
        <add input="{HTTPS}" pattern="^OFF$" />
      </conditions>
      <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
    </rule>
  </rules>
</rewrite>
```

---

## 📊 PASUL 9: MONITORING ȘI LOGS

### 9.1 Configurare Logging

**appsettings.Production.json** - secțiunea Logging:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore": "Warning"
  },
  "Console": {
    "IncludeScopes": false
  },
  "EventLog": {
    "LogLevel": {
      "Default": "Error"
    }
  }
}
```

### 9.2 Failed Request Tracing (FREB)

```powershell
# Activate FREB în IIS
Install-WindowsFeature -Name Web-Http-Tracing

# În IIS Manager:
# 1. Selectează site SeifDigital
# 2. Failed Request Tracing Rules
# 3. Add → Configure pentru .aspx și .cshtml
# 4. Set Status codes: 200-999, Time taken: > 5000 ms
```

### 9.3 Event Viewer Monitoring

```powershell
# Vezi logs de aplicație
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 20 | Format-Table TimeGenerated, Message -AutoSize
```

---

## 🧪 PASUL 10: TEST ȘI VALIDARE

### 10.1 Test Conexiune SQL

```csharp
// în Program.cs sau startup
using (var context = new ApplicationDbContext(options))
{
    bool canConnect = context.Database.CanConnect();
    if (canConnect)
        Console.WriteLine("✅ Database connected!");
    else
        Console.WriteLine("❌ Database connection failed!");
}
```

### 10.2 Health Check Endpoint

```csharp
// În Program.cs, adaugă:
app.MapHealthChecks("/health");

// Test: curl https://seifdigital.yourdomain.com/health
```

### 10.3 Test Login și Funcționalitate

1. **Creare user test** în database:

```sql
USE [SeifDate];
INSERT INTO dbo.UserAccounts (Username, PasswordHash, Email, Created)
VALUES ('testuser', 'hashed_password_here', 'test@wizrom.ro', GETUTCDATE());
```

2. **Test endpoints**:
   - https://seifdigital.yourdomain.com → Home page
   - https://seifdigital.yourdomain.com/Account/Login → Login
   - https://seifdigital.yourdomain.com/Health → Health check

### 10.4 Performance Baseline

```powershell
# Load testing cu Apache Bench
# Download: https://httpd.apache.org/download.cgi
ab -n 1000 -c 50 https://seifdigital.yourdomain.com/
```

---

## 📋 CHECKLIST FINAL PRE-LAUNCH

- [ ] SQL Server instalat și SeifDate database creată
- [ ] Login `seifapp` creat cu permisiuni corecte
- [ ] .NET 8 Hosting Bundle instalat
- [ ] IIS App Pool `seifdigital` creat
- [ ] Website IIS creat cu binding corect
- [ ] Permisiuni NTFS setate pe C:\inetpub\wwwroot\seifdigital
- [ ] Aplicație publicată
- [ ] appsettings.Production.json configurat corect
- [ ] Connection string testat
- [ ] Migrații database aplicate
- [ ] SSL Certificate instalat
- [ ] HTTPS binding setat
- [ ] URL rewrite configured
- [ ] Health check endpoint respondează
- [ ] Test user pot login
- [ ] Logs sunt scrise corect
- [ ] Backup SQL Server configurat
- [ ] Firewall rules deschise (80, 443)

---

## 🚨 TROUBLESHOOTING

### Eroare: "The module DLL could not be loaded"
```powershell
# .NET Runtime nu e instalat
dotnet --version
# Instalează hosting bundle: https://dotnet.microsoft.com/download/dotnet/8.0
```

### Eroare: "Connection string error"
```
- Verifica server name (SERVERUL\SQLEXPRESS)
- Verifica user/password SQL
- Verifica firewall SQL Server (port 1433)
- Verifica TrustServerCertificate=True
```

### Eroare: "Permission denied"
```powershell
# Reset permisiuni
icacls "C:\inetpub\wwwroot\seifdigital" /reset /t
icacls "C:\inetpub\wwwroot\seifdigital" /grant:r "IIS AppPool\seifdigital:(OI)(CI)(F)" /t
```

### Slow Performance
- Verifica SQL Server query logs
- Verifica IIS logs: C:\inetpub\logs\LogFiles
- Verifica memoria disponibilă
- Crește App Pool recycling interval

---

## 📚 REFERINȚE UTILE

- ASP.NET Core Deployment: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/?view=aspnetcore-8.0
- IIS Configuration: https://learn.microsoft.com/en-us/iis/configuration/
- SQL Server Setup: https://learn.microsoft.com/en-us/sql/database-engine/install-windows/install-sql-server
- .NET 8 Download: https://dotnet.microsoft.com/download/dotnet/8.0

---

**Creat pentru**: SeifDigital Application
**Versiune**: 1.0 (ASP.NET Core 8.0)
**Data**: 2025

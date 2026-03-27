# ============================================================================
# AUTOMATED DEPLOYMENT SCRIPT - SeifDigital Production Setup
# ============================================================================
# RUN AS ADMINISTRATOR!
# Usage: .\Deploy-SeifDigital.ps1 -ServerName "PROD-SERVER" -DbPassword "YourStrongPassword"
# ============================================================================

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerName = ".\SQLEXPRESS",
    
    [Parameter(Mandatory=$true)]
    [string]$DbPassword,
    
    [string]$Domain = "seifdigital.yourdomain.com",
    [string]$AppPoolName = "seifdigital",
    [string]$SiteName = "SeifDigital",
    [string]$DbName = "SeifDate",
    [string]$DbUser = "seifapp"
)

# ============================================================================
# UTILITY FUNCTIONS
# ============================================================================

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Check-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "This script must be run as Administrator!"
        exit 1
    }
    Write-Success "Administrator privileges confirmed"
}

# ============================================================================
# STEP 1: CHECK PREREQUISITES
# ============================================================================

function Check-Prerequisites {
    Write-Host "`n📋 CHECKING PREREQUISITES..." -ForegroundColor Cyan
    
    # Check .NET 8
    $dotnetVersion = dotnet --version
    if ($LASTEXITCODE -eq 0) {
        Write-Success ".NET Runtime detected: $dotnetVersion"
    } else {
        Write-Error ".NET 8 Runtime not found! Install from https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    }
    
    # Check SQL Server
    $sqlCheck = Test-Connection -ComputerName $ServerName.Split('\')[0] -Count 1 -Quiet
    if ($sqlCheck) {
        Write-Success "SQL Server host is reachable: $ServerName"
    } else {
        Write-Warning "Could not verify SQL Server connectivity - proceeding anyway"
    }
    
    # Check IIS
    $iisInstalled = (Get-WindowsFeature -Name Web-Server).Installed
    if (!$iisInstalled) {
        Write-Warning "IIS not installed! Installing now..."
        Install-WindowsFeature -Name Web-Server, Web-Asp-Net45, Web-Mgmt-Tools -IncludeManagementTools
        Restart-Computer -Force
    } else {
        Write-Success "IIS is installed"
    }
}

# ============================================================================
# STEP 2: CREATE SQL SERVER DATABASE
# ============================================================================

function Setup-Database {
    Write-Host "`n💾 SETTING UP SQL DATABASE..." -ForegroundColor Cyan
    
    try {
        # SQL Script
        $sqlScript = @"
-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$DbName')
BEGIN
    CREATE DATABASE [$DbName] 
    CONTAINMENT = NONE 
    ON PRIMARY 
    ( NAME = N'$DbName', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\$DbName.mdf' , SIZE = 100MB , FILEGROWTH = 10% )
    LOG ON 
    ( NAME = N'$($DbName)_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\$($DbName)_log.ldf' , SIZE = 50MB , FILEGROWTH = 10%);
    
    ALTER DATABASE [$DbName] SET RECOVERY FULL;
END
ELSE
BEGIN
    PRINT 'Database $DbName already exists'
END

-- Create Login
IF NOT EXISTS (SELECT * FROM sys.syslogins WHERE name = '$DbUser')
BEGIN
    CREATE LOGIN [$DbUser] WITH PASSWORD = N'$DbPassword';
    PRINT 'Login $DbUser created'
END
ELSE
BEGIN
    PRINT 'Login $DbUser already exists'
END

-- Create User and Grant Permissions
USE [$DbName];
IF NOT EXISTS (SELECT * FROM sys.sysusers WHERE name = '$DbUser')
BEGIN
    CREATE USER [$DbUser] FOR LOGIN [$DbUser];
    ALTER ROLE [db_owner] ADD MEMBER [$DbUser];
    PRINT 'User $DbUser created and granted permissions'
END
ELSE
BEGIN
    PRINT 'User $DbUser already exists'
END
"@

        # Save script to temp file
        $tempSqlFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.sql'
        Set-Content -Path $tempSqlFile -Value $sqlScript
        
        # Execute script
        sqlcmd -S $ServerName -i $tempSqlFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Database and login created successfully"
            Remove-Item $tempSqlFile
        } else {
            Write-Error "Failed to create database!"
            Remove-Item $tempSqlFile
            exit 1
        }
    }
    catch {
        Write-Error "Exception during database setup: $_"
        exit 1
    }
}

# ============================================================================
# STEP 3: CREATE DIRECTORY STRUCTURE
# ============================================================================

function Setup-Directories {
    Write-Host "`n📁 CREATING DIRECTORY STRUCTURE..." -ForegroundColor Cyan
    
    $basePath = "C:\inetpub\wwwroot\seifdigital"
    $folders = @("app", "uploads", "logs", "cache", "config")
    
    try {
        if (!(Test-Path -Path $basePath)) {
            New-Item -Path $basePath -ItemType Directory -Force | Out-Null
        }
        
        foreach ($folder in $folders) {
            $path = Join-Path $basePath $folder
            if (!(Test-Path -Path $path)) {
                New-Item -Path $path -ItemType Directory -Force | Out-Null
                Write-Success "Created: $path"
            }
        }
        
        Write-Success "Directory structure created at: $basePath"
    }
    catch {
        Write-Error "Failed to create directories: $_"
        exit 1
    }
}

# ============================================================================
# STEP 4: SET NTFS PERMISSIONS
# ============================================================================

function Setup-Permissions {
    Write-Host "`n🔐 SETTING NTFS PERMISSIONS..." -ForegroundColor Cyan
    
    $basePath = "C:\inetpub\wwwroot\seifdigital"
    $iisUser = "IIS AppPool\$AppPoolName"
    
    try {
        # Reset ACL
        icacls $basePath /reset /t | Out-Null
        
        # Grant permissions
        icacls $basePath /grant:r "$($iisUser):(OI)(CI)(M)" /t | Out-Null
        icacls "$basePath\uploads" /grant:r "$($iisUser):(OI)(CI)(F)" /t | Out-Null
        icacls "$basePath\logs" /grant:r "$($iisUser):(OI)(CI)(F)" /t | Out-Null
        icacls "$basePath\cache" /grant:r "$($iisUser):(OI)(CI)(F)" /t | Out-Null
        
        Write-Success "NTFS permissions configured for: $iisUser"
    }
    catch {
        Write-Error "Failed to set permissions: $_"
        exit 1
    }
}

# ============================================================================
# STEP 5: CREATE IIS APPLICATION POOL
# ============================================================================

function Setup-AppPool {
    Write-Host "`n🔧 CONFIGURING IIS APPLICATION POOL..." -ForegroundColor Cyan
    
    try {
        Import-Module WebAdministration
        
        # Check if pool exists
        $existingPool = Get-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        if ($existingPool) {
            Write-Warning "App Pool $AppPoolName already exists, skipping creation"
            return
        }
        
        # Create App Pool
        New-WebAppPool -Name $AppPoolName | Out-Null
        
        # Configure
        $appPool = Get-Item "IIS:\AppPools\$AppPoolName"
        $appPool.ProcessModel.IdentityType = "ApplicationPoolIdentity"
        $appPool.ManagedRuntimeVersion = ""  # .NET Core/5+
        $appPool.Enable32BitAppOn64bit = $false
        $appPool.ProcessModel.MaxProcesses = 4
        $appPool.ProcessModel.IdleTimeout = [TimeSpan]::FromMinutes(20)
        $appPool | Set-Item
        
        # Start
        Start-WebAppPool -Name $AppPoolName
        
        Write-Success "App Pool $AppPoolName created and configured"
    }
    catch {
        Write-Error "Failed to create App Pool: $_"
        exit 1
    }
}

# ============================================================================
# STEP 6: CREATE IIS WEBSITE
# ============================================================================

function Setup-Website {
    Write-Host "`n🌐 CONFIGURING IIS WEBSITE..." -ForegroundColor Cyan
    
    try {
        Import-Module WebAdministration
        
        $physicalPath = "C:\inetpub\wwwroot\seifdigital\app"
        
        # Check if site exists
        $existingSite = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
        if ($existingSite) {
            Write-Warning "Website $SiteName already exists, updating configuration..."
            # Update if needed
            Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $physicalPath
        } else {
            # Create website
            New-Website -Name $SiteName `
                -PhysicalPath $physicalPath `
                -ApplicationPool $AppPoolName `
                -Port 80 `
                -HostHeader $Domain | Out-Null
        }
        
        # Set App Pool
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
        
        # Start website
        Start-Website -Name $SiteName
        
        Write-Success "Website $SiteName configured and started"
        Write-Success "Binding: http://$Domain:80"
    }
    catch {
        Write-Error "Failed to create Website: $_"
        exit 1
    }
}

# ============================================================================
# STEP 7: PUBLISH APPLICATION
# ============================================================================

function Publish-Application {
    Write-Host "`n📦 PUBLISHING APPLICATION..." -ForegroundColor Cyan
    
    $projectPath = Split-Path -Parent $PSScriptRoot
    $outputPath = "C:\inetpub\wwwroot\seifdigital\app"
    
    try {
        Write-Host "Publishing from: $projectPath"
        Write-Host "Publishing to: $outputPath"
        
        # Clean output
        if (Test-Path $outputPath) {
            Remove-Item $outputPath -Recurse -Force
        }
        
        # Publish
        cd $projectPath
        dotnet publish -c Release -o $outputPath --self-contained=false
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Application published successfully"
        } else {
            Write-Error "Application publish failed!"
            exit 1
        }
    }
    catch {
        Write-Error "Exception during publish: $_"
        exit 1
    }
}

# ============================================================================
# STEP 8: CREATE APPSETTINGS FOR PRODUCTION
# ============================================================================

function Create-ProductionConfig {
    Write-Host "`n⚙️  CREATING PRODUCTION CONFIGURATION..." -ForegroundColor Cyan
    
    $configPath = "C:\inetpub\wwwroot\seifdigital\config\appsettings.Production.json"
    
    # Generate a sample master key (WARNING: Change this in production!)
    $masterKeyBytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RngCryptoServiceProvider]::new()
    $rng.GetBytes($masterKeyBytes)
    $masterKey = [Convert]::ToBase64String($masterKeyBytes)
    
    $configContent = @"
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=$ServerName;Database=$DbName;User Id=$DbUser;Password=$DbPassword;TrustServerCertificate=True;Encrypt=True;"
  },
  
  "Crypto": {
    "MasterKeyBase64": "$masterKey"
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
      "Microsoft.AspNetCore": "Warning"
    }
  },

  "AllowedHosts": "$Domain"
}
"@

    try {
        Set-Content -Path $configPath -Value $configContent
        Write-Success "Production config created at: $configPath"
        Write-Warning "IMPORTANT: Copy appsettings.Production.json to: $outputPath\appsettings.Production.json"
        Write-Warning "Master Key (save securely): $masterKey"
    }
    catch {
        Write-Error "Failed to create config: $_"
        exit 1
    }
}

# ============================================================================
# STEP 9: RUN DATABASE MIGRATIONS
# ============================================================================

function Run-Migrations {
    Write-Host "`n🔄 RUNNING DATABASE MIGRATIONS..." -ForegroundColor Cyan
    
    $appPath = "C:\inetpub\wwwroot\seifdigital\app"
    
    try {
        cd $appPath
        
        # Apply migrations
        dotnet ef database update --configuration Release --verbose 2>&1 | Tee-Object -Variable migrationOutput
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Database migrations completed successfully"
        } else {
            Write-Warning "Migration completed with messages above - verify tables were created"
        }
    }
    catch {
        Write-Warning "Note: If migrations fail here, run manually on production server"
        Write-Host "Command: cd C:\inetpub\wwwroot\seifdigital\app && dotnet ef database update --configuration Release"
    }
}

# ============================================================================
# STEP 10: RESTART IIS
# ============================================================================

function Restart-IIS {
    Write-Host "`n🔄 RESTARTING IIS..." -ForegroundColor Cyan
    
    try {
        iisreset /restart
        Start-Sleep -Seconds 5
        Write-Success "IIS restarted successfully"
    }
    catch {
        Write-Error "Failed to restart IIS: $_"
        exit 1
    }
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

function Main {
    Clear-Host
    Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  SeifDigital Production Deployment Script             ║" -ForegroundColor Cyan
    Write-Host "║  .NET 8.0 | SQL Server | IIS                          ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    Write-Host "`n📋 CONFIGURATION:" -ForegroundColor Magenta
    Write-Host "  SQL Server: $ServerName"
    Write-Host "  Database: $DbName"
    Write-Host "  DB User: $DbUser"
    Write-Host "  App Pool: $AppPoolName"
    Write-Host "  Site Name: $SiteName"
    Write-Host "  Domain: $Domain"
    
    # Run checks
    Check-Admin
    Check-Prerequisites
    
    # Execute deployment steps
    Setup-Database
    Setup-Directories
    Setup-Permissions
    Setup-AppPool
    Setup-Website
    Publish-Application
    Create-ProductionConfig
    Run-Migrations
    Restart-IIS
    
    # Final summary
    Write-Host "`n╔════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  ✅ DEPLOYMENT COMPLETED SUCCESSFULLY!                 ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Green
    
    Write-Host "`n📌 POST-DEPLOYMENT STEPS:" -ForegroundColor Yellow
    Write-Host "  1. Copy appsettings.Production.json to: C:\inetpub\wwwroot\seifdigital\app\"
    Write-Host "  2. Test website: http://$Domain"
    Write-Host "  3. Configure SSL/HTTPS certificate"
    Write-Host "  4. Update firewall rules for ports 80 and 443"
    Write-Host "  5. Setup automated SQL backups"
    Write-Host "  6. Configure application monitoring/logging"
    Write-Host "`n"
}

# ============================================================================
# RUN
# ============================================================================

Main

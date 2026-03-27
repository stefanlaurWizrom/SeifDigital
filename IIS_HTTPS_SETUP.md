# 🌐 IIS & HTTPS Configuration Guide - SeifDigital

## 📋 TABLE OF CONTENTS
1. [IIS Installation & Setup](#iis-installation)
2. [Website Configuration](#website-configuration)
3. [SSL/HTTPS Setup](#ssl-https-setup)
4. [Performance Optimization](#performance-optimization)
5. [Monitoring & Troubleshooting](#monitoring)
6. [Advanced Configuration](#advanced-configuration)

---

## 🔧 IIS Installation <a name="iis-installation"></a>

### 1.1 Windows Features Installation

**PowerShell (Administrator)**:

```powershell
# Install IIS with ASP.NET Core support
Install-WindowsFeature -Name Web-Server, `
    Web-Static-Content, `
    Web-Default-Doc, `
    Web-Dir-Browsing, `
    Web-Http-Errors, `
    Web-Http-Logging, `
    Web-Request-Monitor, `
    Web-Stat-Compression, `
    Web-Dyn-Compression, `
    Web-Mgmt-Tools, `
    Web-Mgmt-Service, `
    Web-Asp-Net45, `
    Web-AppInit, `
    Web-Url-Auth, `
    Web-Windows-Auth `
    -IncludeManagementTools

# Verify installation
Get-WindowsFeature Web-Server
```

### 1.2 Install .NET 8 Hosting Bundle

**Download & Run**:
1. Download from: https://dotnet.microsoft.com/download/dotnet/8.0
2. Choose: "Hosting Bundle (x64)"
3. Run installer with Administrator privileges
4. **CRITICAL**: Restart IIS after installation

```powershell
# Restart IIS
iisreset /restart

# Verify installation
dotnet --version
```

### 1.3 Verify ASP.NET Core Module

```powershell
# Check if ANCM is installed correctly
Get-Content "C:\Windows\System32\inetsrv\config\applicationHost.config" | Select-String "AspNetCoreModule"

# If above returns empty, IIS module isn't properly registered
# Solution: Reinstall .NET Hosting Bundle
```

---

## 🌐 Website Configuration <a name="website-configuration"></a>

### 2.1 Create Application Pool (PowerShell)

```powershell
Import-Module WebAdministration

# Create App Pool
New-WebAppPool -Name "seifdigital" | Out-Null

# Configure App Pool
$appPool = Get-Item "IIS:\AppPools\seifdigital"

# Set Identity to ApplicationPoolIdentity
$appPool.ProcessModel.IdentityType = "ApplicationPoolIdentity"

# .NET Core runtime (empty = managed core)
$appPool.ManagedRuntimeVersion = ""

# Disable 32-bit (x64 only)
$appPool.Enable32BitAppOn64bit = $false

# Max worker processes
$appPool.ProcessModel.MaxProcesses = 4

# Idle timeout (20 minutes)
$appPool.ProcessModel.IdleTimeout = [TimeSpan]::FromMinutes(20)

# Regular recycle to prevent memory leaks
$appPool.Recycling.PeriodicRestart.Time = [TimeSpan]::FromMinutes(1440)  # 24 hours

# Startup time limit
$appPool.ProcessModel.StartupTimeLimit = 90

# Shutdown time limit
$appPool.ProcessModel.ShutdownTimeLimit = 90

# Save changes
$appPool | Set-Item

# Start the pool
Start-WebAppPool -Name "seifdigital"

Write-Host "App Pool 'seifdigital' created successfully"
```

### 2.2 Create Website (PowerShell)

```powershell
Import-Module WebAdministration

# Variables
$siteName = "SeifDigital"
$appPoolName = "seifdigital"
$physicalPath = "C:\inetpub\wwwroot\seifdigital\app"
$hostHeader = "seifdigital.yourdomain.com"
$port = 80

# Create website
New-Website -Name $siteName `
    -PhysicalPath $physicalPath `
    -ApplicationPool $appPoolName `
    -Port $port `
    -HostHeader $hostHeader `
    -Force

# Start website
Start-Website -Name $siteName

# Verify
Get-Website -Name $siteName | Format-List

Write-Host "Website '$siteName' created successfully"
```

### 2.3 Website Settings (IIS Manager GUI)

1. Open **IIS Manager**
2. Navigate to Sites > SeifDigital
3. Configure:

#### Basic Settings
- **Physical Path**: C:\inetpub\wwwroot\seifdigital\app
- **Application Pool**: seifdigital

#### Bindings
- **HTTP**: *:80 (seifdigital.yourdomain.com)
- **HTTPS**: *:443 (seifdigital.yourdomain.com)

#### Handler Mappings
- Check if `aspNetCore` module is present
- If missing, it's likely your Hosting Bundle install failed

#### MIME Types
- Ensure `.js`, `.css`, `.json` are configured

---

## 🔒 SSL/HTTPS Setup <a name="ssl-https-setup"></a>

### 3.1 Get SSL Certificate

#### Option A: Let's Encrypt (FREE - Recommended)

**Using Certbot**:
```powershell
# Install Certbot on Windows
# Download: https://certbot.eff.org/

# Generate certificate
certbot certonly --standalone -d seifdigital.yourdomain.com

# Certificates stored in: C:\Certbot\live\seifdigital.yourdomain.com\
```

**Using ACME.NET**:
```powershell
# Alternative: Use ACMESharp PowerShell module
Install-Module -Name ACMESharp

# Initialize ACME environment
Initialize-ACMEVault

# Create certificate
New-ACMEIdentifier -Dns seifdigital.yourdomain.com -Alias seifdigital

# Challenge and validate
Complete-ACMEChallenge seifdigital -ChallengeType http-01
Submit-ACMEChallenge seifdigital -ChallengeType http-01

# Issue certificate
Update-ACMEIdentifier seifdigital
New-ACMECertificate seifdigital -Generate -Alias seifdigital
Submit-ACMECertificate seifdigital
Update-ACMECertificate seifdigital

# Get certificate
Get-ACMECertificate seifdigital -ExportPkcs12 -OutputPath "C:\certificates\seifdigital.pfx"
```

#### Option B: Commercial Certificate

1. Generate CSR (Certificate Signing Request):
```powershell
# Using IIS Manager: Server Certificates > Create Certificate Request
# Fill in domain details, choose 2048 or 4096 bits
```

2. Submit to CA (GoDaddy, Sectigo, DigiCert, etc.)
3. Receive certificate files
4. Combine into .pfx

### 3.2 Import Certificate to Windows

```powershell
# Variables
$certPath = "C:\certificates\seifdigital.yourdomain.com.pfx"
$certPassword = "YourCertificatePassword"
$storeName = "My"
$storeLocation = "LocalMachine"

# Import certificate
$securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
Import-PfxCertificate -FilePath $certPath `
    -CertStoreLocation "Cert:\$storeLocation\$storeName" `
    -Password $securePassword

# Get thumbprint (you'll need this for IIS binding)
$cert = Get-ChildItem -Path "Cert:\$storeLocation\$storeName" | `
    Where-Object {$_.Subject -like "*seifdigital*"}

Write-Host "Certificate imported successfully"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Subject: $($cert.Subject)"
Write-Host "Expiration: $($cert.NotAfter)"
```

### 3.3 Create HTTPS Binding in IIS

```powershell
Import-Module WebAdministration

# Variables
$siteName = "SeifDigital"
$hostHeader = "seifdigital.yourdomain.com"
$port = 443
$thumbprint = "YOUR_CERTIFICATE_THUMBPRINT"

# Get certificate object
$cert = Get-ChildItem -Path Cert:\LocalMachine\My | `
    Where-Object {$_.Thumbprint -eq $thumbprint}

if ($cert) {
    # Add HTTPS binding
    New-WebBinding -Name $siteName `
        -Protocol https `
        -Port $port `
        -HostHeader $hostHeader `
        -CertificateThumbprint $thumbprint `
        -CertificateStoreName My `
        -SslFlags 0
    
    Write-Host "HTTPS binding created successfully"
} else {
    Write-Host "Certificate not found!"
}
```

**GUI Alternative** (IIS Manager):
1. Sites > SeifDigital > Bindings > Add
2. **Type**: https
3. **IP**: All Unassigned
4. **Port**: 443
5. **Host name**: seifdigital.yourdomain.com
6. **SSL certificate**: Select your certificate

### 3.4 HTTP to HTTPS Redirect

#### Option A: URL Rewrite Module

```powershell
# Install URL Rewrite module
# Download: https://www.iis.net/downloads/microsoft/url-rewrite

# Or via Web Platform Installer
```

Create `web.config` in application root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="Redirect to HTTPS" stopProcessing="true">
          <match url="(.*)" />
          <conditions logicalGrouping="MatchAll">
            <add input="{HTTPS}" pattern="^OFF$" />
          </conditions>
          <action type="Redirect" 
                  url="https://{HTTP_HOST}/{R:1}" 
                  redirectType="Permanent" />
        </rule>
        
        <!-- Remove server header for security -->
        <rule name="Remove Server Header" stopProcessing="true">
          <match url=".*" />
          <action type="None" />
        </rule>
      </rules>
      
      <!-- Compression settings -->
      <outboundRules>
        <rule name="Add Strict-Transport-Security" patternSyntax="Wildcard">
          <match filterByTags="Vary" pattern="*" />
          <action type="Rewrite" value="Strict-Transport-Security: max-age=31536000; includeSubDomains" />
        </rule>
      </outboundRules>
    </rewrite>
    
    <!-- Security headers -->
    <httpProtocol>
      <customHeaders>
        <add name="X-Frame-Options" value="DENY" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <add name="X-XSS-Protection" value="1; mode=block" />
        <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
```

#### Option B: ASP.NET Core Middleware

In `Program.cs`:
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseHsts();
```

---

## ⚡ Performance Optimization <a name="performance-optimization"></a>

### 4.1 Enable Compression

**PowerShell**:
```powershell
Import-Module WebAdministration

# Enable static compression
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST" `
    -filter "system.webServer/httpCompression/staticCompression" `
    -name "enabled" `
    -value $true

# Enable dynamic compression
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST" `
    -filter "system.webServer/httpCompression/dynamicCompression" `
    -name "enabled" `
    -value $true

# Set compression level (0-10, higher = better compression, slower)
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST" `
    -filter "system.webServer/httpCompression/dynamicCompression" `
    -name "compressionLevel" `
    -value 9

Write-Host "Compression enabled"
```

### 4.2 Configure Output Caching

In `web.config`:
```xml
<system.webServer>
  <caching>
    <!-- Cache static content for 1 day -->
    <staticContent>
      <clientCache cacheControlMode="UseExpires" httpExpires="Sun, 19 Nov 2023 05:00:00 GMT" />
    </staticContent>
    
    <!-- Cache headers -->
    <profiles>
      <add extension=".js" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
      <add extension=".css" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
      <add extension=".gif" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
      <add extension=".jpg" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
      <add extension=".png" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
      <add extension=".ico" policy="CacheUntilChange" varyByHeaders="Accept-Encoding" />
    </profiles>
  </caching>
</system.webServer>
```

### 4.3 Request Queue Limit

```powershell
Import-Module WebAdministration

# Set queue length for application pool
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST" `
    -filter "system.applicationHost/applicationPool/add[@name='seifdigital']/queueLength" `
    -value 1000

Write-Host "Request queue limit increased to 1000"
```

### 4.4 Connection Timeout Settings

```xml
<!-- In web.config or via IIS UI -->
<system.webServer>
  <asp>
    <limits
      scriptTimeout="900"
      processorThreadMax="25"
      requestQueueMax="10000" />
  </asp>
</system.webServer>
```

---

## 📊 Monitoring & Troubleshooting <a name="monitoring"></a>

### 5.1 Enable Detailed Logging

**PowerShell**:
```powershell
Import-Module WebAdministration

$siteName = "SeifDigital"
$logPath = "C:\inetpub\logs\LogFiles\W3SVC" # IIS finds next number

# Enable HTTP logging
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST/$siteName" `
    -filter "system.webServer/httpLogging" `
    -name "enabled" `
    -value $true

# Enable Failed Request Tracing
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST/$siteName" `
    -filter "system.webServer/tracing/traceFailedRequests" `
    -name "enabled" `
    -value $true

Write-Host "Logging enabled for $siteName"
```

### 5.2 Enable FREB (Failed Request Tracing)

1. **IIS Manager** → Sites → SeifDigital
2. **Failed Request Tracing Rules**
3. **Add Rule**:
   - Trace what: `*.aspx`, `*.cshtml`
   - Trace status codes: `200-599`
   - Verbosity: `Verbose`
   - Time taken: `> 5000` ms

**PowerShell Setup**:
```powershell
Install-WindowsFeature Web-Http-Tracing

# Enable for site
Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST/SeifDigital" `
    -filter "system.webServer/tracing/traceFailedRequests/@enabled" `
    -value $true

# View FREB logs: C:\inetpub\logs\FailedReqLogFiles\
```

### 5.3 Monitor Application Pool

```powershell
Import-Module WebAdministration

# Get App Pool details
$appPool = Get-Item "IIS:\AppPools\seifdigital"

Write-Host "App Pool Status: $(Get-WebAppPoolState -Name 'seifdigital' | Select-Object -ExpandProperty 'Value')"
Write-Host "Recycling Events:"
Get-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST" `
    -filter "system.applicationHost/applicationPool/add[@name='seifdigital']/recycling" `
    | Select-Object -ExpandProperty Value

# Get process ID
$appPoolState = Get-WebAppPoolState -Name seifdigital
Write-Host "Process ID: $($appPoolState.ProcessId)"

# Check memory usage
if ($appPoolState.ProcessId) {
    Get-Process -Id $appPoolState.ProcessId | Format-Table Name, WorkingSet, ProcessorTime
}
```

### 5.4 View IIS Logs

```powershell
# IIS logs location
$logPath = "C:\inetpub\logs\LogFiles\W3SVC1"

# Get latest 100 log entries
Get-Content "$logPath\u_ex*.log" | Select-Object -Last 100 | `
    Where-Object {$_ -notmatch "^#"} | `
    Format-Table -AutoSize

# Find errors (500 status)
Get-Content "$logPath\u_ex*.log" | `
    Where-Object {$_ -match "500"} | `
    Select-Object -Last 10
```

### 5.5 Common Issues & Solutions

#### Issue: "Unable to load AspNetCoreModule"

**Solution**:
```powershell
# 1. Verify installation
Get-WindowsFeature Web-Asp-Net45

# 2. Reinstall .NET Hosting Bundle
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# 3. Repair IIS
iisreset /stop
iisreset /start

# 4. Check web.config
Get-Content "C:\inetpub\wwwroot\seifdigital\app\web.config"
```

#### Issue: "502 Bad Gateway"

**Causes**:
- Application crash
- Connection string error
- Database unavailable
- Permission denied

**Debug**:
```powershell
# Check application logs
Get-Content "C:\inetpub\wwwroot\seifdigital\app\bin\Release\net8.0\app_log.txt" -Tail 50

# Test database connection
sqlcmd -S YOUR_SERVER -U seifapp -P YOUR_PASSWORD -Q "SELECT @@VERSION"

# Restart app pool
Restart-WebAppPool -Name seifdigital
```

---

## 🔐 Advanced Configuration <a name="advanced-configuration"></a>

### 6.1 Security Headers

Add to `web.config`:
```xml
<httpProtocol>
  <customHeaders>
    <add name="X-Frame-Options" value="SAMEORIGIN" />
    <add name="X-Content-Type-Options" value="nosniff" />
    <add name="X-XSS-Protection" value="1; mode=block" />
    <add name="Referrer-Policy" value="strict-origin-when-cross-origin" />
    <add name="Permissions-Policy" value="geolocation=(), microphone=(), camera=(), payment=()" />
    <add name="Strict-Transport-Security" value="max-age=31536000; includeSubDomains" />
    <add name="Content-Security-Policy" value="default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';" />
  </customHeaders>
</httpProtocol>
```

### 6.2 Disable Directory Browsing

```powershell
Import-Module WebAdministration

Set-WebConfigurationProperty -pspath "MACHINE/WEBROOT/APPHOST/SeifDigital" `
    -filter "system.webServer/directoryBrowse" `
    -name "enabled" `
    -value $false

Write-Host "Directory browsing disabled"
```

### 6.3 Configure IP Restrictions

```powershell
# Allow only specific IPs to access admin section
# IIS Manager → SeifDigital → IP Address and Domain Restrictions
# Or via configuration:

$ipRestrictions = @(
    "192.168.1.0/24",    # Your office network
    "10.0.0.0/8"         # Internal network
)
```

### 6.4 Application Insights Integration

Add to `appsettings.json`:
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "YOUR_KEY_HERE"
  }
}
```

---

## 📝 Complete web.config Template

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <!-- ASP.NET Core Handler -->
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    
    <!-- ASP.NET Core Module Configuration -->
    <aspNetCore processPath=".\SeifDigital.exe" 
                stdoutLogEnabled="false" 
                stdoutLogFile=".\logs\stdout"
                hostingModel="inprocess" />
    
    <!-- Rewrite Rules -->
    <rewrite>
      <rules>
        <rule name="HTTP to HTTPS" stopProcessing="true">
          <match url="(.*)" />
          <conditions logicalGrouping="MatchAll">
            <add input="{HTTPS}" pattern="^OFF$" />
          </conditions>
          <action type="Redirect" url="https://{HTTP_HOST}/{R:1}" redirectType="Permanent" />
        </rule>
      </rules>
    </rewrite>
    
    <!-- Compression -->
    <httpCompression>
      <staticCompression enabled="true" />
      <dynamicCompression enabled="true" />
    </httpCompression>
    
    <!-- Security Headers -->
    <httpProtocol>
      <customHeaders>
        <add name="X-Frame-Options" value="SAMEORIGIN" />
        <add name="X-Content-Type-Options" value="nosniff" />
        <add name="X-XSS-Protection" value="1; mode=block" />
        <add name="Strict-Transport-Security" value="max-age=31536000; includeSubDomains" />
      </customHeaders>
    </httpProtocol>
    
    <!-- Caching -->
    <caching>
      <staticContent>
        <clientCache cacheControlMode="UseExpires" httpExpires="Sun, 19 Nov 2025 05:00:00 GMT" />
      </staticContent>
    </caching>
    
    <!-- Other Settings -->
    <directoryBrowse enabled="false" />
    <requestFiltering>
      <requestLimits maxUrl="4096" maxQueryString="2048" />
    </requestFiltering>
  </system.webServer>
</configuration>
```

---

**Last Updated**: 2025
**For**: SeifDigital Application v1.0 (.NET 8.0)

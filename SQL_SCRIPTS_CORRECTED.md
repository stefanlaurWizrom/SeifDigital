✅ SQL SCRIPTS - CORRECTED FOR ACTUAL DATABASE SCHEMA
======================================================

## Database Information (From Your Screenshot)

**Database Name**: `SelfDate`
**Table Name**: `dbo.AppSettings`

### Column Structure:
```
Column Name    | Data Type        | Nullable | Notes
─────────────────────────────────────────────────────
Key           | nvarchar(128)    | NOT NULL | Primary Key
Value         | nvarchar(1024)   | NULL     | Setting value
UpdatedUtc    | datetime2(3)     | NOT NULL | Last update timestamp
ValueString   | nvarchar(2000)   | NULL     | Description/notes
```

### Current Data in AppSettings:
```
Key                          | Value                    | ValueString
─────────────────────────────────────────────────────────────────────
AuditRetentionDays          | 8                        | NULL
CertAlertDaysThreshold      | 15                       | NULL
CertAlertEmail              | tehnic@wizrom.ro         | NULL
CertVerificationHour        | 8                        | NULL
CertVerificationTimezone    | Europe/Bucharest         | NULL
DbName                      | ToyotaTest2\WIZPROTEST  | NULL
DbPassword                  | CfDJBP_YsnP7BkJu30qQrl4... | NULL
DbPort                      | 50137                    | NULL
DbUser                      | sa                       | NULL
FileCategories              | NULL                     | NULL
...                         | ...                      | NULL
```

---

## SQL Scripts - CORRECTED VERSION

### Script 1: Development Environment

**File**: `SeifDigital/sql/AddCertVerificationMinute_Dev.sql`

```sql
-- Add CertVerificationMinute setting (Development)
-- Script to add minute-level scheduling support for certificate verification
-- Schema: SelfDate.dbo.AppSettings with columns: Key, Value, UpdatedUtc, ValueString

SET NOCOUNT ON;

-- Check if setting already exists
IF NOT EXISTS (SELECT 1 FROM [SelfDate].dbo.AppSettings WHERE [Key] = 'CertVerificationMinute')
BEGIN
    INSERT INTO [SelfDate].dbo.AppSettings ([Key], [Value], UpdatedUtc, ValueString)
    VALUES (
        'CertVerificationMinute',
        '0',
        GETUTCDATE(),
        'Minuta pentru verificarea certificatelor (0-59). Default: 0 (la inceput de ora). Ex: 0 = :00, 30 = :30'
    );
    
    PRINT '✅ Added CertVerificationMinute setting to Development database (SelfDate)';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationMinute setting already exists in Development database';
END

-- Verify the setting was created/exists
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SelfDate].dbo.AppSettings 
WHERE [Key] = 'CertVerificationMinute';
```

### Script 2: LIVE/Production Environment

**File**: `SeifDigital/sql/AddCertVerificationMinute_Live.sql`

```sql
-- Add CertVerificationMinute setting (LIVE/Production)
-- Script to add minute-level scheduling support for certificate verification
-- Schema: SelfDate.dbo.AppSettings with columns: Key, Value, UpdatedUtc, ValueString

SET NOCOUNT ON;

-- Check if setting already exists
IF NOT EXISTS (SELECT 1 FROM [SelfDate].dbo.AppSettings WHERE [Key] = 'CertVerificationMinute')
BEGIN
    INSERT INTO [SelfDate].dbo.AppSettings ([Key], [Value], UpdatedUtc, ValueString)
    VALUES (
        'CertVerificationMinute',
        '0',
        GETUTCDATE(),
        'Minuta pentru verificarea certificatelor (0-59). Default: 0 (la inceput de ora). Ex: 0 = :00, 30 = :30'
    );
    
    PRINT '✅ Added CertVerificationMinute setting to LIVE database (SelfDate)';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationMinute setting already exists in LIVE database';
END

-- Verify the setting was created/exists
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SelfDate].dbo.AppSettings 
WHERE [Key] = 'CertVerificationMinute';
```

---

## How to Execute

### Option 1: SQL Server Management Studio
1. Open SSMS
2. Connect to your SQL Server instance
3. Open the script file
4. Click **Execute** (or F5)
5. Check output for `✅ Added...` message

### Option 2: PowerShell
```powershell
sqlcmd -S "YOUR_SERVER" -d "SelfDate" -i "SeifDigital\sql\AddCertVerificationMinute_Dev.sql"
```

### Option 3: Visual Studio
1. Tools > SQL Server > New Query...
2. Paste script content
3. Execute

---

## What Gets Added

After running the script, your `dbo.AppSettings` table will have a new row:

```
Key                    | Value | UpdatedUtc           | ValueString
───────────────────────────────────────────────────────────────────
CertVerificationMinute | 0     | 2026-05-19 09:30:45  | Minuta pentru verificarea...
```

---

## Column Mapping

The script correctly maps to your database schema:

| Our Code | Script Column | Database Column |
|----------|---------------|-----------------|
| Key name | `'CertVerificationMinute'` | `[Key]` |
| Value | `'0'` | `[Value]` |
| Timestamp | `GETUTCDATE()` | `UpdatedUtc` |
| Description | `'Minuta pentru...'` | `ValueString` |

---

## Verification Query

After running the script, verify it was created with:

```sql
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SelfDate].dbo.AppSettings 
WHERE [Key] = 'CertVerificationMinute';
```

Expected result:
```
Key                    | Value | UpdatedUtc           | ValueString
───────────────────────────────────────────────────────────────────
CertVerificationMinute | 0     | [current datetime]   | Minuta pentru verificarea certificatelor (0-59)...
```

---

## Rollback (If Needed)

To remove the setting:
```sql
DELETE FROM [SelfDate].dbo.AppSettings 
WHERE [Key] = 'CertVerificationMinute';
```

---

## Next Steps

1. ✅ Run `AddCertVerificationMinute_Dev.sql` on Development database
2. ✅ Run `AddCertVerificationMinute_Live.sql` on LIVE database
3. ✅ Restart your application
4. ✅ Open Certificate Settings page
5. ✅ Set Hour and Minute values
6. ✅ Verify Hangfire job runs at the scheduled time

---

## Notes

- **Database Name**: `SelfDate` (not `SeifDigital_Dev` or `SeifDigital`)
- **Column Names**: `Key`, `Value`, `UpdatedUtc`, `ValueString` (not `SettingKey`, `SettingValue`, etc.)
- **Default Minute**: 0 (top of hour, e.g., 08:00 becomes 08:00, 14:30 becomes 14:30)
- **Timestamp**: Automatically set to current UTC time using `GETUTCDATE()`

✅ Scripts are now correctly formatted for your actual database schema!

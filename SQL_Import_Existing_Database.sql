-- ============================================================================
-- SeifDigital - Import Bază de Date Existentă pe Server Nou
-- ============================================================================
-- INSTRUCȚIUNI:
-- 1. Pe serverul DEV: BACKUP DATABASE [SeifDate]
-- 2. Copiază fișierul .bak pe serverul PROD
-- 3. Execută acest script pe serverul PROD
-- ============================================================================

USE [master];
GO

-- ============================================================================
-- PASUL 1: VERIFICA BACKUPUL
-- ============================================================================

-- Verifică dacă fișierul de backup există
DECLARE @BackupFile NVARCHAR(MAX) = N'D:\SQLBackups\SeifDate_etalon.bak';

-- Listează informații din backup (execută asta prima oară ca să vezi)
-- RESTORE FILELISTONLY FROM DISK = @BackupFile;

PRINT 'Step 1: Backup file verified at ' + @BackupFile;
GO

-- ============================================================================
-- PASUL 2: ȘTERGE BAZA VECHE (dacă există)
-- ============================================================================

PRINT 'Step 2: Cleaning up old database (if exists)...';

IF EXISTS (SELECT * FROM sys.databases WHERE name = 'SeifDate')
BEGIN
    -- Închide toate conexiunile
    ALTER DATABASE [SeifDate] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    
    -- Așteaptă puțin
    WAITFOR DELAY '00:00:02';
    
    -- Șterge baza
    DROP DATABASE [SeifDate];
    
    PRINT 'Old SeifDate database dropped.';
END
ELSE
BEGIN
    PRINT 'No existing SeifDate database found.';
END
GO

-- ============================================================================
-- PASUL 3: RESTORE BAZA DIN BACKUP
-- ============================================================================

PRINT 'Step 3: Restoring database from backup...';

RESTORE DATABASE [SeifDate]
FROM DISK = N'D:\SQLBackups\SeifDate_etalon.bak'
WITH 
    MOVE 'SeifDate' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate.mdf',
    MOVE 'SeifDate_log' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate_log.ldf',
    REPLACE,
    RECOVERY,
    STATS = 10;

PRINT 'Database restored successfully!';
GO

-- ============================================================================
-- PASUL 4: CONFIGURARE POST-RESTORE
-- ============================================================================

USE [SeifDate];
GO

PRINT 'Step 4: Configuring database for production...';

-- Set Recovery Mode la FULL
ALTER DATABASE [SeifDate] SET RECOVERY FULL;
PRINT 'Recovery model set to FULL.';

-- Disable AUTO_SHRINK
ALTER DATABASE [SeifDate] SET AUTO_SHRINK OFF;
PRINT 'AUTO_SHRINK disabled.';

-- Set Compatibility Level
ALTER DATABASE [SeifDate] SET COMPATIBILITY_LEVEL = 160;
PRINT 'Compatibility level set to 160 (SQL Server 2022).';

-- Enable Query Store
ALTER DATABASE [SeifDate] SET QUERY_STORE = ON;
PRINT 'Query Store enabled.';

GO

-- ============================================================================
-- PASUL 5: SETARE LOGIN ȘI PERMISIUNI
-- ============================================================================

USE [master];
GO

PRINT 'Step 5: Setting up login and permissions...';

-- Crează login dacă nu există
IF NOT EXISTS (SELECT * FROM sys.syslogins WHERE name = 'seifapp')
BEGIN
    CREATE LOGIN [seifapp] WITH PASSWORD = N'TrebuieSaFiiParolaFORTE@123#SeifDigital';
    ALTER LOGIN [seifapp] ENABLE;
    PRINT 'Login seifapp created.';
END
ELSE
BEGIN
    PRINT 'Login seifapp already exists.';
END
GO

-- Crează user în baza de date
USE [SeifDate];
GO

IF NOT EXISTS (SELECT * FROM sys.sysusers WHERE name = 'seifapp')
BEGIN
    CREATE USER [seifapp] FOR LOGIN [seifapp];
    ALTER ROLE [db_owner] ADD MEMBER [seifapp];
    PRINT 'User seifapp created and granted db_owner role.';
END
ELSE
BEGIN
    PRINT 'User seifapp already exists.';
    -- Asigură că are rolul potrivit
    ALTER ROLE [db_owner] ADD MEMBER [seifapp];
    PRINT 'User seifapp role confirmed.';
END
GO

-- ============================================================================
-- PASUL 6: VERIFICARE INTEGRITATE
-- ============================================================================

PRINT 'Step 6: Verifying database integrity...';

USE [SeifDate];
GO

-- Verifică care tabel sunt în baza
SELECT 'Tables in Database' AS [Check], COUNT(*) AS [Count]
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo';

-- Listează tabelele
SELECT 'Table Name' AS [Type], TABLE_NAME AS [Name]
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME;

-- Verifică statistici tabele
SELECT 
    OBJECT_NAME(p.object_id) AS [TableName],
    SUM(p.rows) AS [RowCount]
FROM sys.partitions p
WHERE p.index_id IN (0, 1) 
    AND OBJECTPROPERTY(p.object_id, 'IsUserTable') = 1
GROUP BY p.object_id
ORDER BY [TableName];

GO

-- ============================================================================
-- PASUL 7: CLEANUP OPȚIONAL (ȘTERGE DATE SENSIBILE)
-- ============================================================================

-- UNCOMMENT dacă vrei să ștergi anumite date din dev/test
/*
USE [SeifDate];
GO

PRINT 'Step 7: Cleaning up sensitive data...';

-- Șterge utilizatori de test
DELETE FROM [dbo].[UserAccounts] 
WHERE [Username] IN ('testuser', 'admin', 'dev');

-- Șterge alte date sensibile (după cum e necesar)
-- DELETE FROM [dbo].[InformatiiSensibile];
-- DELETE FROM [dbo].[AuditLog];

PRINT 'Sensitive data cleaned.';
GO
*/

-- ============================================================================
-- PASUL 8: FINAL VERIFICATION
-- ============================================================================

USE [master];
GO

PRINT '
╔═══════════════════════════════════════════════════════════════╗
║  SeifDigital Database Import Complete!                       ║
║  ✓ Database restored from backup                             ║
║  ✓ Login and permissions configured                          ║
║  ✓ Database ready for application                            ║
╚═══════════════════════════════════════════════════════════════╝

NEXT STEPS:
1. Verify data integrity (if needed)
2. Test application connectivity
3. Monitor backups going forward
4. Archive old development database
5. Update connection string in appsettings.Production.json

If anything went wrong:
- Check if .bak file path is correct
- Verify SQL Server service is running
- Check SQL Server error logs for details
- Ensure you have enough disk space
';

-- Final Status
SELECT 
    'Database Status' AS [Check],
    name AS [DatabaseName],
    state_desc AS [State],
    recovery_model_desc AS [RecoveryModel],
    user_access_desc AS [UserAccess]
FROM sys.databases
WHERE name = 'SeifDate';

GO

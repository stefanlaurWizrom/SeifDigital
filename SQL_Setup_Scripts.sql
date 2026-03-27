-- ============================================================================
-- SeifDigital - SQL Server Setup Scripts
-- ============================================================================
-- Run these scripts in SQL Server Management Studio (SSMS)
-- Execute as: Authenticated user with Create Database privilege
-- ============================================================================

-- ============================================================================
-- SCRIPT 1: CREATE DATABASE AND CONFIGURE
-- ============================================================================

USE [master];
GO

-- Create Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SeifDate')
BEGIN
    CREATE DATABASE [SeifDate]
    CONTAINMENT = NONE
    ON PRIMARY
    (
        NAME = N'SeifDate',
        FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate.mdf',
        SIZE = 100MB,
        FILEGROWTH = 10%
    )
    LOG ON
    (
        NAME = N'SeifDate_log',
        FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate_log.ldf',
        SIZE = 50MB,
        FILEGROWTH = 10%
    );
    
    PRINT 'Database SeifDate created successfully.';
END
ELSE
BEGIN
    PRINT 'Database SeifDate already exists.';
END
GO

-- ============================================================================
-- SCRIPT 2: CREATE SQL LOGIN FOR APPLICATION
-- ============================================================================

USE [master];
GO

-- Create Login with strong password
IF NOT EXISTS (SELECT * FROM sys.syslogins WHERE name = 'seifapp')
BEGIN
    CREATE LOGIN [seifapp] WITH PASSWORD = N'TrebuieSaFiiParolaFORTE@123#SeifDigital';
    ALTER LOGIN [seifapp] ENABLE;
    PRINT 'Login seifapp created successfully.';
END
ELSE
BEGIN
    PRINT 'Login seifapp already exists.';
    -- Uncomment to reset password:
    -- ALTER LOGIN [seifapp] WITH PASSWORD = N'TrebuieSaFiiParolaFORTE@123#SeifDigital';
END
GO

-- ============================================================================
-- SCRIPT 3: CREATE USER IN DATABASE AND GRANT PERMISSIONS
-- ============================================================================

USE [SeifDate];
GO

-- Create User for Login
IF NOT EXISTS (SELECT * FROM sys.sysusers WHERE name = 'seifapp')
BEGIN
    CREATE USER [seifapp] FOR LOGIN [seifapp];
    PRINT 'User seifapp created in SeifDate database.';
END
ELSE
BEGIN
    PRINT 'User seifapp already exists in SeifDate database.';
END
GO

-- Grant Permissions
-- Option 1: Full Database Owner (for initial setup)
ALTER ROLE [db_owner] ADD MEMBER [seifapp];
PRINT 'User seifapp granted db_owner role.';
GO

-- Option 2: More restrictive permissions (uncomment to use instead)
/*
GRANT CONNECT ON DATABASE::[SeifDate] TO [seifapp];
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [seifapp];
GRANT EXECUTE ON SCHEMA::dbo TO [seifapp];
GRANT CREATE TABLE, CREATE PROCEDURE, CREATE VIEW TO [seifapp];
PRINT 'User seifapp granted specific permissions.';
*/

-- ============================================================================
-- SCRIPT 4: VERIFY SETUP
-- ============================================================================

USE [SeifDate];
GO

-- Check Database exists
SELECT 'Database Status' AS [Check], name, compatibility_level, recovery_model_desc, state_desc
FROM sys.databases
WHERE name = 'SeifDate';

-- Check User exists
SELECT 'User Permissions' AS [Check], name, type_desc
FROM sys.sysusers
WHERE name = 'seifapp';

-- Check Login exists
SELECT 'Login Status' AS [Check], name, type, is_disabled, create_date
FROM sys.syslogins
WHERE name = 'seifapp';
GO

-- ============================================================================
-- SCRIPT 5: SET DATABASE OPTIONS FOR PRODUCTION
-- ============================================================================

USE [SeifDate];
GO

-- Set Recovery Mode to FULL (for point-in-time recovery)
ALTER DATABASE [SeifDate] SET RECOVERY FULL;
PRINT 'Recovery model set to FULL.';

-- Enable SQL Agent job to truncate transaction log
ALTER DATABASE [SeifDate] SET AUTO_SHRINK OFF;
PRINT 'AUTO_SHRINK disabled.';

-- Set compatibility level
ALTER DATABASE [SeifDate] SET COMPATIBILITY_LEVEL = 160;  -- SQL Server 2022
PRINT 'Compatibility level set to 160 (SQL 2022).';

-- Enable TRUSTWORTHY for any future needs
-- ALTER DATABASE [SeifDate] SET TRUSTWORTHY ON;

GO

-- ============================================================================
-- SCRIPT 6: CREATE BACKUP DIRECTORY AND BACKUP JOB
-- ============================================================================

-- Create backup directory (if not exists)
EXEC master.dbo.xp_create_subdir N'D:\SQLBackups';
GO

-- Full Database Backup (execute daily)
BACKUP DATABASE [SeifDate]
TO DISK = N'D:\SQLBackups\SeifDate_Full_$(DATE).bak'
WITH
    INIT,
    STATS = 10,
    COMPRESSION,
    NAME = N'SeifDate Full Backup',
    DESCRIPTION = N'Full backup of SeifDate database';
GO

-- Transaction Log Backup (execute every hour)
BACKUP LOG [SeifDate]
TO DISK = N'D:\SQLBackups\SeifDate_Log_$(DATE)_$(TIME).trn'
WITH
    INIT,
    STATS = 10,
    COMPRESSION,
    NAME = N'SeifDate Transaction Log Backup',
    DESCRIPTION = N'Transaction log backup of SeifDate';
GO

-- ============================================================================
-- SCRIPT 7: CREATE MAINTENANCE PLAN (OPTIONAL)
-- ============================================================================

-- Cleanup old backup files (older than 30 days)
DECLARE @DeleteDate DATETIME = DATEADD(DAY, -30, CAST(CONVERT(VARCHAR(10), GETDATE(), 120) AS DATETIME));

-- Execute cleanup (Windows command via xp_cmdshell)
-- EXEC sp_configure 'xp_cmdshell', 1;
-- RECONFIGURE;
-- EXEC xp_cmdshell 'forfiles /S /M SeifDate_*.bak /D +30 /C "cmd /c del @FILE"';

-- ============================================================================
-- SCRIPT 8: CREATE INDEXES FOR PERFORMANCE
-- ============================================================================

USE [SeifDate];
GO

-- This will be created by Entity Framework migrations
-- But you can add custom indexes here as needed

-- Example: Index on AuditLog for efficient querying
/*
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLog_EventTimeUtc' AND object_id = OBJECT_ID('[dbo].[AuditLog]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLog_EventTimeUtc]
    ON [dbo].[AuditLog] ([EventTimeUtc] DESC)
    INCLUDE ([EventType], [ActorUser])
    WITH (FILLFACTOR = 90);
    PRINT 'Index IX_AuditLog_EventTimeUtc created.';
END
GO
*/

-- ============================================================================
-- SCRIPT 9: ENABLE QUERY STATISTICS (OPTIONAL MONITORING)
-- ============================================================================

USE [SeifDate];
GO

-- Enable Query Store (SQL Server 2016+) for performance monitoring
ALTER DATABASE [SeifDate] SET QUERY_STORE = ON;
PRINT 'Query Store enabled.';

-- Set Query Store to automatic mode
ALTER DATABASE [SeifDate]
SET QUERY_STORE (
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (BASE_SIZE_MB = 100, STALE_QUERY_THRESHOLD_DAYS = 30),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    QUERY_CAPTURE_MODE = AUTO,
    MAX_STORAGE_SIZE_MB = 500
);
GO

-- ============================================================================
-- SCRIPT 10: CREATE TEST USER (OPTIONAL)
-- ============================================================================

USE [SeifDate];
GO

-- Insert a test user account
INSERT INTO [dbo].[UserAccounts] ([Username], [Email], [Created], [LastLogin])
VALUES 
    (N'testuser', N'test@wizrom.ro', GETUTCDATE(), NULL),
    (N'admin', N'admin@wizrom.ro', GETUTCDATE(), NULL);

PRINT 'Test users inserted (NOTE: Add passwords via application hash before using).';
GO

-- ============================================================================
-- SCRIPT 11: DISABLE XPPATH IF NOT NEEDED (SECURITY)
-- ============================================================================

EXEC sp_configure 'show advanced options', 1;
RECONFIGURE;
EXEC sp_configure 'xp_cmdshell', 0;
RECONFIGURE;
PRINT 'xp_cmdshell disabled for security.';
GO

-- ============================================================================
-- SCRIPT 12: CREATE BACKUP VERIFICATION JOB
-- ============================================================================

-- Restore latest backup to test environment (optional)
/*
RESTORE DATABASE [SeifDate_Test]
FROM DISK = N'D:\SQLBackups\SeifDate_Full_LATEST.bak'
WITH MOVE 'SeifDate' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate_Test.mdf',
     MOVE 'SeifDate_log' TO 'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\SeifDate_Test_log.ldf',
     REPLACE,
     RECOVERY;
*/

-- ============================================================================
-- SCRIPT 13: MONITORING - CHECK DATABASE HEALTH
-- ============================================================================

USE [SeifDate];
GO

-- Check database size
SELECT
    DB_NAME() AS [Database],
    CAST(SUM(size) * 8.0 / 1024 / 1024 AS DECIMAL(10, 2)) AS [Size_GB],
    CAST(SUM(CASE WHEN type = 0 THEN size ELSE 0 END) * 8.0 / 1024 / 1024 AS DECIMAL(10, 2)) AS [Data_GB],
    CAST(SUM(CASE WHEN type = 1 THEN size ELSE 0 END) * 8.0 / 1024 / 1024 AS DECIMAL(10, 2)) AS [Log_GB]
FROM sys.database_files;

-- Check table row counts
SELECT
    OBJECT_NAME(p.object_id) AS [Table],
    SUM(p.rows) AS [RowCount]
FROM sys.partitions p
WHERE p.index_id IN (0, 1) AND OBJECTPROPERTY(p.object_id, 'IsUserTable') = 1
GROUP BY p.object_id
ORDER BY [Table];

GO

-- ============================================================================
-- SCRIPT 14: CONNECTION TEST
-- ============================================================================

USE [SeifDate];
GO

-- Create test table
IF OBJECT_ID('dbo.ConnectionTest', 'U') IS NOT NULL
    DROP TABLE dbo.ConnectionTest;

CREATE TABLE [dbo].[ConnectionTest] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [TestTime] DATETIME2 DEFAULT GETUTCDATE(),
    [Message] NVARCHAR(100)
);

-- Insert test data
INSERT INTO [dbo].[ConnectionTest] ([Message])
VALUES (N'Connection test successful at ' + CONVERT(VARCHAR, GETUTCDATE(), 121));

-- Verify
SELECT * FROM [dbo].[ConnectionTest];

-- Cleanup
-- DROP TABLE dbo.ConnectionTest;

GO

-- ============================================================================
-- SCRIPT 15: CREATE MAINTENANCE VIEWS
-- ============================================================================

USE [SeifDate];
GO

-- View for monitoring slow queries
CREATE OR ALTER VIEW [dbo].[vw_SlowQueries] AS
SELECT TOP 10
    q.query_id,
    q.object_id,
    OBJECT_NAME(q.object_id) AS [ObjectName],
    rs.avg_elapsed_time / 1000 AS [AvgElapsedTime_ms],
    rs.execution_count,
    rs.last_execution_time
FROM sys.query_store_query q
INNER JOIN sys.query_store_runtime_stats rs ON q.query_id = rs.query_id
ORDER BY rs.avg_elapsed_time DESC;
GO

-- View for monitoring table sizes
CREATE OR ALTER VIEW [dbo].[vw_TableSizes] AS
SELECT
    OBJECT_NAME(i.object_id) AS [TableName],
    SUM(s.used_page_count) * 8 AS [UsedSpaceKB],
    SUM(s.total_page_count) * 8 AS [TotalSpaceKB]
FROM sys.dm_db_partition_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
GROUP BY i.object_id;
GO

-- ============================================================================
-- SCRIPT 16: FINAL VERIFICATION CHECKLIST
-- ============================================================================

-- Run this to verify everything is configured correctly
SELECT
    'Database Created' AS [Check],
    CASE WHEN (SELECT COUNT(*) FROM sys.databases WHERE name = 'SeifDate') > 0 THEN '✓' ELSE '✗' END AS [Status]
UNION ALL
SELECT
    'Login Created',
    CASE WHEN (SELECT COUNT(*) FROM sys.syslogins WHERE name = 'seifapp') > 0 THEN '✓' ELSE '✗' END
UNION ALL
SELECT
    'User Created',
    CASE WHEN (SELECT COUNT(*) FROM sys.sysusers WHERE name = 'seifapp') > 0 THEN '✓' ELSE '✗' END
UNION ALL
SELECT
    'Query Store Enabled',
    CASE WHEN (SELECT query_store_on FROM sys.databases WHERE name = 'SeifDate') = 1 THEN '✓' ELSE '✗' END
UNION ALL
SELECT
    'Recovery Model = FULL',
    CASE WHEN (SELECT recovery_model FROM sys.databases WHERE name = 'SeifDate') = 1 THEN '✓' ELSE '✗' END;

GO

PRINT '
╔═══════════════════════════════════════════════════════════════╗
║  SeifDigital SQL Server Setup Complete                       ║
║  ✓ Database created                                          ║
║  ✓ Login and user configured                                 ║
║  ✓ Permissions granted                                       ║
║  ✓ Query Store enabled                                       ║
║  ✓ Ready for application deployment                          ║
╚═══════════════════════════════════════════════════════════════╝

Next Steps:
1. Deploy application using Deploy-SeifDigital.ps1
2. Run Entity Framework migrations (dotnet ef database update)
3. Verify application can connect to database
4. Monitor Query Store for performance insights
5. Setup automated backups (optional scripts provided)
';

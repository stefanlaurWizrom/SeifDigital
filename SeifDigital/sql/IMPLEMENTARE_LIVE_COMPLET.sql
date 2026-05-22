-- ============================================================
-- IMPLEMENTARE CERTIFICATE VERIFICATION & AUTO-SCHEDULING
-- Database: SeifDate (LIVE)
-- Data: 2026-05-19
-- ============================================================
-- COPY-PASTE ȘI RULEAZĂ TOATĂ SCRIPTUL ACESTA LA RÂND

USE [SeifDate];
GO

PRINT '========================================='
PRINT 'CERTIFICATE VERIFICATION IMPLEMENTATION'
PRINT '========================================='

SET NOCOUNT ON;

-- ============================================================
-- PASUL 1: ADAUGĂ SETAREA PENTRU MINUTE
-- ============================================================
PRINT ''
PRINT 'PASUL 1: Adăugare CertVerificationMinute...'

IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationMinute')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES (
        'CertVerificationMinute',
        '0',
        GETUTCDATE(),
        'Minuta pentru verificarea certificatelor (0-59). Default: 0 (la inceput de ora). Ex: 0 = :00, 30 = :30'
    );
    
    PRINT '✅ CertVerificationMinute adăugat';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationMinute deja exista';
END

-- ============================================================
-- PASUL 2: VERIFICĂ ȘI ADAUGĂ CELELALTE SETĂRI (dacă lipsesc)
-- ============================================================
PRINT ''
PRINT 'PASUL 2: Verificare și adăugare setări certificate...'

-- CertVerificationHour
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationHour')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertVerificationHour', '8', GETUTCDATE(), 'Ora verificării (0-23)');
    PRINT '✅ CertVerificationHour adăugat';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationHour deja exista';
END

-- CertVerificationTimezone
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationTimezone')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertVerificationTimezone', 'Europe/Bucharest', GETUTCDATE(), 'Zona horara IANA');
    PRINT '✅ CertVerificationTimezone adăugat';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationTimezone deja exista';
END

-- CertAlertDaysThreshold
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertAlertDaysThreshold')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertAlertDaysThreshold', '15', GETUTCDATE(), 'Prag alerta (zile)');
    PRINT '✅ CertAlertDaysThreshold adăugat';
END
ELSE
BEGIN
    PRINT '⚠️ CertAlertDaysThreshold deja exista';
END

-- CertAlertEmail
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertAlertEmail')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertAlertEmail', 'admin@wizrom.ro', GETUTCDATE(), 'Email alerte');
    PRINT '✅ CertAlertEmail adăugat';
END
ELSE
BEGIN
    PRINT '⚠️ CertAlertEmail deja exista';
END

-- ============================================================
-- PASUL 3: CREAZĂ TABELĂ CertificateAlertLog (dacă nu există)
-- ============================================================
PRINT ''
PRINT 'PASUL 3: Creare CertificateAlertLog table...'

IF OBJECT_ID('[SeifDate].[dbo].[CertificateAlertLog]', 'U') IS NULL
BEGIN
    CREATE TABLE [SeifDate].[dbo].[CertificateAlertLog] (
        [Id] bigint NOT NULL IDENTITY(1,1) PRIMARY KEY,
        [ManagedCertificateId] int NOT NULL,
        [CertificateUrl] nvarchar(max) NULL,
        [DaysUntilExpiry] int NULL,
        [AlertType] nvarchar(50) NULL,
        [EmailSentTo] nvarchar(255) NULL,
        [AlertSentDateUtc] datetime2(3) NOT NULL DEFAULT GETUTCDATE(),
        [EmailStatus] nvarchar(50) NOT NULL DEFAULT 'Pending',
        [ErrorMessage] nvarchar(max) NULL,
        
        -- Foreign Key
        CONSTRAINT [FK_CertificateAlertLog_ManagedCertificates] 
            FOREIGN KEY ([ManagedCertificateId]) 
            REFERENCES [SeifDate].[dbo].[ManagedCertificates]([Id]) 
            ON DELETE CASCADE
    );
    
    -- Crează indecși pentru performance
    CREATE INDEX [IX_CertificateAlertLog_ManagedCertificateId] 
        ON [SeifDate].[dbo].[CertificateAlertLog]([ManagedCertificateId]);
    
    CREATE INDEX [IX_CertificateAlertLog_AlertSentDateUtc] 
        ON [SeifDate].[dbo].[CertificateAlertLog]([AlertSentDateUtc]);
    
    PRINT '✅ CertificateAlertLog table creat cu indecși';
END
ELSE
BEGIN
    PRINT '⚠️ CertificateAlertLog table deja exista';
END

-- ============================================================
-- VERIFICARE FINALĂ
-- ============================================================
PRINT ''
PRINT '========================================='
PRINT 'VERIFICARE FINALĂ'
PRINT '========================================='

PRINT ''
PRINT '--- CERTIFICATE SETTINGS IN AppSettings ---'
SELECT [Key], [Value], UpdatedUtc FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] LIKE 'Cert%' ORDER BY [Key];

PRINT ''
PRINT '--- CERTIFICATE ALERT LOG TABLE ---'
IF OBJECT_ID('[SeifDate].[dbo].[CertificateAlertLog]', 'U') IS NOT NULL
BEGIN
    PRINT 'CertificateAlertLog table EXISTS ✅'
    SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'CertificateAlertLog' AND TABLE_SCHEMA = 'dbo'
    ORDER BY ORDINAL_POSITION;
END
ELSE
BEGIN
    PRINT 'CertificateAlertLog table NU EXISTĂ ❌'
END

PRINT ''
PRINT '========================================='
PRINT '✅ IMPLEMENTARE COMPLETĂ'
PRINT '========================================='
PRINT 'Toate scripturile au fost rulate cu succes!'
PRINT 'Următorul pas: Deploy aplicație și restart!'

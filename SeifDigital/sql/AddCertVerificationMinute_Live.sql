-- Add CertVerificationMinute setting (LIVE/Production)
-- Script to add minute-level scheduling support for certificate verification
-- Schema: SeifDate.dbo.AppSettings with columns: Key, Value, UpdatedUtc, ValueString

SET NOCOUNT ON;

-- Check if setting already exists
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationMinute')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES (
        'CertVerificationMinute',
        '0',
        GETUTCDATE(),
        'Minuta pentru verificarea certificatelor (0-59). Default: 0 (la inceput de ora). Ex: 0 = :00, 30 = :30'
    );

    PRINT '✅ Added CertVerificationMinute setting to LIVE database (SeifDate)';
END
ELSE
BEGIN
    PRINT '⚠️ CertVerificationMinute setting already exists in LIVE database';
END

-- Verify the setting was created/exists
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] = 'CertVerificationMinute';

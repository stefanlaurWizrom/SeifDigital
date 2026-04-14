-- ========================================
-- Migration: AddNoteFisierToNotes
-- Date: 2024-04-14
-- Description: Adds file attachment support for Notes (meniu Note)
-- 
-- Changes:
--   ✅ Creates table [dbo].[NoteFisier] (junction table for Notes + Files)
--   ✅ Adds 3 indexes for performance
--   ✅ Creates foreign keys with CASCADE DELETE to UserNote and UserFile
--
-- Pre-requisites:
--   - [dbo].[UserNote] table must exist
--   - [dbo].[UserFile] table must exist
--
-- Idempotent: YES (checks if table exists before creating)
-- ========================================

-- Step 1: Create NoteFisier table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier')
BEGIN
    CREATE TABLE [dbo].[NoteFisier] (
        [Id] [bigint] IDENTITY(1,1) NOT NULL,
        [UserNote_Id] [bigint] NOT NULL,
        [UserFile_Id] [bigint] NOT NULL,
        [FileType] [nvarchar](50) NOT NULL,
        [CreatedUtc] [datetime2](3) NOT NULL,
        CONSTRAINT [PK_NoteFisier] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]
    
    PRINT 'Table [dbo].[NoteFisier] created successfully'
END
ELSE
BEGIN
    PRINT 'Table [dbo].[NoteFisier] already exists - skipping creation'
END
GO

-- Step 2: Create indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteFisier_UserNote_Id' AND object_id = OBJECT_ID('[dbo].[NoteFisier]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_NoteFisier_UserNote_Id] 
        ON [dbo].[NoteFisier]([UserNote_Id] ASC)
    PRINT 'Index [IX_NoteFisier_UserNote_Id] created successfully'
END
ELSE
BEGIN
    PRINT 'Index [IX_NoteFisier_UserNote_Id] already exists - skipping'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteFisier_UserFile_Id' AND object_id = OBJECT_ID('[dbo].[NoteFisier]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_NoteFisier_UserFile_Id] 
        ON [dbo].[NoteFisier]([UserFile_Id] ASC)
    PRINT 'Index [IX_NoteFisier_UserFile_Id] created successfully'
END
ELSE
BEGIN
    PRINT 'Index [IX_NoteFisier_UserFile_Id] already exists - skipping'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_NoteFisier_UserNote_Id_UserFile_Id' AND object_id = OBJECT_ID('[dbo].[NoteFisier]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_NoteFisier_UserNote_Id_UserFile_Id] 
        ON [dbo].[NoteFisier]([UserNote_Id] ASC, [UserFile_Id] ASC)
    PRINT 'Index [IX_NoteFisier_UserNote_Id_UserFile_Id] created successfully'
END
ELSE
BEGIN
    PRINT 'Index [IX_NoteFisier_UserNote_Id_UserFile_Id] already exists - skipping'
END
GO

-- Step 3: Create foreign keys
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_NoteFisier_UserNote_UserNote_Id' AND TABLE_NAME = 'NoteFisier')
BEGIN
    ALTER TABLE [dbo].[NoteFisier] 
        WITH NOCHECK ADD CONSTRAINT [FK_NoteFisier_UserNote_UserNote_Id] 
        FOREIGN KEY ([UserNote_Id]) 
        REFERENCES [dbo].[UserNote]([Id]) 
        ON DELETE CASCADE
    
    ALTER TABLE [dbo].[NoteFisier] CHECK CONSTRAINT [FK_NoteFisier_UserNote_UserNote_Id]
    PRINT 'Foreign key [FK_NoteFisier_UserNote_UserNote_Id] created successfully'
END
ELSE
BEGIN
    PRINT 'Foreign key [FK_NoteFisier_UserNote_UserNote_Id] already exists - skipping'
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_NoteFisier_UserFile_UserFile_Id' AND TABLE_NAME = 'NoteFisier')
BEGIN
    ALTER TABLE [dbo].[NoteFisier] 
        WITH NOCHECK ADD CONSTRAINT [FK_NoteFisier_UserFile_UserFile_Id] 
        FOREIGN KEY ([UserFile_Id]) 
        REFERENCES [dbo].[UserFile]([Id]) 
        ON DELETE CASCADE
    
    ALTER TABLE [dbo].[NoteFisier] CHECK CONSTRAINT [FK_NoteFisier_UserFile_UserFile_Id]
    PRINT 'Foreign key [FK_NoteFisier_UserFile_UserFile_Id] created successfully'
END
ELSE
BEGIN
    PRINT 'Foreign key [FK_NoteFisier_UserFile_UserFile_Id] already exists - skipping'
END
GO

-- Step 4: Verification
SELECT 'Migration completed successfully!' AS [Status]
SELECT COUNT(*) AS [NoteFisierCount] FROM [dbo].[NoteFisier]
SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'
GO

-- ========================================
-- ROLLBACK SCRIPT (if needed):
-- ========================================
-- DROP TABLE [dbo].[NoteFisier]
-- ========================================

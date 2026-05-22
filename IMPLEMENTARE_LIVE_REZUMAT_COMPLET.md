🚀 IMPLEMENTARE CERTIFICATE VERIFICATION & AUTO-SCHEDULING - REZUMAT COMPLET
==================================================================================

## 📋 Ce S-a Implementat Azi

### 1. **Sistem de Verificare Automată a Certificatelor SSL/TLS**
   - Verificare zilnică la ora/minuta configurată
   - Detectare certificate expirând în X zile
   - Alertă prin email (dacă configurată)
   - Înregistrare în audit log

### 2. **Programare cu Minute (Nu Doar Ore)**
   - Before: Doar ore (8:00, 14:00, etc.)
   - After: Ore și minute (8:15, 14:30, etc.)
   - Permette testare ușoară (setează la 1-2 minute)

### 3. **Interfață Admin pentru Setări**
   - Ora și minuta verificării
   - Timezone (implicit: Europe/Bucharest)
   - Prag alerta (zile înainte de expirare)
   - Email pentru alerte

### 4. **Hangfire Job Scheduler**
   - Planificare precisă zilnică
   - Rescheduleere instantanee la salvare setări
   - Dashboard la /hangfire (admin-only)

---

## 🔧 CE TREBUIE SĂ RULEZI PE LIVE DATABASE (SeifDate)

### PASUL 1: ADAUGĂ SETAREA PENTRU MINUTE

```sql
-- ============================================================
-- SCRIPT 1: ADD MINUTE-LEVEL SCHEDULING SETTING
-- Database: SeifDate (LIVE)
-- ============================================================

SET NOCOUNT ON;

-- Verifică dacă setarea există deja
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

-- Verifică dacă a fost adăugată
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] = 'CertVerificationMinute';
```

**Rezultat așteptat**: 
```
Key                    | Value | UpdatedUtc              | ValueString
CertVerificationMinute | 0     | [current datetime]      | Minuta pentru...
```

---

### PASUL 2: VERIFICĂ EXISTENȚA CELORLALTE SETĂRI (TREBUIE SĂ EXISTE!)

```sql
-- ============================================================
-- SCRIPT 2: VERIFY ALL CERTIFICATE SETTINGS EXIST
-- Database: SeifDate (LIVE)
-- ============================================================

-- Verifică dacă toate 5 setări certificate există
SELECT [Key], [Value], UpdatedUtc
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] IN (
    'CertVerificationHour',
    'CertVerificationMinute',
    'CertVerificationTimezone',
    'CertAlertDaysThreshold',
    'CertAlertEmail'
)
ORDER BY [Key];
```

**Rezultat așteptat** (5 rânduri):
```
Key                          | Value                | UpdatedUtc
─────────────────────────────────────────────────────────────────
CertAlertDaysThreshold       | 15                   | [datetime]
CertAlertEmail               | [your-email]         | [datetime]
CertVerificationHour         | 8                    | [datetime]
CertVerificationMinute       | 0                    | [datetime]  ← NEW
CertVerificationTimezone     | Europe/Bucharest     | [datetime]
```

**Dacă lipsesc setări**, rulează aceste INSERT-uri:

```sql
-- Dacă lipsește CertVerificationHour
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationHour')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertVerificationHour', '8', GETUTCDATE(), 'Ora verificării (0-23)');
END

-- Dacă lipsește CertVerificationTimezone
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertVerificationTimezone')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertVerificationTimezone', 'Europe/Bucharest', GETUTCDATE(), 'Zona horara IANA');
END

-- Dacă lipsește CertAlertDaysThreshold
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertAlertDaysThreshold')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertAlertDaysThreshold', '15', GETUTCDATE(), 'Prag alerta (zile)');
END

-- Dacă lipsește CertAlertEmail
IF NOT EXISTS (SELECT 1 FROM [SeifDate].[dbo].[AppSettings] WHERE [Key] = 'CertAlertEmail')
BEGIN
    INSERT INTO [SeifDate].[dbo].[AppSettings] ([Key], [Value], UpdatedUtc, ValueString)
    VALUES ('CertAlertEmail', 'admin@wizrom.ro', GETUTCDATE(), 'Email alerte');
END

PRINT '✅ All Certificate Settings are now present';
```

---

### PASUL 3: RULEAZĂ MIGRAȚIA EF CORE (dacă nu e rulată)

```sql
-- ============================================================
-- SCRIPT 3: CREATE CERTIFICATE ALERT LOG TABLE (if not exists)
-- Database: SeifDate (LIVE)
-- ============================================================

-- Verifică dacă tabela CertificateAlertLog există
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
    
    -- Indecși pentru performance
    CREATE INDEX [IX_CertificateAlertLog_ManagedCertificateId] 
        ON [SeifDate].[dbo].[CertificateAlertLog]([ManagedCertificateId]);
    
    CREATE INDEX [IX_CertificateAlertLog_AlertSentDateUtc] 
        ON [SeifDate].[dbo].[CertificateAlertLog]([AlertSentDateUtc]);
    
    PRINT '✅ Created CertificateAlertLog table with indexes';
END
ELSE
BEGIN
    PRINT '⚠️ CertificateAlertLog table already exists';
END

-- Verifică structura tabelei
SELECT * FROM [SeifDate].[dbo].[CertificateAlertLog] WHERE 1=0;
```

---

## 📊 VERIFICARE FINALĂ

După ce ruleaza toate scripturile, verifică:

```sql
-- ============================================================
-- VERIFICARE FINALĂ - RULEAZĂ ASTA PENTRU A CONFIRMA TOT
-- Database: SeifDate (LIVE)
-- ============================================================

PRINT '=== CERTIFICATE SETTINGS ==='
SELECT [Key], [Value] FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] LIKE 'Cert%' ORDER BY [Key];

PRINT ''
PRINT '=== CERTIFICATE ALERT LOG TABLE ==='
SELECT 
    OBJECT_NAME(o.object_id) AS TableName,
    COUNT(*) AS RowCount
FROM [SeifDate].[dbo].[CertificateAlertLog] c
CROSS JOIN sys.objects o
WHERE OBJECT_NAME(o.object_id) = 'CertificateAlertLog'
GROUP BY o.object_id;

PRINT '✅ Verification complete!'
```

---

## 🎯 ORDINE DE EXECUTARE

1. **PASUL 1**: Rulează SCRIPT 1 (CertVerificationMinute)
2. **PASUL 2**: Rulează SCRIPT 2 (verifica setările)
3. **PASUL 2b**: Dacă lipsesc setări, rulează INSERT-urile
4. **PASUL 3**: Rulează SCRIPT 3 (CertificateAlertLog table)
5. **VERIFICARE**: Rulează scriptul de verificare finală

---

## 🚀 DEPLOY APLICAȚIE

După ce ruleaza scripturile SQL:

1. **Rebuild Visual Studio**: `Ctrl+Shift+B`
2. **Deploy pe LIVE**
3. **Restart aplicația**
4. **Deschide Admin > Certificate Settings**
5. **Seteaza ora și minuta dorite**
6. **Verifica Hangfire Dashboard** (/hangfire/recurring)

---

## ✅ CHECKLIST

- [ ] SCRIPT 1: CertVerificationMinute adăugat
- [ ] SCRIPT 2: Toate 5 setări certificate exist
- [ ] SCRIPT 3: CertificateAlertLog table creat
- [ ] Verificare finală: Toate OK
- [ ] Visual Studio rebuild
- [ ] Aplicație deployed pe LIVE
- [ ] Restart aplicație
- [ ] Certificate Settings accesibil
- [ ] Hangfire Dashboard funcțional

---

## 📝 CODUL CARE VINE CU DEPLOYMENTUL

Acestea sunt deja implementate în cod și vor merge imediat după scripturi SQL:

### Fișiere Modificate:
- ✅ `CertificateSettingsService.cs` - Get/Set minute
- ✅ `CertificateSettingsController.cs` - Reschedule Hangfire
- ✅ `Program.cs` - Cron cu minute
- ✅ `Views/CertificateSettings/Index.cshtml` - UI pentru minute
- ✅ `Models/CertificateAlertLog.cs` - Model audit
- ✅ `ApplicationDbContext.cs` - DbSet + configurare

### Fișiere Noi (Documentație):
- ✅ SQL scripts în `SeifDigital/sql/`
- ✅ Documentație în root folder

---

## 🔗 CONEXIUNE STRING

Verifică că `appsettings.json` are:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=SeifDate;Integrated Security=true;"
  },
  "Hangfire": {
    "ConnectionString": "Server=YOUR_SERVER;Database=SeifDate;Integrated Security=true;"
  }
}
```

---

## 📞 SUPORT

**Dacă apare eroare:**

1. Verifică database name: `SeifDate` (nu `SelfDate`)
2. Verifică schema: `[dbo]`
3. Verifică user permissions: `SELECT`, `INSERT`, `CREATE TABLE`
4. Verifică Hangfire tables: `HangfireSchema_*`

**Dacă Hangfire nu se rescheduleaza:**
1. Restart aplicația
2. Verifica Admin > Certificate Settings poate salva
3. Check browser console pentru erori JavaScript
4. Verifica app logs pentru excepții

---

## ✨ REZUMAT FINAL

✅ **Adăugat**: Minuta-level scheduling (0-59)
✅ **Adăugat**: Setare CertVerificationMinute în AppSettings
✅ **Adăugat**: Tabelă CertificateAlertLog pentru audit
✅ **Modificat**: Program.cs pentru citire minuta
✅ **Modificat**: Controller pentru reschedule Hangfire
✅ **Modificat**: UI pentru input hour:minute

**Status**: Gata de LIVE deployment! 🚀

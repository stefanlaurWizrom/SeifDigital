✅ VERIFICATION QUERIES - After SQL Script Execution
=====================================================

**Database Name**: `SeifDate` (NOT SelfDate)

Run these queries to verify everything was added correctly:

---

## QUERY 1: Verify AppSettings Table (New Setting Added)

```sql
-- Check if CertVerificationMinute was added
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] = 'CertVerificationMinute'
ORDER BY [Key];
```

**Expected Result:**
```
Key                    | Value | UpdatedUtc              | ValueString
───────────────────────────────────────────────────────────────────────
CertVerificationMinute | 0     | 2026-05-19 14:30:45.123 | Minuta pentru verifi...
```

---

## QUERY 2: Verify All Certificate Settings

```sql
-- View all certificate-related settings
SELECT [Key], [Value], UpdatedUtc
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] LIKE 'Cert%'
ORDER BY [Key];
```

---

## QUERY 3: Verify Hangfire Job Schedule (CORRECTED)

```sql
-- Check Hangfire job details - CORRECT SYNTAX WITH [dbo]
SELECT 
    Id,
    Cron,
    Queue,
    CreatedAt
FROM [SeifDate].[dbo].[HangfireSchema_RecurringJob]
WHERE Id = 'certificate-check';
```

**Key Point**: Must use `[SeifDate].[dbo].[TableName]` - do NOT skip the `[dbo]` part!

---

## QUICK VERIFICATION (Copy-Paste All 3)

```sql
-- 1. New minute setting
SELECT [Key], [Value], UpdatedUtc, ValueString 
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] = 'CertVerificationMinute';

-- 2. All certificate settings
SELECT [Key], [Value], UpdatedUtc
FROM [SeifDate].[dbo].[AppSettings] 
WHERE [Key] LIKE 'Cert%'
ORDER BY [Key];

-- 3. Hangfire job (CORRECTED - include [dbo])
SELECT Id, Cron, Queue, CreatedAt
FROM [SeifDate].[dbo].[HangfireSchema_RecurringJob]
WHERE Id = 'certificate-check';
```

✅ Database: SeifDate
✅ Schema: dbo
✅ Ready to verify!

# 🚀 LIVE DEPLOYMENT INSTRUCTIONS

## Instrucțiuni pentru Migrația Bazei de Date pe Live

---

## 📌 Informații Migrație

| Detaliu | Valoare |
|---------|---------|
| **Nume** | `AddNoteFisierToNotes` |
| **Data** | 2024-04-14 |
| **EF Version** | 8.0.0 |
| **Tabelul creat** | `[dbo].[NoteFisier]` |
| **SQL Script** | `SeifDigital/Migrations/MIGRATION_SCRIPTS/20260414125535_AddNoteFisierToNotes.sql` |

---

## ⚠️ PRE-REQUISITI

- [x] SQL Server Management Studio (SSMS) deschis
- [x] Acces DBA/Admin la database LIVE
- [x] Backup recent al bazei de date
- [x] Test environment validat (deja făcut ✅)
- [x] Codul deployed pe server (WebApp)

---

## 🔧 METODA 1: Manual SQL (Recomandată)

### **Step 1: Deschide SQL Script**

1. Deschide SSMS
2. Conectează-te la serverul LIVE
3. Selectează database-ul (ex: `SeifDigitalProduction`)

### **Step 2: Copii SQL Script**

Copiază întreaga sursă din fișier:
```
SeifDigital/Migrations/MIGRATION_SCRIPTS/20260414125535_AddNoteFisierToNotes.sql
```

### **Step 3: Executare în SSMS**

1. Click pe "New Query"
2. Paste SQL-ul
3. **Click "Execute"** (F5)

**Output așteptat:**
```
Table [dbo].[NoteFisier] created successfully
Index [IX_NoteFisier_UserNote_Id] created successfully
Index [IX_NoteFisier_UserFile_Id] created successfully
Index [IX_NoteFisier_UserNote_Id_UserFile_Id] created successfully
Foreign key [FK_NoteFisier_UserNote_UserNote_Id] created successfully
Foreign key [FK_NoteFisier_UserFile_UserFile_Id] created successfully
Migration completed successfully!
NoteFisierCount | 0
```

### **Step 4: Verificare**

```sql
-- Verify table exists
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'

-- Verify structure
EXEC sp_columns '[dbo].[NoteFisier]'

-- Verify indexes
SELECT INDEX_NAME FROM INFORMATION_SCHEMA.STATISTICS 
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'
```

---

## 🔧 METODA 2: EF Core CLI (Dacă ai acces la server)

```bash
# Pe server, din folder SeifDigital
cd D:\seifdigital\SeifDigital

# Aplică migrația
dotnet ef database update
```

---

## ✅ SIGN-OFF CHECKLIST

După executarea SQL:

- [ ] Tabel `NoteFisier` creat
- [ ] 3 indexuri create
- [ ] 2 foreign keys create
- [ ] Fără erori în output
- [ ] Test query returnează 0 rows (e gol la început)
- [ ] Backup dat OFF (dacă era ON)
- [ ] Table locks eliberate

---

## 📋 ROLLBACK (dacă merge ceva prost)

**NU PANICA!** Rollback e simplu:

```sql
-- Option 1: Drop table (cascade cleanup automat)
DROP TABLE [dbo].[NoteFisier]

-- Option 2: Stare anterioară (din backup)
-- Restaurează backup-ul anterior migrației
```

---

## 🔍 POST-DEPLOYMENT VERIFICATION

### **1. SQL Checks**

```sql
-- Count records
SELECT COUNT(*) AS NoteFisierCount FROM [dbo].[NoteFisier]

-- Check FK integrity
SELECT CONSTRAINT_NAME, TABLE_NAME, REFERENCED_TABLE_NAME
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
WHERE TABLE_NAME = 'NoteFisier'

-- Check indexes
SELECT INDEX_NAME, COLUMN_NAME
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'
ORDER BY INDEX_NAME
```

### **2. Application Checks**

1. **Start aplikația** (WebApp)
2. **Login user**
3. **Merge Note → Fisiere modal**
4. **Upload test file** → expect success
5. **List fișiere** → afișare corectă
6. **Delete file** → cascade work
7. **Send note cu fișiere** → JSON correct
8. **Receive note** → afișare fișiere
9. **Save received note** → copiere fișiere

---

## 📞 MONITORING POST-DEPLOY

### **Performance**
- Monitorizează CPU, RAM, Disk I/O
- Query time trebuie <100ms
- No blocking queries

### **Errors**
- Verifica Event Log pe server
- Verifica Application Insights
- Check AuditLog table pentru File.* operations

### **Users**
- Comunică schimbarea (Notes + Fisiere feature)
- Oferă tutorial screenshot
- Support disponibil pentru issues

---

## 🎯 SUCCESS CRITERIA

✅ Migrație aplicată fără erori  
✅ Tabel și indexuri create  
✅ Foreign keys funcționale  
✅ Aplicația ruleaza normal  
✅ Utilizatorii pot upload fișiere  
✅ Utilizatorii pot trimite note cu fișiere  
✅ Fișiere se copiază la recepție  

---

## 📊 Timeline Estimat

| Activitate | Durată |
|-----------|--------|
| Backup DB | 2-5 min |
| SQL Execution | <1 min |
| Verification | 3-5 min |
| App restart | 1-2 min |
| User testing | 10-15 min |
| **TOTAL** | **20-30 min** |

---

## 🆘 TROUBLESHOOTING

### **Problema: "Table already exists"**
```
Rezolvare: Migrația e idempotent (checks IF NOT EXISTS)
           Rulează din nou, nu va crea duplicate
```

### **Problema: "Foreign key constraint failed"**
```
Rezolvare: Verifica că [dbo].[UserNote] și [dbo].[UserFile] 
           tabele există și au date valide
Verificare: SELECT COUNT(*) FROM [dbo].[UserNote]
            SELECT COUNT(*) FROM [dbo].[UserFile]
```

### **Problema: "Insufficient permissions"**
```
Rezolvare: User-ul SQL trebuie să fie db_owner pe database
           Contactează DBA sau server admin
```

### **Problema: "Timeout executing query"**
```
Rezolvare: Network issue temporar. Retry după 1 minut.
           Sau crește timeout în SSMS:
           Query → Query Options → Execution → Timeout (default 0 = infinite)
```

---

## 📝 SIGN-OFF FORM

```
DATABASE MIGRATION SIGN-OFF
============================

Migrație: AddNoteFisierToNotes (20260414125535)
Data: _______________
Server: _______________
Database: _______________

Executat de: _______________
Verificat de: _______________

✅ SQL Script executat: [ ]
✅ Tabel creat: [ ]
✅ Indexuri create: [ ]
✅ Foreign keys create: [ ]
✅ Fără erori: [ ]
✅ App testat: [ ]
✅ Users can upload files: [ ]

Status: ☐ SUCCESS / ☐ ROLLBACK / ☐ IN_PROGRESS

Note/Issues:
_________________________________
_________________________________
_________________________________

Signature: _______________ Data: _______________
```

---

## 📞 EMERGENCY CONTACTS

- **DBA**: [Contact DBA]
- **DevOps**: [Contact DevOps]
- **App Support**: [Contact App Team]
- **Escalation**: [Contact Manager]

---

## 📚 REFERENCE

- EF Core Migration: `SeifDigital/Migrations/20260414125535_AddNoteFisierToNotes.cs`
- SQL Script: `SeifDigital/Migrations/MIGRATION_SCRIPTS/20260414125535_AddNoteFisierToNotes.sql`
- Documentation: `IMPLEMENTATION_SUMMARY.md`
- Test Checklist: `TEST_CHECKLIST.md`
- GitHub: `https://github.com/stefanlaurWizrom/SeifDigital` (branch: AdminSettings)

---

## ✨ Notes

- **Idempotent**: SQL-ul poți rula de mai multe ori, nu va crea duplicate
- **Cascade Delete**: Notă ștearsă = NoteFisier entries șterse automat
- **Zero Data Loss**: Operațiune read-only pe alte tabele
- **Reversible**: Simplă DROP TABLE dacă e nevoie

---

**Version**: 1.0  
**Date**: 2024-04-14  
**Status**: ✅ READY FOR PRODUCTION

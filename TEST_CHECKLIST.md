# 🧪 Testare Funcționalitate: Atasare Fișiere pentru Note

## ✅ Checklist de Testare

### **Modulul 1: Upload & Gestionare Fișiere**

- [ ] **Test 1.1**: Deschidere modal "Fisiere" pe o notă
  - Se încarcă lista categorii permise
  - Se afișează "Nu ai fișiere atasate"

- [ ] **Test 1.2**: Upload fișier (drag & drop)
  - Trag fișier în drop zone
  - Se detectează automat categoria (image/document/certificate)
  - Se afișează progress bar
  - Mesaj success "✅ Fișier încărcat cu succes!"
  - Lista se reîncarcă automat

- [ ] **Test 1.3**: Upload fișier (click selectare)
  - Click în zona de upload
  - Selectez fișier din dialog
  - Același flow ca drag & drop

- [ ] **Test 1.4**: Validări upload
  - Extensie nepermisă → mesaj de eroare
  - Fișier prea mare → mesaj cu max size
  - Fișier gol → mesaj de eroare
  - Sesiune expirat → redirect login

- [ ] **Test 1.5**: Afișare lista fișiere
  - Fișierele se afișează cu icon corect (📸📄🔐)
  - Se arată nume, extensie, dimensiune
  - Butoane: ⬇️ (descarcă), 🗑️ (șterge)

- [ ] **Test 1.6**: Descărcare fișier
  - Click pe ⬇️
  - Fișierul se descarcă corect
  - Nume original păstrat

- [ ] **Test 1.7**: Ștergere fișier
  - Click pe 🗑️
  - Confirmare dialog
  - Fișierul dispare din listă
  - Mesaj success
  - Fișierul fizic șters

- [ ] **Test 1.8**: UpdatedUtc actualizat
  - După upload/delete, nota are `UpdatedUtc` nou

---

### **Modulul 2: Trimitere Notă cu Fișiere**

- [ ] **Test 2.1**: Trimitere notă fără fișiere
  - Mesaj normal, fără `AttachedImageFileIds`

- [ ] **Test 2.2**: Trimitere notă cu 1 fișier
  - Notă cu 1 fișier atasat
  - Click "Trimite" → modal
  - Selectez destinatar valid
  - UserMessage creat cu `AttachedImageFileIds` JSON

- [ ] **Test 2.3**: Trimitere notă cu mai multe fișiere
  - Notă cu 3+ fișiere
  - Mesajul conține toate fișierele în JSON
  - Format: `[{fileId: X, fileType: "image"}, ...]`

- [ ] **Test 2.4**: Validări trimitere
  - Email invalid → mesaj eroare
  - Destinatar inexistent → mesaj eroare
  - Nota nu există → mesaj eroare
  - 2FA expirat → redirect login

- [ ] **Test 2.5**: Audit logging
  - `Message.Send` creat cu fileCount
  - Details conțin corect parametrii

---

### **Modulul 3: Recepție Notă cu Fișiere**

- [ ] **Test 3.1**: Afișare în Mesaje
  - Mesaj de tip "Notes"
  - Titlu și text afișate corect
  - Secțiunea "Fișiere atasate" cu preview icoane

- [ ] **Test 3.2**: Afișare fișiere în Mesaje
  - Fiecare fișier cu icon corect
  - Text "Se va copia la salvare"
  - Număr fișier X/Y

- [ ] **Test 3.3**: Fără fișiere
  - Mesaj de tip Notes fără fișiere
  - Nu apare secțiunea "Fișiere atasate"

---

### **Modulul 4: Salvare Notă Primită**

- [ ] **Test 4.1**: Salvare notă fără fișiere
  - Click "Salvează"
  - Notă creată în vault
  - Fișierele nu sunt atasate

- [ ] **Test 4.2**: Salvare notă cu 1 fișier
  - Notă cu 1 fișier
  - Click "Salvează"
  - Notă nouă creată
  - Fișier **copiat** pentru destinatar
  - NoteFisier entry creat
  - **Fișierul devine proprietatea receptorului**

- [ ] **Test 4.3**: Salvare notă cu mai multe fișiere
  - Notă cu 3+ fișiere
  - Fiecare fișier se copiază
  - Fiecare fișier primește ID nou
  - Proprietar nou = destinatar

- [ ] **Test 4.4**: Fișierele copiate sunt independente
  - Modific nota originală → nu afectează copia
  - Șterg nota originală → copia rămâne
  - Șterg fișier din notă original → copia rămâne

- [ ] **Test 4.5**: Audit logging
  - NoteFisier entries create
  - File copy operations loggate

---

### **Modulul 5: Ștergere & Cascade**

- [ ] **Test 5.1**: Ștergere notă cu fișiere
  - Notă cu 2-3 fișiere
  - Click "Șterge" din Notes
  - Confirmare dialog
  - Notă ștearsă
  - NoteFisier entries șterse (cascade)
  - UserFile entries șterse (cascade)
  - Fișierele fizice șterse

- [ ] **Test 5.2**: Verificare fizice
  - După ștergere, fișierul nu există pe disk
  - Folder uploads nu conține fișierul

- [ ] **Test 5.3**: Ștergere din alte conturi
  - Cont A: crează notă cu fișier
  - Cont B: încearcă ștergere
  - Eroare "Note nu există"
  - Fișierul A rămâne intact

---

### **Modulul 6: Securitate**

- [ ] **Test 6.1**: 2FA validation
  - Fără 2FA: redirect login
  - Upload, Delete, Download, GetNoteImages

- [ ] **Test 6.2**: OwnerKey validation
  - Utilizator A nu poate accesa notă B
  - Mesaj "Note nu există"

- [ ] **Test 6.3**: File extension validation
  - Extensie nepermisă → eroare
  - Mesaj: "Fișierul de tip X nu se gaseste"

- [ ] **Test 6.4**: File size validation
  - Fișier > maxSize → eroare
  - Mesaj cu max size permis

- [ ] **Test 6.5**: MIME type validation
  - Fișier mascat (ex: .exe → .jpg)
  - Se detectează și se refuză

---

### **Modulul 7: Categorii & Admin Settings**

- [ ] **Test 7.1**: Citire categorii din Admin
  - Upload modal afișează categorii din Admin
  - Doar extensii permise în Admin se acceptă

- [ ] **Test 7.2**: Modificare categorii în Admin
  - Admin adaugă nouă categorie
  - Cache se reîncarcă (max 1 minut)
  - Extensie nouă se acceptă

- [ ] **Test 7.3**: FileType stocat corect
  - Fișier image → FileType = "image"
  - Fișier document → FileType = "document"
  - Fișier certificate → FileType = "certificate"

---

### **Modulul 8: UI/UX**

- [ ] **Test 8.1**: Buton "Fisiere" vizibil
  - Pe fiecare notă: Edit | Fisiere | Trimite | Șterge
  - Buton culoare warning (orange)

- [ ] **Test 8.2**: Modal deschidere/închidere
  - Modal se deschide corect
  - Se închide cu X, Renunță, ESC
  - Note se reîncarcă la deschidere

- [ ] **Test 8.3**: Drag & Drop UX
  - Drop zone se evidențiază la drag
  - Culoare albastră
  - Click se deschide file selector

- [ ] **Test 8.4**: Responsive design
  - Pe mobile: butoane funcționale
  - Modal se vede corect
  - Lista fișiere se scrollează

- [ ] **Test 8.5**: Mesaje de feedback
  - Upload success: ✅ Fișier încărcat
  - Upload error: ❌ Mesaj specific
  - Delete success: ✅ Fișier șters
  - Delete error: ❌ Eroare

---

### **Modulul 9: Performance**

- [ ] **Test 9.1**: Upload performance
  - Fișier 5MB: se încarcă în <5 sec
  - Progress bar se mișcă

- [ ] **Test 9.2**: Lista fișiere
  - 10+ fișiere: se scrollează rapid
  - Fără slowdown

- [ ] **Test 9.3**: Mesaje cu fișiere
  - Load mesaje cu 5+ fișiere: <1 sec
  - Lista se reîncarcă rapid

---

### **Modulul 10: Audit Log**

- [ ] **Test 10.1**: File.Upload events
  - EventType = "File.Upload"
  - Outcome = "Success" / "Fail"
  - Details: fileName, fileExtension, category, fileSize

- [ ] **Test 10.2**: File.Delete events
  - EventType = "File.Delete"
  - Outcome = "Success"

- [ ] **Test 10.3**: Message.Send events
  - FileCount = corect
  - SourceType = "Notes"

- [ ] **Test 10.4**: Message.Save events
  - Inclusiv copiere fișiere
  - Status = Success

---

## 🔍 Edge Cases

- [ ] Upload 0 byte file → eroare
- [ ] Upload fișier cu spații/caractere speciale → se salvează corect
- [ ] Delete fișier inexistent → 404
- [ ] Nota ștearsă înainte de delete fișier → eroare handled
- [ ] Concurrent uploads → se gestionează corect
- [ ] Session timeout la upload → redirect login
- [ ] CSRF token invalid → eroare 400
- [ ] Browser zoom 125%+ → UI responsiv

---

## 📊 SQL Verification

```sql
-- Verificare tabel creat
SELECT * FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'

-- Verificare date copiate
SELECT nf.Id, nf.UserNote_Id, nf.UserFile_Id, nf.FileType, nf.CreatedUtc
FROM dbo.NoteFisier nf
ORDER BY nf.CreatedUtc DESC

-- Verificare foreign keys
SELECT CONSTRAINT_NAME, TABLE_NAME, REFERENCED_TABLE_NAME
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS
WHERE TABLE_NAME = 'NoteFisier'

-- Verificare indexuri
SELECT INDEX_NAME
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'NoteFisier'
```

---

## 📋 Sign-off

**Tester**: ________________  
**Data**: ________________  
**Status**: ☐ PASS / ☐ FAIL  
**Note**: ________________________________________________

---

**Versiune**: 1.0  
**Data Creării**: 2024-04-14

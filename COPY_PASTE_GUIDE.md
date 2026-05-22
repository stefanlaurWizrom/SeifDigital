🚀 QUICK GUIDE - COPY-PASTE PENTRU LIVE
========================================

## 1️⃣ DU-TE LA: SeifDigital/sql/IMPLEMENTARE_LIVE_COMPLET.sql

## 2️⃣ DESCHIDE SQL SERVER MANAGEMENT STUDIO (SSMS)

## 3️⃣ CONECTEAZĂ LA LIVE DATABASE: SeifDate

## 4️⃣ DESCHIDE NOI QUERY:
   - Click "New Query"
   - SAU: Ctrl+N

## 5️⃣ COPY TOATĂ SCRIPTUL DIN: SeifDigital/sql/IMPLEMENTARE_LIVE_COMPLET.sql

   (Ctrl+A pentru all content, Ctrl+C pentru copy)

## 6️⃣ PASTE ÎN SSMS:
   - Ctrl+V
   - SAU: Click dreapta → Paste

## 7️⃣ RULEAZĂ SCRIPTUL:
   - Click: "Execute"
   - SAU: F5

## 8️⃣ AȘTEPTĂ REZULTAT ✅

Trebuie să vezi:
```
=========================================
CERTIFICATE VERIFICATION IMPLEMENTATION
=========================================

PASUL 1: Adăugare CertVerificationMinute...
✅ CertVerificationMinute adăugat

PASUL 2: Verificare și adăugare setări certificate...
✅ CertVerificationHour adăugat
✅ CertVerificationTimezone adăugat
✅ CertAlertDaysThreshold adăugat
✅ CertAlertEmail adăugat

PASUL 3: Creare CertificateAlertLog table...
✅ CertificateAlertLog table creat cu indecși

=========================================
VERIFICARE FINALĂ
=========================================

--- CERTIFICATE SETTINGS IN AppSettings ---
Key                          Value                UpdatedUtc
CertAlertDaysThreshold       15                   [datetime]
CertAlertEmail               admin@wizrom.ro      [datetime]
CertVerificationHour         8                    [datetime]
CertVerificationMinute       0                    [datetime]
CertVerificationTimezone     Europe/Bucharest     [datetime]

--- CERTIFICATE ALERT LOG TABLE ---
CertificateAlertLog table EXISTS ✅

=========================================
✅ IMPLEMENTARE COMPLETĂ
=========================================
```

---

## 9️⃣ DUPĂ SSMS - DEPLOY APLICAȚIE

1. Visual Studio: Rebuild (Ctrl+Shift+B)
2. Deploy pe LIVE
3. Restart aplicație
4. Deschide: Admin > Certificate Settings
5. Verifica Hangfire: /hangfire/recurring

---

## 📍 LOCAȚIE SCRIPT

**Fișier**: `SeifDigital/sql/IMPLEMENTARE_LIVE_COMPLET.sql`

### Sau rulează INDIVIDUAL (în ordine):

1. `SeifDigital/sql/AddCertVerificationMinute_Live.sql`
2. (Alte script-uri dacă lipsesc setări)

---

✅ **GATA! Acum e moment deploy!**

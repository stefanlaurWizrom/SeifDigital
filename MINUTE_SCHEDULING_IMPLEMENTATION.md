<!-- MINUTE-LEVEL SCHEDULING IMPLEMENTATION SUMMARY -->

# ✅ Minute-Level Certificate Scheduling - Implementation Complete

## Overview
Successfully added minute-level scheduling granularity to the certificate alert system. Users can now schedule certificate verification at any time from 00:00 to 23:59 (previously only by hour: 00:00, 01:00, 02:00, etc.).

## Changes Made

### 1. CertificateSettingsService.cs ✅
**File**: `SeifDigital/Services/CertificateSettingsService.cs`

**Added Methods:**
```csharp
// Get verification minute (0-59)
public async Task<int> GetVerificationMinuteAsync()
{
    return await _settings.GetIntAsync("CertVerificationMinute", 0);
}

// Set verification minute with validation
public async Task SetVerificationMinuteAsync(int minute)
{
    if (minute < 0 || minute > 59)
        throw new ArgumentException("Minute must be between 0 and 59");
    
    await _settings.SetIntAsync("CertVerificationMinute", minute);
}
```

**Updated Methods:**
- `GetAllSettingsAsync()` - Now includes `VerificationMinute` in returned DTO
- `SaveAllSettingsAsync()` - Now saves `VerificationMinute` from form

**Updated ViewModel:**
- `CertificateSettingsViewModel` - Added `VerificationMinute` property (default: 0)

---

### 2. CertificateSettings/Index.cshtml (View) ✅
**File**: `SeifDigital/Views/CertificateSettings/Index.cshtml`

**UI Changes:**
- **Before**: Single hour input (0-23)
- **After**: Combined hour:minute input with visual separator
  - Hour: 0-23 (left side)
  - Minute: 0-59 (right side)
  - Display: "14:30" format (with colon separator)

**Summary Display:**
- Updated to show "HH:MM" format (e.g., "14:30" instead of "14:00")
- Minute value auto-formatted with leading zero

**JavaScript Updates:**
- Added minute input listener to `updateSummary()` function
- Live preview updates both hour and minute simultaneously

**Example UI:**
```
┌─────────────────────────────┐
│ Ora si Minuta Verificare    │
│ [14 : 30] (input fields)    │
│ Format 24h (HH: 0-23, MM: 0-59)
└─────────────────────────────┘

📋 Rezumat configurare:
Verificare zilnica la ora: 14:30
```

---

### 3. Program.cs (Startup Configuration) ✅
**File**: `SeifDigital/Program.cs`

**Updated Hangfire Job Registration:**
```csharp
// Before:
string cronExpression = $"0 {verificationHour} * * *"; // "0 8 * * *"

// After:
string cronExpression = $"{verificationMinute} {verificationHour} * * *"; 
// "0 8 * * *" (08:00), "30 14 * * *" (14:30), etc.
```

**Cron Expression Format:**
- Minute: 0-59 (first position)
- Hour: 0-23 (second position)
- Day: * (every day)
- Month: * (every month)
- Weekday: * (every day of week)

**Examples:**
- `"0 8 * * *"` = 08:00 every day
- `"30 14 * * *"` = 14:30 every day
- `"15 9 * * *"` = 09:15 every day
- `"45 23 * * *"` = 23:45 every day

---

### 4. Database Scripts ✅
**Added SQL Scripts:**

**Development:** `SeifDigital/sql/AddCertVerificationMinute_Dev.sql`
- Adds `CertVerificationMinute` setting to SeifDigital_Dev database
- Default value: 0 (minute)
- Type: int
- Description: "Minuta pentru verificarea certificatelor (0-59)"

**LIVE:** `SeifDigital/sql/AddCertVerificationMinute_Live.sql`
- Adds `CertVerificationMinute` setting to SeifDigital database
- Same configuration as development

---

## How to Use

### For Testing (Minute-Level Scheduling)
1. Open **Certificate Settings** admin page
2. Set Hour: `14` and Minute: `30` (for example)
3. Click **Salveaza Setari**
4. Job will run daily at **14:30** (local timezone)

### For Development Testing (Next 1-2 Minutes)
1. Get current time: 14:25
2. Set Hour: `14` and Minute: `27` (2 minutes from now)
3. Hangfire will pick up the new schedule on next app restart
4. Job will execute at 14:27

### Real-World Examples
- **Every 30 minutes**: Would need multiple jobs (Hangfire limitation)
- **Every morning at 8:15 AM**: Set Hour=8, Minute=15
- **Daily evening check (17:45)**: Set Hour=17, Minute=45
- **Overnight at 2:30 AM**: Set Hour=2, Minute=30

---

## Database Updates Required

### Development Environment
Execute: `SeifDigital/sql/AddCertVerificationMinute_Dev.sql`

### LIVE Environment
Execute: `SeifDigital/sql/AddCertVerificationMinute_Live.sql`

### What Gets Added
```sql
INSERT INTO AppSettings (SettingKey, SettingValue, SettingType, Description)
VALUES (
    'CertVerificationMinute',  -- Key name
    '0',                       -- Default value (start of hour)
    'int',                     -- Data type
    'Minuta pentru verificare...' -- Description
)
```

---

## Backward Compatibility ✅

**No Breaking Changes:**
- Hour scheduling still works (set Minute to 0)
- All existing jobs with hourly schedules continue running
- Default minute value is 0 (maintains original :00 behavior)
- Job name remains "certificate-check" (no Hangfire migration needed)

**Migration Path:**
1. Deploy updated code
2. Run SQL scripts on both databases
3. On app restart, Hangfire reads new minute setting
4. Job reschedules with new cron expression

---

## Technical Details

### Settings Keys (AppSettings Table)
| Key | Value Range | Default | Type |
|-----|------------|---------|------|
| CertVerificationHour | 0-23 | 8 | int |
| **CertVerificationMinute** | **0-59** | **0** | **int** |
| CertVerificationTimezone | IANA string | Europe/Bucharest | string |
| CertAlertDaysThreshold | ≥1 | 15 | int |
| CertAlertEmail | Valid email | admin@wizrom.ro | string |

### Hangfire Behavior
- **Job Name**: `certificate-check` (unchanged)
- **Schedule**: CRON expression `"{minute} {hour} * * *"`
- **Timezone**: System timezone from settings (TimeZoneInfo)
- **Persistence**: SQL Server Hangfire schema
- **Dashboard**: Available at `/hangfire` (admin-only)

### Validation Rules
```csharp
// Hour validation (existing)
if (hour < 0 || hour > 23)
    throw new ArgumentException("Hour must be between 0 and 23");

// Minute validation (new)
if (minute < 0 || minute > 59)
    throw new ArgumentException("Minute must be between 0 and 59");
```

---

## Testing Checklist

- [ ] Build compiles successfully ✅
- [ ] Database scripts execute without errors
- [ ] AppSettings table has `CertVerificationMinute` key
- [ ] Settings form displays hour:minute input
- [ ] Summary shows time in HH:MM format
- [ ] JavaScript updates summary on minute change
- [ ] Hangfire job reschedules on app restart
- [ ] New cron expression visible in Hangfire dashboard
- [ ] Certificate verification runs at configured time

---

## Rollback Plan (If Needed)

1. **Revert Code**: Restore previous version of Program.cs, CertificateSettingsService.cs, Index.cshtml
2. **Remove Setting**: `DELETE FROM AppSettings WHERE SettingKey = 'CertVerificationMinute'`
3. **Restart App**: Job will revert to hour-only scheduling

---

## Files Modified Summary

| File | Changes | Status |
|------|---------|--------|
| SeifDigital/Services/CertificateSettingsService.cs | +GetVerificationMinuteAsync(), +SetVerificationMinuteAsync(), Updated ViewModel | ✅ |
| SeifDigital/Views/CertificateSettings/Index.cshtml | Updated form input (hour:minute), Updated summary display, Updated JS | ✅ |
| SeifDigital/Program.cs | Updated cron expression to include minutes | ✅ |
| SeifDigital/sql/AddCertVerificationMinute_Dev.sql | NEW - Development SQL script | ✅ |
| SeifDigital/sql/AddCertVerificationMinute_Live.sql | NEW - LIVE SQL script | ✅ |

---

## Next Steps

1. **Execute SQL Scripts**: Run on both Dev and LIVE databases
2. **Restart Application**: Changes take effect on app restart
3. **Test Scheduling**: Set time to 1-2 minutes in future to verify execution
4. **Monitor Dashboard**: Check `/hangfire` for job status
5. **Verify Email**: Confirm alerts send at new time

---

## Questions or Issues?

If the minute setting isn't being picked up:
1. Verify AppSettings table has `CertVerificationMinute` row
2. Check app startup logs for cron expression
3. Restart app to reload settings from database
4. Monitor Hangfire dashboard for job schedule

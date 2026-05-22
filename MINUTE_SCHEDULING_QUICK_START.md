📋 MINUTE-LEVEL SCHEDULING - QUICK START GUIDE
================================================

✅ IMPLEMENTATION COMPLETE

---

## What Changed?

Previously, you could only schedule certificate verification by the hour:
- 8:00 AM, 9:00 AM, 10:00 AM, etc.

Now you can schedule at any specific time down to the minute:
- 8:00 AM, 8:15 AM, 8:30 AM, 8:45 AM, etc.
- Perfect for testing (set to 1-2 minutes from now)!

---

## Quick Setup

### 1️⃣ Add Database Setting
Run one of these SQL scripts (depending on your environment):

**Development:**
```
SeifDigital/sql/AddCertVerificationMinute_Dev.sql
```

**LIVE:**
```
SeifDigital/sql/AddCertVerificationMinute_Live.sql
```

These scripts add a new `CertVerificationMinute` setting (default: 0)

### 2️⃣ Restart Application
The new minute setting will be loaded on app startup

### 3️⃣ Open Certificate Settings
Go to: **Admin Settings > Certificate Settings**

You'll now see a combined time input:
```
[14] : [30]
 ↑     ↑
Hour  Minute
```

---

## Usage Examples

### Testing (Run in Next 2 Minutes)
Current time: 14:25

Set: Hour=14, Minute=27

✅ Job will run at 14:27 (2 minutes from now)

### Production (Run Every Day at 8:15 AM)
Set: Hour=8, Minute=15

✅ Job runs at 08:15 every single day

### Late Night Check (02:30 AM)
Set: Hour=2, Minute=30

✅ Job runs at 02:30 every night

---

## Files Modified

✅ **SeifDigital/Services/CertificateSettingsService.cs**
   - Added: GetVerificationMinuteAsync()
   - Added: SetVerificationMinuteAsync()
   - Updated: GetAllSettingsAsync()
   - Updated: SaveAllSettingsAsync()
   - Updated: CertificateSettingsViewModel (added VerificationMinute property)

✅ **SeifDigital/Views/CertificateSettings/Index.cshtml**
   - Changed hour input to combined hour:minute input
   - Updated summary display to show HH:MM format
   - Updated JavaScript to handle minute changes

✅ **SeifDigital/Program.cs**
   - Updated Hangfire cron expression to include minutes
   - Now reads minute setting from database
   - Format changed from "0 {hour} * * *" to "{minute} {hour} * * *"

✅ **SQL Scripts (NEW)**
   - SeifDigital/sql/AddCertVerificationMinute_Dev.sql
   - SeifDigital/sql/AddCertVerificationMinute_Live.sql

---

## Validation Rules

### Hour (HH)
- Range: 0-23 (24-hour format)
- Examples: 0 (midnight), 8 (8 AM), 14 (2 PM), 23 (11 PM)

### Minute (MM)
- Range: 0-59
- Examples: 0 (:00), 15 (:15), 30 (:30), 45 (:45)

---

## Hangfire CRON Expressions

| Time | CRON | Hour | Minute |
|------|------|------|--------|
| 08:00 | `0 8 * * *` | 8 | 0 |
| 08:15 | `15 8 * * *` | 8 | 15 |
| 08:30 | `30 8 * * *` | 8 | 30 |
| 14:30 | `30 14 * * *` | 14 | 30 |
| 23:45 | `45 23 * * *` | 23 | 45 |

---

## ⚠️ Important Notes

1. **Job Reschedules on App Restart**
   - Changes to minute setting take effect when app restarts
   - Hangfire updates the schedule automatically

2. **Default Minute is 0**
   - If you don't set a minute, it defaults to :00 (top of hour)
   - Maintains backward compatibility with old hour-only settings

3. **Database Must Be Updated First**
   - Execute SQL scripts before restarting app
   - Otherwise app will use default minute (0)

4. **Hangfire Dashboard Shows New Schedule**
   - Check `/hangfire` to verify CRON expression
   - Look for job "certificate-check" to see the updated schedule

---

## Testing Checklist

- [ ] Executed SQL script on database
- [ ] Restarted application
- [ ] Opened Certificate Settings page
- [ ] Hour:minute input visible and working
- [ ] Set time to 1-2 minutes in future
- [ ] Verified job executed at the scheduled time
- [ ] Checked Hangfire dashboard for correct CRON
- [ ] Confirmed alert was triggered (if certificates in scope)

---

## Rollback (If Needed)

1. Restore previous version of code files
2. Run: `DELETE FROM AppSettings WHERE SettingKey = 'CertVerificationMinute'`
3. Restart app
4. Job reverts to hour-only scheduling

---

## Need Help?

📋 Full documentation: See **MINUTE_SCHEDULING_IMPLEMENTATION.md**

Common issues:
- **Settings not loading?** → Check SQL script executed successfully
- **Job not running?** → Verify Hangfire dashboard shows correct CRON
- **Time format wrong?** → Make sure minute is 0-59

---

Build Status: ✅ SUCCESSFUL (No compilation errors)
Backward Compatibility: ✅ MAINTAINED (All existing jobs still work)
Ready to Deploy: ✅ YES

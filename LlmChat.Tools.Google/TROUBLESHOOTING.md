# Google Calendar 400 Bad Request Troubleshooting Guide

## ?? **MOST LIKELY ISSUE: API Not Enabled**

**If you're getting a 400 Bad Request error, the #1 cause is:**

### ? **SOLUTION: Enable Google Calendar API**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project
3. Navigate to **"APIs & Services" > "Library"**
4. Search for **"Google Calendar API"**
5. Click on it and click **"ENABLE"**

**This fixes 90% of 400 Bad Request issues!**

### ?? Quick Diagnosis Tools

Run these scripts to diagnose your issue:
```powershell
.\quick-diagnosis.ps1          # Quick check
.\run-google-diagnostics.ps1   # Full diagnostics  
```

---

## Common Causes and Solutions

### 1. **Google Calendar API Not Enabled**
**Most Common Cause**

**Steps to fix:**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project
3. Navigate to "APIs & Services" > "Library"
4. Search for "Google Calendar API"
5. Click on it and click "Enable"

### 2. **OAuth Consent Screen Not Configured**

**Steps to fix:**
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to "APIs & Services" > "OAuth consent screen"
3. Configure the consent screen:
   - **User Type**: Choose "External" for personal use or "Internal" for organization
   - **App name**: "LlmChat Agent" (or your preferred name)
   - **User support email**: Your email
   - **Developer contact email**: Your email
4. Add scopes:
   - Click "Add or Remove Scopes"
   - Search for "calendar" and add "Google Calendar API" with ".../auth/calendar.readonly" scope
5. **Important**: If using "External", you need to either:
   - Publish your app (for production), OR
   - Add test users in the "Test users" section (for development)

### 3. **OAuth Client Configuration Issues**

**Steps to verify/fix:**
1. Go to "APIs & Services" > "Credentials"
2. Click on your OAuth 2.0 Client ID
3. Verify the configuration:
   - **Application type**: Desktop application
   - **Authorized redirect URIs**: Should include:
     - `http://localhost`
     - `urn:ietf:wg:oauth:2.0:oob`

### 4. **Test User Configuration (for External Apps)**

If your OAuth consent screen is set to "External" and not published:
1. Go to "OAuth consent screen"
2. Click "Add Users" in the "Test users" section
3. Add the Google account you're trying to authenticate with

### 5. **Clear Cached Tokens**

Sometimes old/invalid tokens cause issues:
1. Close the application
2. Delete the token storage folder:
   - Windows: `%APPDATA%\LlmChat.GoogleAuth`
   - Or look for a folder named "LlmChat.GoogleAuth" in your user directory
3. Restart the application and re-authenticate

## Quick Verification Steps

### Check if API is Working:
```powershell
# Test with curl (replace ACCESS_TOKEN with a valid token)
curl -H "Authorization: Bearer ACCESS_TOKEN" "https://www.googleapis.com/calendar/v3/calendars/primary/events"
```

### Check Your Configuration:
1. Verify your user secrets are set:
   ```bash
   cd LlmChat.Tools.Google
   dotnet user-secrets list
   ```

2. Should show:
   ```
   Google:ClientId = your-client-id
   Google:ClientSecret = your-client-secret
   ```

## Error-Specific Solutions

### "invalid_client" Error:
- Double-check your Client ID and Client Secret
- Ensure they match exactly what's in Google Cloud Console

### "access_denied" Error:
- User denied permission during OAuth flow
- Try the authentication flow again
- Check if the required scopes are configured in OAuth consent screen

### "redirect_uri_mismatch" Error:
- Add the correct redirect URIs to your OAuth client configuration
- For desktop apps, include: `http://localhost` and `urn:ietf:wg:oauth:2.0:oob`

## Testing the Setup

1. **Enable detailed logging** in your `appsettings.json`:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "LlmChat.Tools.Google": "Debug"
       }
     }
   }
   ```

2. **Run the application** and check the logs for detailed error information.

3. **Try a simple query** like "Show me today's calendar events"

## Still Having Issues?

If you're still getting 400 errors after following these steps:

1. **Check the application logs** - the enhanced error handling will provide more specific information
2. **Verify project quotas** - ensure your Google Cloud project hasn't exceeded API quotas
3. **Try creating a new OAuth client** - sometimes the existing client configuration gets corrupted
4. **Test with a different Google account** - to rule out account-specific issues

## Complete Setup Checklist

- [ ] Google Cloud project created
- [ ] Google Calendar API enabled
- [ ] OAuth consent screen configured
- [ ] OAuth 2.0 client credentials created (Desktop application)
- [ ] Correct redirect URIs configured
- [ ] Test users added (if using External consent screen)
- [ ] Client ID and Secret configured in user secrets
- [ ] First OAuth authentication completed successfully
- [ ] Application has necessary permissions to read calendar
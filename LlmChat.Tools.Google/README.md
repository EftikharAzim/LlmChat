# Google Calendar Configuration Guide

## Setting up User Secrets

Your project is already configured to use user secrets. Here's how to set up your Google Calendar credentials:

### Method 1: Using User Secrets (Recommended for Development)

1. Navigate to the LlmChat.Tools.Google project directory:
   ```bash
   cd LlmChat.Tools.Google
   ```

2. Set your Google Client ID:
   ```bash
   dotnet user-secrets set "Google:ClientId" "your-actual-client-id.apps.googleusercontent.com"
   ```

3. Set your Google Client Secret:
   ```bash
   dotnet user-secrets set "Google:ClientSecret" "your-actual-client-secret"
   ```

### Method 2: Using credentials.json File

1. Replace the template `credentials.json` file in the `LlmChat.Tools.Google` directory with your actual credentials from the Google Cloud Console.

2. The file should look like this:
   ```json
   {
     "installed": {
       "client_id": "your-actual-client-id.apps.googleusercontent.com",
       "project_id": "your-project-id",
       "auth_uri": "https://accounts.google.com/o/oauth2/auth",
       "token_uri": "https://oauth2.googleapis.com/token",
       "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
       "client_secret": "your-actual-client-secret",
       "redirect_uris": ["urn:ietf:wg:oauth:2.0:oob", "http://localhost"]
     }
   }
   ```

### Method 3: Using appsettings.json (Not Recommended for Production)

Update the `appsettings.json` file in the `LlmChat.Wpf` project:
```json
{
  "Google": {
    "ClientId": "your-actual-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-actual-client-secret"
  }
}
```

## Getting Google Calendar API Credentials

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Google Calendar API
4. Go to "Credentials" and create OAuth 2.0 client credentials
5. Configure the OAuth consent screen
6. Download the credentials as JSON or copy the Client ID and Client Secret

## How It Works

The `GoogleCalendarService` class will try to get credentials in this order:
1. User secrets (Google:ClientId and Google:ClientSecret)
2. credentials.json file
3. appsettings.json configuration
4. Environment variables

If no credentials are found, it will throw an informative error message with setup instructions.

## First Run

On the first run, the application will:
1. Open a browser window for OAuth authentication
2. Ask you to sign in to your Google account
3. Ask for permission to access your calendar (read-only)
4. Store the authentication token for future use

The OAuth token is stored securely and will be reused for subsequent runs.
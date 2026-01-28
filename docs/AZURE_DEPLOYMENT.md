# Azure Deployment Guide

This guide covers deploying the Agent Orchestrator to Azure for use with Microsoft Teams and Microsoft 365 Copilot.

## Prerequisites

- Azure CLI installed (`az --version`)
- Azure subscription with permissions to create resources
- .NET 10 SDK installed locally
- Your existing configuration (Azure AD app, Azure OpenAI resource)

## Architecture

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────────────┐
│   Teams /   │────▶│  Azure Bot       │────▶│  Azure App Service  │
│   Copilot   │◀────│  Service         │◀────│  (Your .NET Agent)  │
└─────────────┘     └──────────────────┘     └─────────────────────┘
                           │                          │
                           │                          ▼
                           │                 ┌─────────────────────┐
                           │                 │  Azure OpenAI       │
                           │                 │  Microsoft Graph    │
                           └────────────────▶│  (M365 Data)        │
                                             └─────────────────────┘
```

## Step 1: Create Azure Resources

```bash
# Login to Azure
az login

# Set variables (customize these)
RESOURCE_GROUP="rg-agent-orchestrator"
LOCATION="eastus"
APP_NAME="agent-orchestrator-app"
BOT_NAME="agent-orchestrator-bot"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create App Service plan (Linux, B1 tier)
az appservice plan create \
  --name plan-agent-orchestrator \
  --resource-group $RESOURCE_GROUP \
  --sku B1 \
  --is-linux

# Create Web App with .NET 10 runtime
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan plan-agent-orchestrator \
  --runtime "DOTNETCORE:10.0"
```

## Step 2: Create Azure Bot

### Option A: Azure Portal (Recommended)

1. Go to **Azure Portal** → **Create a resource** → Search **"Azure Bot"**
2. Fill in:
   - **Bot handle**: `agent-orchestrator-bot`
   - **Resource group**: `rg-agent-orchestrator`
   - **Pricing tier**: F0 (free) or S1 (standard)
   - **Type of App**: Multi Tenant
   - **Creation type**: Create new Microsoft App ID
3. Click **Review + Create** → **Create**
4. After creation, note the **Microsoft App ID** from the Configuration blade

### Option B: Azure CLI

```bash
# Create the bot (requires Bot Service extension)
az bot create \
  --resource-group $RESOURCE_GROUP \
  --name $BOT_NAME \
  --kind azurebot \
  --sku F0 \
  --app-type MultiTenant
```

## Step 3: Get Bot Credentials

1. Go to **Azure Portal** → Your Azure Bot → **Configuration**
2. Copy the **Microsoft App ID** (this is `BOT_APP_ID`)
3. Click **Manage Password** next to Microsoft App ID
4. Go to **Certificates & secrets** → **New client secret**
5. Copy the secret value (this is `BOT_CLIENT_SECRET`)

## Step 4: Configure Messaging Endpoint

1. Go to **Azure Bot** → **Configuration**
2. Set **Messaging endpoint** to:
   ```
   https://<APP_NAME>.azurewebsites.net/api/messages
   ```
   Example: `https://agent-orchestrator-app.azurewebsites.net/api/messages`
3. Click **Apply**

## Step 5: Add Microsoft Teams Channel

1. Go to **Azure Bot** → **Channels**
2. Click **Microsoft Teams**
3. Accept the Terms of Service
4. Click **Apply**

## Step 6: Configure Application Settings

In **Azure Portal** → **App Service** → **Configuration** → **Application settings**, add:

### Bot Service Connection
| Name | Value |
|------|-------|
| `Connections__BotServiceConnection__Settings__ClientId` | `<BOT_APP_ID>` |
| `Connections__BotServiceConnection__Settings__ClientSecret` | `<BOT_CLIENT_SECRET>` |
| `Connections__BotServiceConnection__Settings__TenantId` | `<YOUR_TENANT_ID>` |

### Azure AD (for user authentication)
| Name | Value |
|------|-------|
| `AzureAd__TenantId` | `<YOUR_TENANT_ID>` |
| `AzureAd__ClientId` | `<YOUR_AAD_APP_CLIENT_ID>` |
| `AzureAd__ClientSecret` | `<YOUR_AAD_APP_SECRET>` |
| `AzureAd__RedirectUri` | `https://<APP_NAME>.azurewebsites.net/auth/callback` |

### Azure OpenAI
| Name | Value |
|------|-------|
| `AzureOpenAI__Endpoint` | `https://<resource>.openai.azure.com/` |
| `AzureOpenAI__ApiKey` | `<YOUR_OPENAI_API_KEY>` |
| `AzureOpenAI__DeploymentName` | `gpt-4o` |

Click **Save** after adding all settings.

## Step 7: Update Azure AD App Registration

Add the new redirect URI to your Azure AD app:

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations**
2. Select your app
3. Go to **Authentication** → **Add a platform** or edit existing
4. Add redirect URI: `https://<APP_NAME>.azurewebsites.net/auth/callback`
5. Save

## Step 8: Deploy the Application

```bash
cd /Users/tk/src/chat_api_lab_private/src/AgentOrchestrator

# Publish the application
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --src deploy.zip

# Restart the app
az webapp restart --resource-group $RESOURCE_GROUP --name $APP_NAME
```

## Step 9: Test in Web Chat

1. Go to **Azure Portal** → **Azure Bot** → **Test in Web Chat**
2. Send a test message
3. Verify you get a response

## Step 10: Create Teams App Package

### Edit the manifest

Edit `src/AgentOrchestrator/appPackage/manifest.json`:

Replace the placeholders:
- `{{BOT_APP_ID}}` → Your Bot's Microsoft App ID
- `{{BOT_DOMAIN}}` → Your app domain (e.g., `agent-orchestrator-app.azurewebsites.net`)

### Create the package

```bash
cd /Users/tk/src/chat_api_lab_private/src/AgentOrchestrator/appPackage

# Create the zip file
zip manifest.zip manifest.json color.png outline.png
```

## Step 11: Deploy to Microsoft Teams

### Option A: Teams Admin Center (Organization-wide)

1. Go to [Microsoft Teams Admin Center](https://admin.teams.microsoft.com)
2. Navigate to **Teams apps** → **Manage apps**
3. Click **Upload new app**
4. Upload `manifest.zip`
5. The app will be available to users in your organization

### Option B: Sideload for Testing (Personal)

1. Open **Microsoft Teams**
2. Go to **Apps** → **Manage your apps**
3. Click **Upload an app** → **Upload a custom app**
4. Select `manifest.zip`

## Step 12: Test in Teams

1. Open **Microsoft Teams**
2. Go to **Apps** → Search for "Agent Orchestrator"
3. Click **Add** to add it to your personal chat
4. Start a conversation with the bot

## Viewing Logs

### Log Stream (Real-time)

```bash
az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME
```

### Azure Portal

1. Go to **App Service** → **Log stream**
2. Or go to **App Service** → **Diagnose and solve problems**

### Application Insights (Recommended for Production)

1. Go to **App Service** → **Application Insights** → **Turn on**
2. View logs in **Application Insights** → **Logs**

## Troubleshooting

### Bot returns no response

1. Check **Log stream** for errors
2. Verify **Messaging endpoint** is correct in Azure Bot
3. Ensure app settings are configured correctly

### Authentication errors

1. Verify `ClientId` and `ClientSecret` match Azure Bot configuration
2. Check `TenantId` is correct
3. Ensure redirect URIs are added to Azure AD app

### M365 features not working

1. User must authenticate via the web UI first
2. Check Azure AD app has required Graph permissions
3. Verify admin consent was granted for all permissions

## Teams SSO Configuration

For Teams Single Sign-On to work properly, you need additional configuration.

### Step A: Configure Bot Service OAuth Connection

1. Go to **Azure Portal** → **Azure Bot** → **Settings** → **OAuth Connection Settings**
2. Click **Add Setting**
3. Configure:
   - **Name**: `GraphConnection` (must match code)
   - **Service Provider**: `Azure Active Directory v2`
   - **Client ID**: Your Azure AD App Client ID
   - **Client Secret**: Your Azure AD App Client Secret
   - **Tenant ID**: Your Tenant ID
   - **Scopes**: `openid profile email User.Read Mail.Read Calendars.Read Files.Read.All`
4. Click **Save**
5. Click **Test Connection** to verify

### Step B: Configure Azure AD App for SSO

1. Go to **Azure Portal** → **App registrations** → Your app
2. Under **Expose an API**:
   - Set **Application ID URI**: `api://your-app-name.azurewebsites.net/{client-id}`
   - Add scope: `access_as_user` (Admins and users can consent)
3. Under **Authorized client applications**, add:
   - `1fec8e78-bce4-4aaf-ab1b-5451cc387264` (Teams desktop/web)
   - `5e3ce6c0-2b1f-4285-8d4b-75ee78787346` (Teams mobile)
4. Under **Authentication**:
   - Enable **ID tokens** and **Access tokens** under Implicit grant

### Step C: Update Teams Manifest

Ensure your `manifest.json` includes:

```json
{
  "validDomains": [
    "your-app-name.azurewebsites.net",
    "token.botframework.com",
    "login.microsoftonline.com"
  ],
  "webApplicationInfo": {
    "id": "your-azure-ad-client-id",
    "resource": "api://your-app-name.azurewebsites.net/your-azure-ad-client-id"
  }
}
```

### Common Teams SSO Issues

| Issue | Solution |
|-------|----------|
| "Something went wrong" on sign-in | Check OAuth connection name matches code (`GraphConnection`) |
| Sign-in doesn't return to Teams | Add redirect URI `https://token.botframework.com/.auth/web/redirect` to Azure AD app |
| Token exchange fails | Verify SSO scopes match between Bot OAuth and Azure AD app |

## Cost Estimation

| Resource | SKU | Estimated Monthly Cost |
|----------|-----|------------------------|
| App Service Plan | B1 | ~$13 |
| Azure Bot | F0 | Free (10K messages) |
| Azure Bot | S1 | ~$0.50 per 1K messages |
| Azure OpenAI | Pay-as-you-go | Variable |

## Next Steps

- Enable **Application Insights** for monitoring
- Set up **CI/CD** with GitHub Actions or Azure DevOps
- Configure **custom domain** and SSL
- Add **Microsoft 365 Copilot** channel when available

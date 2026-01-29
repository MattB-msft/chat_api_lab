# Quick Start Guide

Get the Agent Orchestrator running in Azure with Teams integration in under 30 minutes.

> **Local Development?** See the [README](README.md#quick-start) for running locally with `dotnet run`.

## Prerequisites Checklist

Before starting, ensure you have:

- [ ] Azure CLI installed (`az --version`)
- [ ] .NET 10 SDK installed (`dotnet --version`)
- [ ] Azure subscription with permissions to create resources
- [ ] Microsoft 365 tenant with Copilot license
- [ ] Azure AD app registration (see [Prerequisites](docs/self-paced/PREREQUISITES.md))

## Step 1: Collect Your Configuration Values

You'll need these values from Azure Portal:

| Setting | Where to find it |
|---------|------------------|
| `TENANT_ID` | Azure Portal → Microsoft Entra ID → Overview |
| `AAD_CLIENT_ID` | Azure Portal → App registrations → Your app → Overview |
| `AAD_CLIENT_SECRET` | Azure Portal → App registrations → Your app → Certificates & secrets |
| `OPENAI_ENDPOINT` | Azure Portal → Azure OpenAI → Your resource → Keys and Endpoint |
| `OPENAI_KEY` | Azure Portal → Azure OpenAI → Your resource → Keys and Endpoint |
| `OPENAI_DEPLOYMENT` | Azure OpenAI Studio → Deployments (e.g., `gpt-4o`) |
| `BOT_CLIENT_ID` | Azure Portal → Azure Bot → Configuration → Microsoft App ID |
| `BOT_CLIENT_SECRET` | Azure Portal → Azure Bot → Configuration → Manage Password |

## Step 2: One-Command Deployment

Run this single command (replace placeholders with your values):

```bash
./scripts/deploy.sh \
  --app-name my-agent-app \
  --resource-group rg-my-agent \
  --create-resources \
  --configure-settings \
  --openai-endpoint "https://YOUR-RESOURCE.openai.azure.com/" \
  --openai-deployment "gpt-4o" \
  --openai-key "YOUR-OPENAI-KEY" \
  --aad-tenant-id "YOUR-TENANT-ID" \
  --aad-client-id "YOUR-AAD-CLIENT-ID" \
  --aad-client-secret "YOUR-AAD-SECRET" \
  --bot-client-id "YOUR-BOT-CLIENT-ID" \
  --bot-client-secret "YOUR-BOT-SECRET" \
  --bot-tenant-id "YOUR-TENANT-ID" \
  --package-manifest
```

This will:
1. ✅ Create Azure resource group and App Service
2. ✅ Build and deploy the .NET application
3. ✅ Configure all app settings
4. ✅ Create Teams manifest package

## Step 3: Configure Azure Bot Messaging Endpoint

1. Go to **Azure Portal** → **Azure Bot** → **Configuration**
2. Set **Messaging endpoint** to:
   ```
   https://my-agent-app.azurewebsites.net/api/messages
   ```
3. Click **Apply**

## Step 4: Add Redirect URI to Azure AD App

1. Go to **Azure Portal** → **App registrations** → Your app → **Authentication**
2. Add redirect URI:
   ```
   https://my-agent-app.azurewebsites.net/auth/callback
   ```
3. Click **Save**

## Step 5: Deploy Teams App

1. Go to [Teams Admin Center](https://admin.teams.microsoft.com)
2. Navigate to **Teams apps** → **Manage apps** → **Upload new app**
3. Upload the generated `src/AgentOrchestrator/appPackage/manifest.zip`
4. Wait for approval/deployment (may take a few minutes)

## Step 6: Test

### Test Web Interface
1. Open `https://my-agent-app.azurewebsites.net`
2. Click **Login with Microsoft**
3. Send a test message: "What meetings do I have tomorrow?"

### Test in Teams
1. Open Microsoft Teams
2. Go to **Apps** → Search for your app name
3. Click **Add** → **Add for me**
4. Start chatting!

## Troubleshooting

### "Something went wrong" when signing in from Teams

Check:
- Azure Bot OAuth connection `GraphConnection` is configured
- Valid domains in manifest include `token.botframework.com` and `login.microsoftonline.com`
- Admin consent granted for all API permissions

### 403 Forbidden from Copilot API

Check:
- User has Microsoft 365 Copilot license assigned
- All required Graph permissions granted (see [Prerequisites](docs/self-paced/PREREQUISITES.md))
- Admin consent granted

### Bot not responding

Check:
- Messaging endpoint URL is correct in Azure Bot Configuration
- App Service is running (`az webapp log tail --resource-group rg-my-agent --name my-agent-app`)

## Next Steps

- Review [Full Azure Deployment Guide](docs/AZURE_DEPLOYMENT.md)
- Complete the [Self-Paced Lab](docs/self-paced/LAB_GUIDE.md)
- See [Troubleshooting Guide](docs/self-paced/TROUBLESHOOTING.md)

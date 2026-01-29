# Local Development Quick Start

Run the Agent Orchestrator locally in under 10 minutes.

> **Deploying to Azure?** See [QUICK_START.md](QUICK_START.md) for Azure deployment with Teams integration.

## Prerequisites

- [ ] .NET 10 SDK installed (`dotnet --version`)
- [ ] Azure AD app registration ([setup guide](docs/self-paced/PREREQUISITES.md#3-register-azure-ad-application))
- [ ] Azure OpenAI resource with a deployed model
- [ ] Microsoft 365 Copilot license (for M365 features)

## Step 1: Collect Your Configuration Values

| Setting | Where to find it |
|---------|------------------|
| `TENANT_ID` | Azure Portal → Microsoft Entra ID → Overview |
| `CLIENT_ID` | Azure Portal → App registrations → Your app → Overview |
| `CLIENT_SECRET` | Azure Portal → App registrations → Your app → Certificates & secrets |
| `OPENAI_ENDPOINT` | Azure Portal → Azure OpenAI → Keys and Endpoint |
| `OPENAI_KEY` | Azure Portal → Azure OpenAI → Keys and Endpoint |
| `OPENAI_DEPLOYMENT` | Azure OpenAI Studio → Deployments (e.g., `gpt-4o`) |

## Step 2: Add Redirect URI

1. Go to **Azure Portal** → **App registrations** → Your app → **Authentication**
2. Add redirect URI: `http://localhost:5001/auth/callback`
3. Click **Save**

## Step 3: Run Setup Script

```bash
./scripts/setup-local.sh
```

The script will prompt for your configuration values and set up .NET user secrets.

**Or configure manually:**

```bash
cd src/AgentOrchestrator

# Initialize user secrets
dotnet user-secrets init

# Azure AD (for authentication)
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "AzureAd:ClientSecret" "your-secret"

# Azure OpenAI
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
```

## Step 4: Run the Application

```bash
cd src/AgentOrchestrator
dotnet run --urls "http://localhost:5001"
```

## Step 5: Test

1. Open **http://localhost:5001** in your browser
2. Click **Login with Microsoft**
3. Complete the Microsoft login
4. Try these queries:
   - "What meetings do I have tomorrow?" (M365)
   - "Summarize my recent emails" (M365)
   - "Explain what microservices are" (General knowledge)

## Troubleshooting

### "Can't connect to localhost:5001"

Port 5000 is used by macOS AirPlay Receiver. We use 5001 instead. If 5001 is also busy:

```bash
dotnet run --urls "http://localhost:5002"
```

### "AADSTS50011: Reply URL mismatch"

Add the exact redirect URI to your Azure AD app:
- `http://localhost:5001/auth/callback`

### "No token found for session"

Clear browser cookies and login again. The session may have expired.

### Azure OpenAI 401/403 errors

- Verify API key is correct
- Check endpoint URL includes trailing slash
- Confirm deployment name matches exactly

### M365 features return errors

- Ensure you have a Microsoft 365 Copilot license
- Grant admin consent for all API permissions
- Check all required scopes are configured (see [Prerequisites](docs/self-paced/PREREQUISITES.md))

## What Works Locally

| Feature | Local | Notes |
|---------|-------|-------|
| Web UI chat | ✅ | Full functionality |
| Microsoft login | ✅ | Requires redirect URI setup |
| Azure OpenAI | ✅ | General knowledge queries |
| M365 Copilot API | ✅ | Requires Copilot license |
| Teams bot | ❌ | Requires Azure deployment |
| Bot Framework | ❌ | Requires Azure Bot Service |

## Next Steps

- [Deploy to Azure](QUICK_START.md) for Teams integration
- [Full Lab Guide](docs/self-paced/LAB_GUIDE.md) to understand the architecture
- [Design Document](DESIGN.md) for technical details

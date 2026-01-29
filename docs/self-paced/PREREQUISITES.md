# Prerequisites

Complete these steps before starting the lab.

## Architecture Overview

This lab builds a .NET 10 Agent using:
- **Microsoft 365 Agents SDK** - Enterprise agent framework with multi-channel support
- **Semantic Kernel** - AI orchestration with plugin pattern
- **M365 Copilot Chat API** - Delegated access to Microsoft 365 data

## Required Licenses & Access

### Microsoft 365
- [ ] Microsoft 365 tenant (E3/E5 or equivalent)
- [ ] Microsoft 365 Copilot license assigned to your user account
- [ ] Global Admin or Application Administrator role (for app registration)

### Azure
- [ ] Active Azure subscription
- [ ] Azure OpenAI Service access (requires application approval)
- [ ] Ability to create Azure AD app registrations

## Development Environment

### Required Software
- [ ] [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [ ] Code editor (Visual Studio 2025, VS Code with C# Dev Kit, or JetBrains Rider)
- [ ] Modern web browser (Microsoft Edge or Google Chrome)
- [ ] [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) (optional, for resource management)

### Verify .NET Installation
```bash
dotnet --version
# Should output: 10.0.x
```

### NuGet Packages (Reference)
The lab uses these key packages (automatically restored):
```xml
<!-- Microsoft 365 Agents SDK -->
<PackageReference Include="Microsoft.Agents.Hosting.AspNetCore" Version="1.1.151" />
<PackageReference Include="Microsoft.Agents.Builder" Version="1.1.151" />

<!-- Semantic Kernel -->
<PackageReference Include="Microsoft.SemanticKernel" Version="1.54.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureOpenAI" Version="1.54.0" />
```

## Azure Resource Setup

### 1. Create Azure OpenAI Resource

1. Go to [Azure Portal](https://portal.azure.com)
2. Create a new **Azure OpenAI** resource
3. Select a region that supports GPT-4o
4. Wait for deployment to complete
5. Navigate to the resource and note:
   - **Endpoint URL**: `https://<your-resource>.openai.azure.com/`
   - **API Key**: Found under "Keys and Endpoint"

### 2. Deploy GPT-4o Model

1. In your Azure OpenAI resource, go to **Model deployments**
2. Click **Create new deployment**
3. Select model: `gpt-4o`
4. Deployment name: `gpt-4o` (or custom name)
5. Click **Create**

### 3. Register Azure AD Application

1. Go to [Azure Portal](https://portal.azure.com) > **Microsoft Entra ID**
2. Navigate to **App registrations** > **New registration**
3. Configure:
   - **Name**: `Agent Orchestrator Lab`
   - **Supported account types**: Accounts in this organizational directory only
   - **Redirect URI**: Web - `http://localhost:5000/auth/callback`
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**

### 4. Configure API Permissions

In your app registration:

1. Go to **API permissions** > **Add a permission**
2. Select **Microsoft Graph** > **Delegated permissions**
3. Add these permissions:

   **Core permissions:**
   - `openid`
   - `profile`
   - `email`
   - `User.Read`

   **M365 data access (for Copilot context):**
   - `Mail.Read`
   - `Calendars.Read`
   - `Files.Read.All`
   - `Sites.Read.All`
   - `People.Read.All` - Required for people/org queries

   **Copilot Chat API permissions:**
   - `Chat.Read` - Required for reading Copilot chat responses
   - `ChannelMessage.Read.All` - Required for Teams channel context
   - `OnlineMeetingTranscript.Read.All` - Required for meeting transcript context
   - `ExternalItem.Read.All` - Required for external data source context

4. Click **Grant admin consent for [your tenant]**

> **Important**: All 7 M365/Copilot permissions are required for the Copilot Chat API to function correctly. Missing any of these will result in a `403 Forbidden` error with a message listing the required scopes.

> **Note**: The Copilot Chat API uses delegated permissions, meaning it operates in the context of the signed-in user and respects their M365 permissions.

### 5. Create Client Secret

1. Go to **Certificates & secrets** > **New client secret**
2. Description: `Lab Secret`
3. Expiration: Choose appropriate duration
4. Click **Add**
5. **IMPORTANT**: Copy the secret value immediately (it won't be shown again)

## Configuration Checklist

After completing setup, you should have:

| Item | Value |
|------|-------|
| Azure AD Tenant ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| Azure AD Client ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| Azure AD Client Secret | `your-secret-value` |
| Azure OpenAI Endpoint | `https://your-resource.openai.azure.com/` |
| Azure OpenAI API Key | `your-api-key` |
| Azure OpenAI Deployment | `gpt-4o` |

## Microsoft 365 Copilot Chat API Setup

The Chat API is the key integration point for this lab. This section ensures you have proper access.

### Understanding the Chat API

The M365 Copilot Chat API allows your application to programmatically interact with Microsoft 365 Copilot:

| Aspect | Detail |
|--------|--------|
| **API Endpoint** | `https://graph.microsoft.com/beta/copilot/conversations` |
| **SDK** | Kiota-generated CopilotSdk (included in project) |
| **API Status** | Beta (subject to change) |
| **Auth Type** | Delegated (user context) |
| **License Required** | Microsoft 365 Copilot per user |

### Step 1: Verify Copilot License

1. **Check in Microsoft 365 Admin Center:**
   - Go to https://admin.microsoft.com
   - Navigate to **Users** > **Active users**
   - Select your user account
   - Click **Licenses and apps**
   - Verify **Microsoft 365 Copilot** is enabled

2. **Verify in Teams (end-user check):**
   - Open Microsoft Teams
   - Look for **Copilot** in the left sidebar
   - Try chatting with Copilot to confirm it works

If you don't see Copilot:
- Contact your IT administrator to assign a Copilot license
- License propagation can take up to 24 hours
- Some tenants require admin enablement of Copilot features

### Step 2: Test Chat API Access with Graph Explorer

Before running the lab, verify the API works for your account:

1. Go to [Graph Explorer](https://developer.microsoft.com/graph/graph-explorer)

2. Sign in with your M365 account (the one with the Copilot license)

3. **Test creating a conversation:**
   ```
   POST https://graph.microsoft.com/beta/copilot/conversations
   Body: {}
   ```

   Expected response:
   ```json
   {
     "id": "abc123-...",
     "@odata.type": "#microsoft.graph.copilotConversation"
   }
   ```

4. **Test sending a chat message** (use the conversation ID from step 3):
   ```
   POST https://graph.microsoft.com/beta/copilot/conversations/{conversationId}/chat
   Body: { "message": { "text": "Hello" }, "locationHint": { "timeZone": "UTC" } }
   ```

   The response will include Copilot's reply directly.

### Common Chat API Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| `403 Forbidden` | Missing Copilot license | Assign license in Admin Center |
| `403 Forbidden` with "Required scopes" message | Missing API permissions | Add all required permissions (see section 4), grant admin consent |
| `403 Forbidden` | Insufficient permissions | Verify all 7 M365/Copilot scopes are granted with admin consent |
| `404 Not Found` | Wrong endpoint | Ensure using `/beta/copilot/conversations` |
| Empty response | Query not understood | Try rephrasing; check Copilot can access the data |
| `401 Unauthorized` | Token expired | Re-authenticate |
| Timeout errors | Copilot API slow response | API can take 10-30 seconds; ensure adequate timeouts |

> **Debugging 403 errors**: The Copilot API response body contains the exact list of required scopes. Check the response body (not just the status code) to see which scopes are missing.

### Chat API vs Direct M365 APIs

This lab uses the **Chat API** rather than direct Graph APIs (like `/me/messages`) because:

| Chat API Approach | Direct Graph API Approach |
|-------------------|--------------------------|
| Natural language queries | Structured API calls |
| Copilot reasons over data | You process raw data |
| Cross-workload context | Single workload per call |
| Built-in summarization | You build summarization |
| Semantic understanding | Keyword/filter matching |

The Chat API lets Copilot handle the complexity of understanding, retrieving, and summarizing M365 data.

## Network Requirements

Ensure your network allows outbound connections to:
- `login.microsoftonline.com` (authentication)
- `graph.microsoft.com` (Microsoft Graph API)
- `*.openai.azure.com` (Azure OpenAI)

## Next Steps

Once all prerequisites are complete, proceed to the [Lab Guide](LAB_GUIDE.md).

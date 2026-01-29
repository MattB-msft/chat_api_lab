# Troubleshooting Guide

This guide covers common issues when running the .NET 10 Agent built with Microsoft 365 Agents SDK and Semantic Kernel.

## Authentication Issues

### "AADSTS50011: The reply URL specified in the request does not match"

**Cause**: Redirect URI mismatch between app registration and application.

**Solution**:
1. Go to Azure Portal > App registrations > Your app
2. Under "Authentication", verify redirect URI is exactly: `http://localhost:5000/auth/callback`
3. Ensure the protocol (http vs https) matches

### "AADSTS65001: The user or administrator has not consented"

**Cause**: API permissions not granted admin consent.

**Solution**:
1. Go to Azure Portal > App registrations > Your app > API permissions
2. Click "Grant admin consent for [tenant]"
3. Verify all permissions show green checkmarks

### "Invalid client secret"

**Cause**: Client secret expired or incorrect.

**Solution**:
1. Go to Azure Portal > App registrations > Your app > Certificates & secrets
2. Check if the secret has expired
3. Create a new secret if needed
4. Update `appsettings.json` with the new value

### Session Expired / Login Loop

**Cause**: Token refresh failed or session timeout.

**Solution**:
1. Clear browser cookies for localhost
2. Restart the application
3. Login again

---

## Azure OpenAI Issues

### "Resource not found" or 404 errors

**Cause**: Incorrect endpoint or deployment name.

**Solution**:
1. Verify endpoint URL in Azure Portal (include trailing slash)
2. Confirm deployment name matches exactly
3. Check the API version is supported

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "ApiVersion": "2024-08-01-preview"
  }
}
```

### "Access denied" or 401/403 errors

**Cause**: Invalid API key or resource not accessible.

**Solution**:
1. Regenerate API key in Azure Portal
2. Verify the key is from the correct resource
3. Check if the resource has any network restrictions

### "Model deployment not found"

**Cause**: The GPT-4o model isn't deployed.

**Solution**:
1. Go to Azure OpenAI Studio
2. Navigate to Deployments
3. Create a new deployment with the model you want
4. Update `DeploymentName` in config

### Rate Limiting (429 errors)

**Cause**: Too many requests to Azure OpenAI.

**Solution**:
1. Add delays between requests
2. Increase your quota in Azure Portal
3. Implement exponential backoff

---

## M365 Copilot Chat API Issues

The Chat API is the core integration for M365 Copilot. Here are common issues and solutions.

### Quick Diagnostic: Test the API Directly

Before troubleshooting the application, test the Chat API directly:

1. Go to [Graph Explorer](https://developer.microsoft.com/graph/graph-explorer)
2. Sign in with your M365 account
3. Run: `POST https://graph.microsoft.com/beta/copilot/conversations` with body `{}`
4. If this fails, the issue is with your M365 setup, not the application

### "Forbidden" or 403 Errors

**Cause 1**: Missing Copilot license

**Diagnosis**:
```
POST /beta/copilot/conversations returns 403
Error: "Access denied. Check that the user has a valid license."
```

**Solution**:
1. Go to Microsoft 365 Admin Center → Users → Your User → Licenses
2. Verify "Microsoft 365 Copilot" is enabled
3. Wait up to 24 hours for license propagation

**Cause 2**: Missing required API permissions

**Diagnosis**:
```
POST /beta/copilot/conversations returns 403
Response body: {
  "error": {
    "code": "unauthorized",
    "message": "Required scopes = [Sites.Read.All, Mail.Read, People.Read.All,
                OnlineMeetingTranscript.Read.All, Chat.Read, ChannelMessage.Read.All,
                ExternalItem.Read.All]."
  }
}
```

**Solution**:
1. Azure Portal → App registrations → Your app → API permissions
2. Add ALL required delegated permissions:
   - `Mail.Read`
   - `Calendars.Read`
   - `Files.Read.All`
   - `Sites.Read.All`
   - `People.Read.All` (not `People.Read`)
   - `Chat.Read`
   - `ChannelMessage.Read.All`
   - `OnlineMeetingTranscript.Read.All`
   - `ExternalItem.Read.All`
3. Click **Grant admin consent for [your tenant]**
4. Re-login in the application

> **Important**: The error response body contains the exact list of missing scopes. Always check the response body, not just the status code.

**Cause 3**: Copilot not enabled for tenant

**Diagnosis**: Copilot works in Graph Explorer with personal account but not with work account.

**Solution**:
1. Contact your M365 administrator
2. Ensure Copilot is enabled in Microsoft 365 Admin Center → Settings → Copilot

### "Resource not found" or 404 Errors

**Cause**: Wrong API endpoint or version

**Diagnosis**:
```
POST /v1.0/copilot/conversations returns 404
```

**Solution**:
- The Chat API is only available in `/beta`, not `/v1.0`
- Correct endpoint: `https://graph.microsoft.com/beta/copilot/conversations`
- Verify `MicrosoftGraph.BaseUrl` in appsettings.json is `https://graph.microsoft.com/beta`

### Empty or Missing Copilot Response

**Cause 1**: Query not understood

**Diagnosis**: Response returns with empty or generic content.

**Solution**:
1. Try rephrasing the query more specifically
2. Ensure the user has relevant M365 data (emails, calendar events, files)

**Cause 2**: No M365 data to query

**Diagnosis**: Copilot responds with "I don't have access to that information."

**Solution**:
1. Verify the user has emails, calendar events, files in their M365 account
2. Try a simpler query: "What's on my calendar today?"
3. Test Copilot directly in Teams to verify it can access data

### Timeout Errors

**Cause**: Copilot taking too long to respond

**Symptoms**:
- `TaskCanceledException` or timeout errors
- Request hangs for extended period

**Solution**:

1. **Increase HTTP client timeout**:
   The Kiota SDK uses HttpClient which has default timeouts

2. **Check network connectivity**:
   Ensure stable connection to Graph API endpoints

3. **Simplify the query**: Complex queries may take longer to process

### Rate Limiting (429 Errors)

**Cause**: Too many API calls to Microsoft Graph

**Solution**:
1. Implement exponential backoff (not yet in this lab)
2. Reduce polling frequency
3. Cache responses where appropriate

### Debugging Chat API Calls

Add logging to see exactly what's happening:

1. In `appsettings.Development.json`:
   ```json
   {
     "Logging": {
       "LogLevel": {
         "AgentOrchestrator.Plugins.M365CopilotPlugin": "Debug",
         "AgentOrchestrator.Agent.OrchestratorAgent": "Debug"
       }
     }
   }
   ```

2. Watch the console for:
   - Request URLs
   - Response status codes
   - Copilot message content
   - Intent classification results
   - Semantic Kernel plugin invocations

### Chat API Considerations

| Aspect | Detail | Notes |
|--------|--------|-------|
| Beta API | May change without notice | Monitor Graph changelog |
| Synchronous | Response returned directly | No polling required |
| Per-user license | Each user needs Copilot license | Required for API access |
| Rate limits | 429 errors under heavy load | Implement backoff, caching |
| Kiota SDK | Type-safe API client | Included in project |

---

## Microsoft 365 Agents SDK Issues

### "No agent registered" or Agent Not Found

**Cause**: Agent not properly registered in DI container.

**Solution**:
Verify `Program.cs` has these lines:
```csharp
builder.AddAgentApplicationOptions();
builder.AddAgent<OrchestratorAgent>();
```

### Activity Handler Not Firing

**Cause**: Activity type not registered in constructor.

**Solution**:
Check `OrchestratorAgent.cs` constructor registers handlers:
```csharp
OnActivity(ActivityTypes.Message, OnMessageActivityAsync);
OnActivity(ActivityTypes.ConversationUpdate, OnConversationUpdateAsync);
```

### "IStorage not registered" Error

**Cause**: Missing storage registration.

**Solution**:
Add to `Program.cs`:
```csharp
builder.Services.AddSingleton<IStorage, MemoryStorage>();
```

---

## Semantic Kernel Issues

### "Plugin not found" or Function Not Found

**Cause**: Plugin not registered with kernel or wrong function name.

**Solution**:
1. Verify plugin registration in `Program.cs`:
   ```csharp
   kernel.Plugins.AddFromObject(new IntentPlugin(kernel), "IntentPlugin");
   ```
2. Check function has `[KernelFunction]` attribute
3. Verify function name matches invocation

### Intent Classification Returns Empty Array

**Cause**: LLM response parsing failed.

**Solution**:
1. Check `IntentPlugin.cs` JSON extraction logic
2. Verify Azure OpenAI deployment is responding
3. Add logging to see raw LLM response:
   ```csharp
   _logger.LogDebug("Raw intent response: {Response}", response);
   ```

### Kernel Invocation Timeout

**Cause**: Azure OpenAI taking too long.

**Solution**:
1. Check Azure OpenAI service status
2. Verify deployment has sufficient capacity
3. Consider reducing prompt complexity

---

## Application Errors

### "Configuration is required" on startup

**Cause**: Missing configuration sections.

**Solution**:
1. Verify all sections exist in `appsettings.json`
2. Check for JSON syntax errors
3. Ensure no placeholder values remain

### Port already in use

**Cause**: Another application using port 5000.

**Solution**:
1. Find and stop the other application
2. Or change the port in `launchSettings.json`:
   ```json
   "applicationUrl": "http://localhost:5001"
   ```

### Static files not loading (404 for CSS/JS)

**Cause**: Static file middleware not configured correctly.

**Solution**:
1. Verify `wwwroot` folder exists with files
2. Check `Program.cs` has `app.UseStaticFiles()`
3. Ensure files have correct extensions

---

## UI Issues

### Chat input stays disabled after login

**Cause**: JavaScript error or auth status not updating.

**Solution**:
1. Open browser Developer Tools (F12)
2. Check Console for errors
3. Verify `/auth/status` returns `isAuthenticated: true`

### Trace panel not updating

**Cause**: SSE connection issue.

**Solution**:
1. Check Network tab for `/api/chat` request
2. Verify response is `text/event-stream`
3. Look for JavaScript errors in Console

### Response text not appearing

**Cause**: Token events not being processed.

**Solution**:
1. Check Network tab for SSE events
2. Verify `token` events are being sent
3. Debug `Chat.handleSSEEvent()` function

---

## Common Configuration Mistakes

### JSON formatting errors

```json
// WRONG - trailing comma
{
  "AzureOpenAI": {
    "Endpoint": "https://...",
  }
}

// CORRECT
{
  "AzureOpenAI": {
    "Endpoint": "https://..."
  }
}
```

### Missing trailing slash on endpoint

```json
// WRONG
"Endpoint": "https://myresource.openai.azure.com"

// CORRECT
"Endpoint": "https://myresource.openai.azure.com/"
```

### Placeholder values not replaced

```json
// WRONG - still has placeholder
"TenantId": "<your-tenant-id>"

// CORRECT
"TenantId": "12345678-1234-1234-1234-123456789abc"
```

---

## Getting Help

If you're still stuck:

1. **Check logs**: Run with `dotnet run` and watch console output
2. **Enable detailed errors**: Set `ASPNETCORE_ENVIRONMENT=Development`
3. **Test components individually**:
   - Auth: Try `/auth/status` endpoint
   - OpenAI: Test in Azure OpenAI Studio
   - Graph: Test in Graph Explorer

### Useful Commands

```bash
# Check .NET version
dotnet --version

# Clear NuGet cache
dotnet nuget locals all --clear

# Rebuild from scratch
dotnet clean && dotnet build

# Run with detailed logging
dotnet run --verbosity detailed
```

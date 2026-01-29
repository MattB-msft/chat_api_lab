#!/bin/bash
# ============================================================================
# Local Development Setup Script
# ============================================================================
# Sets up .NET user secrets for local development.
# Usage: ./scripts/setup-local.sh
# ============================================================================

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SRC_DIR="$PROJECT_ROOT/src/AgentOrchestrator"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Local Development Setup${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "This script configures .NET user secrets for local development."
echo "You'll need values from Azure Portal."
echo ""

# Check prerequisites
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK not found. Install from: https://dotnet.microsoft.com/download${NC}"
    exit 1
fi

cd "$SRC_DIR"

# Initialize user secrets if not already done
if ! dotnet user-secrets list &> /dev/null; then
    echo -e "${BLUE}Initializing user secrets...${NC}"
    dotnet user-secrets init
fi

echo ""
echo -e "${YELLOW}Enter your configuration values (from Azure Portal):${NC}"
echo ""

# Prompt for values
read -p "Tenant ID: " TENANT_ID
read -p "Client ID (Azure AD App): " CLIENT_ID
read -sp "Client Secret: " CLIENT_SECRET
echo ""
read -p "Azure OpenAI Endpoint (e.g., https://myresource.openai.azure.com/): " OPENAI_ENDPOINT
read -sp "Azure OpenAI API Key: " OPENAI_KEY
echo ""
read -p "Azure OpenAI Deployment Name (e.g., gpt-4o): " OPENAI_DEPLOYMENT

echo ""
echo -e "${BLUE}Configuring user secrets...${NC}"

# Set Azure AD secrets
dotnet user-secrets set "AzureAd:TenantId" "$TENANT_ID"
dotnet user-secrets set "AzureAd:ClientId" "$CLIENT_ID"
dotnet user-secrets set "AzureAd:ClientSecret" "$CLIENT_SECRET"

# Set Azure OpenAI secrets
dotnet user-secrets set "AzureOpenAI:Endpoint" "$OPENAI_ENDPOINT"
dotnet user-secrets set "AzureOpenAI:ApiKey" "$OPENAI_KEY"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "$OPENAI_DEPLOYMENT"

# Also set Bot Service connection (uses same credentials for local dev)
dotnet user-secrets set "Connections:BotServiceConnection:Settings:TenantId" "$TENANT_ID"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:ClientId" "$CLIENT_ID"
dotnet user-secrets set "Connections:BotServiceConnection:Settings:ClientSecret" "$CLIENT_SECRET"

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Setup Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Next steps:"
echo ""
echo "1. Add redirect URI to your Azure AD app:"
echo "   http://localhost:5001/auth/callback"
echo ""
echo "2. Run the application:"
echo "   cd src/AgentOrchestrator"
echo "   dotnet run --urls \"http://localhost:5001\""
echo ""
echo "3. Open http://localhost:5001 in your browser"
echo ""

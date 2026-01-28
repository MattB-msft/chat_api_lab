#!/bin/bash
# ============================================================================
# Configure App Settings for Agent Orchestrator
# ============================================================================
# This script configures Azure App Service settings without redeploying.
# Useful for updating Azure OpenAI endpoints, API keys, or other settings.
#
# Usage:
#   ./scripts/configure-appsettings.sh --help
#   ./scripts/configure-appsettings.sh --openai-endpoint URL --openai-deployment NAME
# ============================================================================

set -e

# =============================================================================
# DEFAULTS
# =============================================================================
DEFAULT_RESOURCE_GROUP="rg-agent-orchestrator"
DEFAULT_APP_NAME="agent-orchestrator"

# =============================================================================
# COLORS
# =============================================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_header() {
    echo ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}========================================${NC}"
    echo ""
}

print_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

show_help() {
    echo "Configure App Settings for Agent Orchestrator"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Azure Resource Options:"
    echo "  --app-name NAME         App Service name (default: $DEFAULT_APP_NAME)"
    echo "  --resource-group RG     Resource group (default: $DEFAULT_RESOURCE_GROUP)"
    echo ""
    echo "Azure OpenAI Settings:"
    echo "  --openai-endpoint URL   Azure OpenAI endpoint URL"
    echo "  --openai-deployment N   Azure OpenAI deployment name"
    echo "  --openai-key KEY        Azure OpenAI API key"
    echo ""
    echo "Bot Service Settings:"
    echo "  --bot-client-id ID      Bot Microsoft App ID"
    echo "  --bot-client-secret S   Bot client secret"
    echo "  --bot-tenant-id ID      Bot tenant ID"
    echo ""
    echo "Azure AD Settings:"
    echo "  --aad-tenant-id ID      Azure AD tenant ID"
    echo "  --aad-client-id ID      Azure AD client ID"
    echo "  --aad-client-secret S   Azure AD client secret"
    echo ""
    echo "Other Options:"
    echo "  --show-current          Show current app settings"
    echo "  --dry-run               Show what would be done"
    echo "  -h, --help              Show this help"
    echo ""
    echo "Examples:"
    echo "  # Update Azure OpenAI endpoint"
    echo "  $0 --openai-endpoint https://myresource.openai.azure.com/ \\"
    echo "     --openai-deployment gpt-5-chat"
    echo ""
    echo "  # Show current settings"
    echo "  $0 --show-current"
    echo ""
}

show_current_settings() {
    print_header "Current App Settings"

    print_step "Fetching settings from $APP_NAME..."

    az webapp config appsettings list \
        --resource-group "$RESOURCE_GROUP" \
        --name "$APP_NAME" \
        --query "[?contains(name, 'AzureOpenAI') || contains(name, 'AzureAd') || contains(name, 'BotServiceConnection')].{Name:name, Value:value}" \
        --output table

    echo ""
    print_step "Connection strings:"
    az webapp config connection-string list \
        --resource-group "$RESOURCE_GROUP" \
        --name "$APP_NAME" \
        --output table 2>/dev/null || echo "  No connection strings configured"
}

# =============================================================================
# PARSE ARGUMENTS
# =============================================================================

APP_NAME="$DEFAULT_APP_NAME"
RESOURCE_GROUP="$DEFAULT_RESOURCE_GROUP"

OPENAI_ENDPOINT=""
OPENAI_DEPLOYMENT=""
OPENAI_KEY=""

BOT_CLIENT_ID=""
BOT_CLIENT_SECRET=""
BOT_TENANT_ID=""

AAD_TENANT_ID=""
AAD_CLIENT_ID=""
AAD_CLIENT_SECRET=""

SHOW_CURRENT=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --app-name)
            APP_NAME="$2"
            shift 2
            ;;
        --resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        --openai-endpoint)
            OPENAI_ENDPOINT="$2"
            shift 2
            ;;
        --openai-deployment)
            OPENAI_DEPLOYMENT="$2"
            shift 2
            ;;
        --openai-key)
            OPENAI_KEY="$2"
            shift 2
            ;;
        --bot-client-id)
            BOT_CLIENT_ID="$2"
            shift 2
            ;;
        --bot-client-secret)
            BOT_CLIENT_SECRET="$2"
            shift 2
            ;;
        --bot-tenant-id)
            BOT_TENANT_ID="$2"
            shift 2
            ;;
        --aad-tenant-id)
            AAD_TENANT_ID="$2"
            shift 2
            ;;
        --aad-client-id)
            AAD_CLIENT_ID="$2"
            shift 2
            ;;
        --aad-client-secret)
            AAD_CLIENT_SECRET="$2"
            shift 2
            ;;
        --show-current)
            SHOW_CURRENT=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# =============================================================================
# MAIN
# =============================================================================

print_header "Configure App Settings"

echo "Target:"
echo "  App Name:       $APP_NAME"
echo "  Resource Group: $RESOURCE_GROUP"
echo ""

# Check Azure CLI
if ! command -v az &> /dev/null; then
    print_error "Azure CLI not found"
    exit 1
fi

if ! az account show &> /dev/null; then
    print_error "Not logged in to Azure. Run: az login"
    exit 1
fi

# Show current settings if requested
if [ "$SHOW_CURRENT" = true ]; then
    show_current_settings
    exit 0
fi

# Build settings array
SETTINGS=""

# Azure OpenAI settings
if [ -n "$OPENAI_ENDPOINT" ]; then
    SETTINGS="$SETTINGS AzureOpenAI__Endpoint=$OPENAI_ENDPOINT"
fi
if [ -n "$OPENAI_DEPLOYMENT" ]; then
    SETTINGS="$SETTINGS AzureOpenAI__DeploymentName=$OPENAI_DEPLOYMENT"
fi
if [ -n "$OPENAI_KEY" ]; then
    SETTINGS="$SETTINGS AzureOpenAI__ApiKey=$OPENAI_KEY"
fi

# Bot Service settings
if [ -n "$BOT_CLIENT_ID" ]; then
    SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__ClientId=$BOT_CLIENT_ID"
fi
if [ -n "$BOT_CLIENT_SECRET" ]; then
    SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__ClientSecret=$BOT_CLIENT_SECRET"
fi
if [ -n "$BOT_TENANT_ID" ]; then
    SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__TenantId=$BOT_TENANT_ID"
fi

# Azure AD settings
if [ -n "$AAD_TENANT_ID" ]; then
    SETTINGS="$SETTINGS AzureAd__TenantId=$AAD_TENANT_ID"
fi
if [ -n "$AAD_CLIENT_ID" ]; then
    SETTINGS="$SETTINGS AzureAd__ClientId=$AAD_CLIENT_ID"
fi
if [ -n "$AAD_CLIENT_SECRET" ]; then
    SETTINGS="$SETTINGS AzureAd__ClientSecret=$AAD_CLIENT_SECRET"
fi

# Check if any settings to apply
if [ -z "$SETTINGS" ]; then
    print_warning "No settings provided. Use --help to see available options."
    echo ""
    echo "Common usage:"
    echo "  $0 --openai-endpoint https://myresource.openai.azure.com/ --openai-deployment gpt-5-chat"
    echo "  $0 --show-current"
    exit 1
fi

# Apply settings
print_step "Applying settings..."
echo ""

if [ "$DRY_RUN" = true ]; then
    echo "[DRY RUN] Would set:"
    for setting in $SETTINGS; do
        key="${setting%%=*}"
        value="${setting#*=}"
        if [[ "$key" == *"Secret"* ]] || [[ "$key" == *"Key"* ]]; then
            echo "  $key = *******"
        else
            echo "  $key = $value"
        fi
    done
else
    az webapp config appsettings set \
        --resource-group "$RESOURCE_GROUP" \
        --name "$APP_NAME" \
        --settings $SETTINGS \
        --output none

    print_success "Settings applied"
    echo ""
    echo "Applied settings:"
    for setting in $SETTINGS; do
        key="${setting%%=*}"
        value="${setting#*=}"
        if [[ "$key" == *"Secret"* ]] || [[ "$key" == *"Key"* ]]; then
            echo "  $key = *******"
        else
            echo "  $key = $value"
        fi
    done
fi

echo ""
print_step "Restarting app to apply changes..."

if [ "$DRY_RUN" = true ]; then
    echo "[DRY RUN] az webapp restart --resource-group $RESOURCE_GROUP --name $APP_NAME"
else
    az webapp restart \
        --resource-group "$RESOURCE_GROUP" \
        --name "$APP_NAME" \
        --output none
    print_success "App restarted"
fi

echo ""
print_success "Configuration complete!"

#!/bin/bash
# ============================================================================
# Azure Deployment Script for Agent Orchestrator
# ============================================================================
# This script deploys the Agent Orchestrator to Azure App Service.
#
# Usage:
#   ./scripts/deploy.sh                    # Interactive mode
#   ./scripts/deploy.sh --app-name myapp   # With app name
#   ./scripts/deploy.sh --help             # Show help
#
# Single-command deployment (all required settings):
#   ./scripts/deploy.sh \
#     --app-name my-agent \
#     --resource-group rg-myagent \
#     --create-resources \
#     --configure-settings \
#     --openai-endpoint "https://myresource.openai.azure.com/" \
#     --openai-deployment "gpt-4o" \
#     --openai-key "your-api-key" \
#     --aad-tenant-id "your-tenant-id" \
#     --aad-client-id "your-client-id" \
#     --aad-client-secret "your-secret" \
#     --bot-client-id "your-bot-app-id" \
#     --bot-client-secret "your-bot-secret" \
#     --bot-tenant-id "your-tenant-id"
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - .NET 10 SDK installed
#   - Azure subscription with appropriate permissions
# ============================================================================

set -e  # Exit on error

# =============================================================================
# CONFIGURATION - Update these values for your deployment
# =============================================================================
DEFAULT_RESOURCE_GROUP="rg-agent-orchestrator"
DEFAULT_LOCATION="eastus"
DEFAULT_APP_NAME="agent-orchestrator"
DEFAULT_PLAN_NAME="plan-agent-orchestrator"
DEFAULT_SKU="B1"

# Azure OpenAI Configuration
# Update these with your Azure OpenAI resource details
AZURE_OPENAI_ENDPOINT=""
AZURE_OPENAI_DEPLOYMENT=""
AZURE_OPENAI_API_KEY=""

# =============================================================================
# COLORS
# =============================================================================
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# =============================================================================
# SCRIPT DIRECTORY
# =============================================================================
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
APP_DIR="$PROJECT_ROOT/src/AgentOrchestrator"

# =============================================================================
# FUNCTIONS
# =============================================================================

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
    echo "Azure Deployment Script for Agent Orchestrator"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --app-name NAME       App Service name (default: $DEFAULT_APP_NAME)"
    echo "  --resource-group RG   Resource group name (default: $DEFAULT_RESOURCE_GROUP)"
    echo "  --location LOC        Azure region (default: $DEFAULT_LOCATION)"
    echo "  --plan-name PLAN      App Service Plan name (default: $DEFAULT_PLAN_NAME)"
    echo "  --sku SKU             App Service SKU (default: $DEFAULT_SKU)"
    echo "  --create-resources    Create Azure resources if they don't exist"
    echo "  --skip-build          Skip dotnet build/publish step"
    echo "  --update-manifest     Update Teams manifest with app details"
    echo "  --package-manifest    Create Teams app package (manifest.zip)"
    echo "  --configure-settings  Configure app settings in Azure"
    echo ""
    echo "Azure OpenAI Settings:"
    echo "  --openai-endpoint URL Azure OpenAI endpoint URL"
    echo "  --openai-deployment N Azure OpenAI deployment name"
    echo "  --openai-key KEY      Azure OpenAI API key"
    echo ""
    echo "Azure AD Settings (for user auth):"
    echo "  --aad-tenant-id ID    Azure AD tenant ID"
    echo "  --aad-client-id ID    Azure AD app client ID"
    echo "  --aad-client-secret S Azure AD app client secret"
    echo ""
    echo "Bot Service Settings:"
    echo "  --bot-client-id ID    Bot Microsoft App ID"
    echo "  --bot-client-secret S Bot client secret"
    echo "  --bot-tenant-id ID    Bot tenant ID"
    echo ""
    echo "Other Options:"
    echo "  --dry-run             Show what would be done without executing"
    echo "  -h, --help            Show this help message"
    echo ""
    echo "Examples:"
    echo "  # Quick deploy (existing resources)"
    echo "  $0 --app-name my-agent"
    echo ""
    echo "  # Full deployment with all settings"
    echo "  $0 --app-name my-agent --resource-group rg-myagent --create-resources \\"
    echo "     --configure-settings \\"
    echo "     --openai-endpoint https://myresource.openai.azure.com/ \\"
    echo "     --openai-deployment gpt-4o --openai-key <key> \\"
    echo "     --aad-tenant-id <tenant> --aad-client-id <id> --aad-client-secret <secret> \\"
    echo "     --bot-client-id <bot-id> --bot-client-secret <secret> --bot-tenant-id <tenant>"
    echo ""
    echo "  # Package Teams manifest"
    echo "  $0 --app-name my-agent --skip-build --package-manifest"
    echo ""
}
    echo "  --openai-endpoint URL Azure OpenAI endpoint URL"
    echo "  --openai-deployment N Azure OpenAI deployment name"
    echo "  --openai-key KEY      Azure OpenAI API key"
    echo "  --dry-run             Show what would be done without executing"
    echo "  -h, --help            Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 --app-name my-agent --create-resources"
    echo "  $0 --app-name my-agent --skip-build"
    echo "  $0 --app-name my-agent --configure-settings \\"
    echo "     --openai-endpoint https://myresource.openai.azure.com/ \\"
    echo "     --openai-deployment gpt-4o --openai-key <key>"
    echo ""
}

check_prerequisites() {
    print_step "Checking prerequisites..."

    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI not found. Install from: https://docs.microsoft.com/cli/azure/install-azure-cli"
        exit 1
    fi

    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Run: az login"
        exit 1
    fi

    # Check .NET SDK
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK not found. Install from: https://dotnet.microsoft.com/download"
        exit 1
    fi

    # Check zip command
    if ! command -v zip &> /dev/null; then
        print_error "zip command not found. Install zip utility."
        exit 1
    fi

    print_success "All prerequisites met"
}

create_resources() {
    print_header "Creating Azure Resources"

    # Create resource group
    print_step "Creating resource group: $RESOURCE_GROUP"
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] az group create --name $RESOURCE_GROUP --location $LOCATION"
    else
        az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
        print_success "Resource group created"
    fi

    # Create App Service Plan
    print_step "Creating App Service Plan: $PLAN_NAME"
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] az appservice plan create --name $PLAN_NAME --resource-group $RESOURCE_GROUP --sku $SKU --is-linux"
    else
        az appservice plan create \
            --name "$PLAN_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --sku "$SKU" \
            --is-linux \
            --output none
        print_success "App Service Plan created"
    fi

    # Create Web App
    print_step "Creating Web App: $APP_NAME"
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] az webapp create --name $APP_NAME --resource-group $RESOURCE_GROUP --plan $PLAN_NAME --runtime DOTNETCORE:10.0"
    else
        az webapp create \
            --name "$APP_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --plan "$PLAN_NAME" \
            --runtime "DOTNETCORE:10.0" \
            --output none
        print_success "Web App created"
    fi

    echo ""
    print_success "Azure resources created successfully"
    echo ""
    echo "Web App URL: https://$APP_NAME.azurewebsites.net"
}

build_application() {
    print_header "Building Application"

    cd "$APP_DIR"

    print_step "Cleaning previous builds..."
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] rm -rf ./publish ./deploy.zip"
    else
        rm -rf ./publish ./deploy.zip 2>/dev/null || true
    fi

    print_step "Publishing application..."
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] dotnet publish -c Release -o ./publish"
    else
        dotnet publish -c Release -o ./publish --nologo
        print_success "Application published"
    fi

    print_step "Creating deployment package..."
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] cd publish && zip -r ../deploy.zip ."
    else
        cd publish
        zip -r ../deploy.zip . -q
        cd ..
        print_success "Deployment package created: deploy.zip"
    fi

    cd "$PROJECT_ROOT"
}

deploy_application() {
    print_header "Deploying to Azure"

    print_step "Deploying to App Service: $APP_NAME"
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] az webapp deployment source config-zip --resource-group $RESOURCE_GROUP --name $APP_NAME --src $APP_DIR/deploy.zip"
    else
        az webapp deployment source config-zip \
            --resource-group "$RESOURCE_GROUP" \
            --name "$APP_NAME" \
            --src "$APP_DIR/deploy.zip" \
            --output none
        print_success "Application deployed"
    fi

    print_step "Restarting App Service..."
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] az webapp restart --resource-group $RESOURCE_GROUP --name $APP_NAME"
    else
        az webapp restart \
            --resource-group "$RESOURCE_GROUP" \
            --name "$APP_NAME" \
            --output none
        print_success "App Service restarted"
    fi
}

configure_app_settings() {
    print_header "Configuring App Settings"

    local SETTINGS=""
    local HAS_SETTINGS=false

    # Azure OpenAI settings
    if [ -n "$AZURE_OPENAI_ENDPOINT" ]; then
        SETTINGS="$SETTINGS AzureOpenAI__Endpoint=$AZURE_OPENAI_ENDPOINT"
        HAS_SETTINGS=true
    fi
    if [ -n "$AZURE_OPENAI_DEPLOYMENT" ]; then
        SETTINGS="$SETTINGS AzureOpenAI__DeploymentName=$AZURE_OPENAI_DEPLOYMENT"
        HAS_SETTINGS=true
    fi
    if [ -n "$AZURE_OPENAI_API_KEY" ]; then
        SETTINGS="$SETTINGS AzureOpenAI__ApiKey=$AZURE_OPENAI_API_KEY"
        HAS_SETTINGS=true
    fi

    # Azure AD settings
    if [ -n "$AAD_TENANT_ID" ]; then
        SETTINGS="$SETTINGS AzureAd__TenantId=$AAD_TENANT_ID"
        HAS_SETTINGS=true
    fi
    if [ -n "$AAD_CLIENT_ID" ]; then
        SETTINGS="$SETTINGS AzureAd__ClientId=$AAD_CLIENT_ID"
        HAS_SETTINGS=true
    fi
    if [ -n "$AAD_CLIENT_SECRET" ]; then
        SETTINGS="$SETTINGS AzureAd__ClientSecret=$AAD_CLIENT_SECRET"
        HAS_SETTINGS=true
    fi
    # Set redirect URI based on app name
    if [ -n "$AAD_CLIENT_ID" ]; then
        SETTINGS="$SETTINGS AzureAd__RedirectUri=https://$APP_NAME.azurewebsites.net/auth/callback"
    fi

    # Bot Service settings
    if [ -n "$BOT_CLIENT_ID" ]; then
        SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__ClientId=$BOT_CLIENT_ID"
        HAS_SETTINGS=true
    fi
    if [ -n "$BOT_CLIENT_SECRET" ]; then
        SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__ClientSecret=$BOT_CLIENT_SECRET"
        HAS_SETTINGS=true
    fi
    if [ -n "$BOT_TENANT_ID" ]; then
        SETTINGS="$SETTINGS Connections__BotServiceConnection__Settings__TenantId=$BOT_TENANT_ID"
        HAS_SETTINGS=true
    fi

    if [ "$HAS_SETTINGS" = false ]; then
        print_warning "No settings provided. Use --help to see available options."
        return 1
    fi

    print_step "Applying app settings..."

    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] Settings to apply:"
        for setting in $SETTINGS; do
            key="${setting%%=*}"
            if [[ "$key" == *"Secret"* ]] || [[ "$key" == *"Key"* ]]; then
                echo "    $key = *******"
            else
                echo "    $key = ${setting#*=}"
            fi
        done
    else
        az webapp config appsettings set \
            --resource-group "$RESOURCE_GROUP" \
            --name "$APP_NAME" \
            --settings $SETTINGS \
            --output none
        print_success "App settings configured"
    fi

    # Print summary (mask secrets)
    echo ""
    echo "Settings applied:"
    for setting in $SETTINGS; do
        key="${setting%%=*}"
        if [[ "$key" == *"Secret"* ]] || [[ "$key" == *"Key"* ]]; then
            echo "  $key = *******"
        else
            echo "  $key = ${setting#*=}"
        fi
    done
}

package_manifest() {
    print_header "Creating Teams App Package"

    local MANIFEST_DIR="$APP_DIR/appPackage/build"
    local OUTPUT_FILE="$APP_DIR/appPackage/manifest.zip"

    if [ ! -d "$MANIFEST_DIR" ]; then
        print_error "Manifest directory not found: $MANIFEST_DIR"
        return 1
    fi

    if [ ! -f "$MANIFEST_DIR/manifest.json" ]; then
        print_error "manifest.json not found in $MANIFEST_DIR"
        return 1
    fi

    print_step "Creating manifest.zip..."
    
    if [ "$DRY_RUN" = true ]; then
        echo "  [DRY RUN] cd $MANIFEST_DIR && zip -r $OUTPUT_FILE manifest.json color.png outline.png"
    else
        cd "$MANIFEST_DIR"
        rm -f "$OUTPUT_FILE" 2>/dev/null || true
        zip -j "$OUTPUT_FILE" manifest.json color.png outline.png -q
        cd "$PROJECT_ROOT"
        print_success "Teams app package created: $OUTPUT_FILE"
    fi

    echo ""
    echo "To deploy the Teams app:"
    echo "  1. Go to Teams Admin Center: https://admin.teams.microsoft.com"
    echo "  2. Navigate to Teams apps > Manage apps > Upload new app"
    echo "  3. Upload: $OUTPUT_FILE"
}

update_manifest() {
    print_header "Updating Teams Manifest"

    local MANIFEST_FILE="$APP_DIR/appPackage/manifest.json"
    local BOT_DOMAIN="$APP_NAME.azurewebsites.net"

    if [ ! -f "$MANIFEST_FILE" ]; then
        print_warning "Manifest file not found: $MANIFEST_FILE"
        return
    fi

    print_step "Current manifest placeholders:"
    grep -E "\{\{.*\}\}" "$MANIFEST_FILE" || echo "  No placeholders found"

    echo ""
    echo "To update the manifest, replace these placeholders:"
    echo "  {{BOT_APP_ID}} -> Your Bot's Microsoft App ID"
    echo "  {{BOT_DOMAIN}} -> $BOT_DOMAIN"
    echo ""
    echo "Then create the Teams app package:"
    echo "  cd $APP_DIR/appPackage"
    echo "  zip manifest.zip manifest.json color.png outline.png"
}

show_summary() {
    print_header "Deployment Summary"

    echo "Configuration:"
    echo "  Resource Group: $RESOURCE_GROUP"
    echo "  Location:       $LOCATION"
    echo "  App Name:       $APP_NAME"
    echo "  Plan Name:      $PLAN_NAME"
    echo "  SKU:            $SKU"
    echo ""
    echo "URLs:"
    echo "  Web App:        https://$APP_NAME.azurewebsites.net"
    echo "  Health Check:   https://$APP_NAME.azurewebsites.net/health"
    echo "  Swagger:        https://$APP_NAME.azurewebsites.net/swagger"
    echo "  Bot Endpoint:   https://$APP_NAME.azurewebsites.net/api/messages"
    echo ""
    echo "Next Steps:"
    echo "  1. Configure app settings in Azure Portal (see docs/AZURE_DEPLOYMENT.md)"
    echo "  2. Create Azure Bot and link to this App Service"
    echo "  3. Update Teams manifest with Bot App ID"
    echo "  4. Deploy Teams app to your organization"
    echo ""
    echo "View logs:"
    echo "  az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME"
}

# =============================================================================
# PARSE ARGUMENTS
# =============================================================================

APP_NAME="$DEFAULT_APP_NAME"
RESOURCE_GROUP="$DEFAULT_RESOURCE_GROUP"
LOCATION="$DEFAULT_LOCATION"
PLAN_NAME="$DEFAULT_PLAN_NAME"
SKU="$DEFAULT_SKU"
CREATE_RESOURCES=false
SKIP_BUILD=false
UPDATE_MANIFEST=false
PACKAGE_MANIFEST=false
CONFIGURE_SETTINGS=false
DRY_RUN=false

# Azure AD settings
AAD_TENANT_ID=""
AAD_CLIENT_ID=""
AAD_CLIENT_SECRET=""

# Bot Service settings
BOT_CLIENT_ID=""
BOT_CLIENT_SECRET=""
BOT_TENANT_ID=""

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
        --location)
            LOCATION="$2"
            shift 2
            ;;
        --plan-name)
            PLAN_NAME="$2"
            shift 2
            ;;
        --sku)
            SKU="$2"
            shift 2
            ;;
        --create-resources)
            CREATE_RESOURCES=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --update-manifest)
            UPDATE_MANIFEST=true
            shift
            ;;
        --package-manifest)
            PACKAGE_MANIFEST=true
            shift
            ;;
        --configure-settings)
            CONFIGURE_SETTINGS=true
            shift
            ;;
        --openai-endpoint)
            AZURE_OPENAI_ENDPOINT="$2"
            shift 2
            ;;
        --openai-deployment)
            AZURE_OPENAI_DEPLOYMENT="$2"
            shift 2
            ;;
        --openai-key)
            AZURE_OPENAI_API_KEY="$2"
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
# MAIN EXECUTION
# =============================================================================

print_header "Agent Orchestrator Deployment"

if [ "$DRY_RUN" = true ]; then
    print_warning "DRY RUN MODE - No changes will be made"
    echo ""
fi

echo "Configuration:"
echo "  App Name:       $APP_NAME"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location:       $LOCATION"
echo ""

check_prerequisites

if [ "$CREATE_RESOURCES" = true ]; then
    create_resources
fi

if [ "$SKIP_BUILD" = false ]; then
    build_application
fi

deploy_application

if [ "$UPDATE_MANIFEST" = true ]; then
    update_manifest
fi

if [ "$PACKAGE_MANIFEST" = true ]; then
    package_manifest
fi

if [ "$CONFIGURE_SETTINGS" = true ]; then
    configure_app_settings
fi

show_summary

print_success "Deployment complete!"

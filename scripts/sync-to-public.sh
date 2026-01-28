#!/bin/bash
# ============================================================================
# Sync Private Lab to Public Repository
# ============================================================================
# This script copies files from the private repo to the public repo
# while preserving the public repo's git configuration.
#
# Usage: ./scripts/sync-to-public.sh
# ============================================================================

set -e  # Exit on error

# Configuration
SOURCE_DIR="/Users/tk/src/chat_api_lab_private"
DEST_DIR="/Users/tk/src/chat_api_lab_public"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Syncing Private Lab to Public Repository${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Source: $SOURCE_DIR"
echo "Destination: $DEST_DIR"
echo ""

# Verify source exists
if [ ! -d "$SOURCE_DIR" ]; then
    echo -e "${RED}Error: Source directory does not exist: $SOURCE_DIR${NC}"
    exit 1
fi

# Verify destination exists
if [ ! -d "$DEST_DIR" ]; then
    echo -e "${RED}Error: Destination directory does not exist: $DEST_DIR${NC}"
    exit 1
fi

# Verify destination has .git (is a git repo)
if [ ! -d "$DEST_DIR/.git" ]; then
    echo -e "${YELLOW}Warning: Destination does not appear to be a git repository${NC}"
    read -p "Continue anyway? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Create a temporary backup of .git folder path (we won't actually move it)
echo -e "${YELLOW}Preserving .git directory...${NC}"

# Use rsync to copy files, excluding .git and other unwanted files
echo -e "${YELLOW}Copying files...${NC}"
rsync -av --delete \
    --exclude='.git' \
    --exclude='.git/' \
    --exclude='.claude' \
    --exclude='.claude/' \
    --exclude='appsettings.Development.json' \
    --exclude='appsettings.Local.json' \
    --exclude='*.local.json' \
    --exclude='.env' \
    --exclude='.env.*' \
    --exclude='secrets.json' \
    --exclude='bin/' \
    --exclude='obj/' \
    --exclude='out/' \
    --exclude='publish/' \
    --exclude='.vs/' \
    --exclude='.vscode/' \
    --exclude='.idea/' \
    --exclude='*.user' \
    --exclude='*.suo' \
    --exclude='.DS_Store' \
    --exclude='Thumbs.db' \
    --exclude='deploy.zip' \
    --exclude='*.zip' \
    "$SOURCE_DIR/" "$DEST_DIR/"

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Sync Complete!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Next steps:"
echo "  1. cd $DEST_DIR"
echo "  2. git status                    # Review changes"
echo "  3. git diff                      # Review file changes"
echo "  4. git add -A                    # Stage all changes"
echo "  5. git commit -m 'Sync from private repo'"
echo "  6. git push                      # Push to public repo"
echo ""
echo -e "${YELLOW}Remember to review changes before committing!${NC}"

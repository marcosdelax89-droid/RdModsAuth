#!/bin/bash
# Script de deploy automático para VPS
# Uso: ./deploy-vps.sh

echo "🚀 Iniciando deploy do BelgaAuth..."

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Verificar se está no diretório correto
if [ ! -f "BelgaAuthAPI.csproj" ]; then
    echo -e "${RED}❌ Erro: Execute este script dentro do diretório BelgaAuthAPI${NC}"
    exit 1
fi

# Publicar aplicação
echo -e "${YELLOW}📦 Publicando aplicação...${NC}"
dotnet publish -c Release -o ./publish

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Erro ao publicar aplicação${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Aplicação publicada com sucesso!${NC}"

# Reiniciar serviço (se systemd estiver configurado)
if systemctl is-active --quiet belgaauth; then
    echo -e "${YELLOW}🔄 Reiniciando serviço...${NC}"
    sudo systemctl restart belgaauth
    echo -e "${GREEN}✅ Serviço reiniciado!${NC}"
else
    echo -e "${YELLOW}⚠️  Serviço belgaauth não encontrado. Configure o systemd primeiro.${NC}"
fi

echo -e "${GREEN}🎉 Deploy concluído!${NC}"





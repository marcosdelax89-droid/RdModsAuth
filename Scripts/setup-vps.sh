#!/bin/bash
# Script de setup inicial do VPS para BelgaAuth
# Execute como root: sudo bash setup-vps.sh

echo "🖥️  Configurando VPS para BelgaAuth..."

# Atualizar sistema
echo "📦 Atualizando sistema..."
apt update && apt upgrade -y

# Instalar dependências
echo "📦 Instalando dependências..."
apt install -y wget curl gnupg software-properties-common

# Instalar .NET 7 SDK
echo "📦 Instalando .NET 7 SDK..."
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

apt update
apt install -y dotnet-sdk-7.0

# Verificar instalação
echo "✅ Verificando instalação do .NET..."
dotnet --version

# Instalar Nginx
echo "📦 Instalando Nginx..."
apt install -y nginx
systemctl enable nginx

# Instalar UFW (Firewall)
echo "📦 Configurando firewall..."
apt install -y ufw
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

echo "✅ Setup concluído!"
echo ""
echo "Próximos passos:"
echo "1. Configure o serviço systemd (veja DEPLOY_VPS.md)"
echo "2. Configure o Nginx (veja DEPLOY_VPS.md)"
echo "3. Instale SSL com Certbot (se tiver domínio)"





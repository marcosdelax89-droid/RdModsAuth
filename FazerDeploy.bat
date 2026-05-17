@echo off
echo ========================================
echo    Fazer Deploy - Commit e Push
echo ========================================
echo.

cd /d "%~dp0"

echo Verificando status do Git...
git status
echo.

echo Adicionando arquivos modificados...
git add .
echo.

echo Fazendo commit...
git commit -m "Melhorias de design e animações, removido sistema de logo"
echo.

echo Fazendo push para GitHub...
git push
echo.

echo ========================================
echo    Deploy enviado com sucesso!
echo ========================================
echo.
echo O Render vai detectar automaticamente e fazer
echo o deploy em alguns minutos.
echo.
echo Acesse: https://dashboard.render.com
echo para acompanhar o deploy.
echo.

pause


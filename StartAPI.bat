@echo off
echo ========================================
echo    BelgaAuth API - Iniciando...
echo ========================================
echo.
echo Aguarde enquanto a API inicia...
echo.
echo Quando aparecer a mensagem de "BelgaAuth iniciando"
echo voce pode abrir o navegador em: http://localhost:5000
echo.
echo Para colocar online, veja o arquivo DEPLOY_RAPIDO.md
echo.
echo Para parar a API, pressione Ctrl+C
echo.
echo ========================================
echo.

cd /d "%~dp0"
dotnet run

pause


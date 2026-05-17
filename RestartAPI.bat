@echo off
echo ========================================
echo    Reiniciando BelgaAuth API...
echo ========================================
echo.
echo Parando processos anteriores...
taskkill /F /IM dotnet.exe /T 2>nul
timeout /t 2 /nobreak >nul
echo.
echo Recompilando projeto...
cd /d "%~dp0"
dotnet build
echo.
echo ========================================
echo    Iniciando API...
echo ========================================
echo.
echo Aguarde alguns segundos...
echo Quando aparecer "Now listening on: http://localhost:5000"
echo voce pode abrir o navegador em: http://localhost:5000
echo.
echo Para parar a API, pressione Ctrl+C
echo.
echo ========================================
echo.
dotnet run
pause


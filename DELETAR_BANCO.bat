@echo off
echo ====================================
echo   Deletar Banco de Dados auth.db
echo ====================================
echo.

cd /d "%~dp0"

if exist auth.db (
    del /f auth.db
    echo ✅ Banco de dados auth.db deletado com sucesso!
) else (
    echo ⚠️  Arquivo auth.db não encontrado
)

echo.
echo ====================================
echo   Próximos Passos:
echo ====================================
echo 1. Execute: dotnet run
echo 2. O banco será recriado automaticamente
echo 3. Faça login com: admin / admin123
echo.
pause







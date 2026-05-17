@echo off
echo ========================================
echo   PREPARAR DEPLOY - BelgaAuth API
echo ========================================
echo.

REM Verificar se existe um arquivo de backup recente
echo Procurando arquivo de backup mais recente...
echo.

REM Listar arquivos .db
echo Arquivos de banco de dados encontrados:
dir /b *.db
echo.

REM Perguntar qual arquivo usar
echo.
echo INSTRUCOES:
echo 1. Se voce baixou um backup do site, coloque-o nesta pasta
echo 2. O arquivo deve ter nome como: auth_backup_YYYYMMDD_HHMMSS.db
echo 3. Digite o nome COMPLETO do arquivo de backup que deseja usar
echo    (ou pressione ENTER para manter o auth.db atual)
echo.

set /p BACKUP_FILE="Nome do arquivo de backup (ou ENTER para pular): "

if "%BACKUP_FILE%"=="" (
    echo.
    echo Mantendo o banco de dados atual (auth.db)
    echo.
) else (
    if exist "%BACKUP_FILE%" (
        echo.
        echo Fazendo backup do auth.db atual...
        copy auth.db auth.db.old
        
        echo Substituindo auth.db pelo backup escolhido...
        copy /Y "%BACKUP_FILE%" auth.db
        
        echo.
        echo ✓ Banco de dados atualizado com sucesso!
        echo   Backup anterior salvo como: auth.db.old
        echo.
    ) else (
        echo.
        echo ERRO: Arquivo "%BACKUP_FILE%" nao encontrado!
        echo Certifique-se de que o arquivo esta nesta pasta.
        echo.
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo   PROXIMOS PASSOS PARA DEPLOY
echo ========================================
echo.
echo Opcao 1: DEPLOY MANUAL (Mais Facil)
echo   1. Compacte esta pasta inteira em ZIP
echo   2. Acesse: https://dashboard.render.com
echo   3. Entre no servico "BelgaAuthAPII"
echo   4. Va em Settings ^> Manual Deploy
echo   5. Faca upload do ZIP
echo.
echo Opcao 2: DEPLOY VIA GIT (Se tiver Git instalado)
echo   Execute: FazerDeploy.bat
echo.
echo ========================================
echo.

pause

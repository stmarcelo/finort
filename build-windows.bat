@echo off
setlocal enabledelayedexpansion

echo === Finort - Build para Windows x64 ===
echo.

:: Configuracoes
set CONFIGURATION=Release
set RUNTIME=win-x64
set PROJECT=src\aspnet\Finort.csproj
set OUTPUT=src\aspnet\bin\Release\net9.0\%RUNTIME%\publish
set INSTALLER_SCRIPT=installer\finort.iss

:: Ler versao do Version.props
echo [0/6] Lendo versao do Version.props...
for /f "tokens=2 delims=>" %%a in ('findstr "<Version>" Version.props') do (
    for /f "tokens=1 delims=<" %%b in ("%%a") do set VERSION=%%b
)
if not defined VERSION (
    echo ERRO: Nao foi possivel ler a versao do Version.props
    goto :error
)
echo         Versao: %VERSION%

:: Limpar builds anteriores
echo [1/6] Limpando builds anteriores...
dotnet clean %PROJECT% -c %CONFIGURATION% -r %RUNTIME% --nologo -v q
if %ERRORLEVEL% neq 0 goto :error

:: Restaurar pacotes para o runtime
echo [2/6] Restaurando pacotes para %RUNTIME%...
dotnet restore %PROJECT% -r %RUNTIME% --nologo
if %ERRORLEVEL% neq 0 goto :error

:: Publicar aplicacao
echo [3/6] Publicando aplicacao...
dotnet publish %PROJECT% -c %CONFIGURATION% -r %RUNTIME% --self-contained true -p:PublishSingleFile=false --nologo --no-restore
if %ERRORLEVEL% neq 0 goto :error

:: Publicar updater
echo [4/6] Publicando updater...
dotnet publish tools\Updater\Updater.csproj -c %CONFIGURATION% -r %RUNTIME% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true --nologo
if %ERRORLEVEL% neq 0 goto :error

:: Verificar se Inno Setup esta instalado
echo [5/6] Verificando Inno Setup...
set "ISCC="
where iscc >nul 2>&1
if %ERRORLEVEL% equ 0 (
    set "ISCC=iscc"
) else if exist "C:\Users\stmar\AppData\Local\Programs\Inno Setup 7\ISCC.exe" (
    set "ISCC=C:\Users\stmar\AppData\Local\Programs\Inno Setup 7\ISCC.exe"
) else if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
)

:: Compilar instalador
if defined ISCC (
    echo [6/6] Compilando instalador...
    "%ISCC%" /DMyAppVersion=%VERSION% "%INSTALLER_SCRIPT%"
    if !ERRORLEVEL! neq 0 goto :error
) else (
    echo [6/6] Inno Setup nao encontrado - pulando compilacao do instalador.
    echo        Para instalar: https://jrsoftware.org/isinfo.php
)

echo.
echo === Build concluido com sucesso! ===
if defined ISCC (
    echo Installer: releases\finort-%VERSION%-win-x64-setup.exe
) else (
    echo Publicacao: src\aspnet\bin\Release\net9.0\win-x64\publish\
    echo Para criar o instalador, instale o Inno Setup e execute novamente.
)
goto :end

:error
echo.
echo === Build falhou! ===
exit /b 1

:end
endlocal

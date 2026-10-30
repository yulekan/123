@echo off
rem Запуск приложения двойным щелчком.
cd /d "%~dp0"

if exist "bin\Debug\net48\PPEAccounting.exe" (
    start "" "bin\Debug\net48\PPEAccounting.exe"
) else (
    echo Сборка не найдена, собираю...
    dotnet build PPEAccounting.csproj
    start "" "bin\Debug\net48\PPEAccounting.exe"
)

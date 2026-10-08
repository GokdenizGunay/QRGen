@echo off
rem QRGen - tek dosya, tasinabilir exe olusturur (publish\QRGen.exe)
cd /d "%~dp0"
dotnet publish QRGen.csproj -c Release -o publish
if errorlevel 1 exit /b 1
echo.
echo Hazir: %~dp0publish\QRGen.exe

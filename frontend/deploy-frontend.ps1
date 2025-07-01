#!/usr/bin/env pwsh
# Script de Deploy Automático - Frontend Interventional Academy
# Uso: .\deploy-frontend.ps1

param(
    [string]$ResourceGroup = "rg-interventional-academy",
    [string]$AppName = "interventional-academy-frontend",
    [switch]$SkipBuild = $false
)

Write-Host "🚀 Deploy Frontend - Interventional Academy" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "IA.FrontEnd.csproj")) {
    Write-Host "❌ Error: Este script debe ejecutarse desde el directorio frontend/" -ForegroundColor Red
    exit 1
}

# Paso 1: Limpiar y compilar
if (-not $SkipBuild) {
    Write-Host "🧹 Limpiando proyecto..." -ForegroundColor Yellow
    dotnet clean
    if ($LASTEXITCODE -ne 0) { 
        Write-Host "❌ Error en dotnet clean" -ForegroundColor Red
        exit 1 
    }

    Write-Host "🔨 Compilando en modo Release..." -ForegroundColor Yellow
    dotnet publish -c Release
    if ($LASTEXITCODE -ne 0) { 
        Write-Host "❌ Error en dotnet publish" -ForegroundColor Red
        exit 1 
    }
} else {
    Write-Host "⏭️  Saltando build (SkipBuild = true)" -ForegroundColor Yellow
}

# Paso 2: Crear ZIP
Write-Host "📦 Creando archivo ZIP..." -ForegroundColor Yellow

$wwwrootPath = "bin\Release\net8.0\publish\wwwroot"
if (-not (Test-Path $wwwrootPath)) {
    Write-Host "❌ Error: No se encontró $wwwrootPath" -ForegroundColor Red
    Write-Host "   Ejecuta el script sin -SkipBuild" -ForegroundColor Red
    exit 1
}

# Generar nombre único para el ZIP
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$zipName = "frontend-deploy-$timestamp.zip"
$zipPath = "bin\Release\net8.0\publish\$zipName"

Push-Location $wwwrootPath
Compress-Archive -Path ".\*" -DestinationPath "..\$zipName" -Force
Pop-Location

if (-not (Test-Path $zipPath)) {
    Write-Host "❌ Error: No se pudo crear el ZIP" -ForegroundColor Red
    exit 1
}

Write-Host "✅ ZIP creado: $zipName" -ForegroundColor Green

# Paso 3: Deploy a Azure
Write-Host "☁️  Deploying a Azure..." -ForegroundColor Yellow
Write-Host "   Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host "   App Service: $AppName" -ForegroundColor Cyan

az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $zipPath --type zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "🎉 Deploy completado exitosamente!" -ForegroundColor Green
    Write-Host "🌐 URL: https://$AppName.azurewebsites.net" -ForegroundColor Green
    
    # Limpiar ZIP temporal
    Remove-Item $zipPath -Force
    Write-Host "🧹 Archivo temporal eliminado" -ForegroundColor Gray
} else {
    Write-Host "❌ Error en el deploy" -ForegroundColor Red
    Write-Host "📁 ZIP disponible en: $zipPath" -ForegroundColor Yellow
    exit 1
}

Write-Host "================================================" -ForegroundColor Green
Write-Host "✨ Deploy completado!" -ForegroundColor Green
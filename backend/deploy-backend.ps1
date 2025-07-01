#!/usr/bin/env pwsh
# Script de Deploy Simplificado - Backend Interventional Academy
# Con Azure Blob Storage ya no necesitamos backup/restore de archivos
# Uso: .\deploy-backend-simple.ps1
param(
    [string]$ResourceGroup = "rg-interventional-academy",
    [string]$AppName = "interventional-academy-api",
    [switch]$SkipBuild = $false
)

Write-Host "🚀 Deploy Backend - Interventional Academy (Blob Storage)" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "IA.WebAPI.csproj")) {
    Write-Host "❌ Error: Este script debe ejecutarse desde el directorio backend/" -ForegroundColor Red
    exit 1
}

# Variables
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

try {
    # Paso 1: Limpiar y compilar
    if (-not $SkipBuild) {
        Write-Host "🧹 Limpiando proyecto..." -ForegroundColor Yellow
        dotnet clean
        if ($LASTEXITCODE -ne 0) { 
            Write-Host "❌ Error en dotnet clean" -ForegroundColor Red
            exit 1 
        }

        Write-Host "🔨 Compilando en modo Release..." -ForegroundColor Yellow
        dotnet publish -c Release -o publish
        if ($LASTEXITCODE -ne 0) { 
            Write-Host "❌ Error en dotnet publish" -ForegroundColor Red
            exit 1 
        }
        Write-Host "✅ Build completado" -ForegroundColor Green
    } else {
        Write-Host "⏭️  Saltando build (SkipBuild = true)" -ForegroundColor Yellow
    }

    # Paso 2: Crear ZIP
    Write-Host "📦 Creando archivo ZIP..." -ForegroundColor Yellow
    $publishPath = "publish"
    if (-not (Test-Path $publishPath)) {
        Write-Host "❌ Error: No se encontró $publishPath" -ForegroundColor Red
        Write-Host "   Ejecuta el script sin -SkipBuild" -ForegroundColor Red
        exit 1
    }

    $zipName = "backend-deploy-$timestamp.zip"
    $zipPath = ".\$zipName"

    Push-Location $publishPath
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
    Write-Host "   📁 Archivos ahora en Blob Storage (persistentes)" -ForegroundColor Cyan

    az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $zipPath --type zip

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Deploy completado exitosamente!" -ForegroundColor Green
        Write-Host "🌐 URL: https://$AppName.azurewebsites.net" -ForegroundColor Green
        Write-Host "📚 Swagger: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Green
        Write-Host "🗄️  Storage: Videos y thumbnails en Azure Blob Storage" -ForegroundColor Green
        
        # Limpiar archivos temporales
        Remove-Item $zipPath -Force
        Remove-Item $publishPath -Recurse -Force
        Write-Host "🧹 Archivos temporales eliminados" -ForegroundColor Gray
        
        Write-Host "" -ForegroundColor Green
        Write-Host "💡 Configuración de Azure Storage:" -ForegroundColor Yellow
        Write-Host "   ✅ UseAzureStorage = true (Producción)" -ForegroundColor Gray
        Write-Host "   ✅ Videos: iaacademystorage2025/videos" -ForegroundColor Gray
        Write-Host "   ✅ Thumbnails: iaacademystorage2025/thumbnails" -ForegroundColor Gray
        
    } else {
        Write-Host "❌ Error en el deploy" -ForegroundColor Red
        Write-Host "📁 ZIP disponible en: $zipPath" -ForegroundColor Yellow
        exit 1
    }

} catch {
    Write-Host "❌ Error general: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "=======================================================" -ForegroundColor Green
Write-Host "✨ Deploy con Azure Blob Storage completado!" -ForegroundColor Green
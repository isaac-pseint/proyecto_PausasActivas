<#
.SYNOPSIS
    Script de empaquetado y publicación para PausasActivas (SENA).
.DESCRIPTION
    Compila y empaqueta PausasEmpleado y PausasAdmin como ejecutables únicos (.exe)
    autocontenidos para Windows x64 (.NET 8).
    Garantiza que appsettings.json y appsettings.example.json queden fuera del bundle
    y PRESERVA las contraseñas ya configuradas por el usuario.
#>

[CmdletBinding()]
param(
    [string]$Proyecto = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " PAUSAS ACTIVAS CORPORATIVAS - PUBLICACIÓN DE EJECUTABLES (.EXE) " -ForegroundColor Cyan
Write-Host " Plataforma: Windows x64 | Framework: .NET 8 (Self-Contained)    " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

$baseDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($baseDir)) {
    $baseDir = (Get-Location).Path
}

$publishRoot = Join-Path $baseDir "publish"
$outEmpleado = Join-Path $publishRoot "PausasEmpleado"
$outAdmin = Join-Path $publishRoot "PausasAdmin"

# 1. Respaldar contraseñas/configuraciones existentes para no sobrescribirlas
Write-Host "[1/4] Comprobando configuraciones existentes en .\publish\..." -ForegroundColor Yellow

$backupEmpleadoJson = $null
$backupAdminJson = $null

$cfgEmpPath = Join-Path $outEmpleado "appsettings.json"
$cfgAdmPath = Join-Path $outAdmin "appsettings.json"

if (Test-Path $cfgEmpPath) {
    $backupEmpleadoJson = Get-Content -Raw -Path $cfgEmpPath
    Write-Host "   -> Configuración existente de PausasEmpleado respaldada en memoria." -ForegroundColor DarkGray
}

if (Test-Path $cfgAdmPath) {
    $backupAdminJson = Get-Content -Raw -Path $cfgAdmPath
    Write-Host "   -> Configuración existente de PausasAdmin respaldada en memoria." -ForegroundColor DarkGray
}

# Preparar carpetas de salida
if (-not (Test-Path $publishRoot)) {
    New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
}
if (-not (Test-Path $outEmpleado)) {
    New-Item -ItemType Directory -Force -Path $outEmpleado | Out-Null
}
if (-not (Test-Path $outAdmin)) {
    New-Item -ItemType Directory -Force -Path $outAdmin | Out-Null
}

# Limpiar ejecutables previos para asegurar empaquetado fresco
if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasEmpleado") {
    Get-ChildItem -Path $outEmpleado -Filter "*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force
}
if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasAdmin") {
    Get-ChildItem -Path $outAdmin -Filter "*.exe" -ErrorAction SilentlyContinue | Remove-Item -Force
}

# 2. Publicación de PausasEmpleado
if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasEmpleado") {
    Write-Host "[2/4] Publicando PausasEmpleado (win-x64, SingleFile)..." -ForegroundColor Yellow
    $argsEmpleado = @(
        "publish",
        "PausasEmpleado\PausasEmpleado.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:PublishDir=..\publish\PausasEmpleado\"
    )
    & dotnet @argsEmpleado
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Falló la publicación de PausasEmpleado." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[2/4] Omitiendo PausasEmpleado (publicación selectiva: $Proyecto)." -ForegroundColor DarkGray
}

# 3. Publicación de PausasAdmin
if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasAdmin") {
    Write-Host "[3/4] Publicando PausasAdmin (win-x64, SingleFile)..." -ForegroundColor Yellow
    $argsAdmin = @(
        "publish",
        "PausasAdmin\PausasAdmin.csproj",
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:EnableCompressionInSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "-p:PublishDir=..\publish\PausasAdmin\"
    )
    & dotnet @argsAdmin
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Falló la publicación de PausasAdmin." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[3/4] Omitiendo PausasAdmin (publicación selectiva: $Proyecto)." -ForegroundColor DarkGray
}

# 4. Restauración de credenciales previas y verificación
Write-Host "[4/4] Restaurando y verificando archivos de configuración..." -ForegroundColor Yellow

if ($backupEmpleadoJson) {
    Set-Content -Path $cfgEmpPath -Value $backupEmpleadoJson -Encoding utf8
    Write-Host "   [PRESERVADO] appsettings.json de PausasEmpleado restaurado con su contraseña previa." -ForegroundColor Green
}

if ($backupAdminJson) {
    Set-Content -Path $cfgAdmPath -Value $backupAdminJson -Encoding utf8
    Write-Host "   [PRESERVADO] appsettings.json de PausasAdmin restaurado con su contraseña previa." -ForegroundColor Green
}

function Validar-Salida($carpeta, $exeName, $appTitle) {
    $exePath = Join-Path $carpeta $exeName
    $configPath = Join-Path $carpeta "appsettings.json"
    $examplePath = Join-Path $carpeta "appsettings.example.json"

    Write-Host ""
    Write-Host ">> $appTitle" -ForegroundColor Green
    if (Test-Path $exePath) {
        $sizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)
        Write-Host "   [OK] Ejecutable: $exeName ($sizeMb MB)" -ForegroundColor Gray
    } else {
        Write-Host "   [ERROR] No se encontró $exeName" -ForegroundColor Red
    }

    if (Test-Path $configPath) {
        Write-Host "   [OK] Configuración externa: appsettings.json (editable sin recompilar)" -ForegroundColor Gray
    } else {
        Write-Host "   [AVISO] appsettings.json no encontrado (generando desde example...)" -ForegroundColor Yellow
        if (Test-Path $examplePath) {
            Copy-Item $examplePath $configPath
        }
    }

    if (Test-Path $examplePath) {
        Write-Host "   [OK] Plantilla externa: appsettings.example.json" -ForegroundColor Gray
    }
}

if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasEmpleado") {
    Validar-Salida $outEmpleado "PausasEmpleado.exe" "MÓDULO EMPLEADO (Segundo plano)"
}
if ($Proyecto -eq "" -or $Proyecto -eq "Todos" -or $Proyecto -eq "PausasAdmin") {
    Validar-Salida $outAdmin "PausasAdmin.exe" "MÓDULO ADMINISTRADOR (Panel de control)"
}

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " ¡PUBLICACIÓN COMPLETADA EXITOSAMENTE!                           " -ForegroundColor Cyan
Write-Host " Directorio de entrega: .\publish\                               " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

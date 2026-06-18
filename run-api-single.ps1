$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "LicitIA.Api\LicitIA.Api.csproj"
$output = Join-Path $root ".run\api-single"
$exe = Join-Path $output "LicitIA.Api.exe"

$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Publicando LicitIA API como ejecutable single-file..."
dotnet publish $project `
    -c Debug `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:UseAppHost=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Write-Host "Iniciando LicitIA API en http://localhost:5153 ..."
& $exe

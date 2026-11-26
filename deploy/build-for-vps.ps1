param(
    [string]$ImageName = "wolf-and-rabbits:latest",
    [string]$Platform = "linux/amd64",
    [string]$OutputTar = "dist/wolf-and-rabbits.tar"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $RepoRoot

try {
    if (-not (Test-Path "Server/wwwroot/index.html")) {
        if (Test-Path "Client/Build/Web/index.html") {
            Write-Host "Syncing Client/Build/Web -> Server/wwwroot..." -ForegroundColor Cyan
            Copy-Item -Path "Client/Build/Web" -Destination "Server/wwwroot" -Recurse -Force
        } else {
            throw "Không tìm thấy Server/wwwroot/index.html (hoặc Client/Build/Web/index.html)."
        }
    }

    Write-Host "[1/2] Building Docker image '$ImageName' for $Platform..." -ForegroundColor Cyan
    docker build --platform $Platform -t $ImageName .
    if ($LASTEXITCODE -ne 0) { throw "Docker build failed." }

    $OutDir = Split-Path -Parent $OutputTar
    if ($OutDir -and -not (Test-Path $OutDir)) {
        New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
    }

    Write-Host "[2/2] Exporting image to '$OutputTar'..." -ForegroundColor Cyan
    docker save -o $OutputTar $ImageName
    if ($LASTEXITCODE -ne 0) { throw "Docker save failed." }

    $SizeMB = [Math]::Round((Get-Item $OutputTar).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Done! Image exported: $OutputTar ($SizeMB MB)" -ForegroundColor Green
    Write-Host "Copy to Ubuntu VPS:" -ForegroundColor Yellow
    Write-Host "  scp $OutputTar docker-compose.yml user@<VPS_IP>:~/wolf-and-rabbits/"
    Write-Host "Run on Ubuntu VPS:" -ForegroundColor Yellow
    Write-Host "  cd ~/wolf-and-rabbits && docker load -i wolf-and-rabbits.tar && docker compose up -d"
}
finally {
    Pop-Location
}

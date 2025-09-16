param(
    [string]$fileName
)

$baseUrl = "https://raw.githubusercontent.com/RSDKModding/RSDKv4-Decompilation/main/RSDKv4"
$targetDir = "Hybrid-RSDK Main/RSDKV4/RSDKV4"

if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force
}

$url = "$baseUrl/$fileName"
$output = Join-Path $targetDir $fileName
Write-Host "Downloading $fileName from $url"
try {
    Invoke-WebRequest -Uri $url -OutFile $output -ErrorAction Stop
    Write-Host "Successfully downloaded $fileName"
}
catch {
    Write-Host "Failed to download $fileName"
    Write-Host $_.Exception.Message
    exit 1
}

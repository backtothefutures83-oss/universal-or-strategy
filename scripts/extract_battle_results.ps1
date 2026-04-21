$downloads = "C:\Users\Mohammed Khalid\Downloads"
$dest = "C:\tmp\battle_antigravity_os"

if (!(Test-Path $dest)) { New-Item -ItemType Directory -Path $dest }

# Clear existing contents if any
Get-ChildItem $dest | Remove-Item -Recurse -Force

# Categorize Files
# String Prompts: (base), (1)-(8), nativeaot...
$stringZips = @(
    "antigravity-os-abstraction-layer.zip",
    "antigravity-os-abstraction-layer (1).zip",
    "antigravity-os-abstraction-layer (2).zip",
    "antigravity-os-abstraction-layer (3).zip",
    "antigravity-os-abstraction-layer (4).zip",
    "antigravity-os-abstraction-layer (5).zip",
    "antigravity-os-abstraction-layer (6).zip",
    "antigravity-os-abstraction-layer (7).zip",
    "antigravity-os-abstraction-layer (8).zip",
    "nativeaot-trading-abstraction-layer.zip"
)

# Long Prompts: (9)-(18)
$longZips = @(
    "antigravity-os-abstraction-layer (9).zip",
    "antigravity-os-abstraction-layer (10).zip",
    "antigravity-os-abstraction-layer (11).zip",
    "antigravity-os-abstraction-layer (12).zip",
    "antigravity-os-abstraction-layer (13).zip",
    "antigravity-os-abstraction-layer (14).zip",
    "antigravity-os-abstraction-layer (15).zip",
    "antigravity-os-abstraction-layer (16).zip",
    "antigravity-os-abstraction-layer (17).zip",
    "antigravity-os-abstraction-layer (18).zip"
)

Write-Host "[*] Extracting String Prompts..."
for ($i=0; $i -lt $stringZips.Length; $i++) {
    $zip = Join-Path $downloads $stringZips[$i]
    $folder = Join-Path $dest ("S_" + $i.ToString("00"))
    if (Test-Path $zip) {
        Write-Host " -> Extracting $($stringZips[$i]) to S_$($i.ToString('00'))"
        Expand-Archive -Path $zip -DestinationPath $folder -Force
    } else {
        Write-Warning "File not found: $zip"
    }
}

Write-Host "[*] Extracting Long Prompts..."
for ($i=0; $i -lt $longZips.Length; $i++) {
    $zip = Join-Path $downloads $longZips[$i]
    $folder = Join-Path $dest ("L_" + ($i+10).ToString("00"))
    if (Test-Path $zip) {
        Write-Host " -> Extracting $($longZips[$i]) to L_$($i+10)"
        Expand-Archive -Path $zip -DestinationPath $folder -Force
    } else {
        Write-Warning "File not found: $zip"
    }
}

Write-Host "[+] Done."

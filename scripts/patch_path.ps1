$profilePath = $PROFILE
if (!(Test-Path $profilePath)) {
    New-Item -Path $profilePath -ItemType File -Force
}
$pathLine = '$env:PATH = "$env:USERPROFILE\.local\bin;$env:PATH"'
if (!(Select-String -Path $profilePath -Pattern '\.local\\bin')) {
    Add-Content -Path $profilePath -Value $pathLine
    Write-Host "PATH updated in $profilePath"
} else {
    Write-Host "PATH already contains goose entry."
}

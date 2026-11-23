param(
    [string]$TargetDir
)

$cert = Get-Item Cert:\CurrentUser\My\DF4901019AAA193F9001FB9B33321BF18D15BFF5 -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Warning "SparkDev code-signing certificate not found."
    return
}

$filesToSign = if ($TargetDir -and (Test-Path $TargetDir)) {
    Get-ChildItem -Path $TargetDir -Filter "OpenClaw*" -Recurse -File | Where-Object { $_.Extension -in @(".dll", ".exe") }
} else {
    Get-ChildItem -Path "src", "tests" -Filter "OpenClaw*" -Recurse -File | Where-Object { $_.Extension -in @(".dll", ".exe") }
}

foreach ($file in $filesToSign) {
    try {
        Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert | Out-Null
    } catch {
        # ignore locked files or native assemblies
    }
}
Write-Host "Signed $($filesToSign.Count) OpenClaw binaries with SparkDev cert."

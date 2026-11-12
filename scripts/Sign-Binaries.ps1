param(
    [string]$TargetDir
)

$cert = Get-Item Cert:\CurrentUser\My\DF4901019AAA193F9001FB9B33321BF18D15BFF5 -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Warning "SparkDev code-signing certificate not found."
    return
}

$filesToSign = if ($TargetDir -and (Test-Path $TargetDir)) {
    Get-ChildItem -Path $TargetDir -Include "*.dll", "*.exe" -Recurse -File
} else {
    Get-ChildItem -Path "src", "tests" -Include "*.dll", "*.exe" -Recurse -File
}

foreach ($file in $filesToSign) {
    try {
        Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert | Out-Null
    } catch {
        # ignore locked files or native assemblies
    }
}
Write-Host "Signed $($filesToSign.Count) binaries with SparkDev cert."

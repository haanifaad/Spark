$cert = Get-Item Cert:\CurrentUser\My\DF4901019AAA193F9001FB9B33321BF18D15BFF5 -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Error "SparkDev cert not found"
    return
}

# Sign all built OpenClaw binaries in src
Get-ChildItem -Recurse "C:\Intel\Spark\Spark-main\src" -Include "*.dll","*.exe" | Where-Object {
    $_.FullName -notlike "*\obj\*" -and $_.Name -like "OpenClaw*"
} | ForEach-Object {
    $sig = Get-AuthenticodeSignature $_.FullName
    if ($sig.Status -ne "Valid") {
        Set-AuthenticodeSignature -FilePath $_.FullName -Certificate $cert | Out-Null
        Write-Host "Signed src binary: $($_.FullName)"
    }
}

# Sign test output directory
$testBin = "C:\Intel\Spark\Spark-main\tests\OpenClaw.Shared.Tests\bin\Debug\net10.0"
Get-ChildItem $testBin -Include "*.dll","*.exe" -File | Where-Object {
    $_.Name -like "OpenClaw*"
} | ForEach-Object {
    Set-AuthenticodeSignature -FilePath $_.FullName -Certificate $cert | Out-Null
    Write-Host "Signed test binary: $($_.Name)"
}

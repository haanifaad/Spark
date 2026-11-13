param(
    [string]$TargetDir
)

function Remove-PECertificateTable {
    param([string]$FilePath)
    try {
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        if ($bytes.Length -lt 0x40) { return }
        $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
        if ($peOffset -le 0 -or $peOffset + 4 + 20 + 2 -gt $bytes.Length) { return }
        if ($bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45) { return }
        
        $optHeaderOffset = $peOffset + 4 + 20
        $magic = [BitConverter]::ToUInt16($bytes, $optHeaderOffset)
        
        $secDirOffset = 0
        if ($magic -eq 0x020B) {
            # PE32+ (64-bit)
            $secDirOffset = $optHeaderOffset + 144
        } elseif ($magic -eq 0x010B) {
            # PE32 (32-bit)
            $secDirOffset = $optHeaderOffset + 128
        } else {
            return
        }

        if ($secDirOffset + 8 -gt $bytes.Length) { return }
        $certOffset = [BitConverter]::ToInt32($bytes, $secDirOffset)
        $certSize = [BitConverter]::ToInt32($bytes, $secDirOffset + 4)

        if ($certOffset -gt 0 -and $certSize -gt 0 -and ($certOffset + $certSize) -le $bytes.Length) {
            [Array]::Clear($bytes, $secDirOffset, 8)
            $trimmed = New-Object byte[] $certOffset
            [Array]::Copy($bytes, 0, $trimmed, 0, $certOffset)
            [System.IO.File]::WriteAllBytes($FilePath, $trimmed)
            return $true
        }
    } catch {
        # ignore file lock or read issues
    }
    return $false
}

$dirs = if ($TargetDir -and (Test-Path $TargetDir)) { @($TargetDir) } else { @("src", "tests", "artifacts") }
$count = 0

foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) { continue }
    $files = Get-ChildItem -Path $dir -Include "*.dll", "*.exe" -Recurse -File
    foreach ($file in $files) {
        if (Remove-PECertificateTable -FilePath $file.FullName) {
            $count++
        }
    }
}

Write-Host "Unsigned $count binaries with SparkDev cert."

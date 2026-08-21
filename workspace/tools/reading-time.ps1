$source = [Console]::In.ReadToEnd()
$lines = $source -split "`r`n|`n"

$words = ($lines -join ' ') -split '\s+' | Where-Object { $_ -ne '' }
$minutes = [Math]::Max(1, [Math]::Round($words.Count / 200))
$badge = "*Estimated reading time: $minutes min ($($words.Count) words) -- computed by tools/reading-time.ps1*"

$output = New-Object System.Collections.Generic.List[string]
$inserted = $false
foreach ($line in $lines) {
    $output.Add($line)
    if (-not $inserted -and $line -match '^#\s') {
        $output.Add('')
        $output.Add($badge)
        $inserted = $true
    }
}
if (-not $inserted) {
    $output.Insert(0, $badge)
}

[Console]::Out.Write(($output -join "`n"))

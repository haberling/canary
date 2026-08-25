# Starter toolchain tool, in PowerShell rather than C# -- a tool is just
# an external command that reads a page's markdown from stdin and writes
# transformed markdown to stdout, so it can be written in anything, not
# just .NET. PowerShell (via `powershell.exe`, not `pwsh`) ships with
# every supported Windows install, same as cmd.exe -- no separate
# language runtime to install, unlike e.g. Python or Node. Registered as
# "reading-time" in canary.jsonc's "tools" registry, but not applied
# anywhere yet -- add "reading-time" to a content directory's own
# .toolchain.json to actually run it there. See PLAN.md's "Content
# toolchain" section for the full design (stdin/stdout contract,
# execution order).
#
# Computes a real word count (not an estimate) and inserts a reading-time
# badge right after the page's first `#` heading, or at the very top if
# it has none.
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

$ErrorActionPreference = 'Stop'
$runtimeCommand = Get-Command node -ErrorAction SilentlyContinue
$runtimePath = if ($runtimeCommand) { $runtimeCommand.Source } else { Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe' }
if (-not (Test-Path -LiteralPath $runtimePath)) { throw 'Node.js 22 or later is required. Install the current LTS release from nodejs.org.' }
Write-Host 'Local preview: http://127.0.0.1:8765/ (Ctrl+C to stop)'
& $runtimePath (Join-Path $PSScriptRoot 'scripts/dev.mjs')

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not (Test-Path -LiteralPath 'Builds/WebGL/index.html') -and -not (Test-Path -LiteralPath 'docs/index.html')) { throw 'WebGL build not found. Build it in Unity: Catch AR > 2. Build WebGL.' }
Write-Host 'Open http://localhost:8080. Stop the server with Ctrl+C.'
& node (Join-Path $PSScriptRoot 'Tools/serve.mjs')

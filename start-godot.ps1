param([string]$ProjectPath)

$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v*mono*.exe" -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $godot) {
    Write-Error "Godot not found under $env:LOCALAPPDATA\Microsoft\WinGet\Packages. Run 'make install-win' first."
    exit 1
}

Write-Host "Launching: $godot"
Start-Process $godot -ArgumentList "`"$ProjectPath`""

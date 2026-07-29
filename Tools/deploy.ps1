# Volleyball release deploy — build everything with one version stamp, push the
# server to the Linux box, the WebGL build to the web root, and zip the Windows
# client for sharing. Run from anywhere; the Unity editor must be CLOSED.
#
#   powershell -File Tools\deploy.ps1                # build + deploy everything
#   powershell -File Tools\deploy.ps1 -SkipWebGL     # faster: server + windows only
#
# First-time setup: copy Tools\deploy.config.example.json to Tools\deploy.config.json
# (gitignored) and fill in your box's details.

param(
    [switch]$SkipWebGL,
    [switch]$SkipDeploy   # build + zip only, no scp
)

$ErrorActionPreference = "Stop"
$proj = Split-Path -Parent $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe"
$cfgPath = Join-Path $PSScriptRoot "deploy.config.json"

if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue) {
    throw "Unity editor is running - close it first (builds need the project lock)."
}
if (-not $SkipDeploy -and -not (Test-Path $cfgPath)) {
    throw "Missing $cfgPath - copy deploy.config.example.json and fill it in."
}
$cfg = if (Test-Path $cfgPath) { Get-Content $cfgPath | ConvertFrom-Json } else { $null }

# one version stamp for the whole release: base version + short commit hash;
# the in-game version handshake only lets same-stamp builds play together
$base = (Select-String -Path "$proj\ProjectSettings\ProjectSettings.asset" `
        -Pattern "^\s*bundleVersion:\s*(\S+)").Matches[0].Groups[1].Value -replace "\+.*", ""
$hash = (git -C $proj rev-parse --short HEAD).Trim()
$env:VB_VERSION = "$base+$hash"
Write-Host "=== Deploying Volleyball $env:VB_VERSION ===" -ForegroundColor Cyan

function Invoke-UnityBuild($method, $label) {
    Write-Host "building $label..." -ForegroundColor Yellow
    $log = Join-Path $env:TEMP "vb_deploy_$label.log"
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit', `
        '-projectPath',"`"$proj`"",'-logFile',"`"$log`"",'-executeMethod',$method -Wait -PassThru
    $ok = Select-String -Path $log -Pattern "BUILD OK" -Quiet
    if (-not $ok) {
        Select-String -Path $log -Pattern "BUILD FAIL|error CS" | Select-Object -First 10
        throw "$label build failed (log: $log)"
    }
    Write-Host "  $label OK" -ForegroundColor Green
}

Invoke-UnityBuild "Volleyball.EditorTools.BuildKit.BuildLinuxServer" "linux-server"
Invoke-UnityBuild "Volleyball.EditorTools.BuildKit.BuildWindows" "windows"
if (-not $SkipWebGL) { Invoke-UnityBuild "Volleyball.EditorTools.BuildKit.BuildWebGL" "webgl" }

# Windows client zip, stamped, for sharing with friends
$zip = "$proj\Builds\Volleyball-win-$($env:VB_VERSION -replace '\+','_').zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$proj\Builds\Windows\*" -DestinationPath $zip
Write-Host "windows zip: $zip" -ForegroundColor Green

if ($SkipDeploy) { Write-Host "(-SkipDeploy: not pushing to the box)"; exit 0 }

$dest = "$($cfg.user)@$($cfg.host)"

# game server -> box (delete-then-copy gives fresh inodes; matches already
# running keep executing their old, now-unlinked binary untouched)
Write-Host "pushing server to $dest`:$($cfg.serverPath)..." -ForegroundColor Yellow
ssh $dest "mkdir -p $($cfg.serverPath) && rm -rf $($cfg.serverPath)/*"
scp -r "$proj\Builds\LinuxServer\*" "$dest`:$($cfg.serverPath)/"
ssh $dest "chmod +x $($cfg.serverPath)/Volleyball.x86_64"

# spawn service script (idempotent copy; restart picks up any changes)
scp "$PSScriptRoot\server\vb-spawn.py" "$dest`:$($cfg.spawnScriptPath)"
ssh $dest "systemctl --user restart volleyball-spawn 2>/dev/null || echo '(spawn service not installed yet - see Tools/server/volleyball-spawn.service)'"

# WebGL -> web root
if (-not $SkipWebGL -and $cfg.webglPath) {
    Write-Host "pushing WebGL to $dest`:$($cfg.webglPath)..." -ForegroundColor Yellow
    ssh $dest "mkdir -p $($cfg.webglPath)"
    scp -r "$proj\Builds\WebGL\*" "$dest`:$($cfg.webglPath)/"
}

Write-Host "=== Deploy complete: $env:VB_VERSION ===" -ForegroundColor Cyan
Write-Host "Friends on older builds will be told to update - send them the new zip."

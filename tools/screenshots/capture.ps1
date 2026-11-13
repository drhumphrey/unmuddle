# Takes the README screenshots from an installed or published copy of Unmuddle.
#
# It stops the app, backs up settings.json, plants a made-up draft, mutes the voice, opens each
# window with a developer switch, captures only that window (shot.ps1), puts the settings back,
# and writes cleaned PNGs with transparent margins into docs/images. Nothing else on the screen
# is ever captured, and no clicks or key presses are sent.
#
# Compose's notes only appear if an AI helper is set up (On this PC with the model downloaded,
# or Claude with a key). Without one, the Compose shot shows the draft and the greyed buttons.
#
#   powershell -NoProfile -File tools\screenshots\capture.ps1
#   powershell -NoProfile -File tools\screenshots\capture.ps1 -Exe out\publish\win-x64\Unmuddle.exe
#
# A list for -Only needs -Command: under -File, Windows PowerShell passes "pill,word" as one string.
#   powershell -NoProfile -Command "& '.\tools\screenshots\capture.ps1' -Only pill,word"
param(
    [string] $Exe = (Join-Path $env:LOCALAPPDATA 'Programs\Unmuddle\Unmuddle.exe'),
    [string] $OutDir = (Join-Path $PSScriptRoot '..\..\docs\images'),
    [int] $AiWaitSeconds = 45,
    [string[]] $Only = @(
        'compose', 'player', 'word', 'pill', 'welcome',
        'player-mini', 'menu',
        'settings-voice', 'settings-reading', 'settings-readbutton', 'settings-look',
        'settings-player', 'settings-ai', 'settings-startup', 'settings-privacy')
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$OutDir = [IO.Path]::GetFullPath($OutDir)
$work = Join-Path $env:TEMP 'helpers-screenshots'
New-Item -ItemType Directory -Force -Path $work, $OutDir | Out-Null

$shot = Join-Path $PSScriptRoot 'shot.ps1'
$clean = Join-Path $PSScriptRoot 'clean.ps1'
$reply = Join-Path $PSScriptRoot 'demo-reply.md'
$settings = Join-Path $env:APPDATA 'Unmuddle\settings.json'
$backup = Join-Path $work 'settings.backup.json'

$draft = 'can you chang the login page so it remmbers the user, also when the pasword is wrong it should say somthing and the the button is to small. it worked yesterday before the update so maybe its that'

function Stop-App {
    Get-Process Unmuddle, Helpers.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Start-App([string[]] $Arguments) {
    if (-not $Arguments -or $Arguments.Count -eq 0) {
        Start-Process -FilePath $Exe | Out-Null
        return
    }

    # Start-Process splits an argument with spaces in it unless it carries its own quotes.
    $quoted = @($Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    Start-Process -FilePath $Exe -ArgumentList $quoted | Out-Null
}

function Set-Planted([hashtable] $Values) {
    $planted = Get-Content $settings -Raw | ConvertFrom-Json
    foreach ($key in $Values.Keys) {
        $planted | Add-Member -NotePropertyName $key -NotePropertyValue $Values[$key] -Force
    }

    $planted | ConvertTo-Json -Depth 20 | Set-Content $settings -Encoding UTF8
}

function Capture([string] $Title, [string] $Name, [int] $WaitSeconds, [string[]] $Arguments, [hashtable] $Set = @{}) {
    if ($Only -notcontains $Name) {
        return
    }

    Stop-App
    if ($Set.Count -gt 0) { Set-Planted $Set }
    Start-App $Arguments
    $raw = Join-Path $work ($Name + '.png')
    $deadline = (Get-Date).AddSeconds($WaitSeconds + 20)
    Start-Sleep -Seconds $WaitSeconds
    do {
        $result = & $shot -Title $Title -Out $raw 2>&1
        if ("$result" -like 'saved*') { break }
        Start-Sleep -Milliseconds 300
    } while ((Get-Date) -lt $deadline)
    Write-Output ("{0}: {1}" -f $Name, $result)
    if ("$result" -like 'saved*') {
        & $clean -In $raw -Out (Join-Path $OutDir ($Name + '.png')) | Write-Output
    }
}

Stop-App
$hadSettings = Test-Path $settings
if ($hadSettings) { Copy-Item $settings $backup -Force }

try {
    $json = if ($hadSettings) { Get-Content $settings -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    $json | Add-Member -NotePropertyName ComposeDraft -NotePropertyValue $draft -Force
    $json | Add-Member -NotePropertyName Volume -NotePropertyValue 0.0 -Force
    $json | Add-Member -NotePropertyName FirstRunDone -NotePropertyValue $true -Force
    $json | Add-Member -NotePropertyName PlayerExpanded -NotePropertyValue $true -Force
    # The Settings pages show these lists, so the shots use the app's defaults, not the user's own.
    $json | Add-Member -NotePropertyName ExcludedApps -NotePropertyValue @('1Password', 'Bitwarden', 'KeePass', 'KeePassXC', 'mstsc') -Force
    if ($json.Reading) { $json.Reading.PSObject.Properties.Remove('PronunciationEntries') }
    # One look for every picture, whichever look the user has chosen.
    $json | Add-Member -NotePropertyName Vibe -NotePropertyValue 'Custom' -Force
    $json | Add-Member -NotePropertyName GradientColours -NotePropertyValue @('#8900FF', '#FF4800', '#00D3FF') -Force
    New-Item -ItemType Directory -Force -Path (Split-Path $settings) | Out-Null
    $json | ConvertTo-Json -Depth 20 | Set-Content $settings -Encoding UTF8

    # Compose names the app that was in front as "Sending to". Check it shows nothing private before committing.
    # Tidy isn't in the default list: on a slower PC the local model may not finish in time. Ask for it with -Only tidy.
    Capture 'Compose' 'compose' $AiWaitSeconds @('--compose-check')
    Capture 'Compose' 'tidy' $AiWaitSeconds @('--compose-tidy')
    Capture 'Unmuddle player' 'player' 14 @('--read-file', $reply) @{ PlayerExpanded = $true }
    Capture 'Unmuddle player' 'player-mini' 10 @('--read-file', $reply) @{ PlayerExpanded = $false }
    Capture 'Word' 'word' 10 @('--lookup', 'necessary')
    # The Read button hides after three seconds, so start looking straight away.
    Capture 'Unmuddle read button' 'pill' 0 @('--pill')
    Capture 'Unmuddle menu' 'menu' 0 @('--menu')
    Capture 'Welcome to Unmuddle' 'welcome' 8 @('--first-run')

    $pages = [ordered]@{
        'settings-voice' = 'Voice'; 'settings-reading' = 'Reading'; 'settings-readbutton' = 'Read button'; 'settings-look' = 'Look'
        'settings-player' = 'Player'; 'settings-ai' = 'AI'; 'settings-startup' = 'Start-up'; 'settings-updates' = 'Updates'; 'settings-privacy' = 'Privacy and licences'
    }
    foreach ($page in $pages.GetEnumerator()) {
        # Nothing plays on these pages, so the Voice page can show an ordinary volume rather than the muted one.
        Capture 'Unmuddle settings' $page.Key 6 @('--settings', $page.Value) @{ Volume = 0.8 }
    }
}
finally {
    Stop-App
    if ($hadSettings) { Copy-Item $backup $settings -Force } else { Remove-Item $settings -ErrorAction SilentlyContinue }
    Write-Output 'settings put back'
    Start-App @()
}

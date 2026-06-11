<#
.SYNOPSIS
    Tests the ChatUiT2 OpenAI-compatible API on a deployed (or local) instance.
    Compatible with Windows PowerShell 5.1 and PowerShell 7+.

.DESCRIPTION
    Runs four checks against the API:
      1. GET  /v1/models without a key            -> expects 401 (middleware alive)
      2. GET  /v1/models with the key             -> expects 200 + 'personalhandbok' model
      3. POST /v1/chat/completions (non-stream)   -> expects 200 + assistant answer
      4. POST /v1/chat/completions (stream)       -> expects 200 + SSE chunks ending in [DONE]

.EXAMPLE
    .\scripts\Test-Api.ps1 -BaseUrl https://chat.uit.no -ApiKey '<prod-key>'

.EXAMPLE
    # Uses API_KEY_1 from local user secrets:
    .\scripts\Test-Api.ps1 -BaseUrl http://localhost:5055
#>
param(
    [string]$BaseUrl = 'https://chat.uit.no',
    [string]$ApiKey,
    [string]$Question = 'Hvor mange feriedager har jeg krav paa?'
)

$ErrorActionPreference = 'Stop'

if (-not $ApiKey) {
    # Fall back to API_KEY_1 from user secrets; UserSecretsId is read from the csproj
    $csproj = Join-Path $PSScriptRoot '..\ChatUiT2\ChatUiT2.csproj'
    $secretsId = (Select-String -Path $csproj -Pattern '<UserSecretsId>([^<]+)</UserSecretsId>').Matches[0].Groups[1].Value
    $secretsPath = "$env:APPDATA\Microsoft\UserSecrets\$secretsId\secrets.json"
    if (Test-Path $secretsPath) {
        $ApiKey = (Get-Content $secretsPath -Raw | ConvertFrom-Json).API_KEY_1
        Write-Host "Using API_KEY_1 from user secrets." -ForegroundColor DarkGray
    } else {
        throw "No -ApiKey given and no user secrets found at $secretsPath"
    }
    if ([string]::IsNullOrWhiteSpace($ApiKey)) {
        throw "API_KEY_1 is empty or missing in $secretsPath"
    }
}

$script:results = @()

function Add-Result([string]$Name, [bool]$Pass, [string]$Detail) {
    if ($Pass) { $status = 'PASS'; $color = 'Green' } else { $status = 'FAIL'; $color = 'Red' }
    $script:results += New-Object psobject -Property @{ Test = $Name; Result = $status; Detail = $Detail }
    Write-Host ("[{0}] {1} - {2}" -f $status, $Name, $Detail) -ForegroundColor $color
}

# Invoke-WebRequest wrapper that never throws on HTTP error status (PS 5.1 compatible).
function Invoke-Api {
    param([string]$Uri, [string]$Method = 'GET', [hashtable]$Headers = @{}, [string]$Body)
    $params = @{ Uri = $Uri; Method = $Method; Headers = $Headers; UseBasicParsing = $true; TimeoutSec = 120 }
    if ($Body) { $params.Body = $Body; $params.ContentType = 'application/json' }
    try {
        $r = Invoke-WebRequest @params
        return New-Object psobject -Property @{ Status = [int]$r.StatusCode; Body = [string]$r.Content }
    } catch {
        $resp = $_.Exception.Response
        if (-not $resp) { throw }   # network-level failure (DNS, timeout, TLS) - let caller report it
        $status = [int]$resp.StatusCode
        $text = ''
        try {
            $stream = $resp.GetResponseStream()           # PS 5.1 (HttpWebResponse)
            $reader = New-Object IO.StreamReader($stream)
            $text = $reader.ReadToEnd()
        } catch {
            try { $text = $_.ErrorDetails.Message } catch { }
        }
        return New-Object psobject -Property @{ Status = $status; Body = $text }
    }
}

Write-Host "Testing API at $BaseUrl`n" -ForegroundColor Cyan

# --- 1. No key -> 401 -------------------------------------------------------
try {
    $r = Invoke-Api -Uri "$BaseUrl/v1/models"
    Add-Result 'Rejects request without key' ($r.Status -eq 401) ("got {0}, expected 401" -f $r.Status)
} catch {
    Add-Result 'Rejects request without key' $false $_.Exception.Message
}

# --- 2. Models list ---------------------------------------------------------
try {
    $r = Invoke-Api -Uri "$BaseUrl/v1/models" -Headers @{ 'api-key' = $ApiKey }
    if ($r.Status -eq 200) {
        $ids = @(($r.Body | ConvertFrom-Json).data | ForEach-Object { $_.id })
        Add-Result 'GET /v1/models with key' ($ids -contains 'personalhandbok') ("models: " + ($ids -join ', '))
    } else {
        Add-Result 'GET /v1/models with key' $false ("got {0}: {1}" -f $r.Status, $r.Body)
    }
} catch {
    Add-Result 'GET /v1/models with key' $false $_.Exception.Message
}

# --- 3. Non-streaming completion -------------------------------------------
$bodyJson = @{
    model    = 'personalhandbok'
    messages = @(@{ role = 'user'; content = $Question })
    stream   = $false
} | ConvertTo-Json -Depth 5

try {
    $r = Invoke-Api -Uri "$BaseUrl/v1/chat/completions" -Method POST `
        -Headers @{ Authorization = "Bearer $ApiKey" } -Body $bodyJson
    if ($r.Status -eq 200) {
        $answer = ($r.Body | ConvertFrom-Json).choices[0].message.content
        $ok = -not [string]::IsNullOrWhiteSpace($answer)
        if ($ok) { $preview = ($answer.Substring(0, [Math]::Min(120, $answer.Length)) -replace "`r?`n", ' ') + '...' }
        else { $preview = '<empty answer>' }
        Add-Result 'POST /v1/chat/completions' $ok ("answer: " + $preview)
    } else {
        Add-Result 'POST /v1/chat/completions' $false ("got {0}: {1}" -f $r.Status, $r.Body)
    }
} catch {
    Add-Result 'POST /v1/chat/completions' $false $_.Exception.Message
}

# --- 4. Streaming completion ------------------------------------------------
$bodyStreamJson = $bodyJson -replace '"stream":\s*false', '"stream": true'
$client = $null
try {
    Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(120)
    $req = New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::Post, "$BaseUrl/v1/chat/completions")
    $req.Headers.Add('Authorization', "Bearer $ApiKey")
    $req.Content = New-Object System.Net.Http.StringContent ($bodyStreamJson, [Text.Encoding]::UTF8, 'application/json')
    $resp = $client.SendAsync($req, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
    if ([int]$resp.StatusCode -eq 200) {
        $reader = New-Object IO.StreamReader ($resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
        $chunks = 0
        $gotDone = $false
        while (-not $reader.EndOfStream) {
            $line = $reader.ReadLine()
            if ($line -eq 'data: [DONE]') { $gotDone = $true; break }
            if ($line -like 'data: *') { $chunks++ }
        }
        Add-Result 'POST streaming (SSE)' (($chunks -gt 0) -and $gotDone) ("{0} chunks, [DONE]: {1}" -f $chunks, $gotDone)
    } else {
        Add-Result 'POST streaming (SSE)' $false ("got {0}" -f [int]$resp.StatusCode)
    }
} catch {
    Add-Result 'POST streaming (SSE)' $false $_.Exception.Message
} finally {
    if ($client) { $client.Dispose() }
}

# --- Summary -----------------------------------------------------------------
Write-Host ''
$script:results | Format-Table Test, Result, Detail -AutoSize
$failed = @($script:results | Where-Object { $_.Result -eq 'FAIL' }).Count
exit $failed

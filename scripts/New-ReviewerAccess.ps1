param(
    [string]$BaseUrl = 'http://localhost:5234',
    [string]$OutputDirectory = '.local',
    [ValidateRange(1, 30)]
    [int]$ValidityDays = 30
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/Lamma.Api/Lamma.Api.csproj'
$accounts = [ordered]@{
    Host = 1; Rider1 = 11; Rider2 = 12; Rider3 = 13; Rider4 = 14
    UnapprovedHost = 2; InactiveRider = 15
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$rows = @()
foreach ($account in $accounts.Keys) {
    $id = '10000000-0000-0000-0000-{0:000000000000}' -f $accounts[$account]
    $token = (dotnet user-jwts create --project $project --name $id --audience lamma-demo --valid-for ($ValidityDays.ToString() + 'd') --output token | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $token -notmatch '^ey') { throw "Could not generate $account token." }
    $payload = $token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    while ($payload.Length % 4) { $payload += '=' }
    $claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
    $rows += [pscustomobject]@{ Account = $account; Token = $token; Expires = [DateTimeOffset]::FromUnixTimeSeconds($claims.exp) }
}
$expires = ($rows | Sort-Object Expires | Select-Object -First 1).Expires.ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss') + ' UTC'
$swagger = $BaseUrl.TrimEnd('/') + '/swagger/index.html'
$sections = foreach ($row in $rows) {
    $purpose = switch ($row.Account) {
        'Host' { 'Publish a ride; exactly three passenger seats.' }
        'UnapprovedHost' { 'Negative test: publishing must return 403 HOST_NOT_APPROVED.' }
        'InactiveRider' { 'Negative test: search/booking must return 403 MEMBERSHIP_INACTIVE.' }
        default { 'Search and book one seat. Use a separate fresh Idempotency-Key for each new attempt.' }
    }
    '<section><h2>' + $row.Account + '</h2><p>' + $purpose + '</p><textarea id="' + $row.Account + '" readonly aria-label="' + $row.Account + ' JWT">' +
        [System.Net.WebUtility]::HtmlEncode($row.Token) + '</textarea><button type="button" onclick="copyToken(''' + $row.Account + ''')">Copy ' + $row.Account + ' token</button></section>'
}
$html = @'
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Lamma reviewer access</title>
<style>body{font:16px/1.6 system-ui,sans-serif;max-width:920px;margin:32px auto;padding:0 20px;color:#172b39;background:#f4f7f9}section{background:white;padding:18px 24px;border:1px solid #dce5eb;border-radius:12px;margin:18px 0}h1,h2{line-height:1.2}textarea{display:block;width:100%;box-sizing:border-box;min-height:105px;font:12px/1.5 monospace;overflow-wrap:anywhere}button,.open{display:inline-block;background:#116c60;color:white;border:0;padding:10px 15px;border-radius:6px;margin-top:10px;cursor:pointer;text-decoration:none}#message{min-height:26px;font-weight:600}code{background:#e4edf1;padding:2px 5px}</style></head><body>
<h1>Lamma — reviewer access</h1><p>No project download, .NET installation or command line is required.</p>
'@
$html += '<p><a class="open" href="' + [System.Net.WebUtility]::HtmlEncode($swagger) + '" target="_blank" rel="noopener">Open live Swagger UI</a></p><p><strong>Tokens valid until: ' + $expires + '</strong>. If expired, request a refreshed sheet from the demo owner.</p>'
$html += '<ol><li>Copy the account token below.</li><li>Open Swagger and click <strong>Authorize</strong>. Log out the previous account if switching.</li><li>Paste only the token (without <code>Bearer</code>), apply credentials, and close.</li><li>Use Host to publish; Rider1–4 to search and book. Reuse the same body/key to test a retry. The fourth Rider should receive 409 after the first three book.</li></ol><p>The live link requires the owner’s computer, internet, API and tunnel to remain running. Create a fresh ride for each walkthrough so other reviewers’ bookings do not affect your test.</p><button onclick="newKey()">Generate a fresh booking Idempotency-Key</button><p id="new-key"></p><p id="message" role="status"></p>'
$html += ($sections -join "`n")
$html += @'
<script>
async function copyToken(id){const field=document.getElementById(id);try{await navigator.clipboard.writeText(field.value)}catch(e){field.focus();field.select();document.execCommand('copy')}document.getElementById('message').textContent=id+' token copied. Paste it in Swagger → Authorize.'}
function newKey(){document.getElementById('new-key').textContent=crypto.randomUUID()}
</script></body></html>
'@
$htmlPath = Join-Path $OutputDirectory 'Reviewer-Access.html'
$textPath = Join-Path $OutputDirectory 'Reviewer-Access.txt'
Set-Content -LiteralPath $htmlPath -Value $html -Encoding UTF8
$textLines = @('Lamma reviewer access', "Swagger: $swagger", "Tokens valid until: $expires", '', 'Copy the chosen token into Swagger > Authorize, without Bearer. Log out the previous account when switching.', 'Host publishes; Rider1-4 search/book; UnapprovedHost and InactiveRider test permission failures.', '')
foreach ($row in $rows) { $textLines += @('=== ' + $row.Account + ' ===', $row.Token, '') }
Set-Content -LiteralPath $textPath -Value $textLines -Encoding UTF8
Write-Output "Created $htmlPath and $textPath. Tokens omitted from console output."
Write-Output "Valid until $expires"

param(
    [ValidateSet('Host','UnapprovedHost','Rider1','Rider2','Rider3','Rider4','InactiveRider')]
    [string]$Account = 'Host'
)
$ErrorActionPreference = 'Stop'
$ids = @{
    Host = '10000000-0000-0000-0000-000000000001'
    UnapprovedHost = '10000000-0000-0000-0000-000000000002'
    Rider1 = '10000000-0000-0000-0000-000000000011'
    Rider2 = '10000000-0000-0000-0000-000000000012'
    Rider3 = '10000000-0000-0000-0000-000000000013'
    Rider4 = '10000000-0000-0000-0000-000000000014'
    InactiveRider = '10000000-0000-0000-0000-000000000015'
}
$project = Join-Path $PSScriptRoot '../src/Lamma.Api/Lamma.Api.csproj'
dotnet user-jwts create --project $project --name $ids[$Account] --audience 'lamma-demo' --valid-for 1h --output token
if ($LASTEXITCODE -ne 0) { throw 'Token creation failed.' }

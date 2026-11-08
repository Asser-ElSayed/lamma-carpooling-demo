param([string]$BaseUrl = 'http://localhost:5234')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/Lamma.Api/Lamma.Api.csproj'
function Token([int]$suffix) {
    $id = '10000000-0000-0000-0000-{0:000000000000}' -f $suffix
    $value = dotnet user-jwts create --project $project --name $id --audience lamma-demo --valid-for 1h --output token
    if ($LASTEXITCODE -ne 0) { throw 'Developer token generation failed.' }
    return $value
}
function Booking([string]$jwt, [string]$rideId, [string]$key) {
    return Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/bookings" -Method Post -ContentType 'application/json' `
        -Headers @{ Authorization = "Bearer $jwt"; 'Idempotency-Key' = $key } -Body (@{ rideId = $rideId } | ConvertTo-Json)
}
$swagger = Invoke-WebRequest -UseBasicParsing "$BaseUrl/swagger/index.html"
if ($swagger.StatusCode -ne 200 -or $swagger.Content -notmatch 'swagger-ui') { throw 'Swagger UI unavailable.' }
$spec = Invoke-RestMethod "$BaseUrl/openapi/v1.json"
if (@($spec.paths.PSObject.Properties).Count -ne 3) { throw 'Expected exactly three API paths.' }
$hostJwt = Token 1
$departure = [DateTimeOffset]::UtcNow.AddHours(1)
$offer = @{
    originNodeId = '20000000-0000-0000-0000-000000000001'
    destinationNodeId = '20000000-0000-0000-0000-000000000004'
    departureAtUtc = $departure.ToString('O')
    allowsIntermediateStops = $true
}
$created = Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/rides" -Method Post -ContentType 'application/json' `
    -Headers @{ Authorization = "Bearer $hostJwt" } -Body ($offer | ConvertTo-Json)
if ($created.StatusCode -ne 201) { throw 'Expected 201 publishing a ride.' }
$ride = $created.Content | ConvertFrom-Json
if ($ride.availableSeats -ne 3 -or $ride.seatCapacity -ne 3) { throw 'Expected three passenger seats.' }
Write-Output "POST /rides -> 201; ride $($ride.id); availableSeats=3"
$rider1 = Token 11
$from = [Uri]::EscapeDataString($departure.AddMinutes(-10).ToString('O'))
$to = [Uri]::EscapeDataString($departure.AddMinutes(10).ToString('O'))
$query = "pickupNodeId=20000000-0000-0000-0000-000000000002&dropoffNodeId=20000000-0000-0000-0000-000000000003&fromUtc=$from&toUtc=$to"
$found = Invoke-RestMethod "$BaseUrl/rides/search?$query" -Headers @{ Authorization = "Bearer $rider1" }
if ($ride.id -notin $found.id) { throw 'Created ride missing from search; use a freshly seeded demo database.' }
Write-Output 'GET /rides/search -> 200; created ride matched by ordered corridor and time window'
$key = [Guid]::NewGuid().ToString()
$booked = Booking $rider1 $ride.id $key
$retry = Booking $rider1 $ride.id $key
if ($booked.StatusCode -ne 201 -or $retry.StatusCode -ne 200 -or $booked.Content -ne $retry.Content) { throw 'Idempotency check failed.' }
Write-Output 'POST /bookings -> 201 Confirmed; identical retry -> 200, same booking'
foreach ($account in 12,13) {
    $reply = Booking (Token $account) $ride.id ([Guid]::NewGuid().ToString())
    if ($reply.StatusCode -ne 201) { throw 'Expected a confirmed one-seat booking.' }
}
try {
    $unexpected = Booking (Token 14) $ride.id ([Guid]::NewGuid().ToString())
    throw 'Expected fourth Rider to receive 409.'
} catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409) { throw }
}
Write-Output 'Third booking fills ride; fourth Rider -> 409'
$remaining = Invoke-RestMethod "$BaseUrl/rides/search?$query" -Headers @{ Authorization = "Bearer $rider1" }
if ($ride.id -in $remaining.id) { throw 'Full ride must not appear in search.' }
Write-Output 'Full ride excluded from search. Live smoke test passed; tokens omitted.'

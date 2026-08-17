[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('v1','v2')]
    [string]$Layout,
    [string]$BaseUrl = 'http://localhost:5181',
    [string]$Token = 'local-demo-only'
)

$headers = @{ 'X-Fixture-Token' = $Token }
$body = @{ layout = $Layout } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$($BaseUrl.TrimEnd('/'))/admin/layout" -Headers $headers -ContentType 'application/json' -Body $body

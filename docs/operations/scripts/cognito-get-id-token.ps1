param(
    [string]$UserPoolId = "eu-west-1_GUYadoxnL",
    [string]$UserPoolClientId = "45ggvje3r50cvi1as7lf7d3qvp",
    [string]$Region = "eu-west-1",
    [string]$Email = "alignment.bot@diariointelligente.app",
    [string]$Password = "Alignm3nt!2026",
    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..\\..")
    $OutFile = Join-Path $repoRoot ".runlogs\\alignment\\latest-id-token.txt"
}

function Find-UserByEmail {
    param(
        [string]$PoolId,
        [string]$AwsRegion,
        [string]$TargetEmail
    )

    $users = aws cognito-idp list-users --user-pool-id $PoolId --region $AwsRegion --limit 60 | ConvertFrom-Json
    return $users.Users | Where-Object {
        $emailAttr = $_.Attributes | Where-Object { $_.Name -eq "email" } | Select-Object -First 1
        $emailAttr -and [string]::Equals($emailAttr.Value, $TargetEmail, [System.StringComparison]::OrdinalIgnoreCase)
    } | Select-Object -First 1
}

$user = Find-UserByEmail -PoolId $UserPoolId -AwsRegion $Region -TargetEmail $Email
if (-not $user) {
    [void](aws cognito-idp admin-create-user `
        --user-pool-id $UserPoolId `
        --username $Email `
        --region $Region `
        --message-action SUPPRESS `
        --user-attributes Name=email,Value=$Email Name=email_verified,Value=true)

    $user = Find-UserByEmail -PoolId $UserPoolId -AwsRegion $Region -TargetEmail $Email
}

if (-not $user) {
    throw "Unable to create/find Cognito user for email $Email"
}

$username = $user.Username

[void](aws cognito-idp admin-set-user-password `
    --user-pool-id $UserPoolId `
    --username $username `
    --password $Password `
    --permanent `
    --region $Region)

try {
    [void](aws cognito-idp admin-add-user-to-group `
        --user-pool-id $UserPoolId `
        --username $username `
        --group-name ADMIN `
        --region $Region)
}
catch {
    # Group may be absent or policy may deny; token generation still works.
}

$idToken = aws cognito-idp initiate-auth `
    --auth-flow USER_PASSWORD_AUTH `
    --client-id $UserPoolClientId `
    --auth-parameters "USERNAME=$Email,PASSWORD=$Password" `
    --region $Region `
    --query "AuthenticationResult.IdToken" `
    --output text

if ([string]::IsNullOrWhiteSpace($idToken) -or $idToken -eq "None") {
    throw "Failed to obtain Cognito id token for $Email"
}

New-Item -ItemType Directory -Path (Split-Path $OutFile) -Force | Out-Null
Set-Content -Path $OutFile -Value $idToken -NoNewline

Write-Host "Token generated."
Write-Host "User: $username"
Write-Host "File: $OutFile"
Write-Host "Prefix: $($idToken.Substring(0,24))..."

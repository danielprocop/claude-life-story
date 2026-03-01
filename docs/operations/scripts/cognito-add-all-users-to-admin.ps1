param(
  [Parameter(Mandatory = $false)]
  [string]$UserPoolId = "eu-west-1_GUYadoxnL",
  [Parameter(Mandatory = $false)]
  [string]$Region = "eu-west-1",
  [Parameter(Mandatory = $false)]
  [string]$GroupName = "ADMIN",
  [Parameter(Mandatory = $false)]
  [string]$Description = "Temporary full admin access"
)

$ErrorActionPreference = "Stop"

Write-Output "Ensuring Cognito group '$GroupName' exists in pool '$UserPoolId'..."
$groupExists = $false
try {
  aws cognito-idp get-group --user-pool-id $UserPoolId --group-name $GroupName --region $Region | Out-Null
  $groupExists = $true
} catch {
  $groupExists = $false
}

if (-not $groupExists) {
  aws cognito-idp create-group `
    --user-pool-id $UserPoolId `
    --group-name $GroupName `
    --description $Description `
    --region $Region | Out-Null
  Write-Output "Created group '$GroupName'."
} else {
  Write-Output "Group '$GroupName' already exists."
}

Write-Output "Listing users..."
$usersRaw = aws cognito-idp list-users `
  --user-pool-id $UserPoolId `
  --region $Region `
  --query "Users[].Username" `
  --output text

$usernames = $usersRaw -split "\s+" | Where-Object { $_ -and $_.Trim().Length -gt 0 }

if ($usernames.Count -eq 0) {
  Write-Output "No users found."
  exit 0
}

foreach ($username in $usernames) {
  aws cognito-idp admin-add-user-to-group `
    --user-pool-id $UserPoolId `
    --username $username `
    --group-name $GroupName `
    --region $Region | Out-Null
  Write-Output "ADDED $username -> $GroupName"
}

Write-Output "Done."

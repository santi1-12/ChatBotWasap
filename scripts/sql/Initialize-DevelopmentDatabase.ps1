[CmdletBinding()]
param(
    [string]$ServerInstance = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'WhatsAppBot'
)

$ErrorActionPreference = 'Stop'
if ($Database -ne 'WhatsAppBot') {
    throw 'This Development initializer is scoped to the WhatsAppBot database.'
}

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$schemaDirectory = Join-Path $repositoryRoot 'src\WhatsAppBot.Api\Persistence\StoredProcedures\Schema'
$procedureDirectory = Join-Path $repositoryRoot 'src\WhatsAppBot.Api\Persistence\StoredProcedures\Procedures'
$createDatabase = Join-Path $schemaDirectory '000_CreateDatabase.sql'
$tenantMigration = Join-Path $schemaDirectory '007_TenantMigration.sql'

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'sqlcmd is required. Install SQL Server command-line utilities and retry.'
}
if (-not (Test-Path -LiteralPath $createDatabase)) {
    throw "Database creation script not found: $createDatabase"
}
if (-not (Test-Path -LiteralPath $tenantMigration)) {
    throw "Tenant migration script not found: $tenantMigration"
}

Write-Host "Preparing $Database on $ServerInstance using Windows authentication."
& sqlcmd -S $ServerInstance -E -b -i $createDatabase
if ($LASTEXITCODE -ne 0) {
    throw "Database creation failed with sqlcmd exit code $LASTEXITCODE."
}

$scripts = @(
    Get-ChildItem -LiteralPath $schemaDirectory -Filter '*.sql' -File |
        Where-Object { $_.Name -ne '000_CreateDatabase.sql' } |
        Sort-Object Name
    Get-ChildItem -LiteralPath $procedureDirectory -Filter '*.sql' -File |
        Sort-Object Name
)
foreach ($script in $scripts) {
    Write-Host "Applying $($script.Name)"
    & sqlcmd -S $ServerInstance -E -b -d $Database -i $script.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Applying $($script.Name) failed with sqlcmd exit code $LASTEXITCODE."
    }
}

Write-Host "Database $Database is initialized."

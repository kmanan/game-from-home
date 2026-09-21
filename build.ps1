param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
dotnet build 'src/GameFromHome/GameFromHome.csproj' -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Test) {
    dotnet build 'tests/GameFromHome.Fixture/GameFromHome.Fixture.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build 'tests/GameFromHome.Tests/GameFromHome.Tests.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & './tests/GameFromHome.Tests/bin/Release/net9.0-windows/GameFromHome.Tests.exe' './tests/GameFromHome.Fixture/bin/Release/net9.0-windows/GameFromHome.Fixture.exe'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
dotnet publish 'src/GameFromHome/GameFromHome.csproj' -c Release --no-restore --self-contained false -o 'artifacts/windows-x64' --nologo
exit $LASTEXITCODE

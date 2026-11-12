# Starts ten External processes and one HMI process (requires .NET 8 SDK).
Set-Location $PSScriptRoot
dotnet build .\HmiSignalRDemo.sln
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
for ($i = 1; $i -le 10; $i++) {
    $id = "External-{0:D2}" -f $i
    $port = 5100 + $i
    Start-Process dotnet -WorkingDirectory $PSScriptRoot -ArgumentList @(
        'run','--no-build','--project','External/External.csproj','--',
        "--ServerId=$id","--urls=http://localhost:$port")
}
Start-Process dotnet -WorkingDirectory $PSScriptRoot -ArgumentList @(
    'run','--no-build','--project','Hmi/Hmi.csproj','--','--urls=http://localhost:5000')
Write-Host "HMI: http://localhost:5000 | Externals: 5101-5110"

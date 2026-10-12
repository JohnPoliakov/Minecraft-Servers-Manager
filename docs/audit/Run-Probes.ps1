param(
    [string]$AssemblyDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $AssemblyDirectory) {
    $repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $AssemblyDirectory = Join-Path $repoRoot 'bin/Debug/net9.0-windows'
}
$AssemblyDirectory = (Resolve-Path -LiteralPath $AssemblyDirectory).Path
$auditDirectory = Join-Path ([IO.Path]::GetTempPath()) ('msm-audit-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $auditDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ProbeSource.cs.txt') -Destination (Join-Path $auditDirectory 'Program.cs')

$references = foreach ($assembly in @('Minecraft Servers Manager', 'LiveCharts', 'LiveCharts.Wpf')) {
    $assemblyPath = Join-Path $AssemblyDirectory ($assembly + '.dll')
    if (-not (Test-Path -LiteralPath $assemblyPath)) { throw "Missing assembly: $assemblyPath. Build the application first." }
    $escapedPath = [Security.SecurityElement]::Escape($assemblyPath)
    '<Reference Include="' + $assembly + '"><HintPath>' + $escapedPath + '</HintPath></Reference>'
}
$project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
__REFERENCES__
  </ItemGroup>
</Project>
'@
$projectPath = Join-Path $auditDirectory 'Audit.csproj'
[IO.File]::WriteAllText($projectPath, $project.Replace('__REFERENCES__', ($references -join [Environment]::NewLine)))
Write-Output "Scratch directory: $auditDirectory"
# This project has no NuGet packages; resolve framework references locally.
& dotnet restore $projectPath --source $auditDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Probe restore failed.' }
& dotnet run --project $projectPath --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Probe execution failed.' }

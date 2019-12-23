$ErrorActionPreference = "Stop"

Import-Module Axinom.DevOpsTooling

$path = Join-Path $env:GITHUB_WORKSPACE $env:INPUT_ASSEMBLYINFOPATH

$version = Set-DotNetBuildAndVersionStrings -assemblyInfoPath $path -commitId $ENV:GITHUB_SHA -primaryBranchName $env:INPUT_PRIMARYBRANCH
Write-Host "::set-output name=versionstring::$version"
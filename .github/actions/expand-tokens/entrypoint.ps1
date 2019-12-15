$ErrorActionPreference = "Stop"

Import-Module Axinom.DevOpsTooling

$path = Join-Path $env:GITHUB_WORKSPACE $env:INPUT_PATH

Expand-Tokens -path $path -filenames $env:INPUT_FILENAMES -recursive:([bool]$env:INPUT_RECURSIVE)
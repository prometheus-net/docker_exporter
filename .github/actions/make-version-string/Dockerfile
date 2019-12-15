FROM mcr.microsoft.com/powershell
WORKDIR /action

RUN pwsh -c '$ProgressPreference = "SilentlyContinue"; Install-Module Axinom.DevOpsTooling -Scope AllUsers -AllowPrerelease -Force'

COPY *.ps1 .

ENTRYPOINT ["pwsh", "-c", "& /action/entrypoint.ps1"]
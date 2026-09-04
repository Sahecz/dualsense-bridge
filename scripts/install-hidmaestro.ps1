$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$logPath = Join-Path $projectDirectory '.tools\hidmaestro-install.log'
$dotnetPath = Join-Path $projectDirectory '.tools\dotnet\dotnet.exe'
$cliPath = Join-Path $projectDirectory 'src\DualSenseBridge.Cli\bin\Release\net10.0-windows10.0.26100.0\DualSenseBridge.Cli.dll'

Start-Transcript -LiteralPath $logPath -Force
try {
    Set-Location -LiteralPath $projectDirectory

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdministrator) {
        throw 'Este instalador necesita una terminal abierta con Ejecutar como administrador.'
    }

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $nativeOutput = & $dotnetPath $cliPath --install-driver 2>&1
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorAction
    $nativeOutput | ForEach-Object { Write-Host $_ }

    if ($exitCode -ne 0) {
        throw "DualSenseBridge terminó con el código de error $exitCode."
    }

    Write-Host 'Instalación verificada por el instalador.'
    exit 0
}
finally {
    Stop-Transcript
}

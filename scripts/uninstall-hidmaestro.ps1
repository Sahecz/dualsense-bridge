$ErrorActionPreference = 'Stop'
$projectDirectory = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$logPath = Join-Path $projectDirectory '.tools\hidmaestro-uninstall.log'
$dotnetPath = Join-Path $projectDirectory '.tools\dotnet\dotnet.exe'
$cliPath = Join-Path $projectDirectory 'src\DualSenseBridge.Cli\bin\Release\net10.0-windows10.0.26100.0\DualSenseBridge.Cli.dll'

Start-Transcript -LiteralPath $logPath -Force
try {
    Set-Location -LiteralPath $projectDirectory
    & $dotnetPath $cliPath --uninstall-driver
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $stores = @(
        'Cert:\LocalMachine\My',
        'Cert:\LocalMachine\Root',
        'Cert:\LocalMachine\TrustedPublisher'
    )
    foreach ($store in $stores) {
        Get-ChildItem -Path $store |
            Where-Object { $_.Subject -in @('CN=HIDMaestroTestCert', 'CN=HIDMaestro Self-Signed') } |
            Remove-Item -Force
    }

    Write-Host 'También se retiró el certificado local HIDMaestro Self-Signed.'
    exit 0
}
finally {
    Stop-Transcript
}

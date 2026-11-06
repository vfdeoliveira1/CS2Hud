# Lado do WINDOWS: rode no PowerShell COMO ADMINISTRADOR, depois de instalar o WSL.
#   powershell -ExecutionPolicy Bypass -File .\windows-setup.ps1
# 1) WSL em modo "espelhado": o Ubuntu usa o mesmo IP do Windows (a porta do
#    servidor fica acessível de fora sem redirecionamento extra).
# 2) Libera a porta UDP 27015 no firewall do Windows e do WSL (Hyper-V).

$wslconfig = Join-Path $env:USERPROFILE ".wslconfig"
$content = @"
[wsl2]
networkingMode=mirrored
memory=8GB
"@
if (Test-Path $wslconfig) {
  Copy-Item $wslconfig "$wslconfig.bak" -Force
  Write-Host "Backup do .wslconfig antigo em $wslconfig.bak"
}
Set-Content -Path $wslconfig -Value $content -Encoding ASCII
Write-Host "OK: $wslconfig (rede espelhada, 8 GB de RAM pro Ubuntu)"

if (-not (Get-NetFirewallRule -DisplayName "CS2 Server UDP 27015" -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -DisplayName "CS2 Server UDP 27015" -Direction Inbound -Protocol UDP -LocalPort 27015 -Action Allow | Out-Null
}
Write-Host "OK: firewall do Windows liberado (UDP 27015)"

# firewall do Hyper-V que protege o WSL (só existe no Windows 11 22H2+)
try {
  Set-NetFirewallHyperVVMSetting -Name '{40E0AC32-46A5-438A-A0B2-2B479E8F2E90}' -DefaultInboundAction Allow
  Write-Host "OK: firewall do WSL (Hyper-V) liberado"
} catch {
  Write-Host "Aviso: não consegui ajustar o firewall do WSL (Windows antigo?). Detalhe: $($_.Exception.Message)"
}

wsl --shutdown
Write-Host "WSL reiniciado. Abra o Ubuntu de novo pra continuar."

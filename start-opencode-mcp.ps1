#!/usr/bin/env pwsh
<#
.SYNOPSIS
Inicia OpenCode con el MCP de Unity ya configurado globalmente.

.DESCRIPTION
Este script inicia OpenCode conectado automáticamente a tu MCP global.
El archivo de configuración debe estar en la ubicación de roaming:
  C:\Users\<tu_usuario>\AppData\Roaming\opencode\config\mcp\OpenCodeJSON

Al usar --standalone y --session, el MCP se mantiene activo
y no necesitas reiniciar OpenCode cada vez.
#>

# Nombre de sesión persistente (OpenCode requiere que empiece con "ses")
$sessionName = "ses_mcp_unity"

# Iniciar OpenCode en modo standalone (importante para MCP con transporte stdio)
# y sesión persistente para mantener el MCP activo
Write-Host "=" * 60
Write-Host "Iniciando OpenCode con MCP Unity configurado globalmente" -ForegroundColor Cyan
Write-Host "Sesión MCP: $sessionName" -ForegroundColor Gray
Write-Host "=" * 60

# Nota: La configuración MCP se carga automáticamente desde:
#   C:\Users\<usuario>\AppData\Roaming\opencode\config\mcp\OpenCodeJSON

# Iniciar OpenCode
# --standalone: usa servidor privado (necesario para MCP local con stdio)
# --session: mantiene sesión persistente con MCP activo
opencode --standalone --session "$sessionName"
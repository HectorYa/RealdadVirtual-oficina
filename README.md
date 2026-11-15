# RealdadVirtual-oficina

**CyberSim · Banco Andino**: simulador educativo en Unity 6 (URP) para entrenar al personal de oficina en la detección de correos de **phishing** y en la comprensión de cómo un **malware** se propaga por la red de una entidad financiera.

> Todo el contenido es ficticio. Ningún malware real se ejecuta: el ataque es solo una animación con fines didácticos.

## Recorrido

1. **Hall de ventanillas**: cajas, plataforma de atención, sala de espera, cajero automático y bóveda.
2. **Pasillo de oficinas**: Contabilidad, Créditos y Préstamos, Gerencia y Recursos Humanos, con afiches de concientización.
3. **Zona TI**: puerta con lector de tarjeta, sala de servidores y la estación TI-01, donde llega el correo de phishing. Si se abre el adjunto, un mapa de red muestra cómo la infección se propaga por todo el banco.

Incluye menú principal (configuración de volumen, sensibilidad, calidad gráfica y guía en pantalla), menú de pausa y controles para PC y Android.

## Requisitos

- Unity **6000.3.23f1** con el módulo Android (opcional, para compilar el APK).
- Paquetes: URP 17.3, Input System, TextMeshPro y [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (se instalan solos desde `Packages/manifest.json`).

## Cómo abrirlo

1. Clona el repositorio y ábrelo desde Unity Hub.
2. Abre la escena `Assets/Scenes/CyberSim_Lab.unity` y pulsa **Play**.
3. Para regenerar la escena desde código: menú **Cyber Sim > 1 - Construir escena**.
4. Para Android: **Cyber Sim > 2 - Preparar para Android** y después *File > Build Profiles > Android > Build*.

## Controles

| Acción | PC | Móvil |
|---|---|---|
| Caminar | W A S D | Joystick izquierdo |
| Mirar | Clic derecho + mover | Arrastrar en la mitad derecha |
| Abrir puerta / sentarse | E | Botón en pantalla |
| Pausa | ESC | Botón II |

## Estructura

- `Assets/Editor/CyberSimBuilder.cs`: construye toda la escena por código.
- `Assets/Scripts/`: lógica de la simulación (`SimulationManager`, `MonitorUI`, `InfectionMap`, `DoorInteraction`, `GameMenu`, etc.).
- `Contexto.txt`: requisitos funcionales del simulador (Modo Ataque / Modo Defensa).

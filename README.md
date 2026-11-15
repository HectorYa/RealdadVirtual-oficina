# RealdadVirtual-oficina

**CyberSim · Banco Andino**: simulador educativo en Unity 6 (URP) para entrenar al personal de oficina en la detección de correos de **phishing** y en la comprensión de cómo un **malware** se propaga por la red de una entidad financiera.

> Todo el contenido es ficticio. Ningún malware real se ejecuta: el ataque es solo una animación con fines didácticos.

## Recorrido

1. **Hall de ventanillas**: cajas, plataforma de atención, sala de espera, cajero automático y bóveda.
2. **Pasillo de oficinas**: Contabilidad, Créditos y Préstamos, Gerencia y Recursos Humanos, con afiches de concientización.
3. **Zona TI**: puerta con lector de tarjeta, sala de servidores y la estación TI-01, una PC corporativa realista (inicio de sesión, escritorio, barra de tareas, correo, navegador, documentos, Excel, papelera y un post-it con la clave).

## Modos de jornada

Al iniciar sesión en la PC, el colaborador elige:

- **Modo Ataque** (sin protección ni ayudas): llega el correo «Urgente: Revisión de Nómina y Bonos» con un Excel adjunto y un enlace. Si los abre, se abre un portal falso, aparecen errores del sistema, los iconos se cifran, el malware lee la clave del post-it y la infección se propaga por el mapa de red del banco.
- **Modo Defensa** (filtro, Sandbox, gestor de contraseñas y MFA): el correo queda en **Cuarentena**. Al abrirlo se dibujan **3 rectángulos** (remitente falso, urgencia y enlace/archivo analizado en Sandbox) que hay que pulsar en menos de **3 minutos** antes de usar «Reportar phishing».

Al final se muestra un **reporte de auditoría** (RF-05) con el estado del correo, de la contraseña anotada y del registro de accesos (robado/comprometido o protegido), el tiempo y las señales detectadas.

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

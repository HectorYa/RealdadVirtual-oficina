# RealdadVirtual-oficina

**CyberSim · Banco Andino**: simulador educativo en Unity 6 (URP) para entrenar al personal de oficina en la detección de correos de **phishing** y en la comprensión de cómo un **malware** se propaga por la red de una entidad financiera.

> Todo el contenido es ficticio (marcas, dominios y personas). Ningún malware real se ejecuta: el ataque es solo una animación con fines didácticos.

## Recorrido

1. **Hall de ventanillas**: cajas, plataforma de atención, sala de espera, cajero automático y bóveda.
2. **Pasillo de oficinas**: Contabilidad, Créditos y Préstamos, Gerencia y Recursos Humanos, con afiches de concientización.
3. **Zona TI**: puerta con lector de tarjeta, sala de servidores y la estación TI-01, una PC corporativa realista (inicio de sesión, escritorio, barra de tareas, correo, navegador, documentos, Excel, papelera y un post-it con la clave).

## Modos de jornada

Al iniciar sesión en la PC, el colaborador elige:

- **Modo Ataque** (sin protección ni ayudas): el correo llega a la bandeja de entrada sin advertencias. Si cae en el engaño, se abre una web falsa o se ejecuta el archivo, aparecen errores del sistema, los iconos se cifran, la clave del post-it es robada y la infección se propaga: las **cámaras de seguridad** muestran cómo las pantallas de cada área del banco se ponen en rojo, una tras otra.
- **Modo Defensa** (filtro, Sandbox, gestor de contraseñas y MFA): el correo queda en **Cuarentena** y el sistema advierte por sí solo del post-it con la clave. Al abrir el correo se dibujan **3 rectángulos** (remitente, presión y elemento malicioso) que hay que pulsar en menos de **3 minutos** antes de usar «Reportar phishing».

## Escenarios aleatorios

En cada jornada se elige al azar uno de **7 correos de phishing** (distinto del anterior), cada uno con sus propias 3 señales y su propia forma de infección:

| Escenario | Vector |
|---|---|
| Revisión de nómina y bonos | Excel con macro + enlace a portal falso |
| CV de un candidato | Archivo con doble extensión (`.pdf.exe`) |
| Cambio de contraseña obligatorio | Botón hacia una web falsa (robo de credenciales) |
| Documento confidencial compartido | Servicio externo que pide iniciar sesión |
| Comprobante de transferencia | Imagen que descarga un `.jpg.html` con script |
| Paquete retenido | `.zip` con un script oculto |
| Validar MFA con código QR | QR hacia una web falsa (*quishing*) |

## Reporte de auditoría (RF-05)

Al final se muestra el resultado, el modo, el tiempo y las señales detectadas, junto con:

- las **3 evidencias**: correo sospechoso, contraseña anotada en el escritorio y registro de accesos (robado/comprometido, en riesgo o protegido);
- una **vista del correo sospechoso** (remitente, dirección y vector);
- el **historial de accesos** (inicio de sesión legítimo, accesos no autorizados o bloqueados por MFA, reporte al SOC).

Incluye menú principal (volumen, sensibilidad, calidad gráfica, guía en pantalla y pantalla completa), menú de pausa y controles para PC y Android.

## Requisitos

- Unity **6000.3.23f1** con los módulos Windows y Android (Android Build Support + SDK/NDK + OpenJDK).
- Paquetes: URP 17.3, Input System, TextMeshPro y [MCP for Unity](https://github.com/CoplayDev/unity-mcp) (se instalan solos desde `Packages/manifest.json`).

## Cómo abrirlo y compilar

1. Clona el repositorio y ábrelo desde Unity Hub.
2. Abre la escena `Assets/Scenes/CyberSim_Lab.unity` y pulsa **Play**.
3. Menú **Cyber Sim**:
   - **1 - Construir escena**: regenera toda la escena desde código.
   - **3 - Configurar app**: icono, pantalla de carga, nombre y versión.
   - **4 - Compilar Windows**: genera `Builds/Windows/CyberSim_BancoAndino.exe`.
   - **5 - Compilar Android (APK)**: genera `Builds/Android/CyberSim_BancoAndino.apk`.

## Controles

| Acción | PC | Móvil |
|---|---|---|
| Caminar | W A S D | Joystick izquierdo |
| Mirar | Clic derecho + mover | Arrastrar en la mitad derecha |
| Abrir puerta / sentarse | E | Botón en pantalla |
| Abrir un icono del escritorio | Doble clic | Un toque |
| Pausa | ESC | Botón II |

## Estructura

- `Assets/Editor/CyberSimBuilder.cs`: construye toda la escena por código.
- `Assets/Editor/CyberSimBuildTools.cs`: marca de la app y compilación para Windows / Android.
- `Assets/Scripts/`: lógica de la simulación (`SimulationManager`, `MonitorUI`, `PhishScenarios`, `InfectionMap`, `DoorInteraction`, `GameMenu`, etc.).
- `Docs/Capturas/`: capturas de pantalla para la documentación.
- `Contexto.txt`: requisitos funcionales del simulador (Modo Ataque / Modo Defensa).

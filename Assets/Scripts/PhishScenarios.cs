using UnityEngine;

public enum PhishElement { Attachment, Link, Button, Image, QR }

/// Un correo de phishing con sus 3 señales (remitente, presión y elemento malicioso).
/// Todas las marcas, dominios y personas son FICTICIOS.
public class PhishScenario
{
    public string id, title;
    public string fromName, fromAddr, subject, preview;
    public string greeting, intro, urgency, beforeElement, signature;
    public bool elementBeforeUrgency;
    public PhishElement element;
    public string fileName, fileInfo, openingText;     // adjunto / imagen descargable
    public string linkText;                            // texto del enlace o del botón
    public string realUrl;                             // destino real del enlace / botón / QR
    public bool credentialTheft;                       // true: web falsa que roba la clave; false: archivo que ejecuta malware
    public string fakeSite, fakePrompt;                // página falsa (Modo Ataque)
    public string[] signalNames, signalText;           // las 3 señales (RF-03)
    public string scanVerdict, scanToast;              // resultado del Sandbox / análisis de URL (Modo Defensa)
    public string attackAction;                        // "abrir el ..." para el reporte
    public string lesson;

    public string RealHost
    {
        get
        {
            if (string.IsNullOrEmpty(realUrl)) return "";
            string s = realUrl.Replace("https://", "").Replace("http://", "");
            int i = s.IndexOf('/');
            return i > 0 ? s.Substring(0, i) : s;
        }
    }
}

public static class PhishScenarios
{
    public static readonly PhishScenario[] All =
    {
        new PhishScenario
        {
            id = "nomina", title = "Revisión de nómina y bonos",
            fromName = "RR.HH. Corporativo", fromAddr = "rrhh@banco-andlno.com",
            subject = "Urgente: Revisión de Nómina y Bonos", preview = "Detectamos inconsistencias en el cálculo de su nómina...",
            greeting = "Estimado colaborador:",
            intro = "Detectamos inconsistencias en el cálculo de su nómina y bono anual de este mes.",
            urgency = "Si no confirma sus datos HOY antes de las 17:00, el pago de su nómina y bono será SUSPENDIDO.",
            beforeElement = "Revise el archivo adjunto y confirme sus datos en el portal:",
            signature = "Atentamente,\nRecursos Humanos Corporativo",
            element = PhishElement.Attachment,
            fileName = "Nomina_Bonos_2026.xlsx", fileInfo = "Hoja de cálculo de Excel · 48 KB", openingText = "Abriendo en Excel... Habilitando contenido (macros)",
            linkText = "https://nomina-bancoandino.com.verificar-pago.net", realUrl = "https://nomina-bancoandino.com.verificar-pago.net/login",
            fakeSite = "BANCO ANDINO  ·  Portal de Nómina", fakePrompt = "Confirme sus datos para liberar el pago de su nómina y bono.",
            signalNames = new[] { "Remitente falso", "Urgencia artificial", "Enlace / Excel con macro" },
            signalText = new[]
            {
                "El dominio «banco-andlno.com» imita a «bancoandino.com»: cambia la «i» por una «l». RR.HH. siempre escribe desde @bancoandino.com.",
                "Amenaza con suspender el pago HOY para que actúes sin pensar. Ningún área real presiona por correo con plazos de horas.",
                "El Sandbox abrió el Excel en un entorno aislado: tiene una macro oculta que descarga malware. El enlace lleva a «verificar-pago.net», no al portal oficial.",
            },
            scanVerdict = "Sandbox: MALICIOSO · macro oculta  |  URL: suplantación",
            scanToast = "Sandbox: «Nomina_Bonos_2026.xlsx» contiene una macro oculta MALICIOSA. Descarga bloqueada.",
            attackAction = "abrir el adjunto o el enlace del correo de nómina",
            lesson = "Remitente que imita el dominio (andlno/andino), presión con plazos de horas y un Excel con macro o un portal falso. RR.HH. nunca pide confirmar la nómina por un enlace externo.",
        },
        new PhishScenario
        {
            id = "cv", title = "CV de un candidato",
            fromName = "María López", fromAddr = "maria.lopez.cv2026@correo-rapido.net",
            subject = "Postulación: Analista de Créditos – CV adjunto", preview = "Les envío mi hoja de vida para la vacante...",
            greeting = "Buenos días, estimado equipo:",
            intro = "Les envío mi hoja de vida para la vacante de Analista de Créditos publicada en su página web.",
            urgency = "Solo estaré disponible HOY para la entrevista: por favor abran mi CV cuanto antes y respóndanme.",
            beforeElement = "Adjunto mi CV actualizado:",
            signature = "Saludos cordiales,\nMaría López",
            element = PhishElement.Attachment, elementBeforeUrgency = true,
            fileName = "CV_Maria_Lopez.pdf.exe", fileInfo = "Aplicación · 2,4 MB", openingText = "Ejecutando CV_Maria_Lopez.pdf.exe...",
            signalNames = new[] { "Remitente externo", "Presión de tiempo", "Archivo con doble extensión" },
            signalText = new[]
            {
                "Llega desde un correo gratuito externo («correo-rapido.net») a tu buzón personal, no por el canal de selección de RR.HH.",
                "«Solo hoy» y «cuanto antes» buscan que abras el archivo sin pensarlo. Un candidato real no impone plazos así.",
                "«.pdf.exe» es un PROGRAMA disfrazado de PDF: la extensión real es la última (.exe). Un CV nunca es un ejecutable.",
            },
            scanVerdict = "Sandbox: MALICIOSO · ejecutable disfrazado de PDF",
            scanToast = "Sandbox: «CV_Maria_Lopez.pdf.exe» es un programa ejecutable (troyano). Descarga bloqueada.",
            attackAction = "abrir el «CV» adjunto (un .exe disfrazado de PDF)",
            lesson = "Remitente externo desconocido, presión de tiempo y un archivo con doble extensión. Fíjate siempre en la ÚLTIMA extensión: .exe, .js, .scr o .bat son programas.",
        },
        new PhishScenario
        {
            id = "clave", title = "Cambio de contraseña obligatorio",
            fromName = "Soporte TI", fromAddr = "soporte-ti@bancoandino-seguridad.com",
            subject = "Acción requerida: su contraseña expira hoy", preview = "Por políticas de seguridad su contraseña...",
            greeting = "Estimado usuario:",
            intro = "Por políticas de seguridad su contraseña de acceso a los sistemas del banco debe renovarse.",
            urgency = "Si no la cambia en las próximas 2 HORAS, su cuenta será BLOQUEADA y perderá el acceso a todos los sistemas.",
            beforeElement = "Haga clic en el botón para mantener su cuenta activa:",
            signature = "Mesa de Ayuda · Soporte TI",
            element = PhishElement.Button,
            linkText = "CAMBIAR CONTRASEÑA AHORA", realUrl = "https://bancoandino-seguridad.com/login.php",
            credentialTheft = true,
            fakeSite = "Banco Andino  ·  Restablecer contraseña", fakePrompt = "Ingrese su contraseña ACTUAL para continuar.",
            signalNames = new[] { "Dominio que imita a TI", "Amenaza de bloqueo", "Botón hacia web falsa" },
            signalText = new[]
            {
                "«bancoandino-seguridad.com» NO es nuestro dominio: los atacantes añaden palabras como «seguridad» o «soporte». TI escribe desde @bancoandino.com.",
                "Amenazar con bloquear la cuenta en 2 horas es la presión típica para que no verifiques.",
                "El botón lleva a «bancoandino-seguridad.com/login.php», una web falsa que copia nuestro inicio de sesión para robar tu clave.",
            },
            scanVerdict = "Análisis de URL: MALICIOSA · bancoandino-seguridad.com",
            scanToast = "Análisis de enlaces: el botón del correo apunta a «bancoandino-seguridad.com», un sitio de robo de credenciales.",
            attackAction = "pulsar «Cambiar contraseña» y escribir tu clave en la web falsa",
            lesson = "Dominio con palabras añadidas, amenaza de bloqueo y un botón que lleva a otra web. TI nunca pide tu contraseña actual por un enlace: cámbiala solo desde los sistemas oficiales.",
        },
        new PhishScenario
        {
            id = "documento", title = "Documento confidencial compartido",
            fromName = "Gerencia General (vía DocuNube)", fromAddr = "notificaciones@docunube-share.co",
            subject = "Gerencia compartió contigo: Auditoria_Confidencial_2026", preview = "Carlos Mendoza compartió un documento contigo...",
            greeting = "Hola, Juan:",
            intro = "Carlos Mendoza (Gerencia General) compartió contigo el documento «Auditoria_Confidencial_2026.pdf».",
            urgency = "Documento CONFIDENCIAL: el acceso caduca en 1 hora. No lo comentes con nadie hasta revisarlo.",
            beforeElement = "Inicia sesión para ver el documento:",
            signature = "DocuNube · Notificaciones automáticas",
            element = PhishElement.Button, elementBeforeUrgency = true,
            linkText = "ABRIR DOCUMENTO", realUrl = "https://docunube-share.co/auth/bancoandino",
            credentialTheft = true,
            fakeSite = "DocuNube  ·  Iniciar sesión con tu cuenta del banco", fakePrompt = "Para ver «Auditoria_Confidencial_2026.pdf» inicia sesión.",
            signalNames = new[] { "No es Gerencia", "Secreto y urgencia", "Pide iniciar sesión fuera" },
            signalText = new[]
            {
                "El nombre dice «Gerencia General», pero la dirección real es «docunube-share.co», un servicio externo que el banco no usa.",
                "«Confidencial», «caduca en 1 hora» y «no lo comentes» buscan aislarte y apurarte para que no consultes a nadie.",
                "Para «ver un documento» te pide tu usuario y clave del banco en otra web: es una página de robo de credenciales.",
            },
            scanVerdict = "Análisis de URL: MALICIOSA · docunube-share.co",
            scanToast = "Análisis de enlaces: «docunube-share.co» no es un servicio autorizado del banco y aloja una página de phishing.",
            attackAction = "abrir el «documento compartido» e iniciar sesión en la web falsa",
            lesson = "Nombre de un jefe con una dirección ajena, secreto + urgencia y una web externa que pide tu clave. Si un directivo te comparte algo, confírmalo por teléfono o en persona.",
        },
        new PhishScenario
        {
            id = "comprobante", title = "Comprobante de transferencia",
            fromName = "Pagos Proveedores", fromAddr = "pagos@proveedores-andino.info",
            subject = "Comprobante de transferencia #0921 – confirmar recepción", preview = "Adjuntamos el comprobante de la transferencia...",
            greeting = "Señores Banco Andino:",
            intro = "Adjuntamos la imagen del comprobante de la transferencia realizada a su favor.",
            urgency = "Si no confirma la recepción HOY, la transferencia de S/ 18 450 será ANULADA automáticamente.",
            beforeElement = "Haga clic en la imagen para ver el comprobante en tamaño completo:",
            signature = "Área de Pagos · Proveedores",
            element = PhishElement.Image, elementBeforeUrgency = true,
            fileName = "Comprobante_0921.jpg.html", fileInfo = "Documento HTML · 18 KB", openingText = "Abriendo «Comprobante_0921.jpg.html»... ejecutando script",
            signalNames = new[] { "Proveedor no registrado", "Amenaza de anular el pago", "La imagen es un archivo web" },
            signalText = new[]
            {
                "«proveedores-andino.info» no es un proveedor registrado: el dominio usa nuestro nombre para parecer de confianza.",
                "Amenazar con anular un pago importante HOY es presión para que hagas clic sin verificar.",
                "La «imagen» descarga «Comprobante_0921.jpg.html»: un archivo web con código, no una foto. Las imágenes también pueden ser trampas.",
            },
            scanVerdict = "Sandbox: MALICIOSO · la imagen descarga un .jpg.html con script",
            scanToast = "Sandbox: la imagen del correo descarga «Comprobante_0921.jpg.html», un archivo con script malicioso. Bloqueado.",
            attackAction = "hacer clic en la imagen del comprobante (descargó un .jpg.html con script)",
            lesson = "Proveedor desconocido con un dominio parecido, amenaza de anular un pago y una imagen que en realidad es un archivo web. No hagas clic en imágenes de correos inesperados.",
        },
        new PhishScenario
        {
            id = "paquete", title = "Paquete retenido",
            fromName = "Envíos Express", fromAddr = "entregas@envios-expres.net",
            subject = "Su paquete está retenido – descargue la guía de envío", preview = "No pudimos entregar su paquete...",
            greeting = "Estimado cliente:",
            intro = "No pudimos entregar su paquete N.º 48213 porque la dirección registrada está incompleta.",
            urgency = "Si no confirma la dirección en 24 HORAS, el paquete será DEVUELTO al remitente y se cobrará un recargo.",
            beforeElement = "Descargue la guía adjunta, complétela y reenvíela:",
            signature = "Envíos Express · Centro de Distribución",
            element = PhishElement.Attachment,
            fileName = "Guia_Envio_48213.zip", fileInfo = "Archivo comprimido · 312 KB (contiene Guia_Envio.js)", openingText = "Extrayendo Guia_Envio.js... ejecutando script",
            signalNames = new[] { "Empresa desconocida", "Plazo y recargo", "ZIP con script oculto" },
            signalText = new[]
            {
                "No esperabas ningún paquete y «envios-expres.net» (con una sola «s») no es una empresa con la que trabaje el banco.",
                "El plazo de 24 horas y el «recargo» presionan para actuar rápido.",
                "El .zip esconde «Guia_Envio.js», un script que se ejecuta al abrirlo. Una guía de envío real no viene comprimida con código.",
            },
            scanVerdict = "Sandbox: MALICIOSO · ZIP con script .js oculto",
            scanToast = "Sandbox: «Guia_Envio_48213.zip» contiene «Guia_Envio.js», un descargador de malware. Bloqueado.",
            attackAction = "abrir la «guía de envío» comprimida (.zip con un script)",
            lesson = "Una empresa que no conoces, un plazo con recargo y un archivo comprimido con un script dentro. Desconfía de .zip inesperados: suelen esconder programas.",
        },
        new PhishScenario
        {
            id = "qr", title = "Validar MFA con código QR",
            fromName = "Seguridad Banco Andino", fromAddr = "seguridad@bancoandino.com.mfa-verify.co",
            subject = "Valide su doble factor (MFA) escaneando el código QR", preview = "Detectamos un error en su doble factor...",
            greeting = "Estimado colaborador:",
            intro = "Detectamos un error en la configuración de su doble factor de autenticación (MFA).",
            urgency = "Su cuenta se BLOQUEARÁ esta noche si no valida su MFA. Escanee el código con su celular AHORA.",
            beforeElement = "Escanee este código con la cámara de su celular:",
            signature = "Seguridad de la Información",
            element = PhishElement.QR,
            realUrl = "https://bancoandino.com.mfa-verify.co/validar",
            credentialTheft = true,
            fakeSite = "Banco Andino  ·  Validación de MFA", fakePrompt = "Ingrese su usuario, contraseña y el código de su app MFA.",
            signalNames = new[] { "Dominio disfrazado", "Amenaza de bloqueo", "QR hacia web falsa" },
            signalText = new[]
            {
                "«bancoandino.com.mfa-verify.co»: lo que manda es el FINAL (mfa-verify.co). «bancoandino.com» al principio es solo un disfraz.",
                "Bloquear la cuenta «esta noche» y escanear «AHORA» buscan que no lo consultes con nadie.",
                "El QR esconde la dirección «bancoandino.com.mfa-verify.co/validar»: una web falsa. Con un QR no ves a dónde vas hasta que es tarde.",
            },
            scanVerdict = "Lector QR: apunta a bancoandino.com.mfa-verify.co (MALICIOSO)",
            scanToast = "Lector de QR: el código del correo lleva a «bancoandino.com.mfa-verify.co», un sitio de phishing.",
            attackAction = "escanear el código QR y escribir tu clave en la web falsa",
            lesson = "Dominio que empieza como el nuestro pero termina en otro, amenaza de bloqueo y un QR que oculta la dirección real. Nunca escanees QR de correos para «validar» tu cuenta.",
        },
    };

    /// Elige un escenario al azar distinto del anterior.
    public static int PickIndex(int previous)
    {
        if (All.Length <= 1) return 0;
        int i;
        do { i = Random.Range(0, All.Length); } while (i == previous);
        return i;
    }
}

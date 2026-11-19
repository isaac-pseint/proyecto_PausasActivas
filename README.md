# Sistema Corporativo de Pausas Activas para Windows

Aplicación empresarial de escritorio desarrollada en **.NET 8 (C#)** y **Windows Forms** para la gestión y ejecución de pausas activas laborales, con conexión remota a **Microsoft SQL Server (MonsterASP.NET)**, arquitectura tolerante a fallos y soporte para múltiples monitores.

Proyecto diseñado para sustentación académica y técnica en el **SENA**.

---

## 1. Arquitectura General del Sistema

El sistema implementa una arquitectura modular desacoplada en 3 proyectos:

```
PausasActivas.sln
│
├── PausasCore/              # Biblioteca de clases central (.NET 8.0)
│   ├── Models/              # Modelos de dominio coincidentes con el esquema SQL
│   ├── Configuration/       # DbConfig con SqlConnectionStringBuilder y lectura JSON
│   ├── Security/            # PasswordHasher (PBKDF2-HMAC-SHA256 con tiempo constante)
│   ├── Services/            # ImageCompressor (< 500 KB adaptativo mediante GDI+)
│   └── Repositories/        # Acceso a datos con Microsoft.Data.SqlClient y transacciones
│
├── PausasEmpleado/          # Aplicación WinForms para puestos de trabajo (.NET 8.0-windows)
│   ├── TrayApplicationContext # Ejecución silenciosa en la bandeja del sistema (System Tray)
│   ├── Services/            # CacheService (%LocalAppData%) y SyncService asíncrono
│   ├── Scheduling/          # PausaScheduler (evaluación de horarios e intervalos)
│   ├── Managers/            # PauseManager (coordinación simultánea multi-monitor)
│   └── Forms/               # VentanaPausaForm (pantalla completa) y PinEmergenciaForm
│
└── PausasAdmin/             # Panel de administración corporativa (.NET 8.0-windows)
    ├── Forms/               # LoginForm y MainAdminForm con navegación lateral
    ├── Theme/               # Paleta corporativa moderna (Slate 800, Azul 600, etc.)
    └── Views/               # EmpleadosView (configuración dual-list) e ImagenesView (CRUD + compresión)
```

### Diagrama de Flujo y Comunicación

```
[SQL Server Remoto - MonsterASP.NET]
       ▲                             ▲
       │ (db81083_pausas_emp)        │ (db81083_pausas_adm)
       │ Mínimos privilegios         │ CRUD completo + Transacciones
       ▼                             ▼
[PausasEmpleado]              [PausasAdmin]
 - NotifyIcon en segundo plano - Panel administrativo visual
 - Caché local offline         - Gestión de tiempos y catálogos
 - Multi-monitor a pantalla     - Asignador interactivo de pausas
   completa bloqueante
```

---

## 2. Esquema de Base de Datos y Modelo de Seguridad

### A. Base de Datos en SQL Server
- **`usuarios`**: Registra cuentas corporativas de Windows (`PC01\usuario`), parámetros de pausa (`intervalo_min`, `duracion_seg`, `orden`), horario laboral (`hora_inicio`, `hora_fin`) y presencia (`pc`, `ultima_conexion`).
- **`imagenes`**: Catálogo de ejercicios visuales con metadatos y binarios optimizados (`datos VARBINARY(MAX)` menores a 500 KB).
- **`usuario_imagenes`**: Relación n-a-n con clave compuesta `(usuario_id, imagen_id)` y orden de reproducción (`posicion`).
- **`admins`**: Cuentas administrativas con credenciales aseguradas mediante PBKDF2.

### B. Principio de Privilegios Mínimos (Least Privilege)
El sistema utiliza dos cuentas de base de datos diferenciadas:
1. **`db81083_pausas_emp` (Empleado):**
   - Solo lectura (`SELECT`) en `usuarios`, `imagenes` y `usuario_imagenes`.
   - `INSERT` en `usuarios` restringido únicamente a `(usuario_windows, pc, ultima_conexion)` para autorregistro.
   - `UPDATE` en `usuarios` restringido **exclusivamente** a `pc` y `ultima_conexion`. Cualquier intento de actualizar otras columnas es rechazado a nivel de motor SQL.
   - Acceso totalmente denegado a la tabla `admins`.
2. **`db81083_pausas_adm` (Administrador):**
   - Acceso administrativo para CRUD completo.
   - `SELECT` restringido en `admins` para autenticación.
   - Actualizaciones de asignación protegidas con transacciones atómicas `SqlTransaction` (`DELETE` + `INSERT` ordenado + `UPDATE usuarios SET actualizado = SYSUTCDATETIME()`).

### C. Criptografía y Hashing
- **PBKDF2-HMAC-SHA256**: Generación de hashes con salt criptográfico de 16 bytes, clave derivada de 32 bytes y 100.000 iteraciones en formato:
  `pbkdf2$<iteraciones>$<salt_base64>$<hash_base64>`
- **Mitigación de Timing Attacks**: Validación mediante `CryptographicOperations.FixedTimeEquals`.

### D. Nota Técnica de Arquitectura para Sustentación
> **Nota para el comité evaluador / sustentación:**  
> En este proyecto de escritorio, la conexión a SQL Server se realiza directamente utilizando cuentas de base de datos con permisos estrictamente acotados (principio de privilegios mínimos).  
> En un despliegue empresarial a gran escala en producción, se recomienda interponer una capa de servicios **Web API REST (ASP.NET Core / Minimal APIs)** con autenticación JWT o Azure Entra ID para evitar la exposición del puerto de base de datos (1433) hacia la red pública.

---

## 3. Configuración Inicial de Credenciales

Tanto `PausasEmpleado` como `PausasAdmin` leen su configuración desde un archivo `appsettings.json` ubicado en el directorio del ejecutable.

### Paso 1: Configurar el Módulo de Empleado (`PausasEmpleado`)
En la carpeta de `PausasEmpleado/`:
1. Copie el archivo `appsettings.example.json` y renómbrelo a `appsettings.json`.
2. Configure la contraseña asignada al usuario de base de datos `db81083_pausas_emp`:
```json
{
  "DataSource": "db81083.public.databaseasp.net,1433",
  "InitialCatalog": "db81083",
  "UserId": "db81083_pausas_emp",
  "Password": "SU_PASSWORD_AQUI",
  "ConnectTimeout": 15,
  "Encrypt": true,
  "TrustServerCertificate": true,
  "PinEmergencia": "1234"
}
```

### Paso 2: Configurar el Módulo de Administración (`PausasAdmin`)
En la carpeta de `PausasAdmin/`:
1. Copie el archivo `appsettings.example.json` y renómbrelo a `appsettings.json`.
2. Configure la contraseña del usuario `db81083_pausas_adm`:
```json
{
  "DataSource": "db81083.public.databaseasp.net,1433",
  "InitialCatalog": "db81083",
  "UserId": "db81083_pausas_adm",
  "Password": "SU_PASSWORD_AQUI",
  "ConnectTimeout": 15,
  "Encrypt": true,
  "TrustServerCertificate": true
}
```

> **Importante:** El archivo `.gitignore` ya está configurado para omitir `appsettings.json`, protegiendo sus credenciales reales de ser subidas al repositorio Git, mientras permite versionar `appsettings.example.json`.

---

## 4. Compilación y Publicación

### Requisitos Previos
- **Windows 10 / Windows 11 (64-bit)**
- **SDK de .NET 8 o superior** instalado (verificar con `dotnet --version`).

### Compilación Rápida (Debug o Release)
Para compilar toda la solución en modo Release:
```powershell
dotnet build -c Release PausasActivas.sln
```

### Empaquetado como Ejecutables Únicos (.exe)
Se incluye un script automatizado en PowerShell ([publish.ps1](file:///c:/Users/Isaac/Documents/proyectoVentanasE,mergentes/publish.ps1)) que compila ambas aplicaciones en formato **Self-Contained (Single-File)** con compresión:

```powershell
# Ejecutar en PowerShell desde la raíz de la solución:
.\publish.ps1
```

El script genera la siguiente estructura lista para distribución en la carpeta `.\publish\`:

```
publish/
├── PausasEmpleado/
│   ├── PausasEmpleado.exe          # Ejecutable único (~71 MB con runtime .NET 8 incluido)
│   ├── appsettings.json            # Configuración editable en texto plano sin recompilar
│   └── appsettings.example.json    # Plantilla de referencia
│
└── PausasAdmin/
    ├── PausasAdmin.exe             # Ejecutable único (~71 MB con runtime .NET 8 incluido)
    ├── appsettings.json            # Configuración editable en texto plano sin recompilar
    └── appsettings.example.json    # Plantilla de referencia
```

---

## 5. Resiliencia y Funcionamiento Offline

- **Tolerancia a Caídas de Red**: Si SQL Server se encuentra inalcanzable, la aplicación de empleado continúa funcionando sin interrupciones ni cuadros de diálogo molestos, utilizando la información y las imágenes almacenadas en `%LocalAppData%\PausasActivas\cache\`.
- **Rotación y Auditoría de Logs**: Los eventos y errores se registran automáticamente en:
  - `%LocalAppData%\PausasActivas\app.log` (Empleado)
  - `%LocalAppData%\PausasActivas\admin.log` (Administrador)  
  Ambos archivos rotan de forma automática cuando alcanzan los 2 MB (`.old`).
- **Manejadores Globales de Error**: Ambas aplicaciones implementan `Application.ThreadException` y `AppDomain.CurrentDomain.UnhandledException` para atrapar cualquier excepción imprevista y evitar cierres silenciosos.

---

## 6. Guía Rápida para Demostración en Vivo (Sustentación SENA)

Siga este checklist ordenado durante la presentación ante el jurado:

### Paso 1: Inicio y Login del Panel de Administración
1. Ejecute `PausasAdmin.exe`.
2. Inicie sesión con las credenciales de administrador (ej: usuario `admin`, clave `admin123`).
3. Compruebe la interfaz moderna: barra lateral oscura, tarjetas blancas y fuentes `Segoe UI`.

### Paso 2: Biblioteca de Imágenes y Compresión Automática
1. Diríjase a la sección **"Biblioteca de Imágenes"**.
2. Haga clic en **"+ Subir imagen"** y seleccione cualquier foto pesada (ej: 2 MB a 5 MB en formato JPG o PNG).
3. Observe cómo el diálogo muestra la comparativa:  
   *Tamaño original vs. Tamaño comprimido (< 500 KB)*.
4. Asigne un nombre, categoría (ej: "Cuello") y guarde.
5. Filtre por categoría usando el ComboBox superior.

### Paso 3: Autorregistro del Empleado
1. En el mismo equipo o en otra máquina en red, ejecute `PausasEmpleado.exe`.
2. Verifique que no se abre ninguna ventana molesta; la aplicación se aloja discretamente en la bandeja del sistema (System Tray junto al reloj).
3. Regrese al panel `PausasAdmin`, presione el botón de refrescar (**⟳**) en **"Empleados y Pausas"**:
   - Compruebe que el usuario de Windows actual (`DOMINIO\usuario`) y el nombre del PC aparecen automáticamente registrados.
   - El indicador de estado muestra un punto verde de conexión activa en los últimos minutos.

### Paso 4: Configuración Personalizada del Empleado
1. En `PausasAdmin`, seleccione al empleado en la tabla.
2. Modifique sus parámetros en el panel derecho:
   - Nombre visible (ej: "María Pérez - Contabilidad").
   - Intervalo de pausa: cámbielo a **1 minuto** (para probar la alarma en vivo).
   - Duración de la pausa: cámbielo a **20 segundos**.
   - Asigne imágenes específicas usando los botones de transferencia (`→`, `←`) y ordénelas con `▲` y `▼`.
3. Valide la regla de horarios: intente colocar una hora de fin anterior a la de inicio; observe cómo el sistema bloquea el guardado respetando la restricción SQL `CHECK (hora_inicio < hora_fin)`.
4. Haga clic en **"Guardar cambios"**.

### Paso 5: Sincronización y Ejecución de la Pausa
1. En el icono de la bandeja del empleado (clic derecho):
   - Seleccione **"Sincronizar ahora"** para descargar inmediatamente las nuevas reglas y fotos desde SQL Server.
   - O bien seleccione **"Probar pausa ahora (Demo)"** para forzar el disparo inmediato de la pausa.
2. **Experiencia en Pantalla Completa:**
   - La pantalla se bloquea en fondo oscuro (#0F172A) en todos los monitores conectados.
   - El contador regresivo sincronizado avanza hacia `00:00` con barra de progreso fluida.
   - Intente presionar `Alt+F4` o la tecla Windows: la aplicación cancela el cierre forzado.

### Paso 6: Demostración de Salida de Emergencia
1. Durante la pausa activa, presione el atajo secreto:
   $$\text{Ctrl} + \text{Shift} + \text{Alt} + \text{E}$$
2. Aparecerá el diálogo de **Desbloqueo de Emergencia**.
3. Ingrese el PIN corporativo (`1234` configurado en `appsettings.json`) y presione Enter:
   - La pausa se suspenderá inmediatamente y el empleado volverá a su escritorio de trabajo.

---

## 7. Licencia y Créditos
- Desarrollado con tecnologías abiertas de Microsoft (.NET 8, C#, Windows Forms, SQL Server).
- Cumple con los requerimientos técnicos de arquitectura de software, buenas prácticas de desarrollo seguro y ergonomía laboral corporativa.

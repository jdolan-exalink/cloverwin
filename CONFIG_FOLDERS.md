# Configuración de Carpetas - CloverBridge

## Configuración por Defecto (Rutas Relativas)

Sin cambiar nada, el sistema usa rutas relativas desde la carpeta del ejecutable:

```
./INBOX
./OUTBOX
./ARCHIVE
```

**Ubicación real**: Si el ejecutable está en `D:\DEVs\Cloverwin\dist\`, las carpetas serán:
- `D:\DEVs\Cloverwin\dist\INBOX`
- `D:\DEVs\Cloverwin\dist\OUTBOX`
- `D:\DEVs\Cloverwin\dist\ARCHIVE`

---

## Personalización mediante appsettings.json

Edita `appsettings.json` (en la misma carpeta que el ejecutable) para cambiar las rutas:

### Opción 1: Usar rutas absolutas en ProgramData

Para usar `C:\ProgramData\CloverBridge\...`:

```json
{
  "Folders": {
    "UseCustomPaths": true,
    "DefaultBasePath": "C:\\ProgramData\\CloverBridge",
    "Inbox": null,
    "Outbox": null,
    "Archive": null
  }
}
```

**Resultado**:
- `C:\ProgramData\CloverBridge\INBOX`
- `C:\ProgramData\CloverBridge\OUTBOX`
- `C:\ProgramData\CloverBridge\ARCHIVE`

---

### Opción 2: Usar carpetas personalizadas relativas

Para usar carpetas de datos junto a la aplicación:

```json
{
  "Folders": {
    "UseCustomPaths": true,
    "DefaultBasePath": null,
    "Inbox": "./DATA/INBOX",
    "Outbox": "./DATA/OUTBOX",
    "Archive": "./DATA/ARCHIVE"
  }
}
```

**Resultado**:
- `D:\DEVs\Cloverwin\dist\DATA\INBOX`
- `D:\DEVs\Cloverwin\dist\DATA\OUTBOX`
- `D:\DEVs\Cloverwin\dist\DATA\ARCHIVE`

---

### Opción 3: Rutas completamente personalizadas

Cada carpeta con su propia ruta:

```json
{
  "Folders": {
    "UseCustomPaths": true,
    "Inbox": "D:\\Shared\\CloverBridge\\INBOX",
    "Outbox": "D:\\Shared\\CloverBridge\\OUTBOX",
    "Archive": "\\\\network-server\\archive\\clover"
  }
}
```

---

### Opción 4: Mezclar rutas relativas y absolutas

```json
{
  "Folders": {
    "UseCustomPaths": true,
    "DefaultBasePath": "C:\\Apps\\CloverBridge",
    "Inbox": null,
    "Outbox": null,
    "Archive": "./archive"
  }
}
```

**Resultado**:
- `C:\Apps\CloverBridge\INBOX`
- `C:\Apps\CloverBridge\OUTBOX`
- `C:\Apps\CloverBridge\archive` (relativa al DefaultBasePath)

---

## Prioridad de Configuración

1. **appsettings.json** (mayor prioridad) - Personalización del usuario
2. **clover.yml** - Configuración de Clover
3. **Defaults** (menor prioridad) - Rutas relativas por defecto

Si `appsettings.json` tiene `UseCustomPaths: true`, se aplicarán esas configuraciones sobre las del YAML.

---

## Verificación en Logs

Cuando la aplicación inicia, muestra automáticamente las carpetas configuradas:

```
📁 Carpetas configuradas:
   📥 INBOX: D:\DEVs\Cloverwin\dist\INBOX
   📤 OUTBOX: D:\DEVs\Cloverwin\dist\OUTBOX
   📦 ARCHIVE: D:\DEVs\Cloverwin\dist\ARCHIVE
```

---

## Notas Importantes

- ✅ Las carpetas se crean automáticamente si no existen
- ✅ Se soportan rutas relativas y absolutas
- ✅ Se soportan rutas UNC (red)
- ✅ Cambios en appsettings.json requieren reiniciar la aplicación
- ⚠️ Asegúrate que el usuario que ejecuta la aplicación tenga permisos de lectura/escritura

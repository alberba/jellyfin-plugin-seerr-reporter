# Jellyfin.Plugin.SeerrReporter

Plugin de Jellyfin que permite a los usuarios reportar incidencias de reproducción
(vídeo, audio o subtítulos) directamente desde la interfaz web, creando la issue
correspondiente en [Seerr](https://github.com/seerr-team/seerr) / Overseerr / Jellyseerr.

La API Key de Seerr se guarda en el servidor y **nunca** se expone al navegador:
el cliente solo habla con el endpoint del plugin dentro de Jellyfin.

## Cómo funciona

1. El plugin inyecta `reporter.js` en `index.html` de jellyfin-web al arrancar.
2. El script añade un botón **⚠️ Reportar** en la página de detalle de películas,
   series y episodios.
3. El modal permite elegir tipo (🎬 Vídeo / 🔊 Audio / 💬 Subtítulos / ⚠️ Otro) y
   escribir un comentario.
4. El backend resuelve el TMDB id del ítem, lo traduce al `mediaId` interno de
   Seerr y crea la incidencia con la API Key de administrador.

## Requisitos

- Jellyfin **12.0** (compilado contra `Jellyfin.Controller` 12.0.0, `net10.0`).
- Seerr / Overseerr / Jellyseerr accesible desde el contenedor de Jellyfin.
- El título debe existir en Seerr (`mediaInfo`), es decir, haber sido solicitado
  o escaneado por Seerr. Si no, no hay contra qué abrir la incidencia.
- El directorio web de Jellyfin debe ser escribible para la inyección automática.

## Compilar

Necesitas el SDK de .NET 10.

```bash
dotnet publish src/Jellyfin.Plugin.SeerrReporter/Jellyfin.Plugin.SeerrReporter.csproj -c Release -o out
```

## Instalar

### Desde el repositorio de plugins (recomendado)

Todo desde el panel web, sin tocar ficheros en el servidor.

1. **Dashboard → Plugins → Repositorios → +**
   - Nombre: `Seerr Reporter`
   - URL: `https://raw.githubusercontent.com/alberba/jellyfin-plugin-seerr-reporter/main/manifest.json`
2. **Dashboard → Plugins → Catálogo → Seerr Reporter → Instalar**
3. Reinicia Jellyfin.

Las versiones siguientes aparecen solas en el catálogo: basta con etiquetar
un `v1.0.1` en el repositorio para que la Action compile, publique el `.zip`
y añada la entrada al `manifest.json`.

### Copiando el DLL a mano

```bash
dotnet publish src/Jellyfin.Plugin.SeerrReporter/Jellyfin.Plugin.SeerrReporter.csproj -c Release -o out
scp out/Jellyfin.Plugin.SeerrReporter.dll TU_SERVIDOR:/tmp/
ssh TU_SERVIDOR 'docker exec jellyfin mkdir -p /config/plugins/SeerrReporter \
  && docker cp /tmp/Jellyfin.Plugin.SeerrReporter.dll jellyfin:/config/plugins/SeerrReporter/ \
  && docker restart jellyfin'
```

## Configurar

En **Dashboard → Plugins → Seerr Reporter**:

- **URL de Seerr**: `http://seerr:5055` (misma red Docker `media-network`) o la IP del host.
- **API Key**: Seerr → Ajustes → General → API Key.
- Pulsa **Probar conexión** y guarda.
- Recarga jellyfin-web con **Ctrl+F5** (el navegador cachea `index.html`).

## Endpoints

| Método | Ruta | Auth | Descripción |
| --- | --- | --- | --- |
| `GET` | `/Plugins/SeerrReporter/ClientOptions` | usuario | Tipos habilitados y si está configurado |
| `POST` | `/Plugins/SeerrReporter/Report` | usuario | Crea la incidencia en Seerr |
| `POST` | `/Plugins/SeerrReporter/TestConnection` | admin | Valida URL + API Key |
| `GET` | `/SeerrReporter/reporter.js` | anónimo | Script de cliente |

Cuerpo de `Report`:

```json
{ "itemId": "guid-de-jellyfin", "issueType": 1, "comment": "No está en castellano" }
```

`issueType`: `1` Vídeo, `2` Audio, `3` Subtítulos, `4` Otro (mismos valores que Overseerr).

## Notas y limitaciones

- Seerr indexa **todo** por TMDB id, también las series. Los episodios y temporadas
  se reportan contra su serie padre. Un ítem con solo TVDB id no se puede mapear.
- La incidencia se crea con el usuario dueño de la API Key (administrador). El
  nombre del usuario de Jellyfin que reporta se añade al texto del mensaje.
- Una actualización del servidor reescribe el directorio web y elimina el script;
  el plugin lo vuelve a inyectar en cada arranque.

## Licencia

GPL-3.0-only, igual que Jellyfin.

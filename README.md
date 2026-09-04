# DualSenseBridge

> Un puente abierto para usar controles DualSense como mandos Xbox compatibles en Windows.

DualSenseBridge es un proyecto experimental escrito en C#/.NET. Su objetivo es detectar un control Sony DualSense o DualSense Edge, interpretar sus reportes HID y publicar su estado mediante un control Xbox 360 virtual que pueda ser reconocido por juegos de Windows.

El proyecto nace para cubrir escenarios en los que un juego no ofrece soporte directo para controles de PlayStation, especialmente títulos distribuidos mediante Xbox App o Microsoft Store. La meta a largo plazo es que el puente opere automáticamente en segundo plano, sin depender de Steam ni exigir que el usuario abra una interfaz cada vez que quiera jugar.

## Estado del proyecto

> **En desarrollo activo. No existe todavía una versión estable ni preparada para uso cotidiano.**

Actualmente existe una prueba técnica funcional con:

- detección de los identificadores USB de DualSense y DualSense Edge;
- interpretación inicial de reportes HID por USB, Bluetooth básico y Bluetooth extendido;
- conversión del estado del DualSense al modelo de entrada de Xbox 360;
- creación de un mando Xbox 360 virtual mediante HIDMaestro;
- un trabajador continuo con espera, desconexión, limpieza y reconexión automática;
- simulación de dispositivos para desarrollar sin hardware físico;
- captura versionada de series HID crudas con metadata segura para validar hardware real;
- pruebas automatizadas para el parser, el mapeo y el ciclo de vida del puente.

La salida virtual ya fue validada en Windows mediante un dispositivo simulado. La ruta completa con un DualSense físico todavía necesita validarse por USB y Bluetooth antes de considerarse utilizable.

## Qué busca lograr

- Compatibilidad transparente con juegos que esperan un mando XInput/Xbox.
- Detección y reconexión automática del DualSense.
- Ejecución ligera en segundo plano.
- Inicio seguro junto con la sesión de Windows.
- Limpieza correcta de dispositivos virtuales al cerrar o desconectar el control.
- Una arquitectura independiente de la interfaz gráfica y del proveedor del mando virtual.
- Perfiles de mapeo y configuración sin sacrificar una experiencia sencilla por defecto.
- Control opcional de iluminación y otras salidas compatibles del DualSense en etapas posteriores.

## Cómo funciona

```text
DualSense físico
      │  reportes HID
      ▼
Fuente de entrada ──► Parser ──► Estado neutral del control
                                      │
                                      ▼
                              Mapeo DualSense → Xbox
                                      │
                                      ▼
                              Xbox 360 virtual
                                      │
                                      ▼
                                  Juego XInput
```

El núcleo no depende directamente de HidSharp ni de HIDMaestro. Define contratos para la entrada física y la salida virtual, lo que permite probar el flujo con implementaciones simuladas y cambiar componentes sin reescribir el mapeo.

## Estructura

- `src/DualSenseBridge.Core`: modelos, parser, mapeo, simulador y coordinación del puente.
- `src/DualSenseBridge.Cli`: aplicación de consola y acceso al DualSense mediante HidSharp.
- `src/DualSenseBridge.HidMaestro`: implementación del mando Xbox virtual.
- `tests/DualSenseBridge.Tests`: pruebas automatizadas sin dependencia de hardware.
- `scripts`: tareas administrativas de instalación y limpieza del controlador virtual.
- `third_party/HIDMaestro`: binario redistribuible y avisos legales de HIDMaestro.

## Hoja de ruta

- [x] Modelo neutral de entrada.
- [x] Parser inicial USB y Bluetooth.
- [x] Mapeo básico a Xbox 360.
- [x] Salida Xbox virtual funcional.
- [x] Simulación sin DualSense físico.
- [x] Ciclo de desconexión y reconexión.
- [x] Infraestructura para capturar fixtures HID reales.
- [ ] Validación completa con hardware por USB.
- [ ] Validación completa con hardware por Bluetooth.
- [ ] Propagación de vibración desde el juego.
- [ ] Prevención configurable de doble entrada.
- [ ] Proceso de fondo e inicio con Windows.
- [ ] Empaquetado, actualización y desinstalación para usuarios finales.
- [ ] Interfaz y perfiles de configuración.

## Seguridad y privacidad

DualSenseBridge no incluye telemetría y su funcionamiento normal no requiere enviar datos del control a servicios externos.

La implementación virtual actual utiliza HIDMaestro, que instala componentes de controlador en Windows y requiere privilegios administrativos para crear dispositivos virtuales. Esta restricción se tendrá en cuenta al diseñar el futuro proceso de fondo y el instalador. El proyecto no busca desactivar las protecciones de firma de Windows, evadir sistemas antitrampas ni ocultar software frente a terceros.

## Dependencias principales

- [.NET](https://github.com/dotnet/runtime), bajo licencia MIT.
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp), para comunicación HID.
- [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro), para crear el control virtual.

HIDMaestro se redistribuye bajo su licencia MIT junto con sus avisos de terceros. La procedencia y el hash del binario incluido están documentados en `third_party/HIDMaestro/README.md`.

## Contribuciones

El diseño todavía está evolucionando. Los reportes reproducibles, pruebas con distintos modelos y revisiones del protocolo HID son bienvenidos mediante issues o pull requests. Los cambios deben mantener separadas la lógica central, el acceso al hardware y la salida virtual, e incluir pruebas cuando sea razonable.

## Licencia

DualSenseBridge se distribuye bajo la [licencia MIT](LICENSE). Las dependencias incluidas conservan sus propias licencias y avisos.

## Marcas

DualSense y PlayStation son marcas de Sony Interactive Entertainment. Xbox y Windows son marcas de Microsoft. DualSenseBridge es un proyecto comunitario independiente y no está afiliado, respaldado ni patrocinado por Sony, Microsoft, Valve o los autores de HIDMaestro.

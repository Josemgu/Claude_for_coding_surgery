# Guía de tecnologías

Lo que se ha **medido** sobre las herramientas de este proyecto, con el comando y
la salida. No es documentación de las bibliotecas: es lo que en ESTA máquina y con
ESTAS versiones resultó no ser como uno esperaba.

Titular: el programador (`CLAUDE.md` §7).

Máquina de referencia de todas las mediciones salvo donde se diga otra cosa:
Windows 11 Home 10.0.26200, Python 3.14.7, Tk 9.0.4.

---

## Tk 9.0.4 no tiene catálogo de mensajes en español, y en Windows tampoco importa

**Fecha: 2026-09-03.** Sale del hallazgo 4 de la auditoría de QA: los cuadros de
diálogo del programa salían con el mensaje en español y los botones en **Yes/No**,
contra la regla permanente 4.

**Lo primero que se probó, y por qué no sirve.** La idea evidente es pedirle a Tk
el idioma español y dejar que traduzca los botones. No funciona, y no por estar mal
pedido:

```
$ .venv/Scripts/python.exe -c "import tkinter as tk; r=tk.Tk(); ..."
tk patchlevel : 9.0.4
locale c      -> ['Yes', 'No', 'OK', 'Cancel']
locale en     -> ['Yes', 'No', 'OK', 'Cancel']
locale es     -> ['Yes', 'No', 'OK', 'Cancel']
locale es_es  -> ['Yes', 'No', 'OK', 'Cancel']
```

Las cuatro palabras salen iguales con los cuatro locales, el español incluido:
**Tk 9.0.4 no trae el catálogo `es`**, así que `::msgcat::mc` devuelve la clave sin
traducir. Pedir el locale no da error y no cambia nada, que es la forma más cara de
fallar: parece que funcionó.

**Y hay una segunda capa que lo hace irreversible en Windows.** Aunque el catálogo
existiera, `tkinter.messagebox` en Windows **no dibuja el cuadro**: llama al
diálogo nativo del sistema. Ese se rotula con el idioma de la interfaz de Windows,
que en la máquina del dueño es:

```
$ powershell -NoProfile -Command "(Get-UICulture).Name"
en-US
```

O sea que el rótulo del botón no lo decide el programa por ningún camino.

**Lo que se hizo.** `interfaz/dialogos.py`: un `tk.Toplevel` propio con botones
`ttk` rotulados por este proyecto. Eran 51 puntos de llamada en 10 archivos.
`pruebas/prueba_dialogos_en_espanol.py` lee el árbol de sintaxis de cada archivo de
`interfaz/` y falla si alguno vuelve a importar `messagebox` — se lee el AST y no
el texto porque un `grep` casaría también con la palabra dentro de un comentario,
y estos comentarios hablan del defecto.

**Lo que sigue en inglés y no se arregló:** `filedialog`, el cuadro de elegir
archivo. Es nativo también, pero escribirlo desde cero es un explorador de archivos
entero, y sus botones no confirman nada irreversible. Queda dicho, no resuelto.

**El efecto lateral que valía la pena.** El cuadro propio permite dos cosas que el
nativo no: que el foco entre en el botón de **cancelar** y no en el de seguir —un
Intro por inercia ya no firma— y que `Esc` y la cruz de la ventana cuenten como
«no» y nunca como «sí».

---

## Un `ttk.Button` apagado no para el Tab en esta máquina

**Fecha: medido en la FASE 9, reconfirmado el 2026-09-03.** El código de
`interfaz/correccion.py` afirmaba que el botón de verificar «sigue tomando el foco
con Tab a propósito aunque esté apagado». Es falso: el recorrido completo de la
pantalla con seis personas da el número que corresponde a **no** contarlo.

Vale como aviso general: en Tk, `state=['disabled']` saca al control del recorrido
de Tab, así que **el motivo por el que un botón está apagado no se puede leer
pulsándolo**. Tiene que estar escrito al lado, siempre visible. Por eso
`_motivo_por_el_que_no_se_puede_verificar()` devuelve una frase y no un booleano.

El número exacto vive en `pruebas/prueba_correccion.py` con su desglose, y hoy son
**58 paradas** (eran 57 hasta que entró «Deshacer "Todo correcto"»). La prueba
comprueba el desglose contra el total, para que nadie pueda subir el número sin
decir qué parada añadió.

---

## Perder un ancla y no leer los campos del caso son el mismo suceso

**Fecha: 2026-09-03.** Medido sobre las 14 páginas reales de `pdfs_referencia/`,
con RapidOCR y los modelos PP-OCRv5 del grupo latino.

Un «ancla» es la etiqueta impresa que localiza un campo en la hoja
(`extraccion/etiquetas.py`). Sin ella el extractor no sabe dónde recortar.

| páginas | anclas perdidas | n.º caso | fecha | unidad |
|---|---|---|---|---|
| 10 de 14 | **0** | sí | sí | sí |
| 4 de 14 | 2 o 4 | sí | **no** | **no** |

La separación es limpia: **ni un falso positivo ni un falso negativo** sobre el
material que hay. Por eso «se perdió alguna etiqueta impresa» se pudo usar como
señal —motivo `anclas_perdidas` en `documentos_ilegibles`— sin inventar ningún
umbral nuevo.

⚠️ **Y por qué esto importa más de lo que parece.** El umbral de confianza que
decide `captura_manual` es 0.6, y sale de `DECISIONES.md`. QA midió una página real
con 2 anclas perdidas, la unidad ilegible y un nombre que no parece un nombre con
confianza **0.796**: pasa el umbral y entraba sin marcarse. Bajar el umbral para
cazar ese caso habría cambiado el comportamiento de todas las páginas para arreglar
dos. La lección es la general: cuando un número no separa lo bueno de lo malo, se
busca **otra señal que sí lo haga**, y se mide antes de creérsela.

---

## PyInstaller: cómo se comprueba lo que de verdad viaja dentro del `.exe`

**Fecha: 2026-09-03.** Comprobar un arreglo sobre el código fuente no dice nada del
paquete que se entrega: entre los dos hay una compilación que puede haberse hecho
antes del último cambio. Ocurrió en esta misma sesión —la primera construcción se
lanzó antes de dos ediciones— y se detectó por la hora del archivo, no por una
prueba.

La forma de mirar dentro sin ejecutar la ventana:

```python
from PyInstaller.archive.readers import CArchiveReader, ZlibArchiveReader
archivo = CArchiveReader("Fichas.exe")
# ⚠️ El tipo de entrada del TOC está en el índice 4, no en el 3. Con el 3 la
# búsqueda del PYZ no encuentra nada y levanta StopIteration sin decir por qué.
nombre = next(n for n, e in archivo.toc.items() if e[4] == "z")
datos = archivo.extract(nombre)          # devuelve los bytes, NO una tupla
```

Escribiendo cada objeto de código con la cabecera de `.pyc` —`MAGIC_NUMBER` más
tres enteros de 4 bytes— y colocándolo como `paquete/modulo.pyc`, Python lo importa
igual (importación sin fuente). Poniendo esa carpeta delante en `sys.path` **y
quitando la del repositorio**, lo que se ejecuta es el código empaquetado. Con eso
la comprobación del `.exe` deja de ser un acto de fe.

Lo que este método **no** cubre, dicho: no arranca la ventana ni prueba el
`bootloader`, los `.dll` ni los modelos `.onnx`. Para eso está
`Fichas.exe --solo-mostrar-ruta`, que sí atraviesa el arranque real del paquete y
termina con código 0 sin dibujar nada.

## Tk 9 entrega el trackpad como `<TouchpadScroll>`, y `Canvas` no lo ata

**El síntoma, dicho por el dueño el 2026-09-03:** *«le doy para abajo con el
trackpad de mi laptop y no baja»*. Con la rueda del ratón sí bajaba.

**La causa, medida en esta máquina (Tk 9.0.4, Python 3.14.7).** Tk 9 no entrega el
gesto de dos dedos de un trackpad como `<MouseWheel>`: lo entrega como un evento
propio, `<TouchpadScroll>`, con deltas precisos y muchos más eventos por segundo.
Y trae binding por defecto solo para algunos widgets:

```
$ python -c "import tkinter as t; r=t.Tk(); print(repr(r.tk.call('bind','Canvas','<TouchpadScroll>')))"
''
$ python -c "import tkinter as t; r=t.Tk(); print(r.tk.call('bind','Listbox','<TouchpadScroll>'))"

    if {%# %% 5 == 0} {
	lassign [tk::PreciseScrollDeltas %D] tk::Priv(deltaX) tk::Priv(deltaY)
	...
```

`Listbox` sí, **`Canvas` no**. Las dos superficies que este programa desplaza —la
columna de campos de `interfaz/correccion.py` y el papel de `interfaz/visor.py`—
son `Canvas`, y las dos ataban solo `<MouseWheel>`. Comprobado antes de tocar
nada: `grep -rn TouchpadScroll interfaz/` devolvía **0**.

**Cómo viene el dato.** El `%D` de un `TouchpadScroll` NO es un delta suelto: trae
los dos ejes empaquetados en un entero. El desempaquetado, leído del propio Tk de
esta máquina con `info body ::tk::PreciseScrollDeltas`:

```tcl
set deltaX [expr {$dxdy >> 16}]
set low [expr {$dxdy & 0xffff}]
set deltaY [expr {$low < 0x8000 ? $low : $low - 0x10000}]
```

**La decisión: no se reimplementa esa cuenta.** `interfaz/gestos.py` llama a
`::tk::PreciseScrollDeltas`. Copiarla dejaría dos versiones del mismo
desempaquetado, y el día que Tk cambie de formato una de las dos se queda mal en
silencio.

**Y una diferencia entre las dos superficies, que no es capricho.** La columna de
campos atiende **uno de cada cinco** eventos y se desplaza por *unidades* —es el
mismo 5 que usa Tk en su binding de `Listbox`—; el papel del visor atiende
**todos** y se desplaza por *píxeles*. Un trackpad manda decenas de eventos por
segundo: atenderlos todos en una lista convierte un gesto corto en un salto al
final, y tirar cuatro de cada cinco en el papel se come cuatro quintos del gesto.

⚠️ **Lo que no está medido:** en esta máquina no hay trackpad. Las pruebas
(`pruebas/prueba_trackpad.py`) generan el evento con `event_generate`. Que el
sistema del dueño entregue de verdad `<TouchpadScroll>` con su trackpad solo lo
puede comprobar él en su portátil.

---

## Lo que cuesta pintar el visor: construir el `PhotoImage`, no dibujarlo

**El síntoma, del dueño:** *«lento, se corta, tosco al moverlo»*. Medido en esta
máquina con una hoja de 2700×3500 px en un panel de 560×458, arrastrando:

| | antes | después |
|---|---|---|
| `repintar()` a la escala de arranque | 61,6 ms | — |
| `repintar()` al 300 % | 76,6 ms | — |
| `_dibujar_el_escaneo` al 300 % | 26,8 ms | — |
| un arrastre completo, arranque | — | **1,62 ms** |
| un arrastre completo, 300 % | — | **1,42 ms** |

**Dónde estaba el coste, medido por partes** (`cProfile` sobre 40 arrastres):

| tamaño de la imagen | construir `tk.PhotoImage` | redibujar + refrescar |
|---|---|---|
| 560×458 (0,26 Mpx) | 5,6 ms | 0,9 ms |
| 840×687 (0,58 Mpx) | 13,6 ms | 1,3 ms |
| 1120×916 (1,03 Mpx) | 22,9 ms | 1,2 ms |
| 2202×1254 (2,76 Mpx) | **65,0 ms** | 1,1 ms |

**Redibujar es gratis a cualquier tamaño; construir cuesta en proporción a los
píxeles.** Cortar y reescalar la región es lo más barato de todo: 0,7 ms.

Los dos arreglos salen de esa tabla y de ningún gusto:

1. **Se guarda el trozo ya reescalado, con un cuarto de panel de margen a cada
   lado.** Mientras el arrastre se quede dentro, no se corta, no se reescala y no
   se construye ningún `PhotoImage`: solo se mueve la imagen que ya existe. El
   margen es 0,25 y no 0,5 porque el coste total arrastrando una pantalla entera
   sale casi igual —(1+2m)²/m vale 9 y 8—, pero el **parón** de una construcción
   suelta es cinco veces mayor (65 ms contra 13,6), y lo que el dueño notó es el
   peor caso, no el promedio.
2. **Los rótulos de la barra solo se escriben cuando cambian.** Cuatro llamadas a
   `configure` costaban 2,4 de los 3,1 ms de cada arrastre, y arrastrando no
   cambia ninguno de los cuatro. Un `configure` de Tk cuesta lo mismo escriba algo
   distinto o lo mismo.

⚠️ **Lo que sigue sin reescalarse nunca es la hoja entera**, y esa regla no se
tocó: al 300 % serían 8100×10500 px, 85 Mpx.

---

## En Windows cada widget de Tk es una ventana del sistema, y por eso se dibuja

**El síntoma, del dueño:** *«aún el dibujado del sistema es lento»*, sin cifra
suya. Lo que sigue está medido en esta máquina (Python 3.14.7, Tk 9.0.4) con la
máquina **en silencio** —ninguna suite corriendo, comprobado con
`Get-Process python*` antes de cada medida— y sobre una copia de la base real de
dos casos.

**El control que aísla la causa.** No es nuestro código, ni las consultas, ni el
tema de `ttk`:

```
$ python control_tk2.py
python 3.14.7 tk 9.0.4
  vuelta 0: 186 widgets visibles 0.549s   destroy 0.217s
  vuelta 1: 186 widgets visibles 0.492s   destroy 0.177s
  vuelta 2: 186 widgets visibles 0.469s   destroy 0.175s
```

**Unos 2,6 ms por widget en aparecer y 0,95 ms en irse**, con `ttk` pelado y sin
una sola consulta de por medio. En Windows cada widget de Tk es una ventana hija
del sistema (`HWND`), así que mapearla y destruirla pasa por el escritorio. Dos
mil llamadas Tk triviales cuestan 0,006 s en total: **el coste no está en hablar
con Tk, está en el número de widgets mapeados por clic.**

De ahí sale la regla de este proyecto: **el texto que nadie pulsa se dibuja en un
`Canvas`; los controles con los que se hace algo siguen siendo controles.** Un
lienzo con 186 textos y rectángulos costaba 0,52 s frente a los 3 s de esos
mismos 186 en widgets bajo la misma carga, y borrarlo son 0,01 s frente a 0,5.

**Lo medido en la pantalla de inicio**, mediana de tres vueltas alternando el
árbol de antes y el de después —alternar importa: tres seguidas de cada lado
medirían el estado de la máquina y no el cambio—:

| | antes | después | criterio del pase |
|---|---|---|---|
| widgets del inicio al abrir | 186 | **82** | ≤ 120 |
| `mostrar_inicio` | 0,957 s | **0,190 s** | ≤ 0,5 s |
| volver a inicio desde un caso | 0,582 s | **0,218 s** | ≤ 0,2 s |
| `abrir_caso`, segundo caso | 0,735 s | **0,597 s** | ≤ 0,8 s |
| `mostrar_revisar` | 0,351 s | **0,225 s** | — |
| una tarjeta de «Revisar» | 14 widgets | **6** | — |

**Tres cosas que no se ven en esa tabla y hay que saber.**

1. **Lo que un `Canvas` no da es el tabulador.** Un elemento dibujado no toma el
   foco, así que donde hay recorrido de teclado sigue habiendo controles: las
   filas del bloque rojo del inicio conservan su marco —`takefocus`, resaltado al
   recibir el foco, Intro para abrir—, y los botones de una tarjeta archivada
   siguen siendo botones porque llegar con Tab y leer por qué no se pueden pulsar
   es la única forma de enterarse. Lo que se dibuja dentro de esas piezas es solo
   su texto.
2. **`grid_remove` cuesta 0 y `grid` cuesta lo que mapear.** Conservar las
   pantallas a las que se vuelve —inicio, «Revisar», reportes— quita la
   construcción entera, pero volver a enseñarlas sigue pagando el mapeo de sus
   widgets. Por eso el número de widgets sigue mandando aunque no se destruya
   nada, y por eso `volver a inicio` se queda en 0,218 s: de los 82 que quedan,
   **26 son el menú de iconos**.
3. **El arranque del `.exe` no cambia con esto.** Medido, tres arranques de cada
   uno sobre la misma carpeta: antes 2,450 / 1,194 / 1,165 s; después 2,483
   (frío) / 1,176 / 1,258 / 1,157 s. Lo que domina ahí es cargar Python y sus
   bibliotecas, no dibujar la primera pantalla.

**Un `after` que dispara sobre una pantalla destruida se ve así:**

```
invalid command name "2773842682304_pintar_lo_diferido"
    while executing
"2773842682304_pintar_lo_diferido"
    ("after" script)
```

No rompe nada visible —Tk se lo traga—, pero es un temporizador vivo por cada vez
que se entra y se sale, y en una sesión larga son muchos. Se cancela en **dos**
sitios y hacen falta los dos: en el método que la ventana llama al cambiar de
pantalla, y en un `<Destroy>` filtrado por `evento.widget is self` —porque
`<Destroy>` sube desde cada control de dentro—, ya que una pantalla también se
destruye al cerrar la ventana entera. Medido en la suite: **11 líneas antes, 0
después.**

Y `after info` devuelve **una cadena vacía** cuando no queda ninguno, no una
tupla: se normaliza con `tk.splitlist` antes de compararlo, o la prueba falla
diciendo `() != ''`.

**El desempaquetado de `<TouchpadScroll>` tiene un lado fácil de invertir.**
`tk::PreciseScrollDeltas` deja **x en los 16 bits altos e y en los bajos**. La
rejilla de «Revisar» leía los altos y los movía como si fueran el vertical:
medido con un evento sintético de x=0, y=−40 —un deslizamiento vertical puro—, la
rejilla se movía **0 px**. Es el motivo de que el desempaquetado viva en un solo
sitio (`interfaz/desplazamiento.py`, que llama a la función de Tk en vez de
reimplementarla): con dos copias, una se queda mal en silencio.

Y **`bind_all` reemplaza, no suma.** Dos pantallas que atan la rueda con
`bind_all` y no la sueltan se pisan: la de corrección sobrevivía a su pantalla, la
de inicio la reemplazaba al entrar el ratón en su cola, y al salir el ratón de ahí
la ventana entera se quedaba sin nadie oyendo la rueda.

"""El gesto de dos dedos del trackpad, decodificado como manda Tk 9.

**El fallo que cierra, dicho por el dueno el 2026-09-03:** *«le doy para abajo con
el trackpad de mi laptop y no baja»*. Medido: el programa ata `<MouseWheel>` en la
columna de campos (`interfaz/correccion.py`) y en el visor (`interfaz/visor.py`), y
`grep -rn TouchpadScroll interfaz/` devolvia **cero**.

**Por que eso rompe justo el trackpad y no el raton.** Este proyecto corre sobre
Tk 9.0.4 (medido en esta maquina). Tk 9 entrega el desplazamiento preciso de un
trackpad como un evento **`<TouchpadScroll>`** aparte, y no como `<MouseWheel>`. Y
Tk trae binding por defecto de `<TouchpadScroll>` para `Text`, `Listbox` y algun
otro widget, pero **para `Canvas` no trae ninguno** —comprobado en esta maquina:

    bind Canvas <TouchpadScroll>   ->  (vacio)
    bind Listbox <TouchpadScroll>  ->  (el binding de tk::PreciseScrollDeltas)

Las dos superficies que el dueno intenta desplazar son `Canvas`. Por eso no baja.

**Como viene el dato.** El `%D` de un `TouchpadScroll` NO es un delta suelto: trae
los dos ejes empaquetados en un entero, `deltaX` en los 16 bits altos y `deltaY`
con signo en los 16 bajos. Aqui **no se reimplementa esa cuenta**: se llama a
`::tk::PreciseScrollDeltas`, que es la propia funcion de Tk. Copiarla dejaria dos
versiones del mismo desempaquetado, y el dia que Tk cambie de formato una de las
dos se queda mal en silencio. Su cuerpo en esta maquina, para que conste:

    set deltaX [expr {$dxdy >> 16}]
    set low [expr {$dxdy & 0xffff}]
    set deltaY [expr {$low < 0x8000 ? $low : $low - 0x10000}]

⚠️ **No se ha podido probar con un trackpad de verdad**: la maquina donde se
escribio esto no tiene. Lo que si se prueba es que el evento sintetico llega y que
la superficie se desplaza (`pruebas/prueba_trackpad.py`), y que el desempaquetado
es el de Tk, porque es el de Tk.
"""

# Cada cuantos eventos se atiende uno en las superficies que se desplazan por
# «unidades» —renglones—, no por pixeles. Es el mismo 5 que usa Tk en su propio
# binding de `Listbox`, y no un numero elegido aqui: un trackpad manda decenas de
# eventos por segundo con deltas de una unidad, y atenderlos todos convierte un
# gesto corto en un salto al final de la lista.
UNO_DE_CADA = 5


def deltas_del_trackpad(widget, delta_empaquetado):
    """Los dos ejes del gesto, `(dx, dy)`, desempaquetados por el propio Tk.

    `widget` es cualquier widget vivo: solo se usa para llegar al interprete.
    """
    dx, dy = widget.tk.call("::tk::PreciseScrollDeltas", delta_empaquetado)
    return int(dx), int(dy)


def toca_atender(evento):
    """Si a este evento del trackpad le toca turno, contando uno de cada cinco.

    Se cuenta con el numero de serie del evento —el `%#` de Tk— porque es lo que
    hace Tk en su propio binding, y porque es un contador que no hay que guardar en
    ningun sitio ni reiniciar al soltar los dedos.
    """
    return getattr(evento, "serial", 0) % UNO_DE_CADA == 0

"""Como llega el espejo al disco sin romper el que ya estaba, y sin fallar callado.

El espejo se regenera ENTERO despues de cada guardado. No hay funcion de exportar
y no la va a haber: `PENDIENTES.md` (FASE 4) lo dice con su motivo — «si hay que
acordarse de pulsarlo, algun dia no se pulsa», y ese dia el Excel miente sin que
nadie lo note.

**El orden de escritura, y por que es ese.** Se escribe entero a
`fichas.xlsx.parcial` y solo al final se reemplaza el definitivo de un golpe con
`os.replace`. Lo que la documentacion que trae Python dice de esa funcion, literal
(`os.replace.__doc__`, Python 3.14.7 de este proyecto):

    «Rename a file or directory, overwriting the destination.»

⚠️ La pagina oficial de `os.replace` (docs.python.org) **no la pude consultar**: las
dos veces volvio truncada antes de llegar a esa entrada. Asi que la atomicidad del
reemplazo y el comportamiento con el destino bloqueado NO se afirman por documento
sino por **medicion en esta maquina** (Windows 11, Python 3.14.7), que dio esto:

  - `os.replace` sobre un destino que ya existia: lo sustituye y el parcial
    desaparece.
  - `os.replace` con el destino abierto por otro programa: levanta
    `PermissionError` (errno 13, winerror 5), **el destino se queda intacto** y el
    parcial se queda donde estaba.
  - Al cerrar el otro programa, el mismo `os.replace` pasa.

Lo que NO se midio: un Excel de verdad teniendo el archivo abierto. El bloqueo se
reprodujo con una manija de lectura de Python, que ya basta para que Windows niegue
el reemplazo. Queda para QA.

Por eso el parcial vive en la misma carpeta que el definitivo (`espejo.rutas`) y no
en la de temporales del sistema. Y por eso matar el proceso a mitad no puede dejar
un `.xlsx` a medias: lo que esta a medias se llama `.parcial`.

**El archivo abierto en Excel.** `PermissionError` es la unica excepcion que este
modulo atrapa, y no la atrapa para silenciarla: la convierte en un aviso en espanol
que nombra el archivo y dice que hacer, y lo devuelve al que llamo. Lo que NO se
toca es la base: el dato ya esta guardado y confirmado antes de que el espejo se
intente siquiera.
"""

import os
from collections import namedtuple

from espejo.libro import construir_libro
from espejo.rutas import ruta_del_archivo_parcial, ruta_del_espejo

ResultadoDelEspejo = namedtuple("ResultadoDelEspejo", ("ruta", "escrito", "avisos"))
ResultadoDeGuardado = namedtuple("ResultadoDeGuardado", ("guardado", "espejo"))


def aviso_de_archivo_bloqueado(ruta, causa):
    """El texto que ve Miguel cuando el espejo no se pudo actualizar.

    Dice las tres cosas que necesita saber, en este orden: que sus datos NO se
    perdieron, cual es el archivo que estorba, y que tiene que hacer. Un aviso que
    solo dice «error al escribir» hace que se cierre el programa por si acaso, y
    entonces si se pierde algo.
    """
    return (
        f"AVISO: no se pudo actualizar el Excel espejo '{ruta}' porque otro "
        "programa lo tiene abierto (casi siempre es el propio Excel). "
        "LOS DATOS SÍ QUEDARON GUARDADOS EN LA BASE: no se ha perdido nada y no "
        "hace falta volver a escribirlos. Cierra el archivo en Excel y guarda "
        "cualquier caso otra vez; el espejo se pondrá al día solo. "
        f"El sistema dijo: {causa}"
    )


def _escribir_el_libro(libro, ruta_definitiva, ruta_parcial):
    """Escribe el parcial y lo asciende a definitivo. Devuelve los avisos."""
    ruta_definitiva.parent.mkdir(parents=True, exist_ok=True)
    try:
        libro.save(str(ruta_parcial))
        os.replace(ruta_parcial, ruta_definitiva)
    except PermissionError as causa:
        # El parcial se deja donde esta a proposito: borrarlo aqui podria fallar
        # por lo mismo que fallo el reemplazo, y su nombre es fijo, asi que la
        # siguiente regeneracion lo pisa. No se acumulan restos.
        return (aviso_de_archivo_bloqueado(ruta_definitiva, causa),)
    return ()


def regenerar_espejo(conexion, carpeta_de_datos=None, avisar=print):
    """Vuelve a escribir el `.xlsx` entero desde la base. Devuelve que paso.

    `avisar` recibe cada aviso ya redactado en espanol. Por defecto es `print`,
    igual que en `datos.arranque`: si el valor por defecto fuera no avisar, un
    llamador que se olvide del resultado convertiria esto en un fallo callado, que
    es justo lo que la fase prohibe. La pantalla de correccion (FASE 3) le pasara
    su propia funcion para mostrarlo donde se vea.
    """
    ruta_definitiva = ruta_del_espejo(carpeta_de_datos)
    libro = construir_libro(conexion)
    avisos = _escribir_el_libro(
        libro, ruta_definitiva, ruta_del_archivo_parcial(carpeta_de_datos)
    )

    if avisar is not None:
        for aviso in avisos:
            avisar(aviso)
    return ResultadoDelEspejo(ruta_definitiva, not avisos, avisos)


def guardar_y_regenerar(conexion, guardado, carpeta_de_datos=None, avisar=print):
    """Ejecuta un guardado y regenera el espejo justo despues, siempre.

    Este es el unico punto por el que la interfaz guarda. No es una comodidad: es
    lo que hace imposible guardar sin regenerar. Mientras la pantalla llame aqui en
    vez de llamar al repositorio directamente, no existe ningun camino que deje la
    base al dia y el Excel atrasado, y no hay ningun boton que a alguien se le
    pueda olvidar pulsar.

    `guardado` es la operacion de guardado ya preparada, sin argumentos: por
    ejemplo `lambda: alta_de_caso(conexion, **datos)`. Si levanta, el espejo NO se
    regenera —no hay nada nuevo que reflejar— y la excepcion sube tal cual.
    """
    resultado_del_guardado = guardado()
    return ResultadoDeGuardado(
        resultado_del_guardado,
        regenerar_espejo(conexion, carpeta_de_datos=carpeta_de_datos, avisar=avisar),
    )

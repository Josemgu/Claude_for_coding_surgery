"""El registro de tiempos: `fichas.log`, una linea por cosa que tarda.

**Existe porque el dueno pidio verificar algo que no existia.** Sus palabras el
2026-09-03: *«verifica los logs, un programa como ese debe ser rapido»*. Medido
antes de escribir nada: `grep -rln logging` sobre `fichas.py`, `interfaz/`,
`datos/` e `importacion/` no devolvia una sola linea, y en `Documentos\\Fichas` no
habia ningun `.log`. **No habia nada que verificar.**

**Que se registra, y nada mas:** las cuatro cosas que tardan y que el dueno nota.

    arranque        del doble clic a la ventana pintada
    caso            de pulsar un caso a ver el documento
    pagina          una pagina leida con OCR, que es el paso caro de importar
    paquete         generar el Excel de un companero, o un informe

Cada linea lleva su duracion en segundos. Un registro que no lleva el numero no
sirve para lo que este existe: convertir «lento» en una cifra que se puede comparar
entre dos maquinas.

⚠️ **NUNCA entra un dato de una persona.** Ni nombres, ni MRN, ni fechas de viaje.
Lo que se puede escribir es el numero de caso, el id, cuantas paginas y cuanto
tardo. El motivo no es formal: este archivo se va a mandar por correo o por
WhatsApp para diagnosticar una lentitud, y un `.log` con cedulas de miembro dentro
es una fuga de datos que nadie ve venir. `apuntar` no recibe texto libre por eso.

**Rotacion a 1 MB por tres archivos.** Sin tope, un registro que crece sin freno
acaba llenando la carpeta de datos del dueno; con tres archivos queda historia
suficiente para comparar «hoy» con «la semana pasada», que es para lo que sirve.

**Nada de esto puede tumbar el programa.** Si el archivo no se puede abrir —carpeta
de solo lectura, disco lleno, OneDrive con el archivo bloqueado—, se apunta en
memoria y se sigue. Un programa que no arranca porque no pudo escribir su propio
registro de tiempos seria peor que uno sin registro.
"""

import logging
import time
from logging.handlers import RotatingFileHandler

NOMBRE_DEL_ARCHIVO = "fichas.log"

# Un mega por archivo y tres archivos: unas 10 000 lineas por archivo con el
# formato de abajo. Con una jornada normal de 500 documentos son varias semanas.
BYTES_POR_ARCHIVO = 1_000_000
CUANTOS_ARCHIVOS = 3

# Las cuatro cosas que se cronometran. Se escriben como constantes y no como texto
# suelto en cada sitio: son lo que despues hay que poder contar y agrupar, y cuatro
# maneras distintas de escribir «arranque» no se agrupan.
ARRANQUE = "arranque"
CASO = "caso"
PAGINA = "pagina"
PAQUETE = "paquete"

_registrador = None


def preparar_el_registro(carpeta_de_datos):
    """Deja `fichas.log` listo dentro de la carpeta de datos. Devuelve la ruta.

    Se llama una vez, al arrancar, cuando ya se sabe en que carpeta se escribe.
    Devuelve None si no se pudo abrir el archivo: no es un fallo del programa y no
    se levanta.
    """
    global _registrador
    registrador = logging.getLogger("fichas.tiempos")
    registrador.setLevel(logging.INFO)
    registrador.propagate = False
    for anterior in list(registrador.handlers):
        registrador.removeHandler(anterior)
        anterior.close()
    ruta = carpeta_de_datos / NOMBRE_DEL_ARCHIVO
    try:
        salida = RotatingFileHandler(
            ruta, maxBytes=BYTES_POR_ARCHIVO, backupCount=CUANTOS_ARCHIVOS,
            encoding="utf-8",
        )
    except OSError:
        # Carpeta de solo lectura, disco lleno, o el archivo bloqueado por otro
        # proceso. Se sigue sin registro: no se atrapa para callar, se atrapa para
        # que un registro de tiempos no impida usar el programa.
        _registrador = None
        return None
    salida.setFormatter(logging.Formatter("%(asctime)s  %(message)s"))
    registrador.addHandler(salida)
    _registrador = registrador
    return ruta


def apuntar(que, segundos, **detalles):
    """Deja una linea: que fue, cuanto tardo, y los numeros que lo acompanan.

    `detalles` admite **solo numeros y numeros de caso**, nunca texto libre: es lo
    que impide que un nombre o un MRN acabe dentro. Lo que no sea un entero, un
    decimal o una cadena corta sin espacios se escribe como su tipo y no como su
    valor.

    No levanta nunca. Si no hay registro preparado, no hace nada.
    """
    if _registrador is None:
        return
    partes = [f"{que}", f"{segundos:.3f}s"]
    partes.extend(f"{clave}={_valor_seguro(valor)}" for clave, valor in detalles.items())
    _registrador.info("  ".join(partes))


def _valor_seguro(valor):
    """Lo que se puede escribir de un detalle, o su tipo si no se puede.

    Un entero y un decimal se escriben tal cual. Una cadena solo si es corta y no
    lleva espacios —un numero de caso, un motivo en clave—, que es lo que hace
    imposible que se cuele un nombre. Todo lo demas se reduce a su tipo: se ve que
    habia algo y no se dice que era.
    """
    if isinstance(valor, bool) or valor is None:
        return str(valor)
    if isinstance(valor, (int, float)):
        return str(valor)
    texto = str(valor)
    if len(texto) <= 24 and " " not in texto:
        return texto
    return f"<{type(valor).__name__}>"


class Cronometro:
    """Cronometra un bloque y deja su linea al salir. Se usa con `with`.

    Deja la linea **tambien cuando algo levanta dentro**: lo que tarda en fallar es
    justo lo que interesa cuando algo se cuelga, y un cronometro que solo apunta lo
    que sale bien no sirve para diagnosticar nada.
    """

    def __init__(self, que, **detalles):
        self.que = que
        self.detalles = detalles
        self._desde = None

    def __enter__(self):
        self._desde = time.perf_counter()
        return self

    def __exit__(self, tipo, causa, rastro):
        apuntar(self.que, time.perf_counter() - self._desde, **self.detalles)
        return False

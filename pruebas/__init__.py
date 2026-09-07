"""Las pruebas del proyecto, y el guardian que impide un verde vacio.

**El fallo que este archivo cierra.** La suite se descubre con un patron, y el
patron de este proyecto no es el que `unittest` trae de fabrica:

    python -m unittest discover -s pruebas -t . -p "prueba_*.py"   499 pruebas
    python -m unittest discover -s pruebas -t .                      0 pruebas

La segunda forma —la misma orden sin el `-p`— no encontraba nada, porque el patron
de fabrica es `test*.py` y aqui los archivos se llaman `prueba_*.py` (regla
permanente 4: espanol en todo). Una puerta escrita asi se queda **aprobando para
siempre sin ejecutar nada**.

⚠️ **La medicion de QA sobre este punto no se sostiene, y se comprobo antes de
tocar nada.** El informe decia «EXIT=0». Medido en esta maquina, Python 3.14.7:

    $ .venv/Scripts/python.exe -m unittest discover -s pruebas -t .
    Ran 0 tests in 0.000s
    NO TESTS RAN
    EXIT=5

Python devuelve **5**, no 0. El `0` sale de haber pasado la orden por una tuberia
—`| tail`, por ejemplo—, que entrega el codigo de salida del ultimo mandato de la
tuberia y no el de `unittest`. Asi que una puerta escrita con esa orden ya fallaba
en rojo; lo que si era cierto, y es lo que se arregla aqui, es que **no ejecutaba
ni una sola prueba**, incluidas las cuatro auditorias que protegen las reglas
permanentes.

**Como lo cierra.** `unittest` llama a `load_tests` de este archivo al descubrir el
paquete, y le pasa el patron que pidieron. Se comprobo aqui antes de escribirlo
(Python 3.14.7): se llama con `pattern='test*.py'`, y lo que devuelva es lo que se
ejecuta —el descubrimiento no recorre la carpeta por su cuenta—. Con eso:

  - un patron que casa con algo se respeta, y correr un solo archivo sigue
    funcionando;
  - un patron que no casa con nada cae al patron bueno, y la orden sin `-p`
    ejecuta la suite entera en vez de aprobar en vacio;
  - si aun asi no se cargara ninguna prueba, esto levanta. `unittest` convierte
    ese fallo en una prueba que falla, y la orden termina en rojo.

Lo que este archivo NO puede arreglar, dicho: si alguien pasa el resultado por una
tuberia, el codigo de salida que lee es el del ultimo mandato de la tuberia y no
el de las pruebas. Eso no lo arregla el codigo de las pruebas; lo arregla no
escribir la puerta asi.
"""

from pathlib import Path

CARPETA = Path(__file__).resolve().parent

# Como se llaman los archivos de prueba de este proyecto. En espanol, por la regla
# permanente 4, y por eso el patron de fabrica de `unittest` no los ve.
PATRON_DE_LAS_PRUEBAS = "prueba_*.py"


def modulos_que_casan(patron):
    """Los modulos de esta carpeta cuyo archivo casa con el patron, ordenados.

    Se buscan con `glob` sobre la carpeta y no con `loader.discover`: `discover`
    sobre este mismo paquete volveria a llamar a `load_tests`, y eso no termina.
    """
    return [ruta.stem for ruta in sorted(CARPETA.glob(patron)) if ruta.stem != "__init__"]


def load_tests(cargador, pruebas_estandar, patron):
    """Lo que `unittest` ejecuta al descubrir este paquete. Devuelve la suite.

    El nombre y la firma los fija `unittest`, y por eso son los dos unicos de este
    proyecto que no estan en espanol: cambiarlos haria que no se llamara nunca.
    """
    nombres = modulos_que_casan(patron) if patron else []
    if not nombres:
        nombres = modulos_que_casan(PATRON_DE_LAS_PRUEBAS)

    for nombre in nombres:
        pruebas_estandar.addTests(cargador.loadTestsFromName(f"pruebas.{nombre}"))

    if pruebas_estandar.countTestCases() == 0:
        raise RuntimeError(
            "No se cargó ninguna prueba, y eso NO es un aprobado: es una suite "
            f"vacía. Se buscó con el patrón {patron!r} y después con "
            f"{PATRON_DE_LAS_PRUEBAS!r} en «{CARPETA}», y no casó ningún archivo. "
            "Compruebe que la carpeta de pruebas es la correcta."
        )
    return pruebas_estandar


__all__ = ["PATRON_DE_LAS_PRUEBAS", "load_tests", "modulos_que_casan"]

"""Lista blanca de dependencias: ni una sola llamada a un modelo de lenguaje.

Es el criterio que protege la **regla permanente 1**, la que impide que un MRN o
una fecha de viaje salgan inventados por un modelo. Un MRN inventado manda a una
persona al templo con la recomendacion mal.

**No es un `grep`.** Un `grep` de nombres de proveedores falla en los dos
sentidos a la vez: suspende codigo limpio —cualquier URL de documentacion en un
comentario casa con `https://`, y este proyecto manda poner esas URL— y aprueba
codigo culpable, porque no caza al proveedor que salga el mes que viene.

Lo que hace en su lugar: enumera con el arbol de sintaxis **todas** las
importaciones de los archivos, y comprueba que cada modulo es biblioteca estandar
de Python o uno de los seis de `CLAUDE.md` §3. **Cualquier otro nombre es un
hallazgo**, se llame como se llame y exista o no hoy. Un comentario no es una
importacion, asi que la URL deja de dar falso positivo por construccion.

    python -m pruebas.auditoria_dependencias        todo el codigo del proyecto
    python -m pruebas.auditoria_dependencias datos  solo esa carpeta

⚠️ **Lo que este guardian NO caza, y esta aceptado como deuda en `DECISIONES.md`:**
la importacion que no se escribe como importacion. QA le paso cinco mutantes en la
auditoria final y sobrevivio a tres: `importlib.import_module("openai")`,
`__import__("open"+"ai")` y `exec("import openai")` pasan sin que esto los vea,
porque en el arbol de sintaxis no son un `Import` sino una llamada a una funcion
con una cadena dentro. Caza los dos que si son importaciones —`import openai` y
`from anthropic import ...`—. Queda dicho para que nadie lo lea como una garantia
que no da.

**Por que las dos listas de abajo se derivan y no se escriben a mano.** La de
paquetes propios se escribia a mano y se quedo desfasada: nacio con cuatro
—`extraccion`, `datos`, `espejo`, `pruebas`— y el proyecto llego a ocho. Correr
esto sobre el codigo entero devolvia **103 hallazgos, los 103 falsos**: eran los
propios paquetes del proyecto importandose entre si. Un guardian que grita 103
veces sobre su propio codigo es un guardian que nadie vuelve a correr, y entonces
la regla permanente 1 se queda sin quien la vigile. Es el mismo motivo por el que
`datos/esquema.py` deriva `VERSION_ACTUAL` de la lista de migraciones en vez de
copiarla: «un numero copiado a mano se queda desfasado el dia que alguien anade
una migracion y se olvida de subirlo».
"""

import ast
import sys
from pathlib import Path

RAIZ_DEL_PROYECTO = Path(__file__).resolve().parent.parent

# Los seis de `CLAUDE.md` §3, y nada mas. `numpy` entra porque es como viajan las
# imagenes entre pypdfium2 y opencv: no es una dependencia elegida, es el tipo de
# dato que esas dos ya imponen.
#
# `onnxruntime` entra por lo mismo, y esto se midio antes de anadirlo: es el motor
# que ejecuta los tres `.onnx` de PP-OCRv5 —sin el no hay OCR—, esta fijado en
# `requirements.txt` (`onnxruntime==1.29.0`) y `fichas.spec` lo nombra como una de
# las dos bibliotecas nativas que viajan dentro del ejecutable. Medido en esta
# maquina: `pip show rapidocr` (3.9.2) NO lo declara entre sus `Requires`, asi que
# es una dependencia directa de este proyecto y no una transitiva.
#
# ⚠️ La tabla de `CLAUDE.md` §3 no lo lista. Ese documento no lo toca el
# programador: queda anotado para el dueno, porque la lista de tecnologias y esta
# lista blanca tendrian que decir lo mismo.
DEPENDENCIAS_ADMITIDAS = frozenset(
    {
        "pypdf",
        "pypdfium2",
        "cv2",
        "rapidocr",
        "onnxruntime",
        "sqlite3",
        "openpyxl",
        "numpy",
    }
)


def paquetes_del_proyecto(raiz=RAIZ_DEL_PROYECTO):
    """Las carpetas de la raiz que son paquetes de Python, leidas del disco.

    Un paquete es una carpeta con `__init__.py` dentro, que es la definicion que
    usa el propio interprete. Se descartan las que empiezan por `.` o `_`
    —`.venv`, `.git`, `__pycache__`— porque no son codigo de este proyecto: la
    carpeta del entorno virtual tiene dentro cientos de paquetes de terceros y
    auditarlos aqui no dice nada de este programa.
    """
    return frozenset(
        carpeta.name
        for carpeta in sorted(raiz.iterdir())
        if carpeta.is_dir()
        and not carpeta.name.startswith((".", "_"))
        and (carpeta / "__init__.py").is_file()
    )


def modulos_del_proyecto(raiz=RAIZ_DEL_PROYECTO):
    """Todo lo que este proyecto puede importar de si mismo: paquetes y sueltos.

    Los `.py` sueltos de la raiz cuentan como modulos propios porque eso es lo que
    son para el interprete: `from referencia_prueba import ...` importa el archivo
    de al lado, no una biblioteca de nadie. Sin esto, un archivo de la raiz que
    importe a otro de la raiz sale como hallazgo, que es un falso positivo.
    """
    return paquetes_del_proyecto(raiz) | frozenset(
        ruta.stem for ruta in raiz.glob("*.py")
    )


def carpetas_por_defecto(raiz=RAIZ_DEL_PROYECTO):
    """Todo el codigo del proyecto: los paquetes y los `.py` sueltos de la raiz.

    Los sueltos entran porque uno de ellos es `fichas.py`, el punto de entrada del
    programa: dejarlo fuera seria auditar todo menos el archivo que arranca. Se
    devuelven rutas absolutas para que el resultado no dependa de desde donde se
    llame.
    """
    paquetes = [raiz / nombre for nombre in sorted(paquetes_del_proyecto(raiz))]
    sueltos = [ruta for ruta in sorted(raiz.glob("*.py"))]
    return [str(ruta) for ruta in paquetes + sueltos]


def modulos_importados(ruta):
    """Cada modulo raiz importado en el archivo, con la linea donde se importa.

    Se queda con la raiz: `google.generativeai` cuenta como `google`, que es lo
    que hay que juzgar. Una importacion dentro de una funcion cuenta igual que
    una de arriba: `ast.walk` recorre el archivo entero.
    """
    arbol = ast.parse(Path(ruta).read_text(encoding="utf-8"), filename=str(ruta))
    encontrados = []
    for nodo in ast.walk(arbol):
        if isinstance(nodo, ast.Import):
            for alias in nodo.names:
                encontrados.append((nodo.lineno, alias.name.split(".")[0]))
        elif isinstance(nodo, ast.ImportFrom):
            # Una importacion relativa (`from .x import y`) no tiene modulo raiz
            # externo: `nodo.level > 0` y `nodo.module` puede ser None.
            if nodo.level == 0 and nodo.module:
                encontrados.append((nodo.lineno, nodo.module.split(".")[0]))
    return encontrados


def es_admitido(modulo, propios=None):
    """Cierto si el modulo es estandar, del proyecto, o de los seis permitidos."""
    propios = modulos_del_proyecto() if propios is None else propios
    return (
        modulo in DEPENDENCIAS_ADMITIDAS
        or modulo in propios
        or modulo in sys.stdlib_module_names
    )


def _archivos_de(ruta):
    """Los `.py` que cuelgan de una ruta, sea carpeta o archivo suelto."""
    camino = Path(ruta)
    return sorted(camino.rglob("*.py")) if camino.is_dir() else [camino]


def auditar(carpetas):
    """Devuelve `(modulos_distintos, hallazgos)` sobre los .py de las carpetas."""
    propios = modulos_del_proyecto()
    distintos = set()
    hallazgos = []
    for carpeta in carpetas:
        for ruta in _archivos_de(carpeta):
            for linea, modulo in modulos_importados(ruta):
                distintos.add(modulo)
                if not es_admitido(modulo, propios):
                    hallazgos.append((str(ruta), linea, modulo))
    return distintos, hallazgos


def main(argumentos):
    carpetas = argumentos or carpetas_por_defecto()
    distintos, hallazgos = auditar(carpetas)
    print(f"carpetas auditadas: {carpetas}")
    print(f"modulos propios reconocidos: {sorted(modulos_del_proyecto())}")
    print(f"modulos distintos importados (N): {len(distintos)}")
    print(f"  {sorted(distintos)}")
    print(f"hallazgos (modulo fuera de la lista blanca): {len(hallazgos)}")
    for ruta, linea, modulo in hallazgos:
        print(f"  HALLAZGO {ruta}:{linea} importa {modulo!r}, que no esta en la lista blanca")

    # Cero importaciones examinadas no es un aprobado, es un denominador vacio: la
    # ruta estaba mal escrita o la carpeta no existe. Se dictamina distinto que un
    # hallazgo (2 y no 1) para que quien lo lea sepa cual de las dos cosas paso.
    # Es el mismo guardian que ya llevaban `auditoria_sql` y `auditoria_rutas`.
    if not distintos:
        print("VEREDICTO: NO PASA — cero importaciones examinadas no es un aprobado.")
        return 2
    if hallazgos:
        print("VEREDICTO: NO PASA — hay importaciones fuera de la lista blanca.")
        return 1
    print(
        f"VEREDICTO: PASA — {len(distintos)} modulos distintos, todos admitidos, "
        "y ni una sola llamada a un modelo de lenguaje."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

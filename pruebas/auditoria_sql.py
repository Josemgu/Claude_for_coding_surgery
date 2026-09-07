"""Lista blanca de instrucciones SQL: enumera TODAS y dictamina una a una.

FASE 1, criterio 3a-3c de `PENDIENTES.md`. La forma de comprobar no es buscar las
construcciones malas que a uno se le ocurrieron —eso es una lista negra y solo
prueba lo que su autor imagino—, sino **enumerar todas las llamadas al motor y
revisar cada una**.

Por eso esto no es un `grep`: lee el arbol de sintaxis de Python. Un `grep`
anclado en `execute(` no puede ver el caso mas natural de escribir mal —la
consulta se arma con una f-string arriba y se ejecuta diez lineas mas abajo—
porque en la linea del `execute` no hay nada raro que ver. El arbol si lo ve,
porque puede seguir el nombre de la variable hasta donde se asigno.

    python -m pruebas.auditoria_sql datos pruebas
"""

import ast
import sys
from pathlib import Path

FUNCIONES_DEL_MOTOR = ("execute", "executemany", "executescript")

CONFORME = "CONFORME"
INSEGURA = "INSEGURA"
NO_RESUELTA = "NO RESUELTA"


class Hallazgo:
    """Una llamada al motor, con su veredicto y el motivo de ese veredicto."""

    def __init__(self, archivo, linea, funcion, veredicto, motivo):
        self.archivo = archivo
        self.linea = linea
        self.funcion = funcion
        self.veredicto = veredicto
        self.motivo = motivo

    def __str__(self):
        return (
            f"{self.archivo}:{self.linea}  {self.funcion:<15} "
            f"{self.veredicto:<11} {self.motivo}"
        )


def _nombre_de_la_funcion(nodo):
    """El nombre de la funcion que se esta llamando, o None si no es del motor."""
    destino = nodo.func
    nombre = destino.attr if isinstance(destino, ast.Attribute) else getattr(destino, "id", None)
    return nombre if nombre in FUNCIONES_DEL_MOTOR else None


def _es_interpolacion(valor):
    """Dice si un nodo produce texto pegando trozos: f-string, +, % o .format()."""
    if isinstance(valor, ast.JoinedStr):
        return "f-string"
    if isinstance(valor, ast.BinOp) and isinstance(valor.op, ast.Add):
        return "concatenacion con +"
    if isinstance(valor, ast.BinOp) and isinstance(valor.op, ast.Mod):
        return "operador %"
    if (
        isinstance(valor, ast.Call)
        and isinstance(valor.func, ast.Attribute)
        and valor.func.attr == "format"
    ):
        return ".format()"
    return None


def _asignaciones_del_modulo(arbol):
    """Todas las asignaciones del archivo, indexadas por nombre de variable."""
    asignaciones = {}
    for nodo in ast.walk(arbol):
        destinos = []
        if isinstance(nodo, ast.Assign):
            destinos = nodo.targets
        elif isinstance(nodo, ast.AnnAssign) and nodo.value is not None:
            destinos = [nodo.target]
        for destino in destinos:
            if isinstance(destino, ast.Name):
                asignaciones.setdefault(destino.id, []).append(nodo.value)
    return asignaciones


def _dictaminar_argumento(valor, asignaciones, profundidad=0):
    """Devuelve (veredicto, motivo) para el primer argumento de la llamada."""
    forma = _es_interpolacion(valor)
    if forma is not None:
        return INSEGURA, f"la instruccion se arma con {forma}"

    if isinstance(valor, ast.Constant) and isinstance(valor.value, str):
        return CONFORME, "cadena literal escrita en el sitio"

    if isinstance(valor, ast.Name):
        if profundidad >= 5:
            return NO_RESUELTA, f"la variable '{valor.id}' encadena demasiadas asignaciones"
        origenes = asignaciones.get(valor.id)
        if not origenes:
            return NO_RESUELTA, f"la variable '{valor.id}' no se asigna en este archivo"
        motivos = []
        for origen in origenes:
            veredicto, motivo = _dictaminar_argumento(origen, asignaciones, profundidad + 1)
            if veredicto != CONFORME:
                return veredicto, f"la variable '{valor.id}': {motivo}"
            motivos.append(motivo)
        return CONFORME, f"la variable '{valor.id}' es una constante literal de modulo"

    return NO_RESUELTA, f"el argumento es un {type(valor).__name__} que no se puede seguir"


def _revisar_parametros(nodo, sql_literal):
    """Si la instruccion trae marcadores `?`, tiene que venir la tupla de valores."""
    if sql_literal is None or "?" not in sql_literal:
        return None
    if len(nodo.args) < 2:
        return "tiene marcadores ? pero no se le pasa la tupla de parametros"
    return None


def auditar_archivo(ruta):
    """Todas las llamadas al motor de un archivo .py, con su veredicto."""
    texto = Path(ruta).read_text(encoding="utf-8")
    arbol = ast.parse(texto, filename=str(ruta))
    asignaciones = _asignaciones_del_modulo(arbol)

    hallazgos = []
    for nodo in ast.walk(arbol):
        if not isinstance(nodo, ast.Call):
            continue
        funcion = _nombre_de_la_funcion(nodo)
        if funcion is None or not nodo.args:
            continue

        veredicto, motivo = _dictaminar_argumento(nodo.args[0], asignaciones)
        literal = nodo.args[0].value if isinstance(nodo.args[0], ast.Constant) else None
        falta = _revisar_parametros(nodo, literal)
        if veredicto == CONFORME and falta is not None:
            veredicto, motivo = INSEGURA, falta
        hallazgos.append(Hallazgo(ruta, nodo.lineno, funcion, veredicto, motivo))
    return hallazgos


def auditar(rutas):
    """Audita todos los .py que cuelgan de las rutas dadas, ordenados."""
    archivos = []
    for ruta in rutas:
        camino = Path(ruta)
        archivos.extend(sorted(camino.rglob("*.py")) if camino.is_dir() else [camino])

    hallazgos = []
    for archivo in archivos:
        hallazgos.extend(auditar_archivo(archivo))
    return archivos, hallazgos


def main(argumentos):
    rutas = argumentos or ["datos", "pruebas"]
    archivos, hallazgos = auditar(rutas)

    print(f"Archivos .py revisados: {len(archivos)}")
    for archivo in archivos:
        print(f"  {archivo}")
    print()
    print(f"N = llamadas a execute / executemany / executescript encontradas: {len(hallazgos)}")
    print()
    for hallazgo in hallazgos:
        print(f"  {hallazgo}")
    print()

    conformes = [h for h in hallazgos if h.veredicto == CONFORME]
    print(f"CONFORMES: {len(conformes)} de {len(hallazgos)}")
    if not hallazgos:
        print("VEREDICTO: NO PASA — cero llamadas al motor no es un aprobado.")
        return 2
    if len(conformes) != len(hallazgos):
        print("VEREDICTO: NO PASA — hay llamadas que no son conformes.")
        return 1
    print("VEREDICTO: PASA — N de N revisadas y N de N conformes.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

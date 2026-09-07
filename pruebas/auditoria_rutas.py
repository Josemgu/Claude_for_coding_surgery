"""Lista blanca de literales de cadena: la ruta de datos no se compone por nombre.

FASE 1, criterio 1c de `PENDIENTES.md`, que sale de `DECISIONES.md` (2026-09-02):
la carpeta de datos se obtiene con la API de carpetas conocidas de Windows,
**nunca** componiendola con las cadenas «Documentos» ni «Documents».

Igual que la auditoria de SQL, esto no es un `grep`: enumera **todos** los
literales de cadena del arbol de sintaxis y comprueba que ninguno es uno de los
dos nombres prohibidos. Es lista blanca, no lista negra, y por eso distingue lo
que un `grep` no distingue: un comentario o un nombre de variable que mencione la
palabra no es un trozo de ruta, y un literal que la contenga si lo es.

    python -m pruebas.auditoria_rutas datos
"""

import ast
import sys
from pathlib import Path

NOMBRES_PROHIBIDOS = ("documentos", "documents")


def literales_del_archivo(ruta):
    """Cada literal de cadena del archivo, con su linea."""
    arbol = ast.parse(Path(ruta).read_text(encoding="utf-8"), filename=str(ruta))
    return [
        (nodo.lineno, nodo.value)
        for nodo in ast.walk(arbol)
        if isinstance(nodo, ast.Constant) and isinstance(nodo.value, str)
    ]


def auditar(rutas):
    """Devuelve (archivos, total de literales, prohibidos, mencionados)."""
    archivos = []
    for ruta in rutas:
        camino = Path(ruta)
        archivos.extend(sorted(camino.rglob("*.py")) if camino.is_dir() else [camino])

    total = 0
    prohibidos = []
    mencionados = []
    for archivo in archivos:
        for linea, valor in literales_del_archivo(archivo):
            total += 1
            if valor.strip().lower() in NOMBRES_PROHIBIDOS:
                prohibidos.append((archivo, linea, valor))
            elif any(nombre in valor.lower() for nombre in NOMBRES_PROHIBIDOS):
                mencionados.append((archivo, linea, valor.strip().splitlines()[0][:70]))
    return archivos, total, prohibidos, mencionados


def main(argumentos):
    archivos, total, prohibidos, mencionados = auditar(argumentos or ["datos"])

    print(f"Archivos .py revisados: {len(archivos)}")
    print(f"Literales de cadena examinados (denominador): {total}")
    print()
    print(f"Literales que SON el nombre de la carpeta: {len(prohibidos)}")
    for archivo, linea, valor in prohibidos:
        print(f"  {archivo}:{linea}  {valor!r}")
    print()
    print(f"Literales que solo MENCIONAN la palabra (informativo, no compone ruta): "
          f"{len(mencionados)}")
    for archivo, linea, extracto in mencionados:
        print(f"  {archivo}:{linea}  {extracto!r}")
    print()

    if total == 0:
        print("VEREDICTO: NO PASA — cero literales examinados no es un aprobado.")
        return 2
    if prohibidos:
        print("VEREDICTO: NO PASA — hay literales que componen la ruta por nombre.")
        return 1
    print("VEREDICTO: PASA — ningun literal de cadena es 'Documentos' ni 'Documents'.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

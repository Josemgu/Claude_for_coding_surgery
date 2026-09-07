"""Corre el extractor sobre los formularios reales y comprueba lo estructural.

NO esta entre las `prueba_*.py` que recoge el descubrimiento automatico, y es a
proposito por dos motivos: tarda unos 75 segundos, y depende de los PDF de
`pdfs_referencia/`, que llevan nombres y MRN de personas reales y estan fuera del
repositorio por `.gitignore`. En otra maquina no habria nada que leer.

    python -m pruebas.auditoria_extraccion_real

**Ninguna comprobacion de aqui mira un valor personal.** Se comprueban el numero
de paginas, el tamano de la imagen, la procedencia de los campos y el conteo de
personas. Que la fecha de viaje de una persona concreta sea tal dia no se
escribe: eso convertiria este archivo en una copia de los datos.
"""

import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
CARPETA_DE_LOS_PDF = RAIZ / "pdfs_referencia"

# Lo medido el 2026-09-02 sobre los cuatro documentos, con `pypdf`.
PAGINAS_ESPERADAS = 9
ANCHO_ESPERADO_PX = 2705
ALTO_ESPERADO_PX = 3500
PERSONAS_ESPERADAS = 15
TRAZOS_SIN_CLASIFICAR_ESPERADOS = 0


def _documentos():
    """Los PDF de referencia, sin el duplicado byte a byte."""
    vistos = {}
    for ruta in sorted(CARPETA_DE_LOS_PDF.glob("*.pdf")):
        vistos.setdefault(ruta.stat().st_size, ruta)
    return sorted(vistos.values())


def main():
    if not CARPETA_DE_LOS_PDF.is_dir():
        print(f"No esta {CARPETA_DE_LOS_PDF}: no hay nada que auditar.")
        return 0

    from extraccion.formulario import extraer_documento
    from extraccion.ocr import crear_motor_ocr

    motor = crear_motor_ocr()
    fallos = []
    paginas = personas = trazos_sin_clasificar = 0
    tachones_que_anularon = 0
    segundos = 0.0

    for ruta in _documentos():
        formularios = extraer_documento(ruta, motor)
        for formulario in formularios:
            paginas += 1
            personas += len(formulario.personas)
            segundos += formulario.segundos
            trazos_sin_clasificar += formulario.resumen_de_anotaciones["desconocido"]
            if formulario.tamano_de_la_imagen != (ANCHO_ESPERADO_PX, ALTO_ESPERADO_PX):
                fallos.append(
                    f"{ruta.name[:8]} p{formulario.indice_de_pagina}: la imagen salio "
                    f"{formulario.tamano_de_la_imagen} y no "
                    f"({ANCHO_ESPERADO_PX}, {ALTO_ESPERADO_PX})"
                )
            if max(formulario.tamano_de_la_imagen) > ALTO_ESPERADO_PX:
                fallos.append(f"{ruta.name[:8]} p{formulario.indice_de_pagina}: paso del tope")
            if formulario.fecha_viaje.anulado_por_tachon and formulario.fecha_viaje.valor:
                # El caso que la fase existe para resolver: habia un tachon y aun
                # asi salio un valor, asi que gano una correccion escrita.
                tachones_que_anularon += 1
                if formulario.fecha_viaje.origen != "anotacion":
                    fallos.append(
                        f"{ruta.name[:8]} p{formulario.indice_de_pagina}: con tachon y valor, "
                        f"el origen deberia ser 'anotacion' y fue "
                        f"{formulario.fecha_viaje.origen!r}"
                    )
                if formulario.fecha_viaje.confianza != 1.0:
                    fallos.append(
                        f"{ruta.name[:8]} p{formulario.indice_de_pagina}: un valor de anotacion "
                        f"debe llevar confianza 1.0 y llevo {formulario.fecha_viaje.confianza}"
                    )

    print(f"documentos            : {len(_documentos())}")
    print(f"paginas procesadas    : {paginas} (esperadas {PAGINAS_ESPERADAS})")
    print(f"personas extraidas    : {personas} (esperadas {PERSONAS_ESPERADAS})")
    print(f"trazos sin clasificar : {trazos_sin_clasificar} (esperados {TRAZOS_SIN_CLASIFICAR_ESPERADOS})")
    print(f"fechas con tachon anulado y correccion aplicada: {tachones_que_anularon}")
    print(f"segundos totales      : {segundos:.1f} ({segundos / max(1, paginas):.2f} por pagina)")

    if paginas != PAGINAS_ESPERADAS:
        fallos.append(f"se procesaron {paginas} paginas y se esperaban {PAGINAS_ESPERADAS}")
    if personas != PERSONAS_ESPERADAS:
        fallos.append(f"salieron {personas} personas y se esperaban {PERSONAS_ESPERADAS}")
    if trazos_sin_clasificar != TRAZOS_SIN_CLASIFICAR_ESPERADOS:
        fallos.append(f"hay {trazos_sin_clasificar} trazos sin clasificar")
    if tachones_que_anularon < 1:
        fallos.append(
            "no se demostro la precedencia: ningun tachon anulo una banda que "
            "luego ganara una correccion escrita"
        )

    print(f"\nfallos: {len(fallos)}")
    for fallo in fallos:
        print(f"  FALLO {fallo}")
    return 1 if fallos else 0


if __name__ == "__main__":
    sys.exit(main())

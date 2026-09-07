# MATERIAL DE PRUEBA - FASE 0. Se descarta al cerrar la fase; no es codigo del programa Fichas.
"""Fabrica el PDF de prueba con texto latino impreso (tildes y enes).

Escribe el PDF a mano, sin bibliotecas de generacion, para no meter una
dependencia mas en el requirements.txt que esta fase tiene que congelar. Usa la
fuente base Helvetica con /WinAnsiEncoding, que cubre a e i o u con tilde y la
ene. El texto sale de referencia_prueba.py, que se escribe antes que el PDF.
"""

import sys
from pathlib import Path

from referencia_prueba import LINEAS_ACOMPANANTES, LINEA_REFERENCIA

RUTA_PDF = Path(__file__).resolve().parent / "prueba_latina.pdf"

ANCHO_PAGINA = 612
ALTO_PAGINA = 792
# 13 pt, no 26. Con 26 la linea de referencia (69 caracteres) medía unos 990 pt
# sobre una pagina de 612 y se salia por el borde derecho: el OCR devolvia
# "...Peña via" y el criterio 4 anotaba 24 caracteres de diferencia que no eran
# del OCR sino del PDF mal dibujado. A 13 pt la linea ocupa unos 495 pt y cabe.
TAMANO_FUENTE = 13
INTERLINEADO = 45
MARGEN_IZQUIERDO = 45
PRIMERA_LINEA_Y = 700


def escapar_texto_pdf(texto: str) -> bytes:
    """Codifica una cadena a WinAnsi y escapa los tres caracteres reservados."""
    crudo = texto.encode("cp1252")
    for original, sustituto in ((b"\\", b"\\\\"), (b"(", b"\\("), (b")", b"\\)")):
        crudo = crudo.replace(original, sustituto)
    return crudo


def construir_flujo_de_texto(lineas: tuple) -> bytes:
    """Arma el flujo de contenido que dibuja una linea de texto por elemento."""
    partes = [b"BT\n/F1 %d Tf\n" % TAMANO_FUENTE]
    for indice, linea in enumerate(lineas):
        posicion_y = PRIMERA_LINEA_Y - indice * INTERLINEADO
        partes.append(b"1 0 0 1 %d %d Tm\n" % (MARGEN_IZQUIERDO, posicion_y))
        partes.append(b"(" + escapar_texto_pdf(linea) + b") Tj\n")
    partes.append(b"ET\n")
    return b"".join(partes)


def construir_pdf(lineas: tuple) -> bytes:
    """Devuelve el PDF completo con su tabla de referencias cruzadas correcta."""
    flujo = construir_flujo_de_texto(lineas)
    objetos = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 %d %d] "
        b"/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>"
        % (ANCHO_PAGINA, ALTO_PAGINA),
        b"<< /Length %d >>\nstream\n" % len(flujo) + flujo + b"endstream",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica "
        b"/Encoding /WinAnsiEncoding >>",
    ]

    salida = bytearray(b"%PDF-1.4\n%\xe1\xe9\xf1\n")
    desplazamientos = []
    for numero, cuerpo in enumerate(objetos, start=1):
        desplazamientos.append(len(salida))
        salida += b"%d 0 obj\n" % numero + cuerpo + b"\nendobj\n"

    inicio_xref = len(salida)
    salida += b"xref\n0 %d\n" % (len(objetos) + 1)
    salida += b"0000000000 65535 f \n"
    for desplazamiento in desplazamientos:
        salida += b"%010d 00000 n \n" % desplazamiento
    salida += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (
        len(objetos) + 1,
        inicio_xref,
    )
    return bytes(salida)


def main() -> int:
    lineas = LINEAS_ACOMPANANTES[:2] + (LINEA_REFERENCIA,) + LINEAS_ACOMPANANTES[2:]
    RUTA_PDF.write_bytes(construir_pdf(lineas))
    print(f"PDF escrito: {RUTA_PDF} ({RUTA_PDF.stat().st_size} bytes)")
    print(f"Linea de referencia: {LINEA_REFERENCIA}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

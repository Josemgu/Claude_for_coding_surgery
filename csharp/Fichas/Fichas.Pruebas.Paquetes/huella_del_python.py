"""Saca la huella estructural de la hoja «Por verificar» que produce HOY el Python.

Solo LEE del repositorio principal (por PYTHONPATH) y escribe unicamente en esta
carpeta temporal. No abre ninguna base de datos: `construir_libro_de_trabajo` es una
funcion pura que recibe filas y devuelve el libro en memoria.
"""

import json
import sys

sys.path.insert(0, r"C:\Users\josem\OneDrive\Escritorio\Trabajo")

from paquete.columnas import COLUMNAS, ancho_de, armar_la_clave  # noqa: E402
from paquete.trabajo import construir_libro_de_trabajo  # noqa: E402

FILAS = [
    {
        "numero_caso": "BALC2609",
        "fecha_solicitud": None,
        "fecha_viaje": "2026-10-15",
        "templo": "Santo Domingo",
        "unidad_nombre": "Cuatricentenaria (7000014)",
        "estaca": None,
        "nombre": "Elena Rosa Muestra",
        "mrn": "055-1111-385A",
        "a_que_va": "Investidura",
        "clave": armar_la_clave("BALC2609", "055-1111-385A", 12),
    },
    {
        "numero_caso": "BALC2609",
        "fecha_solicitud": None,
        "fecha_viaje": "2026-10-15",
        "templo": "Santo Domingo",
        "unidad_nombre": "Cuatricentenaria (7000014)",
        "estaca": None,
        "nombre": "Julia Luz Inventada",
        "mrn": "055-1111-3853",
        "a_que_va": "Investidura",
        "clave": armar_la_clave("BALC2609", "055-1111-3853", 12),
    },
]


def color(objeto):
    """El RRGGBB de un color de openpyxl, sin el canal alfa, o None."""
    valor = getattr(objeto, "rgb", None)
    if not isinstance(valor, str):
        return None
    return valor[-6:].upper()


def huella():
    libro = construir_libro_de_trabajo(FILAS, agente="Sandy")
    hoja = libro.active
    return {
        "hoja": hoja.title,
        "congelado": hoja.freeze_panes,
        "protegida": bool(hoja.protection.sheet),
        "cinco_lineas": [hoja.cell(row=n, column=1).value for n in range(1, 6)],
        "color_de_la_fecha_limite": color(hoja.cell(row=3, column=1).font.color),
        "titulos": [c.value for c in hoja[6]],
        "fondo_de_los_titulos": color(hoja.cell(row=6, column=3).fill.fgColor),
        "columnas": [
            {
                "nombre": columna.nombre,
                "titulo": columna.titulo,
                "clase": columna.clase,
                "clave": columna.clave,
                "editable": columna.editable,
                "respuesta": columna.respuesta,
                "ancho": ancho_de(columna.nombre),
                "formato": hoja.cell(row=7, column=numero).number_format,
                "bloqueada": bool(hoja.cell(row=7, column=numero).protection.locked),
                "fondo": color(hoja.cell(row=7, column=numero).fill.fgColor),
                "valor_fila_7": str(hoja.cell(row=7, column=numero).value),
            }
            for numero, columna in enumerate(COLUMNAS, start=1)
        ],
        "menus": len(hoja.data_validations.dataValidation),
        "formulas_de_los_menus": sorted(
            {v.formula1 for v in hoja.data_validations.dataValidation}
        ),
        "menus_rechazan": sorted(
            {bool(v.showErrorMessage) for v in hoja.data_validations.dataValidation}
        ),
    }


if __name__ == "__main__":
    print(json.dumps(huella(), ensure_ascii=False, indent=2))

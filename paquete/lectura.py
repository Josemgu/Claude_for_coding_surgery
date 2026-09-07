"""Leer un `.xlsx` que vuelve, sea el de trabajo o uno cualquiera de Miguel.

Este modulo NO decide nada sobre los datos: los saca del archivo y los deja en
diccionarios. Quien decide que hacer con ellos es `paquete/reconciliacion.py`.
Estan separados a proposito: el importador de Excel libre (`paquete/mapeo.py`)
reutiliza esta mitad entera y solo cambia como se llaman las columnas.

**Se lee con `data_only=True`.** Una celda con formula devuelve entonces el ultimo
valor que Excel calculo y guardo, no el texto `=A2&""`. Sin eso, un companero que
armara su columna con una formula devolveria formulas y todas sus filas se irian a
descartados.

**Lo que se lee se normaliza a texto, y ahi hay un peligro concreto.** Si el
companero teclea un MRN sobre una celda cuyo formato se perdio, Excel lo puede
devolver como numero: `5511113` en vez de `055-1111-3853`. Ese valor no casa, y NO
se arregla aqui —rellenar ceros seria inventar un MRN, que es exactamente lo que
la regla permanente 1 prohibe—. Se deja como llego y la fila cae en descartados con
su motivo, que es donde Miguel lo ve.

Sin `pandas` (regla permanente 3): solo `openpyxl`.
"""

from collections import namedtuple
from datetime import date, datetime
from pathlib import Path

from openpyxl import load_workbook

from paquete.columnas import COLUMNA_DE_LA_CLAVE, COLUMNAS_POR_NOMBRE, NOMBRE_DE_LA_HOJA

# Donde estan los titulos cuando la hoja no dice donde estan. Un Excel que arma
# Miguel por su cuenta pone los titulos arriba del todo.
FILA_DE_TITULOS_POR_DEFECTO = 1

# Hasta que fila se busca la fila de titulos. La hoja «Por verificar» la trae en la
# 6, y el tope deja aire por si alguien anade una linea a la cabecera. Mas abajo de
# eso, lo que se encontrara no es una cabecera sino una fila de datos que casualmente
# dice «clave».
ULTIMA_FILA_DONDE_SE_BUSCA_LA_CABECERA = 30

# Tope de filas que se leen de un archivo. Un `.xlsx` con la columna entera
# formateada trae un millon de filas vacias, y recorrerlas cuelga la ventana sin
# decir por que. 20 000 filas son mas personas de las que este programa vera nunca.
MAXIMO_DE_FILAS = 20_000

FilaLeida = namedtuple("FilaLeida", ("numero", "valores"))
LibroLeido = namedtuple(
    "LibroLeido", ("hoja", "titulos", "filas", "avisos", "fila_de_titulos")
)


class ErrorDeLectura(RuntimeError):
    """El archivo no se pudo abrir o no tiene forma de tabla."""


def texto_de_celda(valor):
    """El valor de una celda como texto limpio, o None si no hay nada.

    Un entero llega de Excel como `float` cuando la celda tuvo formato de numero:
    `3.0` en vez de `3`. Se recorta a entero solo cuando no pierde nada, para que
    una fila `3` no se convierta en el texto `'3.0'` y deje de casar.

    Una fecha se devuelve en ISO-8601, que es el formato en que las guarda toda la
    base (`docs/ARQUITECTURA.md` §1.2).
    """
    if valor is None:
        return None
    if isinstance(valor, datetime):
        return valor.date().isoformat()
    if isinstance(valor, date):
        return valor.isoformat()
    if isinstance(valor, bool):
        return "1" if valor else "0"
    if isinstance(valor, float) and valor.is_integer():
        valor = int(valor)
    texto = str(valor).strip()
    return texto or None


def _elegir_hoja(libro, nombre_de_la_hoja):
    """La hoja pedida; si no esta, la activa, con su aviso.

    Caer a la hoja activa no es tragarse un fallo: un companero puede guardar el
    archivo desde otro programa que renombre la pestana, y perder la ronda entera
    por el nombre de una pestana seria desproporcionado. Lo que no se hace es
    callarlo.
    """
    if nombre_de_la_hoja and nombre_de_la_hoja in libro.sheetnames:
        return libro[nombre_de_la_hoja], ()
    hoja = libro.active
    if nombre_de_la_hoja:
        return hoja, (
            f"AVISO: el archivo no tiene ninguna hoja llamada «{nombre_de_la_hoja}», "
            f"así que se leyó la hoja «{hoja.title}». Compruebe que es la correcta.",
        )
    return hoja, ()


def _titulos_de_una_fila(hoja, numero):
    """Los titulos de esa fila, en texto y sin espacios de sobra."""
    for fila in hoja.iter_rows(min_row=numero, max_row=numero, values_only=True):
        return [texto_de_celda(valor) for valor in fila]
    return []


def buscar_la_fila_de_titulos(hoja):
    """En que fila estan los titulos. Devuelve la 1 si no encuentra otra cosa.

    ⚠️ **No se da por sabido que los titulos esten en la fila 1**, y eso no es una
    concesion: la hoja «Por verificar» lleva CINCO filas de cabecera encima —el
    caso, el templo, la fecha limite, el agente y la instruccion— y los titulos
    empiezan en la 6. Leer la fila 1 devolveria el titulo del documento como si
    fuera una lista de columnas, y todas las filas se irian a descartados.

    Se busca por la columna «clave», que es la unica que no puede faltar en una hoja
    de este programa. Un archivo que Miguel arme por su cuenta no la tiene y cae a la
    fila 1, que es donde el la habra puesto.
    """
    titulo_de_la_clave = COLUMNAS_POR_NOMBRE[COLUMNA_DE_LA_CLAVE].titulo.strip().lower()
    tope = min(hoja.max_row or 1, ULTIMA_FILA_DONDE_SE_BUSCA_LA_CABECERA)
    for numero in range(1, tope + 1):
        titulos = [
            (titulo or "").strip().lower() for titulo in _titulos_de_una_fila(hoja, numero)
        ]
        if titulo_de_la_clave in titulos:
            return numero
    return FILA_DE_TITULOS_POR_DEFECTO


def leer_libro(ruta, nombre_de_la_hoja=NOMBRE_DE_LA_HOJA):
    """Abre el `.xlsx` y devuelve sus titulos y sus filas, sin interpretarlas.

    Cada fila viene como `FilaLeida(numero, valores)`, donde `numero` es el numero
    de fila DE EXCEL. Ese numero se conserva desde aqui hasta la lista de
    descartados: un motivo que dice «la fila 14 no casa» se puede ir a mirar; uno
    que dice «una fila no casa» no sirve de nada.

    La fila de titulos se BUSCA y no se da por sabida: la hoja «Por verificar»
    lleva cinco filas de cabecera encima. Lo que devuelve `fila_de_titulos` es la
    que se uso, para que quien lea pueda comprobarlo.

    Las filas totalmente vacias se saltan y no cuentan como descartadas: son el
    resto de haber borrado el contenido de una fila, que es justo lo que el criterio
    de cierre de la FASE 6 hace a proposito.
    """
    ruta = Path(ruta)
    if not ruta.is_file():
        raise ErrorDeLectura(f"No existe el archivo «{ruta}».")
    try:
        libro = load_workbook(str(ruta), data_only=True, read_only=False)
    except Exception as causa:
        raise ErrorDeLectura(
            f"No se pudo abrir «{ruta.name}» como libro de Excel: {causa}. "
            "Compruebe que es un archivo .xlsx y que no está dañado."
        ) from causa

    try:
        hoja, avisos = _elegir_hoja(libro, nombre_de_la_hoja)
        fila_de_titulos = buscar_la_fila_de_titulos(hoja)
        titulos = _titulos_de_una_fila(hoja, fila_de_titulos)
        if not any(titulos):
            raise ErrorDeLectura(
                f"La hoja «{hoja.title}» de «{ruta.name}» no tiene títulos en la "
                f"fila {fila_de_titulos}, así que no se sabe qué es cada columna."
            )

        primera_fila_de_datos = fila_de_titulos + 1
        filas = []
        avisos = list(avisos)
        for numero, valores in enumerate(
            hoja.iter_rows(min_row=primera_fila_de_datos, values_only=True),
            start=primera_fila_de_datos,
        ):
            if len(filas) >= MAXIMO_DE_FILAS:
                avisos.append(
                    f"AVISO: el archivo trae más de {MAXIMO_DE_FILAS} filas y solo se "
                    "leyeron las primeras. Revise que sea el archivo correcto."
                )
                break
            limpios = [texto_de_celda(valor) for valor in valores]
            if not any(limpios):
                continue
            filas.append(FilaLeida(numero, limpios))
        return LibroLeido(
            hoja.title, titulos, tuple(filas), tuple(avisos), fila_de_titulos
        )
    finally:
        libro.close()


def fila_por_titulo(titulos, fila):
    """Una fila leida como diccionario `titulo -> valor`.

    Un titulo repetido se queda con el valor de la PRIMERA columna que lo lleva, y
    no con el de la ultima. Es arbitrario y hay que elegir algo; se elige la
    primera porque es la que ve quien mira el archivo de izquierda a derecha.
    """
    valores = {}
    for titulo, valor in zip(titulos, fila.valores):
        if titulo and titulo not in valores:
            valores[titulo] = valor
    return valores

"""Que dice cada etiqueta impresa del formulario, en cada idioma en que llega.

**Por que existe este modulo.** El extractor se construyo sobre cuatro documentos
en INGLES y las etiquetas quedaron escritas en ingles, repartidas entre
`formulario.py` y `personas.py`. Los formularios del dueno son la version
ESPANOLA del mismo papel: el pie del formulario en blanco lo dice literalmente,
«Traduccion de General Temple Patron Assistance Fund Request and Approval Form.
Spanish». Misma hoja, mismas casillas, mismos sitios; otro idioma en los rotulos.

Sin las formas espanolas no casaba ni una etiqueta, no se localizaba ni un campo,
y la pagina entera salia vacia. Este archivo es el unico sitio donde se dice como
se llama cada cosa, para que anadir un tercer idioma sea tocar aqui y nada mas.

**Un campo NO es su etiqueta.** La clave con la que el resto del programa se
refiere a un campo es un nombre en espanol —`CAMPO_DE_LA_FECHA_DE_VIAJE`— y no la
cadena impresa, porque ahora hay dos cadenas impresas para lo mismo. Ese nombre
es tambien lo que se le ensena a Miguel cuando una etiqueta no aparece, y por eso
esta en espanol y no en jerga.

**Lo que se midio antes de mezclar los dos idiomas** (2026-09-03, sobre los dos
PDF espanoles del dueno y los cuatro ingleses de `pdfs_referencia/`):

  - Cruce maximo entre CUALQUIER etiqueta inglesa y CUALQUIER espanola: **0.357**,
    muy por debajo del 0.85 que exige el ancla. Por eso las dos formas pueden
    convivir en la misma familia sin que una pagina inglesa case con un rotulo
    espanol ni al reves. `pruebas/prueba_etiquetas_bilingues.py` lo vigila: si
    ese numero sube, la prueba se pone roja y hay que separar los juegos.

  - Cruces PELIGROSOS dentro del espanol: «Fecha de viaje al templo» contra
    «Fecha de regreso del templo» da **0.704**, contra «Fecha de la cita del
    templo» **0.667**, y «...del barrio / rama» contra «...de la estaca /
    distrito» **0.694**. Ninguno llega a 0.85, pero estan lo bastante cerca como
    para que la regla de la etiqueta rival de `bandas.py` siga siendo la defensa
    que de verdad importa. Confundir la fecha de ida con la de vuelta es mandar a
    una persona al templo el dia equivocado.

**De donde salen las cadenas espanolas.** De dos sitios que coinciden, y por eso
se dan por buenas: del OCR sobre los formularios reales del dueno, y del perfil
`perfiles/barc2608.json` de un proyecto anterior del dueno que ya las tenia
verificadas contra el formulario oficial en blanco.
"""

import re
import unicodedata

# Los nombres con los que el programa se refiere a cada campo. En espanol (regla
# permanente 4) y en minusculas, porque se leen tal cual en el aviso que dice
# «no se encontraron en el papel estas etiquetas: ...».
CAMPO_DE_LA_FECHA_DE_VIAJE = "fecha de viaje al templo"
CAMPO_DE_LA_FECHA_DE_REGRESO = "fecha de regreso del templo"
CAMPO_DE_LA_UNIDAD = "nombre y número de unidad del barrio o rama"
CAMPO_DE_LA_ESTACA = "nombre y número de unidad de la estaca o distrito"
CAMPO_DE_LOS_NOMBRES = "nombre(s) de pila"
CAMPO_DEL_MRN = "número de cédula de miembro"
CAMPO_DEL_NOMBRE_DEL_TEMPLO = "nombre del templo"
CAMPO_DE_LA_CITA_DEL_TEMPLO = "fecha de la cita del templo"
CAMPO_DE_LOS_COSTOS = "costos asociados"

# Las formas en que cada etiqueta llega impresa: `(en ingles, en espanol)`.
#
# El orden importa poco para buscar —se prueban todas y gana la que mas se
# parezca— pero se mantiene fijo para que una prueba pueda decir «la forma
# espanola de este campo» sin ambiguedad.
FORMAS_DE_CADA_CAMPO = {
    CAMPO_DE_LA_FECHA_DE_VIAJE: (
        "Date traveling to the temple",
        "Fecha de viaje al templo",
    ),
    CAMPO_DE_LA_FECHA_DE_REGRESO: (
        "Date traveling home from the temple",
        "Fecha de regreso del templo",
    ),
    CAMPO_DE_LA_UNIDAD: (
        "Ward/Branch Name and Unit Number",
        "Nombre y número de unidad del barrio / rama",
    ),
    CAMPO_DE_LA_ESTACA: (
        "Stake/District Name and Unit Number",
        "Nombre y número de unidad de la estaca / distrito",
    ),
    CAMPO_DE_LOS_NOMBRES: ("Full Name(s)", "Nombre(s) de pila"),
    CAMPO_DEL_MRN: ("Membership Record Number", "Número de cédula de miembro"),
    CAMPO_DEL_NOMBRE_DEL_TEMPLO: ("Temple Name", "Nombre del templo"),
    CAMPO_DE_LA_CITA_DEL_TEMPLO: ("Temple Appointment Date", "Fecha de la cita del templo"),
    CAMPO_DE_LOS_COSTOS: ("Associated Costs", "Costos asociados"),
}

CAMPOS_DEL_FORMULARIO = tuple(FORMAS_DE_CADA_CAMPO)

# Las etiquetas que cierran el bloque de personas por abajo. Son varias porque
# ninguna aparece en las nueve paginas de referencia: «Temple Name» falto en dos
# escaneos malos y «Temple Appointment Date» en uno. Se toma la primera que
# aparezca por debajo de las cabeceras; si no aparece ninguna, no se extrae
# ninguna persona de esa pagina.
CAMPOS_QUE_CIERRAN_LAS_PERSONAS = (
    CAMPO_DEL_NOMBRE_DEL_TEMPLO,
    CAMPO_DE_LA_CITA_DEL_TEMPLO,
    CAMPO_DE_LOS_COSTOS,
)

_ESPACIOS_SEGUIDOS = re.compile(r"\s+")


def formas_de(campo):
    """Las cadenas impresas que valen para ese campo, en los dos idiomas."""
    return FORMAS_DE_CADA_CAMPO[campo]


# La posicion de la forma espanola dentro de la tupla `(ingles, espanol)`. Se
# nombra en vez de escribir un `[1]` suelto: el dia que entre un tercer idioma, el
# indice deja de significar «el espanol» y esta constante es lo que hay que mirar.
_POSICION_DE_LA_FORMA_ESPANOLA = 1


def forma_espanola_de(campo):
    """Como se llama esa etiqueta en un formulario espanol.

    Existe para la INTERFAZ, y no para la extraccion: la extraccion prueba todas
    las formas y gana la que mas se parezca, pero la pantalla tiene que escribir
    UNA, y por la regla permanente 4 es la espanola **sea cual sea el idioma del
    papel**. La alternativa —escribir en pantalla la etiqueta que se encontro en la
    hoja— dejaria la ventana en ingles delante de un formulario ingles, que es el
    defecto que QA midio el 2026-09-03: `Case Number`, `Date traveling to the
    temple`, `Full Name(s)`, `Membership Record Number` y `Ward/Branch Name and
    Unit Number`, y **0 de 5 aparecian en el papel del dueno**, que es espanol.
    """
    return FORMAS_DE_CADA_CAMPO[campo][_POSICION_DE_LA_FORMA_ESPANOLA]


def normalizar_para_comparar(texto):
    """Baja a minusculas, quita las tildes y junta los espacios de sobra.

    Es SOLO para comparar. El valor que se guarda conserva sus tildes: aqui no se
    corrige nada de lo leido, se decide a que etiqueta se parece.

    Quitar las tildes no es cosmetico y esta medido (2026-09-03): el OCR devuelve
    «Numero de cedula de miembro» sin acentos con frecuencia, y sin normalizar esa
    lectura se parece un 0.926 a la etiqueta buena. Sigue pasando el 0.85, pero
    gasta casi toda la holgura en las tildes en vez de gastarla en el error de
    lectura de verdad. Normalizando da 1.000 y el margen queda para lo que hace
    falta. Las tres etiquetas espanolas con tilde suben de 0.926, 0.977 y 0.941 a
    1.000.
    """
    descompuesto = unicodedata.normalize("NFKD", texto or "")
    sin_tildes = "".join(letra for letra in descompuesto if not unicodedata.combining(letra))
    return _ESPACIOS_SEGUIDOS.sub(" ", sin_tildes).strip().lower()

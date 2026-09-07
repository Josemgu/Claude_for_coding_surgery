"""El importador de Excel libre: Miguel dice que columna es cada cosa.

Por que existe: **nadie nombra las columnas igual dos veces.** Un compañero que
arma su propia hoja escribe `MRN`, otro `N. de registro`, otro `mrn_persona`. Sin
esta pantalla, cada archivo que no sea exactamente el que genero el programa se
descarta entero, y quien lo mira concluye que el programa esta roto.

**Lo que este modulo propone y lo que NO decide.** Propone una correspondencia
mirando los titulos, y **la propuesta no se aplica sola**: se le ensena a Miguel y
el confirma o la cambia (regla permanente 5, la misma de siempre). Un titulo que no
reconoce se queda sin asignar, y no se le adivina un parecido remoto: emparejar
`Fecha` con `fecha_viaje` porque las dos llevan la palabra fecha es como emparejar
por nombre, y de eso va todo este proyecto.

**Y lo que el importador libre NO hace: insertar.** Traduce las columnas y pasa el
resultado por el MISMO motor de `paquete/reconciliacion.py`, con sus mismas dos
reglas —se casa por `numero_caso` + `mrn`, y lo que no casa no se inserta—.

⚠️ **Decision mia que el dueno tiene que revisar.** El pase pedia «importador de
Excel libre con pantalla de mapeo» y no decia si sirve para DAR DE ALTA casos
nuevos o solo para actualizar los que ya estan. Se eligio **solo actualizar**,
porque es el lado seguro: una lista de Excel mal mapeada que inserta crea casos
fantasma con MRN inventados, y eso es el dano que este programa existe para
evitar. Si el dueno quiere el alta masiva, es otra fase y necesita su
especificacion.
"""

import unicodedata
from collections import namedtuple

from paquete.columnas import COLUMNAS, COLUMNAS_POR_NOMBRE
from paquete.lectura import FilaLeida, fila_por_titulo, leer_libro

Correspondencia = namedtuple("Correspondencia", ("campo", "titulo", "titulo_en_la_hoja"))

# Los campos que el importador libre puede recibir. Son los del Excel de trabajo y
# ni uno mas: lo que no se puede reconciliar tampoco se puede importar.
CAMPOS_QUE_SE_PUEDEN_MAPEAR = tuple(columna.nombre for columna in COLUMNAS)

# Los dos sin los cuales no se puede hacer nada.
CAMPOS_OBLIGATORIOS = ("numero_caso", "mrn")

# Las formas en que se ha visto escrito cada campo. Se comparan ya normalizadas
# —sin tildes, sin mayusculas y sin signos—, asi que `N.º de caso`, `n de caso` y
# `NDECASO` son la misma entrada.
#
# ⚠️ Esta lista es una AYUDA para no teclear, no una autoridad. Lo que se aplica es
# lo que Miguel confirma en la pantalla.
SINONIMOS = {
    "numero_caso": ("numerodecaso", "numerocaso", "ndecaso", "nrodecaso", "caso",
                    "casenumber", "case", "numerodelcaso"),
    "mrn": ("mrn", "numeroderegistro", "nderegistro", "nroderegistro",
            "recordnumber", "membershiprecordnumber", "nregistro",
            "numerodemiembro", "mrnpersona"),
    "nombre": ("nombre", "nombrecompleto", "nombreyapellidos", "name", "fullname",
               "persona"),
    "fecha_viaje": ("fechadeviaje", "fechaviaje", "fecha", "viaje",
                    "datetravelingtothetemple", "fechadelviaje"),
    "unidad_nombre": ("unidad", "nombredelaunidad", "barrio", "rama", "ward",
                      "branch", "wardbranchname"),
    "estaca": ("estaca", "distrito", "estacaodistrito", "stake", "stakedistrictname"),
    "fecha_solicitud": ("fechadesolicitud", "fechadelasolicitud", "solicitud"),
    "a_que_va": ("aqueva", "ordenanza", "ordenanzas", "proposito"),
    "clave": ("clave", "llave", "key"),
    "paso_preparacion": ("preparacion", "1preparacion"),
    "paso_informacion": ("informacion", "2informacion"),
    "paso_cita_del_templo": ("citadeltemplo", "cita", "3citadeltemplo"),
    "paso_acciones_requeridas": ("accionesrequeridas", "acciones", "4accionesrequeridas"),
    "paso_entrevistas": ("entrevistas", "entrevista", "5entrevistas"),
    "paso_listo_para_el_templo": ("listoparaeltemplo", "listo", "6listoparaeltemplo"),
    "llamo_al_lider": ("llamoallider", "llamoalliderr", "llamada", "contactoconellider"),
}


def normalizar_titulo(titulo):
    """Un titulo reducido a letras y digitos, sin tildes ni mayusculas.

    Se quitan las tildes descomponiendo el texto en Unicode y tirando las marcas
    diacriticas. Es lo mismo que hace cualquier comparacion «laxa» de texto, y aqui
    solo se usa para PROPONER: la reconciliacion sigue comparando los valores
    literales, sin normalizar nada, porque un MRN normalizado ya no es ese MRN.
    """
    if not titulo:
        return ""
    descompuesto = unicodedata.normalize("NFKD", str(titulo))
    sin_tildes = "".join(letra for letra in descompuesto if not unicodedata.combining(letra))
    return "".join(letra for letra in sin_tildes.lower() if letra.isalnum())


def proponer_correspondencia(titulos_de_la_hoja):
    """Que columna de la hoja parece ser cada campo. Devuelve `campo -> titulo`.

    Un campo al que no se le encuentra columna NO aparece en el diccionario, y esa
    ausencia es informacion: la pantalla la ensena como «sin asignar» y Miguel
    elige. Rellenarla con la columna que mas se parezca seria adivinar.

    Una misma columna no se propone para dos campos: la primera que la reclama se
    la queda, y el segundo campo se queda sin proponer. Dos campos leyendo la misma
    columna es siempre un error, y es mejor verlo vacio que verlo mal.
    """
    por_normalizado = {}
    for titulo in titulos_de_la_hoja:
        if titulo:
            por_normalizado.setdefault(normalizar_titulo(titulo), titulo)

    propuesta = {}
    usados = set()
    for campo in CAMPOS_QUE_SE_PUEDEN_MAPEAR:
        for forma in (campo,) + SINONIMOS.get(campo, ()):
            titulo = por_normalizado.get(normalizar_titulo(forma))
            if titulo is not None and titulo not in usados:
                propuesta[campo] = titulo
                usados.add(titulo)
                break
    return propuesta


def campos_sin_asignar(correspondencia):
    """Los campos obligatorios que la correspondencia todavia no cubre."""
    return tuple(campo for campo in CAMPOS_OBLIGATORIOS if not correspondencia.get(campo))


def traducir(libro, correspondencia):
    """Reescribe las filas del libro con los titulos del Excel de trabajo.

    Devuelve `(titulos, filas)` listos para `reconciliar_filas`. Es lo que permite
    que el importador libre y la vuelta del paquete pasen por el MISMO motor: si
    cada uno tuviera el suyo, la regla de no insertar habria que escribirla dos
    veces, y algun dia una de las dos se quedaria atras.

    Los numeros de fila de Excel se conservan intactos. Un descarte que dice «la
    fila 27» tiene que poder abrirse en el archivo que Miguel eligio, no en una
    tabla intermedia que solo existe en memoria.
    """
    titulos_destino = [
        COLUMNAS_POR_NOMBRE[campo].titulo
        for campo in CAMPOS_QUE_SE_PUEDEN_MAPEAR
        if campo in correspondencia
    ]
    campos = [campo for campo in CAMPOS_QUE_SE_PUEDEN_MAPEAR if campo in correspondencia]

    filas = []
    for fila in libro.filas:
        valores_por_titulo = fila_por_titulo(libro.titulos, fila)
        filas.append(
            FilaLeida(
                fila.numero,
                [valores_por_titulo.get(correspondencia[campo]) for campo in campos],
            )
        )
    return titulos_destino, tuple(filas)


def abrir_para_mapear(ruta):
    """Abre un `.xlsx` cualquiera y devuelve el libro con su correspondencia propuesta.

    Se abre SIN pedir una hoja concreta: un archivo que Miguel arma por su cuenta no
    tiene por que tener una pestana llamada «trabajo». Se lee la hoja activa, y el
    nombre de la hoja leida se ensena en la pantalla para que se pueda comprobar.
    """
    libro = leer_libro(ruta, nombre_de_la_hoja=None)
    return libro, proponer_correspondencia(libro.titulos)

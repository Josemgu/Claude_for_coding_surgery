"""El Excel que vuelve: que fila actualiza a quien, y que fila no entra.

**Las dos reglas que gobiernan este archivo, y ninguna es mia:**

  1. **Se casa por `numero_caso` + `mrn`, NUNCA por nombre** (`DECISIONES.md`,
     2026-09-02). Un acento de mas crea un registro fantasma. En este modulo el
     nombre que devuelve el companero **no se lee para casar y no se escribe en la
     base**: se ignora entero.
  2. **La fila cuyo par no existe NO se inserta.** Va a la lista de descartados con
     su motivo escrito, para que Miguel decida. No se inserta «a ver si suena la
     flauta» (`PENDIENTES.md`, FASE 6).

**Aqui no se inserta ni se borra NI UNA persona, y no hay ningun `DELETE`.** Sobre
`personas` la unica escritura que sale de este modulo es la de
`datos.propuestas.guardar_propuesta`, que es un `UPDATE` sobre una persona que ya
existe. Por eso el conteo de filas de `personas` es el mismo antes y despues.

⚠️ **Lo que si se inserta desde el 2026-09-03 es el renglon de cada descarte**
(`datos/descartadas.py`), y es una tabla aparte que no toca a nadie: es constancia
de lo que NO entro. La consecuencia, dicha: reconciliar dos veces el mismo archivo
sigue sin duplicar ninguna persona —la segunda pasada escribe encima lo mismo—
pero **si deja los renglones de descarte otra vez**, con su fecha y su hora. Es lo
correcto: son dos cargas distintas del mismo archivo y las dos pasaron.

**Lo que entra es una PROPUESTA, no una verificacion** (regla permanente 5). El
estado del caso lo sigue escribiendo Miguel a mano desde la pantalla de correccion,
mirando lo que el companero propuso. Ver `datos/propuestas.py`, que explica ademas
por que no se puede usar `verificado_por` para esto.

**Y una tercera regla, desde la auditoria final de QA: la fila que pisaria la
propuesta de OTRO companero tampoco entra.** `guardar_propuesta` la rechaza y esa
fila cae en `descartadas` con su motivo, por el mismo camino que las que no casan.
Este archivo no necesito ni una linea para eso —ya atrapaba `ErrorDeValidacion`
por fila— y es a proposito: la regla vive en la capa de datos, donde vale para
todos los que escriban, y no repartida por cada uno de los que llaman.
"""

from collections import namedtuple

from datos.companeros import leer_companero
from datos.descartadas import anotar_las_descartadas_de_la_vuelta
from datos.esquema import marca_de_tiempo
from datos.marcas_de_revision import aplicar_las_marcas_de_la_hoja
from datos.pasos import (
    COLUMNA_DE_LA_LLAMADA,
    PASOS,
    ROTULO_DE_LA_LLAMADA,
    RespuestaIlegible,
    estado_de_los_pasos,
    leer_respuesta,
)
from datos.propuestas import guardar_propuesta, personas_que_casan_con_la_clave
from datos.validacion import ErrorDeValidacion
from paquete.columnas import COLUMNA_DE_LA_CLAVE, COLUMNAS, partir_la_clave
from paquete.lectura import fila_por_titulo, leer_libro

# El unico estado que se puede deducir de los seis pasos, y el motivo de que sea
# uno solo.
#
# ⚠️ **De «los seis dicen que sí» NO se deduce ningun valor**, y no es un olvido.
# `datos/estados.py` solo conoce `no_indicada` e `incompleta`; el valor que
# significaria «resuelta» es uno de los que el dueno todavia no ha dicho
# (`DECISIONES.md`, P-1) y aqui no se inventa. Una persona con los seis pasos en
# «Sí» se guarda con `estado_propuesto` a nulo **y con sus seis columnas puestas**,
# que es donde consta que alguien la miro y que salio bien. Cuando el dueno diga esa
# palabra, es una linea de este archivo.
ESTADO_DE_QUIEN_NO_ESTA_LISTA = "incompleta"

FilaAplicada = namedtuple(
    "FilaAplicada", ("numero", "numero_caso", "mrn", "persona_id", "estado", "nota")
)
FilaDescartada = namedtuple(
    "FilaDescartada", ("numero", "numero_caso", "mrn", "nombre", "motivo")
)
ResultadoDeLaVuelta = namedtuple(
    "ResultadoDeLaVuelta",
    ("aplicadas", "descartadas", "sin_nada_que_proponer", "avisos", "companero"),
)

# Los motivos por los que una fila no entra, escritos aqui y no repartidos por el
# codigo: son lo que Miguel lee para decidir, y una lista de motivos redactados
# cada uno en un sitio distinto acaba diciendo lo mismo de cuatro maneras.
MOTIVO_SIN_CLAVE = (
    "la fila no trae el número de caso o no trae el MRN, y sin los dos no se puede "
    "saber a qué persona se refiere. NUNCA se empareja por nombre"
)
MOTIVO_CLAVE_BORRADA = (
    "este renglón no trae nada en la columna «clave», así que no se sabe de quién "
    "es. O se la borraron, o es una fila añadida a mano. La clave va a la vista "
    "precisamente para que se note cuando falta, y sin ella la fila NO se busca "
    "por nombre: nunca se empareja por nombre"
)
MOTIVO_CLAVE_ROTA = (
    "la clave «{clave}» no tiene la forma esperada (caso, dos puntos y cédula de "
    "miembro), así que no se puede saber a qué persona se refiere"
)
MOTIVO_RESPUESTA_ILEGIBLE = (
    "en la columna «{rotulo}», {detalle}. Esa persona se quedó sin aplicar entera: "
    "no se aplica media fila"
)
MOTIVO_SIN_PAR = (
    "el par número de caso + MRN no existe en la base. La fila NO se inserta: "
    "puede ser una persona añadida a mano en el Excel, o un MRN retecleado"
)
MOTIVO_REPETIDA = "ese mismo par ya venía en la fila {primera} de este archivo"
# El motivo que nacio con la version 12 del esquema. Sale cuando la clave no lleva
# el id del caso —un paquete generado antes del 2026-09-03, o un Excel armado a
# mano— y el número de caso lleva a DOS personas de dos casos distintos. No se
# escoge una: escribir el trabajo del compañero sobre la familia equivocada es
# peor que no escribirlo, y esto al menos deja su renglón.
MOTIVO_CLAVE_AMBIGUA = (
    "ese número de caso y ese MRN llevan a {cuantas} personas de {cuantas} casos "
    "distintos, así que no se sabe a cuál se refiere. El número de caso son cuatro "
    "letras y el año y el mes: identifica una unidad y un mes, no una familia, y "
    "dos documentos lo comparten. NO se escoge uno al azar. Vuelva a generar el "
    "paquete: las claves nuevas llevan dentro el número de caso, el MRN y el id del "
    "caso, y esa sí distingue"
)


def _titulos_esperados():
    """Como se llaman en la hoja las columnas que este modulo necesita leer."""
    return {columna.nombre: columna.titulo for columna in COLUMNAS}


def _valor(valores, titulos_esperados, nombre):
    """El valor de una columna del Excel de trabajo, buscada por su titulo."""
    return valores.get(titulos_esperados[nombre])


def _normalizar_numero_de_caso(valor):
    """El numero de caso tal como se guarda: sin espacios y en mayusculas.

    Las mayusculas se fuerzan porque `casos.numero_caso` las exige por `CHECK` y
    porque Excel no las conserva por su cuenta: un `casp2609` tecleado en minuscula
    es el MISMO caso, y descartarlo por eso seria perder trabajo bueno. **No es
    inventar un dato**: cambiar la caja de una letra no cambia que caso es. Eso lo
    distingue del MRN, donde rellenar un cero SI inventaria.
    """
    return valor.strip().upper() if isinstance(valor, str) else valor


def _par_de_la_fila(valores, titulos_esperados):
    """Lo que identifica la fila, y de donde salio. Devuelve `(clave, motivo)`.

    La clave es la terna `(numero_caso, mrn, caso_id)`. El `caso_id` es None cuando
    la hoja no lo trae —un paquete anterior al 2026-09-03, o un Excel armado a mano
    y mapeado a mano—, y entonces la fila se resuelve por el camino viejo, que puede
    quedar ambiguo cuando dos casos comparten numero.

    **La clave manda sobre las dos columnas sueltas**, y ese orden importa: la clave
    es UNA celda, se ve de un vistazo si se borro, y lleva el par entero. Un dígito
    cambiado a mano en la columna del MRN no se nota, y esa fila se iría a
    descartados sin que nadie entendiera por que.

    Cuando la hoja **no trae** columna «clave» —un Excel que Miguel armo por su
    cuenta y paso por la pantalla de mapeo— se cae a las dos columnas sueltas. Es el
    mismo par, leido de otro sitio: la regla de casar por `numero_caso` + `mrn` no
    cambia.
    """
    titulo_de_la_clave = titulos_esperados[COLUMNA_DE_LA_CLAVE]
    if titulo_de_la_clave in valores:
        clave = valores.get(titulo_de_la_clave)
        if not clave:
            return None, MOTIVO_CLAVE_BORRADA
        par = partir_la_clave(clave)
        if par is None:
            return None, MOTIVO_CLAVE_ROTA.format(clave=clave)
        numero_caso, mrn, caso_id = par
        return (_normalizar_numero_de_caso(numero_caso), mrn, caso_id), None

    numero_caso = _normalizar_numero_de_caso(_valor(valores, titulos_esperados, "numero_caso"))
    mrn = _valor(valores, titulos_esperados, "mrn")
    if not numero_caso or not mrn:
        return None, MOTIVO_SIN_CLAVE
    return (numero_caso, mrn, None), None


def _respuestas_de_la_fila(valores, titulos_esperados):
    """Las siete respuestas de la hoja. Devuelve `(respuestas, motivo del reparo)`.

    Una respuesta que no se entiende **para la fila entera** y no solo su columna.
    Es lo que hace el proyecto viejo y es lo correcto: aplicar cinco pasos de seis
    dejaria a esa persona con un estado a medias que nadie escribio, y el sexto paso
    —el que no se entendio— es justo el que podria estar diciendo que falta algo.
    """
    respuestas = {}
    for nombre, rotulo in tuple(PASOS) + ((COLUMNA_DE_LA_LLAMADA, ROTULO_DE_LA_LLAMADA),):
        titulo = titulos_esperados[nombre]
        crudo = valores.get(titulo)
        try:
            respuestas[nombre] = leer_respuesta(crudo)
        except RespuestaIlegible as causa:
            return None, MOTIVO_RESPUESTA_ILEGIBLE.format(rotulo=rotulo, detalle=causa)
    return respuestas, None


def _clasificar_fila(conexion, numero, valores, titulos_esperados, ya_vistos):
    """Decide que hacer con una fila. Devuelve (persona, descarte, hay_algo).

    Una de las dos primeras siempre es None: o la fila resuelve a una persona, o
    resuelve a un descarte con su motivo.
    """
    nombre = _valor(valores, titulos_esperados, "nombre")
    respuestas, reparo = _respuestas_de_la_fila(valores, titulos_esperados)
    hay_algo = respuestas is not None and any(
        valor is not None for valor in respuestas.values()
    )

    par, motivo = _par_de_la_fila(valores, titulos_esperados)
    if par is None:
        numero_caso = _normalizar_numero_de_caso(
            _valor(valores, titulos_esperados, "numero_caso")
        )
        mrn = _valor(valores, titulos_esperados, "mrn")
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, motivo), hay_algo

    numero_caso, mrn, caso_id = par
    if reparo is not None:
        # El reparo se devuelve como descarte y con `hay_algo` en True: la fila
        # traia respuestas, no se aplico ninguna, y eso tiene que contarse como
        # trabajo perdido y no como una fila que nadie toco.
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, reparo), True

    if not mrn or (caso_id is None and not numero_caso):
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, MOTIVO_SIN_CLAVE), hay_algo

    if par in ya_vistos:
        motivo = MOTIVO_REPETIDA.format(primera=ya_vistos[par])
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, motivo), hay_algo

    casan = personas_que_casan_con_la_clave(conexion, numero_caso, mrn, caso_id)
    if len(casan) > 1:
        # ⚠️ Dos personas de dos casos distintos, y la clave no distingue. NO se
        # escoge una: el trabajo del companero se escribiria sobre la familia
        # equivocada y nadie se enteraria. Se descarta con el motivo escrito.
        motivo = MOTIVO_CLAVE_AMBIGUA.format(cuantas=len(casan))
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, motivo), hay_algo
    if not casan:
        return None, FilaDescartada(numero, numero_caso, mrn, nombre, MOTIVO_SIN_PAR), hay_algo
    persona = casan[0]

    ya_vistos[par] = numero
    persona["_numero_caso"] = numero_caso
    persona["_pasos"] = respuestas
    persona["_estado"] = (
        ESTADO_DE_QUIEN_NO_ESTA_LISTA if estado_de_los_pasos(respuestas) is False else None
    )
    persona["_nota"] = None
    return persona, None, hay_algo


def reconciliar_filas(conexion, titulos, filas, companero_id, ruta_excel=None):
    """Aplica las filas ya leidas. Es la mitad que no toca el disco.

    Vive aparte de `reconciliar_excel` para que el importador de Excel libre pueda
    reutilizarla despues de traducir sus columnas, y para poder probarla sin
    escribir un archivo.

    `ruta_excel` viaja solo para dejarla escrita en el renglon de cada descarte: es
    lo que permite abrir DESPUES el archivo del que salio esa fila. Admite None
    porque el importador de Excel libre puede reconciliar filas que ya tenia en
    memoria, y una ruta inventada seria peor que ninguna.
    """
    companero = leer_companero(conexion, companero_id)
    if companero is None:
        raise ErrorDeValidacion(
            f"No se puede cargar un Excel sin decir de qué compañero viene: no hay "
            f"ningún compañero con el id {companero_id!r}."
        )

    titulos_esperados = _titulos_esperados()
    # Con la columna «clave» basta: lleva el par entero dentro. Sin ella hacen falta
    # las dos sueltas, que es como llega un Excel armado a mano y mapeado a mano.
    if titulos_esperados[COLUMNA_DE_LA_CLAVE] not in titulos:
        faltan = [
            titulo
            for nombre, titulo in titulos_esperados.items()
            if nombre in ("numero_caso", "mrn") and titulo not in titulos
        ]
        if faltan:
            raise ErrorDeValidacion(
                "Este archivo no se puede reconciliar: no trae la columna «clave» y "
                f"además le faltan las columnas {faltan}, que son las dos que "
                "identifican a cada persona. ¿Es la hoja «Por verificar» que generó "
                "el programa?"
            )

    aplicadas, descartadas, sin_nada, avisos = [], [], [], []
    ya_vistos = {}
    for fila in filas:
        valores = fila_por_titulo(titulos, fila)
        persona, descarte, hay_algo = _clasificar_fila(
            conexion, fila.numero, valores, titulos_esperados, ya_vistos
        )
        if descarte is not None:
            descartadas.append(descarte)
            continue
        if not hay_algo:
            sin_nada.append(fila.numero)
            continue
        try:
            guardar_propuesta(
                conexion,
                persona["id"],
                persona["_estado"],
                persona["_nota"],
                companero_id,
                persona["_pasos"],
            )
        except ErrorDeValidacion as causa:
            descartadas.append(
                FilaDescartada(
                    fila.numero,
                    persona["_numero_caso"],
                    persona["mrn"],
                    persona["nombre"],
                    f"la persona existe, pero lo que trae la fila no se puede "
                    f"guardar: {causa}",
                )
            )
            continue
        aplicadas.append(
            FilaAplicada(
                fila.numero,
                persona["_numero_caso"],
                persona["mrn"],
                persona["id"],
                persona["_estado"],
                persona["_nota"],
            )
        )

    # ⚠️ Los descartes se ANOTAN, y esa es la mitad del hallazgo ALTO de la
    # auditoría final de QA. Que la fila no entre está bien decidido —casar por
    # nombre crea registros fantasma— pero hasta el 2026-09-03 la lista se perdía
    # al cerrar la ventana, y con ella la única pista de que un compañero había
    # hecho un trabajo que nadie recogió. En la ida y vuelta que QA midió, la
    # persona sin MRN volvió con 0 de 7 pasos y nadie se habría enterado.
    #
    # Se anota AQUÍ y no en la pantalla, por lo mismo que
    # `importacion/guardado.py` anota los ilegibles en el camino común: un renglón
    # que hay que acordarse de escribir es un renglón que algún día no se escribe.
    anotar_las_descartadas_de_la_vuelta(conexion, descartadas, companero_id, ruta_excel)

    if aplicadas and descartadas:
        avisos.append(
            f"AVISO: se aplicaron {len(aplicadas)} filas y se descartaron "
            f"{len(descartadas)}. Las descartadas NO están en la base: revíselas "
            "una a una antes de dar la ronda por cerrada. Quedan guardadas en la "
            "lista de filas descartadas, así que no se pierden al cerrar la ventana."
        )
    return ResultadoDeLaVuelta(
        tuple(aplicadas), tuple(descartadas), tuple(sin_nada), tuple(avisos), companero
    )


def reconciliar_excel(conexion, ruta, companero_id):
    """Lee el Excel que devolvio un companero y aplica lo que se pueda aplicar.

    ⚠️ **Y desde el 2026-09-03 marca ademas el estado de cada documento que volvio**
    (`datos/marcas_de_revision.py`). Lo decidio el dueno: «el documento que ellos
    llenan de Excel es el que marca». Aqui no se escribe ese estado a mano: se llama
    a la funcion que lo hace, que vive en la capa de datos y vale para todos los que
    carguen.

    **`propuesto_desde` se toma ANTES de abrir el archivo, y ese orden es todo.** Es
    la frontera entre «lo que acaba de volver» y «lo que ya estaba». Sin ella, una
    segunda carga volveria a marcar todo lo de la primera —lo midio quien escribio
    esa funcion— y eso pisaria las correcciones que Miguel hubiera hecho entre las
    dos con los botones de la tarjeta. Tomada despues de leer, se quedaria fuera lo
    que la propia lectura acaba de guardar.
    """
    desde = marca_de_tiempo()
    libro = leer_libro(ruta)
    resultado = reconciliar_filas(
        conexion, libro.titulos, libro.filas, companero_id, ruta_excel=ruta
    )
    # `str(ruta)` y no `ruta`: la ruta acaba en una columna de texto, y `sqlite3`
    # no sabe guardar un `WindowsPath` —«type 'WindowsPath' is not supported»,
    # medido—. La conversion se hace aqui, que es donde se sabe que lo que hay es
    # una ruta, y no dentro de la funcion que solo sabe que le dan un texto.
    resumen = aplicar_las_marcas_de_la_hoja(conexion, str(ruta), propuesto_desde=desde)
    return resultado._replace(
        avisos=libro.avisos + resultado.avisos + (resumen.texto(),)
    )


def resumen_de_la_vuelta(resultado):
    """El texto en espanol que se le ensena a Miguel al terminar la carga.

    Dice SIEMPRE las tres cifras, incluso las que valen cero. Un resumen que se
    calla los descartados porque no hubo ninguno ensena a no buscarlos el dia que
    los haya.
    """
    lineas = [
        f"Excel de «{resultado.companero['nombre']}» cargado.",
        f"  · {len(resultado.aplicadas)} filas aplicadas sobre personas que ya estaban.",
        f"  · {len(resultado.descartadas)} filas descartadas: NO se insertó ninguna.",
        f"  · {len(resultado.sin_nada_que_proponer)} filas venían con los seis pasos "
        "en blanco: nadie las miró, que no es lo mismo que un «no».",
        "",
        "Nada de esto queda verificado: lo que trae el compañero es una propuesta, "
        "y el caso lo sigue confirmando usted.",
    ]
    return "\n".join(lineas)

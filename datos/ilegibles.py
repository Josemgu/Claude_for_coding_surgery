"""El registro de lo que no se pudo leer: un renglon por documento o por pagina.

Lo pidio el dueno el 2026-09-02: «debe poder leer letra dentro de PDF escaneados,
y si no puede debe ponerlo en un renglón que notifique que tales documentos no son
legibles». La palabra que manda ahi es **renglon**: no un aviso que se cierra, sino
una fila que se queda y se puede volver a mirar manana.

**Los nueve motivos, y por que son nueve y no uno.** «No se pudo leer» no sirve
de nada: no dice si el problema es el archivo, el escaneo o el lector, y sin esa
distincion nadie puede arreglar nada. Estos nueve se pueden distinguir con lo que
el programa ya sabe en el momento de fallar, y cada uno lleva a un sitio distinto:

  - `no_se_pudo_abrir`  — el PDF no se pudo ni abrir. El problema es el ARCHIVO:
                          esta corrupto, cifrado, o no es un PDF. Se anota una vez
                          por documento, sin pagina.
  - `sin_texto`         — la pagina se rasterizo y el OCR no devolvio NI UNA linea.
                          El problema es el ESCANEO: pagina en blanco, al reves,
                          o tan mala que el detector no encuentra ni un renglon.
  - `sin_campos`        — el OCR si leyo lineas, pero de la pagina no salio NADA:
                          ni numero de caso, ni fecha, ni unidad, ni una sola
                          persona. El lector ve letra y no reconoce el formulario.
                          El problema esta entre el papel y las reglas.
  - `sin_numero_de_caso`— salio algo —personas, fecha, unidad— pero no el numero
                          de caso. La pagina SI se guarda como caso y el renglon
                          lleva su `caso_id`: se abre, se teclea el numero y listo.
  - `caso_ya_existente` — la pagina se leyo entera y NO entro, porque su numero de
                          caso ya estaba en la base. Es el unico de los cinco que
                          no habla de legibilidad, y esta en esta tabla porque la
                          pregunta que contesta es la misma: «¿que no entro y por
                          que?». Un rechazo que solo se decia en un cuadro de
                          dialogo que se cierra es un documento perdido en
                          silencio, y con cientos de PDF nadie lo va a recordar.
                          ⚠️ **Ninguna importacion escribe ya este codigo**: desde
                          la version 12 del esquema ninguna pagina se rechaza por
                          repetir numero. Se conserva porque en la base del dueno
                          ya hay renglones con el —seis en una sola tanda suya—.
  - `anclas_perdidas`   — la pagina entro y SUS DATOS SE GUARDARON, pero el lector
                          no encontro alguna de las etiquetas impresas del
                          formulario. Sin el ancla no sabe donde mirar, y lo leido
                          en esa fila puede venir de otro sitio de la hoja. Entro
                          el 2026-09-03: medido sobre las 14 paginas reales de
                          `pdfs_referencia/`, 10 pierden CERO anclas y las 4 que
                          pierden alguna son exactamente las 4 de las que no salio
                          ni fecha ni unidad. Separacion limpia, sin un falso
                          positivo.
  - `campo_discrepante` — esta hoja repetia el numero de caso de otra del mismo
                          documento pero leia OTRO valor en un campo del caso, asi
                          que **no se unio y no entro**. El numero de caso son
                          cuatro letras mas el ano y el mes: identifica una unidad y
                          un mes, no una familia, y dos familias distintas de la
                          misma unidad lo comparten. Fundirlas dejaba a una persona
                          con la fecha de viaje de otra familia —medido por QA el
                          2026-09-03— y eso es mandar a alguien al templo el dia
                          equivocado. ⚠️ **Ninguna importacion escribe ya este
                          codigo**: desde la version 12 esa hoja entra como caso
                          aparte y deja `hoja_aparte`. Se conserva porque en la
                          base del dueno ya hay renglones con el.
  - `entro_como_duplicado` — la pagina entro y SUS DATOS SE GUARDARON, y ademas
                          REPITE a un caso que ya estaba. El que ya estaba no se
                          toca; este entra aparte y marcado, y Miguel decide.
                          Entro el 2026-09-03, cuando el dueno dijo «si hay
                          documentos duplicados debe decirlo y no rechazarlo».
  - `hoja_aparte`       — la pagina repetia el numero de caso de otra del mismo
                          documento pero leia OTRO valor en un campo, asi que no
                          se unio: entro como caso aparte con todo lo suyo.
                          Sustituye a `campo_discrepante`, que rechazaba.

La frase en espanol se compone aqui, al mostrar, y NO se guarda en la tabla. Lo
guardado es el codigo, que es lo unico que se puede contar: de 500 documentos,
cuantos de cada clase. Una frase escrita distinta cada vez no se agrupa.

Nada de este modulo marca nada como verificado ni toca un caso: solo anota y lee.
"""

from datos.esquema import marca_de_tiempo

NO_SE_PUDO_ABRIR = "no_se_pudo_abrir"
SIN_TEXTO = "sin_texto"
SIN_CAMPOS = "sin_campos"
SIN_NUMERO_DE_CASO = "sin_numero_de_caso"

CASO_YA_EXISTENTE = "caso_ya_existente"

# Los dos que entraron el 2026-09-03, con los hallazgos ALTO y MEDIO de QA. Estan
# en esta tabla por lo mismo que `caso_ya_existente`: la pregunta que contestan es
# «¿que no salio bien y por que?», y un aviso que se cierra con un boton no es una
# respuesta que se pueda volver a mirar manana.
#
# ⚠️ Los dos NO son lo mismo, y esa diferencia decide de que color sale el renglon:
# con `anclas_perdidas` la pagina SI entro y sus datos estan guardados; con
# `campo_discrepante` la pagina NO entro —desde la auditoria final de QA, esa hoja
# ya no se une al caso— y lo unico que queda de ella es este renglon.
ANCLAS_PERDIDAS = "anclas_perdidas"
CAMPO_DISCREPANTE = "campo_discrepante"

# Los dos que entraron el 2026-09-03 con la version 12 del esquema, cuando el
# numero de caso dejo de ser la identidad del caso. Los dos hablan de paginas que
# **SI entraron**, y por eso los dos estan en `MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO`.
#
# ⚠️ `caso_ya_existente` y `campo_discrepante` NO se retiran de la lista de arriba
# aunque desde hoy ninguna importacion los escriba: en la base del dueno ya hay
# renglones con esos codigos, y quitarlos dejaria esas filas sin frase que
# ensenar. Un catalogo de motivos se amplia, no se reescribe.
ENTRO_COMO_DUPLICADO = "entro_como_duplicado"
HOJA_APARTE = "hoja_aparte"

MOTIVOS = (
    NO_SE_PUDO_ABRIR, SIN_TEXTO, SIN_CAMPOS, SIN_NUMERO_DE_CASO, CASO_YA_EXISTENTE,
    ANCLAS_PERDIDAS, CAMPO_DISCREPANTE, ENTRO_COMO_DUPLICADO, HOJA_APARTE,
)

# Los motivos en los que la pagina SI entro y sus datos estan guardados. La lista
# de renglones los pinta en ambar —«tiene arreglo»— y a los demas en rojo —«de esto
# no quedo nada»—.
#
# ⚠️ Va escrita aqui y no en la pantalla, y no es una cuestion de sitio: hasta el
# 2026-09-03 la pantalla preguntaba `motivo == SIN_NUMERO_DE_CASO` y pintaba de
# rojo todo lo demas. Con los dos motivos nuevos eso habria dicho «de esta pagina
# no quedo nada» de dos paginas cuyos datos SI estan guardados, y Miguel se habria
# puesto a buscar algo que no se ha perdido. Un motivo nuevo tiene que decidir aqui
# de que color sale, no en el archivo que lo dibuja.
#
# `campo_discrepante` estuvo en esta lista y **salio el 2026-09-03**: mientras la
# hoja que discrepaba se unia al caso, sus datos si estaban guardados. Desde que no
# se une, de esa hoja no queda mas que este renglon, y pintarla en ambar diria que
# hay algo guardado que no existe.
MOTIVOS_EN_QUE_LA_PAGINA_SI_ENTRO = (
    SIN_NUMERO_DE_CASO, ANCLAS_PERDIDAS, ENTRO_COMO_DUPLICADO, HOJA_APARTE,
)

# Lo que se le ensena a Miguel de cada motivo: que paso, y que puede hacer con eso.
# Cada frase dice el hecho primero y la accion despues, porque el hecho es lo que
# hay que creerse y la accion es lo que hay que hacer.
FRASES_DE_LOS_MOTIVOS = {
    NO_SE_PUDO_ABRIR: (
        "El archivo no se pudo abrir siquiera, así que de él no se leyó ninguna "
        "página. Compruebe que es un PDF, que no está a medio copiar y que no pide "
        "contraseña."
    ),
    SIN_TEXTO: (
        "El lector no encontró NI UNA línea de texto en esta página. O viene en "
        "blanco, o el escaneo está del revés o demasiado sucio para leerlo. Ábrala "
        "y mírela: si trae formulario, hay que volver a escanearla o teclearla."
    ),
    SIN_CAMPOS: (
        "El lector SÍ encontró texto en esta página, pero de ella no se dio por "
        "bueno ningún dato: ni número de caso, ni fecha, ni unidad, ni una sola "
        "persona. O no es una página de formulario, o el escaneo salió demasiado "
        "torcido o borroso para fiarse de lo que leyó."
    ),
    CASO_YA_EXISTENTE: (
        "Esta página NO entró porque su número de caso ya estaba en la base, y lo "
        "que ya estaba no se ha tocado: volver a importar no reemplaza las "
        "correcciones hechas a mano. ⚠️ Compruébelo: el número de caso son cuatro "
        "letras y el año y el mes, así que dos formularios de familias distintas de "
        "la misma unidad y el mismo mes lo comparten. Si es otra familia, esta "
        "página se quedó fuera y hay que meterla a mano."
    ),
    SIN_NUMERO_DE_CASO: (
        "De esta página sí se leyeron datos y SE GUARDARON: no se ha perdido nada. "
        "Lo único que faltó fue el número de caso. Abra el caso, mire el PDF en "
        "esta página y tecléelo."
    ),
    ANCLAS_PERDIDAS: (
        "Esta página SÍ entró, pero el lector no encontró algunas de las etiquetas "
        "impresas del formulario, y sin ellas no sabe dónde mirar: lo que haya "
        "leído en esas filas puede ser de otro sitio de la hoja. ⚠️ No se ha "
        "marcado como ilegible porque su confianza pasó el umbral, así que «Todo "
        "correcto» la firmaría sin más aviso que este. Ábrala y compare con el PDF "
        "antes de darla por buena."
    ),
    CAMPO_DISCREPANTE: (
        "Esta página traía el mismo número de caso que otra página del mismo "
        "documento, pero leyó OTRO valor en un campo del caso, así que NO se unió a "
        "ese caso y NO entró. ⚠️ Casi seguro es otra familia: el número de caso son "
        "cuatro letras y el año y el mes, o sea que identifica una unidad y un mes, "
        "no una familia. Unirlas dejaría a estas personas con la fecha de viaje de "
        "la otra familia, que es mandar a alguien al templo el día equivocado. Mire "
        "el PDF en esta página: si de verdad es otro formulario, hay que meterlo a "
        "mano; si es el mismo y uno de los dos se leyó mal, corrija el caso que sí "
        "entró y vuelva a importar esta página."
    ),
    ENTRO_COMO_DUPLICADO: (
        "Esta página SÍ entró y sus datos están guardados, pero REPITE a un caso "
        "que ya estaba: comparte con él número de caso y cédula de miembro, o viene "
        "de la misma hoja del mismo archivo. El caso que ya estaba NO se ha tocado "
        "—sus correcciones a mano y sus firmas siguen intactas— y este entra aparte, "
        "marcado. ⚠️ Nada se ha fundido solo: decida usted qué hacer con los dos. "
        "Debajo se dice de qué caso es duplicado."
    ),
    HOJA_APARTE: (
        "Esta página traía el mismo número de caso que otra página del mismo "
        "documento, pero leyó OTRO valor en un campo del caso, así que NO se unió a "
        "ese caso: entró como CASO APARTE, con todo lo que se le leyó. Casi seguro "
        "es otra familia —el número de caso son cuatro letras y el año y el mes, o "
        "sea que identifica una unidad y un mes, no una familia—. ⚠️ Si de verdad "
        "son la misma familia y uno de los dos se leyó mal, aquí quedan dos casos "
        "donde debería haber uno: mire el PDF en esta página y corrija."
    ),
}

# Lo mismo dicho en una linea, para la franja de avisos. **No sustituye a las
# frases de arriba: son las dos mitades del mismo aviso.** La linea es lo que se
# ve —una, con la cuenta delante— y la frase es lo que se lee al pedir el detalle.
#
# Tres reglas al escribir una linea nueva, y las tres salen de como se pinta:
#
#   1. **Empieza por su propio sustantivo** —«página que…», «PDF que…»— porque la
#      franja compone «3 páginas que entraron sin número de caso…» pegando la
#      cuenta delante. Un texto que empiece por un verbo se lee mal ahi.
#   2. **Cabe en 90 caracteres** (`interfaz/avisos_de_correccion.py`), que es lo
#      que entra en la linea de 1100 px junto a otro grupo.
#   3. **Dice el hecho y la salida**, en ese orden, igual que la frase larga. Un
#      aviso que solo dice que hay un problema no sirve de nada.
LINEAS_DE_LOS_MOTIVOS = {
    NO_SE_PUDO_ABRIR: "PDF que no se pudo abrir: ¿está a medias o pide contraseña?",
    SIN_TEXTO: "página sin una sola línea de texto: puede venir en blanco o del revés",
    SIN_CAMPOS: "página con texto del que no salió ningún dato: compárela con el PDF",
    SIN_NUMERO_DE_CASO: "página que entró sin número de caso: mírela en el PDF y tecléelo",
    CASO_YA_EXISTENTE: "página que no entró porque su número de caso ya estaba en la base",
    ANCLAS_PERDIDAS: "página con etiquetas del formulario sin encontrar: compare con el PDF",
    CAMPO_DISCREPANTE: "página que no entró: repetía el número de caso con otro valor",
    ENTRO_COMO_DUPLICADO: "página que entró y repite a un caso que ya estaba: decida cuál vale",
    HOJA_APARTE: "página que entró como caso aparte: repetía el número con otro valor",
}

# Cuanto texto leido se guarda como detalle. Es para diagnosticar por que fallo la
# lectura, no para reconstruir la pagina: con el principio ya se ve si el OCR leyo
# el formulario, otra cosa, o basura. Sin tope, una pagina densa metería miles de
# caracteres en cada fila de una tabla que puede tener cientos.
LARGO_MAXIMO_DEL_DETALLE = 400


def frase_del_motivo(motivo):
    """La frase en espanol de ese motivo, o el codigo si es uno que no consta.

    Devuelve el codigo en vez de levantar: un motivo desconocido en una lista que
    existe para no perder nada tiene que verse, no tumbar la pantalla que lo
    ensena.
    """
    return FRASES_DE_LOS_MOTIVOS.get(motivo, motivo)


def linea_del_motivo(motivo):
    """Ese motivo en una linea, o el codigo si es uno que no consta.

    Devuelve el codigo por lo mismo que `frase_del_motivo`: un motivo desconocido
    en una lista que existe para no perder nada tiene que verse.
    """
    return LINEAS_DE_LOS_MOTIVOS.get(motivo, motivo)


def recortar_el_detalle(texto):
    """El detalle, recortado al tope y diciendo que se recorto. Nulo sigue nulo."""
    if texto is None:
        return None
    texto = str(texto)
    if len(texto) <= LARGO_MAXIMO_DEL_DETALLE:
        return texto
    return texto[:LARGO_MAXIMO_DEL_DETALLE] + " […recortado]"


def anotar_documento_ilegible(
    conexion, ruta_pdf, motivo, pagina_pdf=None, detalle=None, lineas_leidas=None,
    caso_id=None,
):
    """Deja el renglon de una lectura que fallo. Devuelve el id de la fila.

    `pagina_pdf` en base 1 —«página 2 de 6»—, o None cuando el fallo es del
    documento entero y no hay pagina que senalar.
    """
    cursor = conexion.execute(
        "INSERT INTO documentos_ilegibles (ruta_pdf, pagina_pdf, motivo, detalle, "
        "lineas_leidas, caso_id, registrado_en) VALUES (?, ?, ?, ?, ?, ?, ?)",
        (
            str(ruta_pdf),
            pagina_pdf,
            motivo,
            recortar_el_detalle(detalle),
            lineas_leidas,
            caso_id,
            marca_de_tiempo(),
        ),
    )
    return cursor.lastrowid


def documentos_ilegibles(conexion):
    """Todos los renglones, el mas reciente primero.

    El mas reciente primero y no por ruta: lo que se abre esta lista a mirar es
    «que paso en la tanda que acabo de importar», y esa es la de arriba.
    """
    return conexion.execute(
        "SELECT i.id, i.ruta_pdf, i.pagina_pdf, i.motivo, i.detalle, "
        "       i.lineas_leidas, i.caso_id, i.registrado_en, c.numero_caso "
        "FROM documentos_ilegibles i "
        "LEFT JOIN casos c ON c.id = i.caso_id "
        "ORDER BY i.registrado_en DESC, i.id DESC"
    ).fetchall()


def renglones_del_caso(conexion, caso_id):
    """Los renglones que apuntan a ESE caso, el mas antiguo primero.

    Existe para que la pantalla de correccion pueda decir arriba lo que la
    importacion vio y no pudo arreglar: que otra hoja leyo otra fecha, que a esta
    pagina le faltaban etiquetas. Hasta el 2026-09-03 eso solo se decia en el
    resumen de la importacion, que se cierra con un boton — y con cientos de PDF,
    quien abre el caso tres dias despues no vio ese resumen.

    El mas antiguo primero, al reves que `documentos_ilegibles`: aqui son dos o
    tres de un solo caso y se leen en el orden en que pasaron, no como una bandeja
    de entrada.
    """
    return conexion.execute(
        "SELECT id, ruta_pdf, pagina_pdf, motivo, detalle, lineas_leidas, "
        "       caso_id, registrado_en "
        "FROM documentos_ilegibles WHERE caso_id = ? "
        "ORDER BY registrado_en, id",
        (caso_id,),
    ).fetchall()


def contar_documentos_ilegibles(conexion):
    """Cuantos renglones hay, para poder decirlo sin traerlos todos."""
    return conexion.execute("SELECT COUNT(*) FROM documentos_ilegibles").fetchone()[0]


def contar_por_motivo(conexion):
    """Cuantos renglones hay de cada motivo, como `{motivo: cuantos}`.

    Es la pregunta que la tabla existe para contestar y que hoy nadie puede: de
    todo lo que no se leyo, ¿cuanto es del archivo, cuanto del escaneo y cuanto del
    lector? Con eso se sabe donde mirar antes de abrir un solo PDF.
    """
    filas = conexion.execute(
        "SELECT motivo, COUNT(*) AS cuantos FROM documentos_ilegibles GROUP BY motivo"
    ).fetchall()
    return {fila["motivo"]: fila["cuantos"] for fila in filas}

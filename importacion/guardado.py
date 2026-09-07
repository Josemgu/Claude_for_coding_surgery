"""Una pagina leida, guardada en la base con toda su procedencia.

Lo que este modulo garantiza, y que es la razon de que exista en vez de repartir
estas llamadas por la pantalla: **no se guarda un valor sin guardar de donde
salio**. Cada campo del caso y cada campo de cada persona deja su fila en
`procedencia_campo` con su origen, su confianza, lo que el OCR habia leido y
donde estaba en el escaneo. Si eso se hiciera desde la interfaz, bastaria una
pantalla nueva que se olvidara de una llamada para que un dato apareciera sin
saber de donde vino.

**Nada se marca como verificado aqui** (regla permanente 5). Las filas de
procedencia nacen con `verificado = 0`, y solo `marcar_campo_verificado` —que
exige quien y cuando— las cambia.

⚠️ **NINGUNA PAGINA SE RECHAZA. Ni una.** Es la decision del 2026-09-03, y lo que
la obliga esta medido en la PC del trabajo por el dueno: importo diez documentos de
su carpeta real y el programa contesto «6 caso ya existente» —seis documentos
enteros fuera—. Uno de ellos, literal: «El caso BALC2609 ya estaba en la base con 1
persona(s)... MRN en comun: 0». **Era otra familia.** El numero de caso son cuatro
letras mas el ano y el mes: identifica una UNIDAD Y UN MES, no una familia, y en su
carpeta muchos documentos distintos lo comparten por construccion.

Asi que la identidad de un caso pasa a ser **el documento del que salio**, no su
numero. Cada pagina importada entra, con lo que se leyo y con lo que no. Mismo
numero con MRN distintos = otra familia = otro caso nuevo.

⚠️ **Y un documento repetido tampoco se rechaza: entra MARCADO.** Lo dijo el dueno
el mismo dia: «Si hay documentos duplicados debe decirlo y no rechazarlo». Un
archivo que ya se importo entra como cualquier otro y su caso nace con
`casos.duplicado_de` apuntando al que repite. **Lo que sigue prohibido es PISAR:**
el caso que ya estaba, con sus correcciones a mano y sus firmas, no se toca ni un
byte, y **nada se fusiona solo**. La marca se ve en la lista de pendientes, en la
pantalla del caso y en el resumen de la importacion, y Miguel decide.

Como se reconoce un duplicado, dicho con lo que hay y no con lo que seria ideal:
por **MRN en comun dentro del mismo numero de caso**, o por **la misma hoja del
mismo archivo** (`casos.ruta_pdf` + `casos.pagina_pdf`). No hay huella criptografica
del archivo guardada en ninguna parte, asi que **un archivo copiado a otra ruta y
sin ningun MRN legible no se reconoce**: entra como caso nuevo sin marca. Queda
dicho aqui para que nadie lo descubra despues.

**Las hojas de un mismo documento se siguen uniendo** (opcion A de `DECISIONES.md`,
2026-09-02). Un formulario de grupo ocupa seis paginas con el MISMO numero de caso;
sin esto, `SURB2609` entraba con 1 persona de 12, medido. La frontera es la
importacion en curso, y por eso vive en `casos_de_esta_tanda`: un diccionario que
empieza vacio en cada documento y solo guarda los casos que ESE documento ha creado.

⚠️ **Y unirse no es gratis: la hoja tiene que NO CONTRADECIR al caso** (decision
del 2026-09-03, tras la auditoria final de QA). QA junto los dos PDF reales del
dueno en un documento de dos hojas y salio «1 caso, 5 personas, fecha 2026-08-25»
cuando la hoja 2 habia leido `2026-08-26` — una persona con la fecha de viaje de
otra familia, sin marcha atras. Una hoja que lee otra fecha u otra unidad **no se
une**; lo que cambia hoy es que **tampoco se tira**: abre su propio caso con todo lo
suyo y deja su renglon con motivo `hoja_aparte`.
"""

import sqlite3
from collections import namedtuple

from datos.ilegibles import (
    ENTRO_COMO_DUPLICADO,
    HOJA_APARTE,
    anotar_documento_ilegible,
)
from datos.procedencia import guardar_procedencia_de_campo
from datos.repositorio import (
    alta_de_caso,
    alta_de_persona,
    leer_caso_por_id,
)
from datos.validacion import ErrorDeValidacion
from importacion.diagnostico import (
    AnotacionDeIlegible,
    anotaciones_de_lo_dudoso,
    aviso_de_anclas_perdidas,
    diagnosticar_la_pagina,
)

# Los campos del caso que llevan procedencia, en el orden en que se dibujan en la
# pantalla de correccion. El nombre de cada uno es el de su columna en `casos`:
# `procedencia_campo.campo` guarda ese mismo nombre, y que coincidan es lo que
# permite cruzarlos sin una tabla de traduccion en medio.
CAMPOS_DEL_CASO = ("numero_caso", "fecha_viaje", "unidad_numero", "unidad_nombre")

# Los dos campos de una persona que se leen del papel. Las seis casillas NO llevan
# procedencia: no se leen (`extraccion/casillas.py` entrega la lectura desactivada
# a falta de calibrar), y una fila de procedencia que dijera `origen='vacio'` para
# las seis de cada persona serian 36 filas por formulario que no informan de nada.
CAMPOS_DE_LA_PERSONA = ("nombre", "mrn")

ResultadoDeLaPagina = namedtuple(
    "ResultadoDeLaPagina",
    (
        "importada",
        "caso_nuevo",
        "caso_id",
        "numero_caso",
        "pagina_pdf",
        "personas",
        "avisos",
        "motivo",
        # Los dos van al final y con valor por defecto para que nada de lo que ya
        # construia un `ResultadoDeLaPagina` tenga que cambiar.
        #
        # `pendiente_de_identificar` distingue las dos formas de entrar: con numero
        # de caso, o sin el. El resumen las tiene que contar por separado —«12
        # entraron, 3 quedaron a medias»— porque no son lo mismo para quien va a
        # tener que abrirlas.
        #
        # `ilegibles` son las `AnotacionDeIlegible` que hay que dejar en la tabla
        # `documentos_ilegibles`, y una tupla vacia cuando la pagina salio bien.
        # Viajan aqui y no se escriben desde `guardar_formulario` porque el renglon
        # necesita el `caso_id` que acaba de nacer, y quien coordina la tanda es
        # quien lo tiene todo delante.
        #
        # ⚠️ **Es una TUPLA y hasta el 2026-09-03 era una sola anotacion.** Una
        # pagina puede tener varias cosas que decir a la vez —haber perdido
        # etiquetas Y contradecir a la hoja que abrio el caso— y con una sola
        # ranura la segunda se perdia. Que es como se perdian: en silencio.
        # `duplicado_de` entro el 2026-09-03 con la version 13 del esquema: es el
        # id del caso que ESTA pagina repite, o None. Viaja en el resultado —y no
        # solo en la columna— porque el resumen de la importacion lo cuenta y lo
        # dice, y leerlo de vuelta de la base seria una consulta por pagina para un
        # dato que se acaba de calcular.
        "pendiente_de_identificar",
        "ilegibles",
        "duplicado_de",
    ),
    defaults=(False, (), None),
)


def _sola(anotacion):
    """Una anotacion metida en una tupla, o la tupla vacia si no hay ninguna.

    Existe para que los tres sitios que juntan anotaciones sumen tuplas y ninguno
    tenga que preguntar por None antes de sumar.
    """
    return () if anotacion is None else (anotacion,)


def _pagina_humana(formulario):
    """La pagina contada desde 1, que es como la cuentan las personas y el visor."""
    return formulario.indice_de_pagina + 1


def _no_importada(formulario, motivo, numero_caso=None, ilegibles=()):
    """Una pagina que no entro, con el motivo escrito para el resumen.

    ⚠️ **Desde el 2026-09-03 NINGUN camino de este modulo llama aqui**, porque
    ninguna pagina se rechaza: la que repetia numero entra marcada como duplicado y
    la que contradecia a su hermana abre su propio caso. Se conserva —y no se
    borra— porque el resto de la maquinaria que la rodea (`ResultadoDeLaPagina` con
    `importada=False`, `paginas_no_importadas`, la cifra del resumen) sigue en pie y
    es lo que tendria que usar cualquier motivo futuro por el que una pagina no
    pueda entrar. Borrarla ahora obligaria a reinventarla, y el proyecto tiene
    escrita la regla de que en desarrollo lo que parece muerto se documenta, no se
    borra.

    Se devuelve en vez de levantar: un PDF de seis paginas donde falla la tercera
    tiene que seguir importando la cuarta. Lo que no puede es fallar callado, y
    por eso el motivo viaja pegado y el resumen lo cuenta.

    `ilegibles` es una TUPLA de `AnotacionDeIlegible`, por lo mismo que en
    `ResultadoDeLaPagina`: los dos motivos por los que una pagina no entra —el caso
    ya estaba en la base, o esta hoja contradice a la que abrio el caso— dejan hoy
    un renglon cada uno, pero una pagina puede tener mas de una cosa que decir.
    """
    return ResultadoDeLaPagina(
        importada=False,
        caso_nuevo=False,
        caso_id=None,
        numero_caso=numero_caso,
        pagina_pdf=_pagina_humana(formulario),
        personas=0,
        avisos=(),
        motivo=motivo,
        pendiente_de_identificar=False,
        ilegibles=tuple(ilegibles),
    )


def _guardar_procedencia(conexion, tabla, registro_id, campo, extraido, banda):
    """La fila de procedencia de un campo, con su banda del escaneo."""
    guardar_procedencia_de_campo(
        conexion,
        tabla,
        registro_id,
        campo,
        origen=extraido.origen,
        confianza=extraido.confianza,
        valor_ocr=extraido.valor_ocr,
        banda=banda,
        anulado_por_tachon=1 if extraido.anulado_por_tachon else 0,
    )


def _guardar_procedencia_del_caso(conexion, caso_id, formulario):
    """Las cuatro filas de procedencia del caso, una por campo."""
    for campo in CAMPOS_DEL_CASO:
        _guardar_procedencia(
            conexion,
            "casos",
            caso_id,
            campo,
            getattr(formulario, campo),
            formulario.bandas.get(campo),
        )


PersonaGuardada = namedtuple("PersonaGuardada", ("persona_id", "aviso"))

# Lo que se devuelve cuando la fila venia en blanco: ni id ni aviso. No es un
# descarte que haya que contarle a nadie —el formulario trae seis renglones y casi
# nunca vienen los seis llenos—, y avisarlo por cada hueco enterraria los avisos
# que si importan.
FILA_EN_BLANCO = PersonaGuardada(None, None)


def _guardar_una_persona(
    conexion, caso_id, persona, casillas, banda, fila_formulario, pagina_pdf
):
    """Una persona con sus seis casillas y la procedencia de sus dos campos.

    `pagina_pdf` es la hoja de la que sale ESTA persona, y se guarda en la persona
    ademas de en el caso. Con las hojas de un grupo unidas en un solo caso, la
    pagina del caso es solo la de la hoja que lo abrio: la tira del escaneo de una
    persona de la hoja 4 hay que recortarla de la hoja 4.

    Devuelve el id y, cuando la fila no entro por algo que Miguel deba saber, el
    aviso que lo explica. Las dos formas de no entrar son distintas y por eso se
    devuelven distintas:

      - **Sin nombre y sin MRN**: es un renglon vacio del formulario. No es un
        fallo y no genera aviso.
      - **MRN repetido dentro del mismo caso**: choca con `UNIQUE (caso_id, mrn)`.
        Con las paginas de un grupo unidas en un solo caso esto ya puede pasar
        entre dos hojas, y antes subia como `IntegrityError` y se llevaba por
        delante la importacion entera. Aqui se convierte en aviso: **no se
        silencia**, se nombra la fila y se dice que se descarto.
    """
    try:
        persona_id = alta_de_persona(
            conexion,
            caso_id,
            mrn=persona.mrn.valor,
            nombre=persona.nombre.valor,
            fila_formulario=fila_formulario,
            pagina_pdf=pagina_pdf,
            **casillas,
        )
    except ErrorDeValidacion:
        return FILA_EN_BLANCO
    except sqlite3.IntegrityError:
        return PersonaGuardada(
            None,
            f"la fila {persona.fila_formulario} de esta página se descartó porque "
            "su MRN ya estaba en este mismo caso. Compruébelo en el PDF: o es la "
            "misma persona repetida, o el lector leyó mal uno de los dos.",
        )

    for campo in CAMPOS_DE_LA_PERSONA:
        _guardar_procedencia(conexion, "personas", persona_id, campo, getattr(persona, campo), banda)
    return PersonaGuardada(persona_id, None)


def _ultima_fila_del_caso(conexion, caso_id):
    """El numero de fila mas alto que ya tiene el caso, o 0 si no tiene ninguna.

    Existe por las paginas unidas. Cada hoja numera sus personas desde 1, asi que
    seis hojas del mismo grupo traerian seis «fila 1». Con `ORDER BY
    fila_formulario` eso deja la pantalla de correccion con las seis primeras
    personas de las seis paginas juntas y luego las segundas: el orden del papel
    se pierde justo donde hay que comparar contra el papel. Numerando seguido, el
    grupo de doce sale 1..12 en el orden en que estaba escrito.
    """
    fila = conexion.execute(
        "SELECT COALESCE(MAX(fila_formulario), 0) FROM personas WHERE caso_id = ?",
        (caso_id,),
    ).fetchone()
    return fila[0] or 0


def _fila_corrida(fila_de_la_pagina, desplazamiento):
    """La fila de esta pagina, corrida detras de las que el caso ya tenia.

    Con desplazamiento 0 —el caso acaba de nacer— devuelve exactamente el numero
    que traia la pagina: una pagina suelta se numera igual que antes de este
    arreglo, y los huecos que dejan las filas descartadas se conservan.
    """
    if fila_de_la_pagina is None:
        return None
    return fila_de_la_pagina + desplazamiento


def _guardar_las_personas(conexion, caso_id, formulario):
    """Todas las personas de la pagina. Devuelve cuantas entraron y los avisos."""
    desplazamiento = _ultima_fila_del_caso(conexion, caso_id)
    guardadas = 0
    avisos = []
    for indice, persona in enumerate(formulario.personas):
        resultado = _guardar_una_persona(
            conexion,
            caso_id,
            persona,
            formulario.casillas_por_persona[indice],
            formulario.bandas_por_persona[indice],
            _fila_corrida(persona.fila_formulario, desplazamiento),
            _pagina_humana(formulario),
        )
        if resultado.persona_id is not None:
            guardadas += 1
        if resultado.aviso is not None:
            avisos.append(resultado.aviso)
    return guardadas, tuple(avisos)


# Las etiquetas en espanol de los tres campos del caso que se comparan entre hojas.
# Son las mismas palabras que la pantalla de correccion pone encima de cada campo:
# el renglon y la pantalla tienen que nombrar la misma cosa igual, o Miguel lee
# «unidad_numero» en un sitio y «N.º de unidad» en el otro y no sabe si son dos.
#
# `numero_caso` no esta, y no es un olvido: dos hojas se unen precisamente porque
# lo leyeron IGUAL. Comparar el campo por el que se unieron no puede dar nunca otra
# cosa que igualdad.
ETIQUETAS_DE_LOS_CAMPOS_DEL_CASO = {
    "fecha_viaje": "Fecha de viaje",
    "unidad_numero": "N.º de unidad",
    "unidad_nombre": "Unidad",
}


def _discrepancias_entre_hojas(conexion, caso_id, formulario):
    """En que campos del caso esta hoja lee otra cosa que la que abrio el caso.

    Devuelve una lista de `(etiqueta, lo_guardado, lo_de_esta_hoja)`, vacia cuando
    las dos hojas dicen lo mismo o cuando esta no leyo el campo.

    ⚠️ **«No lo lei» NO es una discrepancia**, y esa es la mitad que hace util al
    aviso. Las hojas 5 y 6 del grupo real del dueno entran sin fecha y sin unidad
    —medido: 4 y 2 anclas perdidas, `fecha_viaje` y `unidad_numero` a None—, y
    contar eso como contradiccion pondria un renglon en cada grupo de mas de cuatro
    hojas. Un aviso que sale siempre no lo lee nadie, y entonces el que si importa
    tampoco. Contradecir es decir OTRA cosa, no callarse.

    Lo mismo al reves: si el caso guardado no tiene el campo y esta hoja si lo trae,
    tampoco hay contradiccion. Ahi no hay dos versiones, hay una.
    """
    guardado = leer_caso_por_id(conexion, caso_id)
    discrepancias = []
    for campo, etiqueta in ETIQUETAS_DE_LOS_CAMPOS_DEL_CASO.items():
        de_esta_hoja = getattr(formulario, campo).valor
        lo_guardado = guardado[campo]
        if de_esta_hoja is None or lo_guardado is None:
            continue
        if str(de_esta_hoja) != str(lo_guardado):
            discrepancias.append((etiqueta, lo_guardado, de_esta_hoja))
    return discrepancias


def _texto_de_las_discrepancias(discrepancias):
    """Los campos que no coinciden, dichos uno detras de otro en una frase.

    Se junta todo en UNA frase y no una por campo: es un solo hecho —«esta hoja no
    es de la misma familia que la que abrio el caso»— y tres renglones seguidos con
    la misma pagina se leen como tres problemas distintos.
    """
    return " ".join(
        f"«{etiqueta}»: el caso guardó «{lo_guardado}» de la hoja que lo abrió, y "
        f"esta hoja leyó «{de_esta_hoja}»."
        for etiqueta, lo_guardado, de_esta_hoja in discrepancias
    )


def _aviso_de_la_hoja_que_no_se_unio(discrepancias, numero_caso):
    """Lo que el resumen de la importacion dice de la hoja que abrio caso aparte."""
    return (
        f"Esta página dice ser del caso {numero_caso}, que otra página de este mismo "
        f"documento acaba de abrir, pero NO coincide con ella: "
        f"{_texto_de_las_discrepancias(discrepancias)} Las dos no pueden ser ciertas, "
        "así que esta página NO se unió a ese caso: ENTRÓ COMO CASO APARTE, con todo "
        "lo que se le leyó. Dos familias distintas de la misma unidad y el mismo mes "
        "comparten número de caso por construcción."
    )


def _anotacion_de_la_hoja_que_no_se_unio(
    conexion, discrepancias, numero_caso, formulario
):
    """El renglon de la hoja que abrio caso aparte, con lo que se le habia leido.

    Las discrepancias van DELANTE del detalle del choque a proposito: el detalle se
    recorta a 400 caracteres al guardarlo (`datos/ilegibles.py`), y lo primero que
    hay que poder leer es que campo no cuadra.
    """
    return (
        AnotacionDeIlegible(
            HOJA_APARTE,
            _texto_de_las_discrepancias(discrepancias)
            + " "
            + _detalle_del_choque(conexion, numero_caso, formulario),
            formulario.lineas_leidas,
        ),
    )


def _unir_a_un_caso_de_esta_tanda(conexion, caso_id, numero_caso, formulario):
    """Anade las personas de esta pagina al caso que nacio en esta importacion.

    Lo que NO se toca son los campos del caso ni su procedencia. La fecha, la
    unidad y la pagina del PDF se quedan las de la hoja que abrio el caso: si cada
    hoja las pisara, la ultima ganaria siempre y una fecha bien leida en la pagina
    1 la borraria una pagina 6 con la fecha tachada, sin que nadie se entere.
    Corregir esos cuatro campos es exactamente para lo que existe la pantalla de
    correccion, y ahi se ve de donde salio cada uno.

    ⚠️ **Y por eso una hoja que CONTRADICE al caso no se une, desde el 2026-09-03.**
    QA unio los dos PDF reales del dueno en un documento de dos hojas —que es lo que
    produce un escaner de lote— y midio:

        Se guardó 1 caso de 2 páginas, con 5 personas en total.
        caso num=BARC2608 fecha=2026-08-25   personas por hoja: {1: 4, 2: 1}

    La hoja 2 habia leido `2026-08-26`. **La persona de la hoja 2 quedo con la fecha
    de viaje de otra familia**, y no hay marcha atras: ninguna funcion mueve una
    persona de caso. El numero de caso son cuatro letras mas el ano y el mes, o sea
    que identifica UNA UNIDAD Y UN MES, no una familia: dos familias distintas de la
    misma unidad en el mismo mes lo comparten por construccion.

    Una hoja se une solo si **no contradice** los campos del caso —misma fecha y
    misma unidad, o el campo sin leer—.

    ⚠️ **Y si contradice, devuelve None en vez de un rechazo, desde el 2026-09-03.**
    None significa aqui «esta hoja no es de este caso», y quien llama la manda a
    abrir su propio caso. Antes se tiraba entera con todo lo leido dentro, y eso
    era el mismo error que el `UNIQUE` del numero: dar por hecho que dos papeles con
    el mismo numero son el mismo papel.
    """
    discrepancias = _discrepancias_entre_hojas(conexion, caso_id, formulario)
    if discrepancias:
        return None
    personas, avisos = _guardar_las_personas(conexion, caso_id, formulario)
    return ResultadoDeLaPagina(
        importada=True,
        caso_nuevo=False,
        caso_id=caso_id,
        numero_caso=numero_caso,
        pagina_pdf=_pagina_humana(formulario),
        personas=personas,
        avisos=avisos + aviso_de_anclas_perdidas(formulario),
        motivo=None,
        pendiente_de_identificar=False,
        ilegibles=(
            _sola(diagnosticar_la_pagina(formulario))
            + anotaciones_de_lo_dudoso(formulario)
        ),
    )


def _mrn_de_la_pagina(formulario):
    """Las cedulas de miembro que ESTA pagina trajo legibles. Nunca `None`."""
    return {
        persona.mrn.valor for persona in formulario.personas if persona.mrn.valor
    }


def _caso_que_comparte_mrn(conexion, numero_caso, mrn_de_la_pagina):
    """El caso mas antiguo con ese numero que comparta alguna cedula, o None.

    Compartir una cedula de miembro con un caso que lleva el mismo numero es la
    senal fuerte: el numero solo dice unidad y mes, pero el MRN es de UNA persona.
    Si la persona de este papel ya esta guardada bajo el mismo numero, este papel
    repite a aquel.

    Las cedulas se cruzan **en Python y no con un `IN (...)` armado**: la lista de
    marcadores tendria que construirse con una f-string, y `pruebas/auditoria_sql.py`
    dictamina INSEGURA cualquier instruccion armada asi —aunque lo que se pegue sean
    interrogaciones nuestras—. Una lista blanca que admite excepciones deja de ser
    una lista blanca. El coste es leer las personas de los casos que llevan ese
    numero, que son unas pocas.
    """
    if not mrn_de_la_pagina or numero_caso is None:
        return None
    for fila in conexion.execute(
        "SELECT p.caso_id, p.mrn FROM personas p JOIN casos c ON c.id = p.caso_id "
        "WHERE c.numero_caso = ? AND p.mrn IS NOT NULL ORDER BY p.caso_id, p.id",
        (numero_caso,),
    ):
        if fila["mrn"] in mrn_de_la_pagina:
            return fila["caso_id"]
    return None


def _caso_de_la_misma_hoja(conexion, formulario):
    """El caso que ya salio de ESTA hoja de ESTE archivo, o None.

    Es la segunda via, y la que atrapa el archivo repetido cuyos MRN el lector no
    pudo leer: si ya hay un caso con la misma ruta de PDF y la misma pagina, este
    papel es ese papel otra vez.

    ⚠️ **No es una huella del contenido: es la ruta.** El mismo archivo copiado a
    otra carpeta, o renombrado, no se reconoce por aqui. Con MRN legibles lo atrapa
    la otra via; sin MRN y desde otra ruta, entra como caso nuevo y sin marca. La
    base no guarda ninguna huella del archivo y este pase no la anade.
    """
    if not formulario.ruta_pdf:
        return None
    fila = conexion.execute(
        "SELECT MIN(id) AS id FROM casos WHERE ruta_pdf = ? AND pagina_pdf = ?",
        (str(formulario.ruta_pdf), _pagina_humana(formulario)),
    ).fetchone()
    return fila["id"] if fila is not None else None


def _caso_del_que_es_duplicado(conexion, numero_caso, formulario):
    """El id del caso que esta pagina REPITE, o None si no repite a ninguno.

    Las dos vias van en este orden porque contestan a preguntas distintas y la
    primera es la que Miguel puede comprobar mirando el papel: «esta persona ya
    estaba». La segunda —misma hoja del mismo archivo— es la red de abajo.
    """
    por_mrn = _caso_que_comparte_mrn(
        conexion, numero_caso, _mrn_de_la_pagina(formulario)
    )
    return por_mrn if por_mrn is not None else _caso_de_la_misma_hoja(conexion, formulario)


def _detalle_del_choque(conexion, numero_caso, formulario):
    """Con que se choco esta pagina, para poder decidir si es la misma o no.

    Se dicen las personas de las DOS: las que trae esta pagina y las que ya tienen
    los casos guardados con ese mismo numero. Con eso se puede decidir sin abrir
    nada —si los nombres no se parecen, son dos familias distintas—, que es la unica
    pregunta que importa aqui.

    Los nombres salen de la base y del papel, no se inventa ninguno.
    """
    ya_estaban = conexion.execute(
        "SELECT p.nombre, p.mrn FROM personas p JOIN casos c ON c.id = p.caso_id "
        "WHERE c.numero_caso = ? ORDER BY p.fila_formulario, p.id",
        (numero_caso,),
    ).fetchall()
    nombres_guardados = [fila["nombre"] for fila in ya_estaban if fila["nombre"]]
    nombres_de_la_pagina = [
        persona.nombre.valor for persona in formulario.personas if persona.nombre.valor
    ]
    mrn_guardados = {fila["mrn"] for fila in ya_estaban if fila["mrn"]}
    mrn_de_la_pagina = _mrn_de_la_pagina(formulario)
    comunes = len(mrn_guardados & mrn_de_la_pagina)
    return (
        f"Con el número {numero_caso} ya había {len(ya_estaban)} persona(s) en la "
        f"base: {', '.join(nombres_guardados) or 'ninguna con nombre'}. "
        f"Esta página traía {len(formulario.personas)} persona(s): "
        f"{', '.join(nombres_de_la_pagina) or 'ninguna con nombre'}. "
        f"MRN en común: {comunes} (la página trae {len(mrn_de_la_pagina)} MRN legibles "
        f"y lo guardado con ese número tiene {len(mrn_guardados)}). "
        + (
            "Comparten MRN, así que casi seguro es el mismo formulario otra vez."
            if comunes
            else "No comparten NINGÚN MRN, así que casi seguro es OTRA familia de la "
            "misma unidad y el mismo mes —el número de caso no distingue eso—. Las "
            "dos entraron: son dos casos. Ojo, que el 0 también sale cuando el "
            "lector no pudo leer los MRN: en los PDF de referencia leyó 9 de 12."
        )
    )


def _detalle_del_duplicado(conexion, caso_repetido_id, numero_caso, formulario):
    """El renglon del duplicado: de que caso repite, y con que coincide.

    Dice el id del caso original y no solo su numero porque desde la version 12 el
    numero ya no identifica a nadie: dos casos con `BALC2609` delante son dos casos,
    y sin el id no hay forma de saber a cual se refiere.
    """
    original = leer_caso_por_id(conexion, caso_repetido_id)
    numero_del_original = (original or {}).get("numero_caso")
    return (
        f"Es DUPLICADO del caso n.º {caso_repetido_id} "
        f"({numero_del_original or 'sin número de caso'}), que NO se ha tocado. "
        + _detalle_del_choque(conexion, numero_caso, formulario)
    )


AVISO_DE_PAGINA_SIN_NUMERO = (
    "esta página se guardó SIN número de caso porque no se pudo leer. Todo lo "
    "demás que se leyó está dentro y no hay que volver a teclearlo. Abra el caso, "
    "mire el PDF en esta página y escriba el número; hasta entonces el caso no "
    "puede cruzarse con el Excel que devuelven los compañeros."
)


def _aviso_de_sin_numero(numero_caso):
    """El aviso de la pagina sin identificar, o ninguno si el numero si salio."""
    return () if numero_caso is not None else (AVISO_DE_PAGINA_SIN_NUMERO,)


def guardar_formulario(conexion, formulario, casos_de_esta_tanda=None):
    """Guarda una pagina leida y devuelve que paso con ella.

    El caso se guarda **aunque tenga campos que no valen**: la fecha vacia, la
    unidad ilegible y el MRN sin leer son exactamente lo que la pantalla de
    correccion existe para arreglar, y negarse a guardarlos dejaria fuera del
    programa justo los formularios que hay que atender.

    ⚠️ **Y desde la version 7 del esquema, eso incluye el numero de caso.** Hasta
    ese dia una pagina sin numero se devolvia sin guardar, con todo lo que se le
    habia leido dentro: los nombres, los MRN, la fecha. El dueno lo midio con sus
    PDF el 2026-09-02 y el programa contesto «Se guardaron 0 casos de 1 página, con
    0 personas en total».

    Tirar una pagina ya leida es lo contrario de lo que este programa existe para
    hacer: el trabajo estaba hecho, y tirarlo obliga a teclear de cero lo que la
    maquina habia leido bien —y deja el formulario fuera del programa, que es justo
    el que hay que atender—. Ahora entra con `numero_caso` a NULL, que significa
    «todavia no se sabe», con su aviso y con su renglon en `documentos_ilegibles`.
    El numero **no se inventa** (regla permanente 1): se teclea a mano.

    `casos_de_esta_tanda` es el diccionario `numero_caso -> caso_id` de los casos
    que ESTE documento ha creado, y se rellena aqui segun se crean. Es lo que une
    las seis hojas de un formulario de grupo en un solo caso.

    ⚠️ **Lo que ya NO hace, desde el 2026-09-03: rechazar.** Un numero de caso que
    ya estaba en la base no impide nada; la pagina abre su propio caso, y si ademas
    repite a uno que ya estaba, ese caso nace marcado con `duplicado_de`. El caso
    viejo no se toca.
    """
    numero_caso = formulario.numero_caso.valor
    unida = _unir_si_es_hoja_del_mismo_caso(
        conexion, formulario, numero_caso, casos_de_esta_tanda
    )
    if unida is not None:
        return unida
    return _abrir_un_caso_para_esta_pagina(
        conexion, formulario, numero_caso, casos_de_esta_tanda
    )


def _unir_si_es_hoja_del_mismo_caso(
    conexion, formulario, numero_caso, casos_de_esta_tanda
):
    """El resultado de unir esta hoja al caso de su documento, o None si no procede.

    None significa las tres cosas que llevan al mismo sitio —esta pagina abre caso
    propio—: que no haya diccionario de tanda, que su numero no lo haya abierto
    ninguna hoja anterior de ESTE documento, o que lo haya abierto pero esta hoja lo
    contradiga.
    """
    if numero_caso is None or casos_de_esta_tanda is None:
        return None
    if numero_caso not in casos_de_esta_tanda:
        return None
    return _unir_a_un_caso_de_esta_tanda(
        conexion, casos_de_esta_tanda[numero_caso], numero_caso, formulario
    )


def _anotaciones_de_la_pagina_que_entra(
    conexion, formulario, numero_caso, duplicado_de, discrepancias
):
    """Todos los renglones que deja una pagina que SI entro. Puede no dejar ninguno.

    Se juntan aqui las tres cosas que una misma pagina puede tener que decir a la
    vez —lo que no se pudo leer, que repite a otro caso, y que no se unio a la hoja
    hermana—, porque hasta el 2026-09-03 habia UNA sola ranura y la segunda se
    perdia. En silencio, que es como se pierden.
    """
    anotaciones = _sola(diagnosticar_la_pagina(formulario)) + anotaciones_de_lo_dudoso(
        formulario
    )
    if duplicado_de is not None:
        anotaciones += (
            AnotacionDeIlegible(
                ENTRO_COMO_DUPLICADO,
                _detalle_del_duplicado(
                    conexion, duplicado_de, numero_caso, formulario
                ),
                formulario.lineas_leidas,
            ),
        )
    if discrepancias:
        anotaciones += _anotacion_de_la_hoja_que_no_se_unio(
            conexion, discrepancias, numero_caso, formulario
        )
    return anotaciones


def _abrir_un_caso_para_esta_pagina(
    conexion, formulario, numero_caso, casos_de_esta_tanda
):
    """Da de alta el caso de esta pagina, con su procedencia y sus personas.

    ⚠️ **El duplicado se busca ANTES del alta**, y el orden no es un detalle: si se
    buscara despues, el caso recien nacido se encontraria a si mismo y todo caso
    seria duplicado de si mismo.

    `hermana` es el caso que otra hoja de ESTE documento abrio con el mismo numero,
    cuando lo hay. Se lee antes del alta porque el alta puede escribir en el
    diccionario de la tanda, y entonces esta pagina se encontraria a si misma como
    su propia hermana.
    """
    hermana = (casos_de_esta_tanda or {}).get(numero_caso)
    discrepancias = (
        _discrepancias_entre_hojas(conexion, hermana, formulario)
        if hermana is not None
        else []
    )
    duplicado_de = _caso_del_que_es_duplicado(conexion, numero_caso, formulario)

    alta = alta_de_caso(
        conexion,
        numero_caso=numero_caso,
        unidad_numero=formulario.unidad_numero.valor,
        fecha_viaje=formulario.fecha_viaje.valor,
        captura_manual=1 if formulario.captura_manual else 0,
        ruta_pdf=formulario.ruta_pdf,
        pagina_pdf=_pagina_humana(formulario),
        unidad_nombre=formulario.unidad_nombre.valor,
        # ⚠️ El templo se guarda en la columna y **NO lleva fila de procedencia**,
        # que es la unica excepcion a lo que promete el encabezado de este modulo.
        # Va dicha aqui a proposito, no escondida.
        #
        # El motivo es que una fila de procedencia obliga a que ese campo se pueda
        # firmar, y firmarlo exige dibujarlo en la pantalla de correccion. Anadir un
        # quinto campo a esa pantalla no lo pidio el pase —pide leer el templo y
        # pintarlo en la cabecera del Excel del agente— y esa pantalla acaba de pasar
        # auditoria. Con procedencia y sin campo en pantalla el resultado seria peor
        # que no tenerlo: un campo que nadie puede firmar dejaria «Todo correcto»
        # bloqueado para siempre en todos los casos.
        #
        # Lo que cuesta, dicho para que nadie lo descubra despues: un templo mal
        # leido **no se puede corregir a mano** hoy. Queda en el informe de este
        # pase como lo que le falta a esta entrega.
        templo_nombre=formulario.templo_nombre.valor,
        duplicado_de=duplicado_de,
    )
    _guardar_procedencia_del_caso(conexion, alta.id, formulario)
    if casos_de_esta_tanda is not None and numero_caso is not None and hermana is None:
        # El None NUNCA entra en el diccionario de la tanda, y no es una
        # precaucion de estilo. Con la clave None, la segunda pagina sin numero de
        # un documento se uniria al caso de la primera: dos formularios de dos
        # familias distintas quedarian como un solo caso con las personas
        # revueltas, y nadie tendria forma de separarlos despues. «No se sabe el
        # numero» no es un numero comun.
        #
        # ⚠️ Y `hermana is None` es la otra mitad: la hoja que contradijo a la que
        # abrio el caso NO se queda con la clave. Si se la quedara, la hoja
        # siguiente se uniria a ella y la cadena acabaria fundiendo lo que este
        # cambio existe para separar. La primera hoja manda sobre su numero durante
        # todo el documento.
        casos_de_esta_tanda[numero_caso] = alta.id

    personas, avisos_de_las_personas = _guardar_las_personas(conexion, alta.id, formulario)
    return ResultadoDeLaPagina(
        importada=True,
        caso_nuevo=True,
        caso_id=alta.id,
        numero_caso=numero_caso,
        pagina_pdf=_pagina_humana(formulario),
        personas=personas,
        avisos=(
            tuple(alta.avisos)
            + avisos_de_las_personas
            + _aviso_de_sin_numero(numero_caso)
            + _aviso_de_duplicado(conexion, duplicado_de)
            + _aviso_de_la_hoja_aparte(discrepancias, numero_caso)
            + aviso_de_anclas_perdidas(formulario)
        ),
        motivo=None,
        pendiente_de_identificar=numero_caso is None,
        ilegibles=_anotaciones_de_la_pagina_que_entra(
            conexion, formulario, numero_caso, duplicado_de, discrepancias
        ),
        duplicado_de=duplicado_de,
    )


def _aviso_de_duplicado(conexion, duplicado_de):
    """Lo que el resumen de la importacion dice del caso que repite a otro.

    Nombra el id **y** el numero del caso original: desde la version 12 dos casos
    pueden llevar el mismo numero, asi que el numero solo no lleva a ninguna parte.
    """
    if duplicado_de is None:
        return ()
    original = leer_caso_por_id(conexion, duplicado_de) or {}
    numero = original.get("numero_caso") or "sin número de caso"
    return (
        f"este documento REPITE al caso n.º {duplicado_de} ({numero}), que NO se ha "
        "tocado. Entró igual, aparte y marcado como duplicado: nada se ha fundido "
        "solo. Mírelos y decida qué hacer con los dos.",
    )


def _aviso_de_la_hoja_aparte(discrepancias, numero_caso):
    """Lo que el resumen dice de la hoja que no se unio a su hermana."""
    if not discrepancias:
        return ()
    return (_aviso_de_la_hoja_que_no_se_unio(discrepancias, numero_caso),)


def _anotar_lo_que_no_entro_bien(conexion, formulario, resultado):
    """Deja los renglones de `documentos_ilegibles` de esta pagina. Pueden ser varios.

    Va aqui y no en `guardar_formulario` porque el renglon necesita el `caso_id`
    que acaba de nacer, y aqui ya esta. Y va en el camino comun del guardado y no
    en la pantalla: un renglon que hay que acordarse de escribir es un renglon que
    algun dia no se escribe, y entonces el documento ilegible se pierde otra vez —
    que es exactamente lo que esta tabla existe para impedir.

    Recorre una tupla y no escribe una sola fila desde el 2026-09-03: una pagina
    puede haber perdido etiquetas Y contradecir a la hoja que abrio el caso, y con
    una sola ranura la segunda cosa se perdia.
    """
    for anotacion in resultado.ilegibles:
        anotar_documento_ilegible(
            conexion,
            formulario.ruta_pdf,
            anotacion.motivo,
            pagina_pdf=_pagina_humana(formulario),
            detalle=anotacion.detalle,
            lineas_leidas=anotacion.lineas_leidas,
            caso_id=resultado.caso_id,
        )


def guardar_las_paginas_del_documento(conexion, formularios):
    """Guarda las paginas de UN documento, uniendo las que repiten numero de caso.

    Es el unico sitio donde nace el diccionario de la tanda, y por eso es el unico
    sitio donde una pagina se puede unir a otra: empieza vacio en cada documento,
    asi que un caso que ya estaba en la base de antes jamas entra en el.
    """
    casos_de_esta_tanda = {}
    resultados = []
    for formulario in formularios:
        resultado = guardar_formulario(conexion, formulario, casos_de_esta_tanda)
        _anotar_lo_que_no_entro_bien(conexion, formulario, resultado)
        resultados.append(resultado)
    return resultados

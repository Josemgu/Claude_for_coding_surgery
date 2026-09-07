"""Lo que un companero propone sobre una persona, y como se resuelve el par.

**Una propuesta no es una verificacion.** Es la regla permanente 5 escrita en
codigo: el companero devuelve su Excel, lo que trae se guarda con su nombre y su
fecha, y `procedencia_campo.verificado` sigue en 0 hasta que Miguel pulsa el boton.
Este modulo no escribe ni una vez en `procedencia_campo`.

⚠️ **El pase de la FASE 6 pedia guardar la propuesta «con su nombre en
`verificado_por`», y eso el esquema no lo admite.** Medido antes de escribir nada
(SQLite 3.50.4, esta maquina):

    UPDATE procedencia_campo SET verificado_por = 1 WHERE id = 1
    -> CHECK constraint failed:
       (verificado = 0 AND verificado_por IS NULL AND verificado_en IS NULL)
       OR (verificado = 1 AND verificado_por IS NOT NULL AND verificado_en IS NOT NULL)

Ese `CHECK` es la regla permanente 5 hecha estructura (`docs/ARQUITECTURA.md`
§2.7). Cumplir la letra del pase exigia poner `verificado = 1` porque llego un
Excel, que es exactamente marcar algo como verificado automaticamente. Se cumple la
regla permanente: la propuesta vive en cuatro columnas propias de `personas`
(version 5 del esquema) y `verificado_por` lo sigue rellenando solo Miguel.

**Lo que resuelve es la clave del paquete, y nunca el nombre** (`DECISIONES.md`,
2026-09-02). El nombre no es unico y un acento de mas crea un registro fantasma.

⚠️ **Y la clave dejo de ser `numero_caso` + `mrn` el 2026-09-03.** El motivo
estaba medido en el esquema —`UNIQUE (numero_caso)` en `casos` y
`UNIQUE (caso_id, mrn)` en `personas` encadenaban a una fila o a ninguna, nunca a
dos (`docs/ARQUITECTURA.md` §4)—, y ese `UNIQUE` **ya no existe**: la version 12
lo quito porque el numero de caso identifica una unidad y un mes, no una familia,
y rechazaba seis de cada diez documentos reales del dueno. La clave lleva ahora
tambien el **id del caso** (`paquete/columnas.armar_la_clave`), que es lo unico
que no se repite. Sin el, la fila que vuelve puede casar con dos personas de dos
familias distintas, y entonces **no se aplica a ninguna**: se descarta con su
motivo escrito, que es lo unico que no destruye trabajo ajeno.
"""

from datos.companeros import leer_companero
from datos.esquema import marca_de_tiempo
from datos.estados import ESTADOS_RECOMENDACION
from datos.pasos import COLUMNAS_QUE_RELLENA_EL_COMPANERO, PASOS
from datos.validacion import ErrorDeValidacion, validar_estado_recomendacion

# Tope de longitud de la nota del companero. Es un campo de texto libre que
# rellena otra persona en su Excel, fuera de este programa: sin tope, una columna
# entera pegada por error entra en la base y desborda la pantalla.
LARGO_MAXIMO_DE_LA_NOTA = 500

# ⚠️ Las siete columnas de la hoja del companero se escriben **letra por letra**
# dentro de cada instruccion de abajo, y no se arman uniendo
# `COLUMNAS_QUE_RELLENA_EL_COMPANERO`. No es descuido ni repeticion por gusto:
# `pruebas/auditoria_sql.py` lee el arbol de sintaxis y dictamina INSEGURA
# cualquier instruccion armada con una f-string o con un `+`, aunque lo que se
# pegue sea una constante nuestra. Una lista blanca que admite excepciones deja de
# ser una lista blanca.
#
# Lo que si comprueba `_exigir_que_las_columnas_cuadren` es que las dos listas —la
# de `datos.pasos` y la que se escribe aqui— sigan diciendo lo mismo. Si alguien
# anade un paso y se olvida de una de las instrucciones, esto levanta al importar
# el modulo en vez de guardar la propuesta a medias.


_COLUMNAS_ESCRITAS_A_MANO = (
    "paso_preparacion",
    "paso_informacion",
    "paso_cita_del_templo",
    "paso_acciones_requeridas",
    "paso_entrevistas",
    "paso_listo_para_el_templo",
    "llamo_al_lider",
)


def _exigir_que_las_columnas_cuadren():
    """Levanta si lo escrito en el SQL de aqui deja de coincidir con `datos.pasos`.

    Levanta al importar y no devuelve un aviso, por lo mismo que
    `datos/estados.py`: un programa que no arranca se arregla en un minuto, y una
    propuesta guardada a medias porque un paso nuevo no llego a su columna no se
    nota hasta que alguien no entra al templo.
    """
    if _COLUMNAS_ESCRITAS_A_MANO != COLUMNAS_QUE_RELLENA_EL_COMPANERO:
        raise ErrorDeValidacion(
            "Las columnas de la propuesta escritas en las instrucciones de "
            f"datos/propuestas.py son {_COLUMNAS_ESCRITAS_A_MANO} y las que declara "
            f"datos/pasos.py son {COLUMNAS_QUE_RELLENA_EL_COMPANERO}. Mientras no "
            "coincidan, lo que el compañero conteste en la hoja no llega entero a "
            "la base."
        )


_exigir_que_las_columnas_cuadren()


def validar_pasos(pasos):
    """Las siete respuestas del compañero, normalizadas a 1, 0 o None.

    Lo que no venga cuenta como sin contestar, y lo que venga con un nombre que no
    es de los siete levanta: una respuesta guardada en una columna que no es la
    suya es peor que una respuesta perdida, porque parece un dato bueno.
    """
    pasos = pasos or {}
    sobran = [nombre for nombre in pasos if nombre not in COLUMNAS_QUE_RELLENA_EL_COMPANERO]
    if sobran:
        raise ErrorDeValidacion(
            f"La propuesta trae respuestas que no son de la hoja del compañero: "
            f"{sobran}. Las que hay son {list(COLUMNAS_QUE_RELLENA_EL_COMPANERO)}."
        )
    normalizados = {}
    for nombre in COLUMNAS_QUE_RELLENA_EL_COMPANERO:
        valor = pasos.get(nombre)
        if valor is None:
            normalizados[nombre] = None
        elif valor in (0, 1, True, False):
            normalizados[nombre] = 1 if valor else 0
        else:
            raise ErrorDeValidacion(
                f"El paso '{nombre}' vale {valor!r}, y solo admite 1 (sí), 0 (no) o "
                "nulo (nadie lo ha mirado)."
            )
    return normalizados


def validar_estado_propuesto(estado):
    """Uno de los valores de `datos.estados`, o nulo.

    Se valida contra la MISMA lista que `casos.estado_recomendacion`, y no contra
    una copia: si el desplegable del Excel y esta comprobacion pudieran separarse,
    un valor que el companero puede elegir seria uno que la base rechaza.

    ⚠️ Esa lista tiene hoy DOS valores —`no_indicada` e `incompleta`— y el dueno no
    ha dicho los otros. No se inventan aqui (`DECISIONES.md`, P-1).
    """
    return validar_estado_recomendacion(estado)


def validar_nota(nota):
    """La nota libre del companero, sin espacios de sobra. El vacio queda en nulo."""
    if nota is None:
        return None
    if not isinstance(nota, str):
        nota = str(nota)
    limpia = nota.strip()
    if not limpia:
        return None
    if len(limpia) > LARGO_MAXIMO_DE_LA_NOTA:
        raise ErrorDeValidacion(
            f"El campo 'nota' no vale: se recibieron {len(limpia)} caracteres y el "
            f"máximo son {LARGO_MAXIMO_DE_LA_NOTA}. Una nota más larga que eso casi "
            "siempre es una columna entera pegada por error."
        )
    return limpia


def personas_que_casan_con_la_clave(conexion, numero_caso, mrn, caso_id=None):
    """TODAS las personas a las que puede referirse esa clave. Cero, una, o varias.

    ⚠️ **Devuelve una lista y no una persona, y esa es la novedad del 2026-09-03.**
    Mientras `numero_caso` fue `UNIQUE`, el par `numero_caso` + `mrn` encadenaba a
    una fila o a ninguna, nunca a dos. Desde la version 12 no lo es —el numero son
    cuatro letras mas el ano y el mes: identifica una unidad y un mes, no una
    familia— y ademas un documento duplicado entra en vez de rechazarse, asi que
    **dos casos pueden llevar el mismo numero y las mismas personas**. Escribir la
    propuesta del companero sobre «la primera que salga» seria escribirla sobre la
    familia equivocada sin que nadie se entere.

    Con `caso_id` —el que la clave del paquete lleva dentro desde ese dia— la
    respuesta vuelve a ser como mucho una: `UNIQUE (caso_id, mrn)` lo garantiza. Y
    entonces el numero de caso ya no se mira: si Miguel corrigio el numero despues
    de generar el paquete, el Excel del companero sigue diciendo el viejo, y exigir
    que coincidiera tiraria trabajo bueno.

    Un `mrn` vacio devuelve la lista vacia a proposito. En SQLite dos `NULL` no son
    iguales entre si, asi que un caso puede tener varias personas sin MRN sin violar
    `UNIQUE (caso_id, mrn)`; buscarlas por ese par devolveria varias filas o
    ninguna, y no hay forma de saber a cual se referia el companero.
    """
    if not mrn:
        return []
    if caso_id is not None:
        filas = conexion.execute(
            "SELECT p.id, p.caso_id, p.mrn, p.nombre, p.fila_formulario, "
            "       p.estado_propuesto, p.nota_companero, p.propuesto_por, p.propuesto_en, "
            "       p.paso_preparacion, p.paso_informacion, p.paso_cita_del_templo, "
            "       p.paso_acciones_requeridas, p.paso_entrevistas, "
            "       p.paso_listo_para_el_templo, p.llamo_al_lider, "
            "       c.numero_caso "
            "FROM personas p JOIN casos c ON c.id = p.caso_id "
            "WHERE p.caso_id = ? AND p.mrn = ?",
            (caso_id, mrn),
        ).fetchall()
        return [dict(fila) for fila in filas]
    if not numero_caso:
        return []
    filas = conexion.execute(
        "SELECT p.id, p.caso_id, p.mrn, p.nombre, p.fila_formulario, "
        "       p.estado_propuesto, p.nota_companero, p.propuesto_por, p.propuesto_en, "
        "       p.paso_preparacion, p.paso_informacion, p.paso_cita_del_templo, "
        "       p.paso_acciones_requeridas, p.paso_entrevistas, "
        "       p.paso_listo_para_el_templo, p.llamo_al_lider, "
        "       c.numero_caso "
        "FROM personas p JOIN casos c ON c.id = p.caso_id "
        "WHERE c.numero_caso = ? AND p.mrn = ? ORDER BY p.caso_id, p.id",
        (numero_caso, mrn),
    ).fetchall()
    return [dict(fila) for fila in filas]


def resolver_persona_por_par(conexion, numero_caso, mrn, caso_id=None):
    """La persona que identifica esa clave, o None si no existe **o si son varias**.

    Devuelve None —y no levanta— cuando el par no casa: la fila que no casa NO se
    inserta y va a la lista de descartados (`DECISIONES.md`, FASE 6), y eso es un
    resultado normal del trabajo, no un fallo del programa.

    ⚠️ **Tambien devuelve None cuando casa con VARIAS**, y eso es lo contrario de
    una comodidad: escoger una de dos escribiria el trabajo del companero sobre la
    familia equivocada. Quien necesite distinguir «ninguna» de «varias» —para poder
    decirlo en el motivo del descarte— llama a `personas_que_casan_con_la_clave`.
    """
    casan = personas_que_casan_con_la_clave(conexion, numero_caso, mrn, caso_id)
    return casan[0] if len(casan) == 1 else None


def propuesta_vigente(conexion, persona_id):
    """Lo que hay escrito hoy sobre esa persona, o None si no hay nada ni nadie.

    «Nada» es que no haya un `propuesto_por`: la propuesta puede traer estado, nota
    o las dos, pero lo que decide si hay algo que respetar es que la firme alguien.
    Una persona que no existe devuelve None igual que una sin propuesta —quien
    llama pregunta «¿hay algo debajo?» y la respuesta es no en los dos casos—; que
    la persona exista lo comprueba el `UPDATE`, que es quien lo necesita.
    """
    fila = conexion.execute(
        "SELECT p.estado_propuesto, p.nota_companero, p.propuesto_por, p.propuesto_en, "
        "       p.paso_preparacion, p.paso_informacion, p.paso_cita_del_templo, "
        "       p.paso_acciones_requeridas, p.paso_entrevistas, "
        "       p.paso_listo_para_el_templo, p.llamo_al_lider, "
        "       co.nombre AS nombre_del_companero "
        "FROM personas p LEFT JOIN companeros co ON co.id = p.propuesto_por "
        "WHERE p.id = ? AND p.propuesto_por IS NOT NULL",
        (persona_id,),
    ).fetchone()
    return dict(fila) if fila is not None else None


def _describir_la_propuesta(vigente):
    """Lo que dijo el companero anterior, en una frase que se pueda leer."""
    partes = []
    if vigente["estado_propuesto"] is not None:
        partes.append(f"estado «{vigente['estado_propuesto']}»")
    if vigente["nota_companero"] is not None:
        partes.append(f"nota «{vigente['nota_companero']}»")
    contestados = sum(1 for nombre, _ in PASOS if vigente.get(nombre) is not None)
    if contestados:
        partes.append(f"{contestados} de los {len(PASOS)} pasos contestados")
    return " y ".join(partes) if partes else "una propuesta sin estado y sin nota"


def _exigir_que_no_pise_a_otro(conexion, persona_id, companero_id):
    """Levanta si debajo hay una propuesta de OTRO companero. Devuelve None.

    Es la mitad de datos del hallazgo ALTO de la auditoria final de QA: dos
    companeros sobre el mismo caso y la propuesta del primero borrada sin rastro.
    Medido antes de escribir esto (esta maquina, 2026-09-02):

        tras A: [('incompleta', 'A dice: falta la firma del obispo', 'Companero A')]
        tras B: [('no_indicada', 'B dice: todo bien',                'Companero B')]
        QUEDA RASTRO DE LO QUE DIJO A EN ALGUN SITIO DE LA BASE: 0 filas

    Se rechaza en vez de conservar las dos, y el motivo es que las dos no caben:
    las cuatro columnas de la propuesta son UNA por persona (version 5 del
    esquema), y guardar dos exigiria una tabla de historial que nadie ha pedido y
    que el dueno no ha decidido. Rechazar conserva lo de A **y** se lo dice a
    Miguel, que son las dos cosas que el pase pedia; lo de B no se pierde, sigue
    en el Excel de B, y ahora Miguel sabe que hay dos versiones y cual eligio.

    El mismo companero SI escribe encima de lo suyo: corregir su nota y volver a
    mandar el archivo es trabajo normal, y `paquete/reconciliacion.py` promete que
    reconciliar dos veces el mismo Excel no rompe nada.

    ⚠️ **Lo que este arreglo deja abierto, dicho aqui para que no sorprenda:** hoy
    no hay ninguna forma de sustituir la propuesta de un companero por la de otro
    —no existe ninguna funcion que la retire, ni ningun boton—. Si la buena resulta
    ser la segunda, Miguel se queda sin donde pulsar. Es peor quedarse sin boton
    que perder el dato de una persona real sin enterarse, asi que se cierra asi y
    la decision vuelve al dueno junto con la otra que falta: si un caso puede
    llevarlo mas de una persona a la vez. El mensaje de abajo **no nombra ningun
    boton que no exista**.
    """
    vigente = propuesta_vigente(conexion, persona_id)
    if vigente is None or vigente["propuesto_por"] == companero_id:
        return
    raise ErrorDeValidacion(
        f"Esta persona ya trae una propuesta de «{vigente['nombre_del_companero']}» "
        f"del {vigente['propuesto_en']}: {_describir_la_propuesta(vigente)}. "
        "NO se ha escrito nada encima: lo que dijo el primer compañero se conserva "
        "tal cual, y lo que traía esta fila NO se ha guardado. La fila queda en la "
        "lista de descartados con este mismo motivo, y lo que decía sigue en el "
        "Excel del segundo compañero. Decida usted cuál de las dos vale."
    )


def guardar_propuesta(conexion, persona_id, estado_propuesto, nota, companero_id, pasos=None):
    """Guarda lo que un companero propone sobre una persona. Devuelve `persona_id`.

    `pasos` son las siete respuestas de la hoja —los seis pasos de «Preparación
    para las ordenanzas» y si llamó al líder— como diccionario de nombre de columna
    a 1, 0 o None. Es opcional para que quien solo tenga un estado y una nota
    —`interfaz/mapeo.py`, un Excel libre de Miguel— siga llamando igual; lo que no
    venga se escribe a nulo, que es lo que significa «nadie lo ha mirado».

    Escribe encima de lo que el MISMO companero hubiera propuesto antes: la ronda
    que acaba de llegar es la suya y es la vigente, y un historial de propuestas es
    una tabla que nadie ha pedido.

    ⚠️ **Lo que NO hace es pisar la propuesta de OTRO companero**: si debajo hay
    algo firmado por otra persona, esto levanta `ErrorDeValidacion` y no escribe.
    Antes lo pisaba en silencio, y QA lo midio en la auditoria final: A escribia
    «falta la firma del obispo», B escribia «todo bien», y Miguel solo veia a B.
    El motivo entero esta en `_exigir_que_no_pise_a_otro`.

    Quien llama no tiene que hacer nada nuevo: `paquete/reconciliacion.py` ya
    atrapa `ErrorDeValidacion` por fila y la manda a la lista de descartados con
    su motivo, que es donde Miguel la lee.

    ⚠️ **No toca `casos.estado_recomendacion`.** El estado del caso lo escribe
    Miguel desde la pantalla de correccion, mirando lo que el companero propuso.
    Copiarlo aqui seria dar por bueno un dato porque llego en un archivo.
    """
    if leer_companero(conexion, companero_id) is None:
        raise ErrorDeValidacion(
            f"No se puede guardar una propuesta sin decir quién la hace: no hay "
            f"ningún compañero con el id {companero_id!r}."
        )
    _exigir_que_no_pise_a_otro(conexion, persona_id, companero_id)
    respuestas = validar_pasos(pasos)
    conexion.execute(
        "UPDATE personas SET estado_propuesto = ?, nota_companero = ?, "
        "propuesto_por = ?, propuesto_en = ?, "
        "paso_preparacion = ?, paso_informacion = ?, paso_cita_del_templo = ?, "
        "paso_acciones_requeridas = ?, paso_entrevistas = ?, "
        "paso_listo_para_el_templo = ?, llamo_al_lider = ? "
        "WHERE id = ?",
        (
            validar_estado_propuesto(estado_propuesto),
            validar_nota(nota),
            companero_id,
            marca_de_tiempo(),
            respuestas["paso_preparacion"],
            respuestas["paso_informacion"],
            respuestas["paso_cita_del_templo"],
            respuestas["paso_acciones_requeridas"],
            respuestas["paso_entrevistas"],
            respuestas["paso_listo_para_el_templo"],
            respuestas["llamo_al_lider"],
            persona_id,
        ),
    )
    return persona_id


def propuestas_del_caso(conexion, caso_id):
    """Lo que los companeros propusieron sobre las personas de un caso.

    Solo devuelve las personas sobre las que un companero contesto algo: una lista
    donde la mayoria de las filas estan vacias esconde las dos que no lo estan.

    ⚠️ **El filtro es `propuesto_por IS NOT NULL`, y antes era «tiene estado o tiene
    nota».** Se cambio al traer los seis pasos, y el motivo es medible: una persona
    con los seis pasos contestados que si, y por tanto lista, se guarda con
    `estado_propuesto` a NULL —«completa» no es un valor que nadie haya decidido
    todavia (`DECISIONES.md`, P-1)— y con el filtro viejo se caia de esta lista sin
    dejar rastro. Quien firma la propuesta es lo que dice que alguien la miro.
    """
    filas = conexion.execute(
        "SELECT p.id, p.mrn, p.nombre, p.fila_formulario, p.estado_propuesto, "
        "       p.nota_companero, p.propuesto_por, p.propuesto_en, "
        "       p.paso_preparacion, p.paso_informacion, p.paso_cita_del_templo, "
        "       p.paso_acciones_requeridas, p.paso_entrevistas, "
        "       p.paso_listo_para_el_templo, p.llamo_al_lider, "
        "       co.nombre AS nombre_del_companero "
        "FROM personas p LEFT JOIN companeros co ON co.id = p.propuesto_por "
        "WHERE p.caso_id = ? AND p.propuesto_por IS NOT NULL "
        "ORDER BY p.fila_formulario, p.id",
        (caso_id,),
    ).fetchall()
    return [dict(fila) for fila in filas]


def estados_propuestos_del_caso(conexion, caso_id):
    """Los estados distintos que se propusieron para un caso, sin repetir.

    Existe para que la pantalla pueda avisar cuando NO hay uno solo: un caso cuyas
    personas vuelven con `incompleta` y `no_indicada` a la vez no tiene un estado
    que copiar, y elegir uno por el programa seria inventar. Devuelve la lista y
    quien la mire decide.
    """
    return sorted(
        {
            propuesta["estado_propuesto"]
            for propuesta in propuestas_del_caso(conexion, caso_id)
            if propuesta["estado_propuesto"] is not None
        },
        key=lambda estado: ESTADOS_RECOMENDACION.index(estado)
        if estado in ESTADOS_RECOMENDACION
        else len(ESTADOS_RECOMENDACION),
    )

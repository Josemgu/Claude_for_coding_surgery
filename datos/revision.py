"""Lo que la pantalla «Revisar» lee: una fila por documento, con todo lo de su tarjeta.

Esto es la pantalla sin interfaz: funciones que devuelven datos. Ni una ventana, ni
un color, ni una frase de pantalla — el mismo reparto que `datos/calendario.py`.

**Una sola consulta para toda la lista, y no una por tarjeta.** El dueno lo puso en
numeros el 2026-09-03: «imagina que tenga 3000 formularios». Contar las personas, los
campos por comprobar y la asignacion con una subconsulta correlacionada por documento
—que es como lo hace hoy `datos/pendientes.py`, y con razon, porque alli son pocos—
serian 9 000 recorridos. Lo que hay aqui son TRES agrupaciones que el motor resuelve
una vez cada una y cruza por `caso_id`, mas tres `LEFT JOIN` por clave. Medido en
`pruebas/prueba_revision.py`: 3 000 documentos leidos con sus siete cuentas en
**0,141 s**.

**Aqui no se marca nada.** Las escrituras viven en `datos/marcas_de_revision.py`.

**«Hoy» entra como parametro**, igual que en `datos/calendario.py` y por lo mismo:
una funcion que mira el reloj por dentro no se puede probar por los bordes, y el
borde —la fecha que ya paso— es justo lo que hay que probar.
"""

from datetime import date, datetime

from datos.estados import COMPLETA, NO_COMPLETA

# Los tres estados de una tarjeta, que NO son los de la base: `SIN_REVISAR` es lo que
# se ve cuando `estado_recomendacion` esta vacio. Van aqui, en el modulo que los
# reparte, y no repetidos en la interfaz.
SIN_REVISAR = "sin_revisar"

# Los filtros de la barra, con el nombre con el que se piden. El orden es el de la
# pantalla. `completas` es ademas el tablero de completados: es el mismo conjunto.
FILTROS = (
    "todo",
    "sin_revisar",
    "incompletas",
    "completas",
    "sin_asignar",
    "el_companero_dice_lista",
    "fecha_pasada",
)

_CONSULTA_DE_LOS_DOCUMENTOS = """
SELECT c.id, c.numero_caso, c.ruta_pdf, c.pagina_pdf, c.fecha_viaje,
       c.unidad_numero, c.unidad_nombre, c.templo_nombre, c.archivado,
       c.duplicado_de, c.estado_recomendacion,
       c.estado_marcado_en, c.estado_marcado_origen,
       c.estado_del_companero, c.estado_del_companero_en,
       cm.nombre AS estado_marcado_por_nombre,
       cc.nombre AS estado_del_companero_por_nombre,
       original.numero_caso AS numero_del_original,
       pe.personas, pe.con_propuesta, pe.con_algun_no, pe.con_los_seis_si,
       pe.hoja_primera, pe.hoja_ultima, pe.texto_de_personas,
       pr.campos, pr.campos_verificados,
       asg.asignado_a, asg.companeros_vivos
FROM casos c
LEFT JOIN companeros cm ON cm.id = c.estado_marcado_por
LEFT JOIN companeros cc ON cc.id = c.estado_del_companero_por
LEFT JOIN casos original ON original.id = c.duplicado_de
LEFT JOIN (
    SELECT p.caso_id,
           COUNT(*) AS personas,
           SUM(CASE WHEN p.propuesto_por IS NOT NULL THEN 1 ELSE 0 END) AS con_propuesta,
           SUM(CASE WHEN p.paso_preparacion = 0 OR p.paso_informacion = 0
                      OR p.paso_cita_del_templo = 0 OR p.paso_acciones_requeridas = 0
                      OR p.paso_entrevistas = 0 OR p.paso_listo_para_el_templo = 0
                    THEN 1 ELSE 0 END) AS con_algun_no,
           SUM(CASE WHEN p.paso_preparacion = 1 AND p.paso_informacion = 1
                     AND p.paso_cita_del_templo = 1 AND p.paso_acciones_requeridas = 1
                     AND p.paso_entrevistas = 1 AND p.paso_listo_para_el_templo = 1
                    THEN 1 ELSE 0 END) AS con_los_seis_si,
           MIN(p.pagina_pdf) AS hoja_primera,
           MAX(p.pagina_pdf) AS hoja_ultima,
           GROUP_CONCAT(COALESCE(p.nombre, '') || ' ' || COALESCE(p.mrn, ''), ' ')
               AS texto_de_personas
    FROM personas p GROUP BY p.caso_id
) pe ON pe.caso_id = c.id
LEFT JOIN (
    SELECT caso_id, COUNT(*) AS campos, SUM(verificado) AS campos_verificados
    FROM (
        SELECT pc.registro_id AS caso_id, pc.verificado
        FROM procedencia_campo pc WHERE pc.tabla = 'casos'
        UNION ALL
        SELECT p.caso_id, pc.verificado
        FROM procedencia_campo pc JOIN personas p ON p.id = pc.registro_id
        WHERE pc.tabla = 'personas'
    ) GROUP BY caso_id
) pr ON pr.caso_id = c.id
LEFT JOIN (
    SELECT a.caso_id, MIN(co.nombre) AS asignado_a, COUNT(*) AS companeros_vivos
    FROM asignaciones a JOIN companeros co ON co.id = a.companero_id
    WHERE a.activa = 1 GROUP BY a.caso_id
) asg ON asg.caso_id = c.id
ORDER BY c.fecha_viaje IS NULL, c.fecha_viaje, c.id
"""


def _a_texto_de_fecha(hoy):
    """Hoy en 'AAAA-MM-DD', acepte un `date`, un `datetime` o ya el texto.

    Un `datetime` se recorta a su dia: con la hora pegada, toda comparacion contra
    `fecha_viaje` —que es 'AAAA-MM-DD' pelado— saldria corrida sin que nadie lo note.
    """
    if isinstance(hoy, datetime):
        return hoy.date().isoformat()
    if isinstance(hoy, date):
        return hoy.isoformat()
    return str(hoy)


def estado_propuesto_del_documento(fila):
    """Que dijo la hoja del companero de ESE documento: completa, no completa o nada.

    Es `datos.pasos.estado_de_los_pasos` subido de la persona al documento, con la
    misma logica de tres valores y en el mismo orden:

      - si alguna persona trae algun paso en «No», el documento no esta completo;
      - si TODAS las personas traen los seis en «Sí», esta completo;
      - en cualquier otro caso no se sabe, y **no se sabe no es «no»**: una casilla
        en blanco es una pregunta que nadie miro.

    Un documento del que no volvio ninguna propuesta devuelve None igual, y quien
    llama lo separa mirando `con_propuesta`: no es lo mismo «no lo devolvieron» que
    «lo devolvieron a medias», aunque las dos se queden sin marcar.
    """
    if not fila["con_propuesta"]:
        return None
    if fila["con_algun_no"]:
        return NO_COMPLETA
    if fila["personas"] and fila["con_los_seis_si"] == fila["personas"]:
        return COMPLETA
    return None


def _hojas_del_documento(fila):
    """El rango de hojas del PDF que ocupa el documento, o None si no se sabe.

    No hay ninguna columna con el rango y no hace falta: sale del minimo y el maximo
    entre la hoja que abrio el caso (`casos.pagina_pdf`) y las de sus personas
    (`personas.pagina_pdf`). De un documento importado antes de la version 4 nadie
    guardo la hoja de cada persona, y entonces esto devuelve None en vez de inventar.
    """
    hojas = [
        hoja
        for hoja in (fila["pagina_pdf"], fila["hoja_primera"], fila["hoja_ultima"])
        if hoja is not None
    ]
    return (min(hojas), max(hojas)) if hojas else None


def _documento_de_la_fila(fila, hoy):
    """Una fila cruda convertida en lo que la tarjeta necesita, ya calculado."""
    documento = dict(fila)
    documento["personas"] = fila["personas"] or 0
    documento["campos"] = fila["campos"] or 0
    documento["campos_verificados"] = fila["campos_verificados"] or 0
    documento["por_comprobar"] = documento["campos"] - documento["campos_verificados"]
    documento["hojas"] = _hojas_del_documento(fila)
    documento["estado_de_la_tarjeta"] = fila["estado_recomendacion"] or SIN_REVISAR
    documento["propuesta_del_companero"] = estado_propuesto_del_documento(fila)
    documento["sin_devolver"] = not fila["con_propuesta"]
    documento["sin_asignar"] = not fila["companeros_vivos"]
    documento["es_duplicado"] = fila["duplicado_de"] is not None
    documento["fecha_pasada"] = (
        fila["fecha_viaje"] is not None and fila["fecha_viaje"] < hoy
    )
    return documento


def documentos_para_revisar(conexion, hoy):
    """Todos los documentos, uno por fila, con lo que se ve en su tarjeta.

    **Entran tambien los archivados**, al reves que `datos/calendario.py` y
    `datos/pendientes.py`. No es un olvido del filtro: el mockup de esta pantalla
    dibuja la tarjeta archivada con sus dos botones inactivos y su palabra
    «archivado», y esconderla haria imposible ver que un documento se archivo.
    Quien no los quiera, filtra por `archivado`.

    Orden: lo que viaja antes, primero; lo que no tiene fecha, al final —donde lo
    pondria al reves el orden natural de SQLite, porque NULL ordena antes que
    cualquier texto—. Un documento al que ni la fecha se le leyo hay que atenderlo,
    pero no antes que uno que viaja el martes.
    """
    hoy = _a_texto_de_fecha(hoy)
    filas = conexion.execute(_CONSULTA_DE_LOS_DOCUMENTOS).fetchall()
    return [_documento_de_la_fila(fila, hoy) for fila in filas]


def casa_con(documento, texto):
    """Si ese documento casa con lo escrito en el buscador.

    Busca en el numero de caso, la unidad —numero y nombre—, el nombre del archivo y
    el nombre y el MRN de cada persona. Sin distinguir mayusculas.

    ⚠️ **No hay ninguna columna «cedula» en el esquema**, y el pase la nombra. Lo que
    identifica a una persona aqui es el MRN, y es en el MRN donde se busca. Si hace
    falta la cedula dominicana, es una columna nueva y su migracion.
    """
    if not texto:
        return True
    aguja = texto.strip().lower()
    if not aguja:
        return True
    pajar = " ".join(
        str(valor)
        for valor in (
            documento["numero_caso"],
            documento["unidad_numero"],
            documento["unidad_nombre"],
            documento["ruta_pdf"],
            documento["texto_de_personas"],
        )
        if valor
    ).lower()
    return aguja in pajar


def _pasa_el_filtro(documento, filtro):
    """Si ese documento entra en ese filtro. Uno solo, y `todo` no filtra nada."""
    if filtro == "todo":
        return True
    if filtro == "sin_revisar":
        return documento["estado_de_la_tarjeta"] == SIN_REVISAR
    if filtro == "incompletas":
        return documento["estado_de_la_tarjeta"] == NO_COMPLETA
    if filtro == "completas":
        return documento["estado_de_la_tarjeta"] == COMPLETA
    if filtro == "sin_asignar":
        return documento["sin_asignar"]
    if filtro == "el_companero_dice_lista":
        # Lo que falta por confirmar: el companero dijo que si y el documento
        # todavia no esta marcado como completo. Uno ya marcado no es trabajo.
        return (
            documento["propuesta_del_companero"] == COMPLETA
            and documento["estado_de_la_tarjeta"] != COMPLETA
        )
    if filtro == "fecha_pasada":
        return documento["fecha_pasada"]
    raise ValueError(
        f"No hay ningún filtro {filtro!r} en la pantalla «Revisar». Los que hay son: "
        + ", ".join(FILTROS)
    )


def filtrar(documentos, filtro="todo", texto=""):
    """Los documentos que pasan ese filtro y casan con lo buscado.

    El filtro y la busqueda se cruzan, no se excluyen: buscar dentro de «Completas»
    deja las completas que ademas casan.
    """
    return [
        documento
        for documento in documentos
        if _pasa_el_filtro(documento, filtro) and casa_con(documento, texto)
    ]


def cuentas_de_los_filtros(documentos):
    """Cuantos documentos hay en cada filtro, para las cifras de las pastillas.

    Se calcula sobre la lista ya leida y no con siete consultas mas: son las mismas
    filas y el motor no tiene por que recorrer la tabla siete veces.
    """
    return {filtro: len(filtrar(documentos, filtro)) for filtro in FILTROS}

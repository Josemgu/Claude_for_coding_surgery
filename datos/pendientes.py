"""La lista de trabajo: casos que entraron y que nadie ha dado por buenos todavia.

Es la cuarta consulta de la FASE 5, y va aparte de `datos/calendario.py` porque no
pregunta lo mismo. El calendario pregunta *cuando* viaja un caso; esto pregunta
*si alguien ya lo miro*. Ninguna de las dos necesita a la otra.

⚠️ No confundir con `PENDIENTES.md`, que es el documento del planificador. Aqui
«pendiente» significa una sola cosa: un caso al que le quedan campos sin verificar.

**Que cuenta como verificado.** Lo dice `procedencia_campo`, y solo esa tabla: un
campo esta verificado cuando lleva `verificado = 1` con quien y cuando, que es la
regla permanente 5 hecha estructura. Un caso esta pendiente mientras le quede
**aunque sea un campo** sin verificar, contando los suyos y los de sus personas.

**Y un caso sin ninguna fila de procedencia tambien esta pendiente.** Ese es el
que un `WHERE verificado = 0` dejaria fuera: cero filas sin verificar no es lo
mismo que todo verificado. Es justo el caso de un alta a mano, o de un caso que
entro antes de que se guardara la procedencia. Por eso la consulta cuenta cuantos
campos hay y cuantos estan verificados, en vez de buscar campos sin verificar.

Nada de esto marca nada: solo lee. Nada se da por verificado aqui ni en ninguna
otra parte del programa sin que Miguel lo confirme.
"""

from datos.estados import recomendacion_sin_resolver


def casos_pendientes_de_verificar(conexion):
    """Los casos sin archivar a los que les quedan campos por verificar.

    Ordenados por fecha de viaje ascendente: lo que viaja primero se atiende
    primero. Los casos **sin** fecha de viaje van al final, no al principio —que es
    donde los pondria el orden natural de SQLite, porque `NULL` ordena antes que
    cualquier texto—. Un caso al que ni siquiera se le leyo la fecha hay que
    atenderlo, pero no antes que uno que viaja el martes.

    Cada fila trae `campos`, `campos_verificados` y `campos_pendientes` para que se
    pueda decir «faltan 3 de 12» sin volver a consultar, y `sin_ninguna_lectura`
    para distinguir el caso a medio verificar del que nadie ha tocado.

    El filtro `archivado = 0` va escrito desde el primer dia, igual que en las tres
    consultas del calendario y por el mismo motivo (`PENDIENTES.md`, FASE 5,
    criterio 6).
    """
    filas = conexion.execute(
        "SELECT * FROM ("
        "  SELECT c.id, c.numero_caso, c.unidad_numero, c.unidad_nombre, c.fecha_viaje, "
        "         c.estado_recomendacion, c.captura_manual, c.creado_en, "
        # `duplicado_de` viaja en la fila desde el 2026-09-03: un caso que repite a
        # otro tiene que verse EN LA LISTA y no solo al abrirlo. Si hay que entrar
        # para enterarse, con cincuenta pendientes nadie entra.
        "         c.duplicado_de, "
        "         (SELECT COUNT(*) FROM personas p WHERE p.caso_id = c.id) AS personas, "
        "         (SELECT COUNT(*) FROM procedencia_campo pc "
        "            WHERE (pc.tabla = 'casos' AND pc.registro_id = c.id) "
        "               OR (pc.tabla = 'personas' AND pc.registro_id IN "
        "                   (SELECT p2.id FROM personas p2 WHERE p2.caso_id = c.id))"
        "         ) AS campos, "
        "         (SELECT COUNT(*) FROM procedencia_campo pc "
        "            WHERE pc.verificado = 1 "
        "              AND ((pc.tabla = 'casos' AND pc.registro_id = c.id) "
        "                OR (pc.tabla = 'personas' AND pc.registro_id IN "
        "                    (SELECT p2.id FROM personas p2 WHERE p2.caso_id = c.id)))"
        "         ) AS campos_verificados "
        "  FROM casos c "
        "  WHERE c.archivado = 0"
        ") "
        "WHERE campos = 0 OR campos_verificados < campos "
        "ORDER BY fecha_viaje IS NULL, fecha_viaje, numero_caso"
    ).fetchall()

    pendientes = []
    for fila in filas:
        caso = dict(fila)
        caso["campos_pendientes"] = caso["campos"] - caso["campos_verificados"]
        caso["sin_ninguna_lectura"] = caso["campos"] == 0
        caso["recomendacion_sin_resolver"] = recomendacion_sin_resolver(
            caso["estado_recomendacion"]
        )
        pendientes.append(caso)
    return pendientes

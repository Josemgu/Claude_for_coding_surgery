"""Los motivos de aviso del panel, contados y agrupados, no listados uno a uno.

Existe por la regla del dueno del 2026-09-03: *«Los avisos iguales se agrupan:
cinco páginas con el mismo problema son un aviso con "× 5 páginas", no cinco
bloques.»* La agrupacion no se hace pintando: **se hace contando**, aqui, y la
pantalla solo recibe tres numeros. Una pantalla que recibe la lista entera acaba
tarde o temprano dibujando un renglon por elemento, que es justo la captura que el
dueno rechazo.

Los tres motivos son los que el mockup pone en la franja, y ni uno mas: documentos
duplicados sin decidir, hojas que no se pudieron leer, y documentos sin fecha de
viaje.

**El tercero no esta en el mockup y se anade a proposito, con su motivo.** El
panel del mockup no tiene bloque para los documentos sin fecha, y el panel viejo
si los pintaba en ambar. Un documento sin fecha **no puede salir en el calendario
ni en la ventana de siete dias** —no hay con que compararlo—, asi que si tampoco
sale aqui no sale en ningun sitio del panel, y un documento invisible es
exactamente el que se pierde. Va como aviso de una linea, que es lo que la regla
permite, en vez de como bloque.
"""

from datos.ilegibles import contar_documentos_ilegibles


def contar_duplicados_sin_decidir(conexion):
    """Documentos vivos marcados como repeticion de otro.

    `duplicado_de` entro con la version 13 del esquema: un documento que repite a
    otro ENTRA igual y queda marcado, nunca se pisa el que ya estaba
    (`DECISIONES.md`, 2026-09-03, P-5: «un duplicado se avisa, no se rechaza»).
    Mientras nadie decida que hacer con el, cuenta aqui.
    """
    return conexion.execute(
        "SELECT COUNT(*) FROM casos WHERE archivado = 0 AND duplicado_de IS NOT NULL"
    ).fetchone()[0]


def contar_sin_fecha_de_viaje(conexion):
    """Documentos vivos a los que no se les leyo la fecha.

    Nunca se les inventa una: sin fecha no se puede decir si son urgentes, y
    ponerles la de hoy seria afirmar que viajan hoy, que es justo lo que no se
    sabe.
    """
    return conexion.execute(
        "SELECT COUNT(*) FROM casos WHERE archivado = 0 AND fecha_viaje IS NULL"
    ).fetchone()[0]


def motivos_de_aviso(conexion):
    """Los tres motivos con su cuenta, ya sin los que valen cero.

    Devuelve una lista de `(texto, cuenta)`. Los de cuenta 0 no salen: un aviso
    que dice «0 hojas ilegibles» es ruido, y la regla del dueno es que la franja
    ocupe lo minimo.

    **Cada texto lleva dentro su propio sustantivo** —«documentos duplicados…»,
    «hojas que no se pudieron leer»— en vez de traer la unidad en un campo aparte.
    Asi la linea se arma pegando la cuenta delante y ya se lee sola: «3 documentos
    duplicados sin decidir».

    Y hay un segundo motivo, menos obvio: `pruebas/auditoria_rutas.py` prohibe que
    exista en el codigo un literal que sea **exactamente** «documentos», porque asi
    es como se compondria a mano la ruta de la carpeta de datos —y en esta maquina
    hay tres carpetas con ese nombre, dos de ellas sincronizando a la nube con MRN
    de personas reales (`DECISIONES.md`, 2026-09-02). Un campo `unidad` con ese
    valor suelto era justo ese literal. La frase entera no lo es.
    """
    motivos = (
        ("documentos duplicados sin decidir", contar_duplicados_sin_decidir(conexion)),
        ("hojas que no se pudieron leer", contar_documentos_ilegibles(conexion)),
        ("documentos sin fecha de viaje", contar_sin_fecha_de_viaje(conexion)),
    )
    return [motivo for motivo in motivos if motivo[1]]

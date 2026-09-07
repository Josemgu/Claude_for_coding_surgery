"""Las filas del Excel de un compañero que NO entraron: un renglón por cada una.

Es la misma decisión que `datos/ilegibles.py`, aplicada al otro extremo del
circuito. Allí se guarda lo que no se pudo LEER de un PDF; aquí, lo que VOLVIÓ del
compañero y no se pudo aplicar. La pregunta que las dos contestan es la misma —«¿qué
no entró y por qué?»— y la respuesta tiene que sobrevivir a que alguien cierre una
ventana.

**Lo pidió esta tabla la propia pantalla que la echaba de menos.**
`interfaz/descartados.py` lleva escrito desde que se escribió: «no se guarda en la
base. Se ve mientras la ventana está abierta [...] si quiere que los descartes
sobrevivan al cierre del programa, es una tabla nueva y su migración». El dueño lo
decidió tras la auditoría final de QA (2026-09-03), y esta es esa tabla.

**El daño que cierra, medido por QA:** en una ida y vuelta de seis perfiles, la
persona sin MRN volvió con 0 de 7 pasos. Que se descarte es lo correcto —casar por
nombre crea registros fantasma (`DECISIONES.md`, 2026-09-02)—, pero hasta hoy la
lista de lo descartado se perdía al cerrar la ventana, y con ella la única pista de
que había trabajo hecho que nadie recogió.

**El motivo se guarda como FRASE y no como código**, al revés que en
`datos/ilegibles.py`. No es una incoherencia: allí hay siete motivos cerrados que
se pueden contar —«de 500 documentos, cuántos no se abrieron»—; aquí el motivo
lleva dentro el número de la fila del Excel con la que chocó y su causa concreta,
que es exactamente lo que hace falta para ir a mirar esa fila. Un código perdería
justo eso.

Nada de este módulo aplica nada ni toca una persona: solo anota y lee.
"""

from datos.esquema import marca_de_tiempo


def anotar_fila_descartada(
    conexion, companero_id, motivo, ruta_excel=None, fila_excel=None,
    numero_caso=None, mrn=None, nombre=None,
):
    """Deja el renglón de una fila que volvió y no entró. Devuelve el id de la fila.

    `fila_excel` es el número de fila DEL EXCEL, en base 1 y tal como lo enseña
    Excel: es lo que permite abrir el archivo e ir a mirarla, que es la única forma
    de decidir sobre ella.
    """
    cursor = conexion.execute(
        "INSERT INTO filas_descartadas (companero_id, ruta_excel, fila_excel, "
        "numero_caso, mrn, nombre, motivo, registrado_en) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
        (
            companero_id,
            None if ruta_excel is None else str(ruta_excel),
            fila_excel,
            numero_caso,
            mrn,
            nombre,
            motivo,
            marca_de_tiempo(),
        ),
    )
    return cursor.lastrowid


def filas_descartadas(conexion):
    """Todos los renglones, el más reciente primero, con el nombre del compañero.

    El más reciente primero por lo mismo que en `datos/ilegibles.py`: lo que se abre
    esta lista a mirar es «qué pasó con el Excel que acabo de cargar», y esa es la
    de arriba.
    """
    return conexion.execute(
        "SELECT d.id, d.companero_id, d.ruta_excel, d.fila_excel, d.numero_caso, "
        "       d.mrn, d.nombre, d.motivo, d.registrado_en, c.nombre AS companero "
        "FROM filas_descartadas d "
        "LEFT JOIN companeros c ON c.id = d.companero_id "
        "ORDER BY d.registrado_en DESC, d.id DESC"
    ).fetchall()


def contar_filas_descartadas(conexion):
    """Cuántos renglones hay, para poder decirlo sin traerlos todos."""
    return conexion.execute("SELECT COUNT(*) FROM filas_descartadas").fetchone()[0]


def anotar_las_descartadas_de_la_vuelta(conexion, descartadas, companero_id, ruta_excel=None):
    """Guarda de golpe las descartadas de una carga. Devuelve cuántas anotó.

    Recibe las `FilaDescartada` que arma `paquete/reconciliacion.py` y no las
    construye: quién decide que una fila no entra es aquel módulo, y este solo deja
    constancia. Separarlos es lo que permite que la regla de descarte cambie sin
    tocar la tabla, y al revés.
    """
    for descartada in descartadas:
        anotar_fila_descartada(
            conexion,
            companero_id,
            descartada.motivo,
            ruta_excel=ruta_excel,
            fila_excel=descartada.numero,
            numero_caso=descartada.numero_caso,
            mrn=descartada.mrn,
            nombre=descartada.nombre,
        )
    return len(descartadas)

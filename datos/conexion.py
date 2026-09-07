"""Apertura de conexiones a la base.

Toda conexion enciende `PRAGMA foreign_keys = ON` ANTES de la primera consulta.
La documentacion oficial de SQLite
(<https://www.sqlite.org/foreignkeys.html>, consultada 2026-09-02) dice, literal:

    «Foreign key constraints are disabled by default (for backwards
    compatibility), so must be enabled separately for each database connection.»

Sin ese pragma cada `REFERENCES` del esquema es un comentario decorativo y los
`RESTRICT` no impiden nada.

El modo de diario se queda en el de por defecto: NO se activa WAL
(docs/ARQUITECTURA.md §1.8, porque la carpeta de datos puede caer bajo OneDrive
y los archivos satelite `-wal`/`-shm` se sincronizarian desacompasados).
"""

import sqlite3
from pathlib import Path


class ErrorDeConexion(RuntimeError):
    """La conexion se abrio pero no quedo en el estado que el esquema exige."""


def abrir_conexion(ruta_de_la_base):
    """Abre la base, enciende las claves foraneas y comprueba que quedaron ON.

    Devuelve filas indexables por nombre de columna. `isolation_level=None` deja
    las escrituras en autoconfirmacion: una capa de datos sin interfaz no tiene
    a quien preguntar si confirmar o no.
    """
    conexion = sqlite3.connect(str(Path(ruta_de_la_base)), isolation_level=None)
    conexion.row_factory = sqlite3.Row
    conexion.execute("PRAGMA foreign_keys = ON")

    encendidas = conexion.execute("PRAGMA foreign_keys").fetchone()[0]
    if encendidas != 1:
        conexion.close()
        raise ErrorDeConexion(
            "Las claves foráneas no quedaron encendidas en esta conexión "
            f"(PRAGMA foreign_keys devolvió {encendidas}). Sin ellas el motor "
            "dejaría borrar un caso con personas dentro."
        )
    return conexion

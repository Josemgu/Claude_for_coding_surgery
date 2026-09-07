"""El procedimiento oficial de SQLite para reconstruir una tabla, en un solo sitio.

Cambiar un `CHECK` o quitar un `NOT NULL` en SQLite no se puede hacer con
`ALTER TABLE`: hay que reconstruir la tabla entera. La documentacion oficial
(<https://www.sqlite.org/lang_altertable.html>, seccion «Making Other Kinds Of
Table Schema Changes», consultada 2026-09-02) publica un procedimiento de 12
pasos, y los que aplican estan citados literales en `datos/migraciones.py`.

**Por que este codigo vive aqui y no donde nacio.** Lo escribio la migracion a la
version 2 y estaba guardado dentro de `datos/migraciones.py` con nombre privado.
La version 7 lo necesita otra vez —tambien reconstruye `casos`— y vive en otro
modulo. Copiarlo habria dejado dos procedimientos de 12 pasos que hay que revisar
por separado, y el dia que uno se corrija el otro se queda mal. Se movio entero,
sin tocar una linea de su logica.

`ErrorDeMigracion` se sigue pudiendo importar de `datos.migraciones`: ese modulo
lo reexporta, asi que nada de lo que ya lo importaba tiene que cambiar.
"""


class ErrorDeMigracion(RuntimeError):
    """Una migracion no se pudo aplicar, o se aplico y no quedo integra."""


def comprobar_que_no_quedaron_huerfanos(conexion):
    """Paso 10 del procedimiento oficial, antes de confirmar la transaccion."""
    huerfanos = conexion.execute("PRAGMA foreign_key_check").fetchall()
    if huerfanos:
        raise ErrorDeMigracion(
            "La migración dejó filas huérfanas y se deshace entera: "
            f"PRAGMA foreign_key_check devolvió {len(huerfanos)} fila(s). "
            "La base se queda como estaba."
        )


def reconstruir_tabla(conexion, rehacer):
    """Aplica un procedimiento de reconstruccion completo, o no aplica ninguno.

    Las claves foraneas se apagan y se vuelven a encender FUERA de la transaccion,
    que es donde el pragma surte efecto. Si algo falla, la transaccion se deshace y
    las claves vuelven a encenderse igual: una base a medio migrar con las claves
    apagadas seria peor que no haber empezado.
    """
    conexion.execute("PRAGMA foreign_keys = OFF")
    try:
        conexion.execute("BEGIN")
        try:
            rehacer(conexion)
            comprobar_que_no_quedaron_huerfanos(conexion)
        except Exception:
            conexion.execute("ROLLBACK")
            raise
        conexion.execute("COMMIT")
    finally:
        conexion.execute("PRAGMA foreign_keys = ON")
        encendidas = conexion.execute("PRAGMA foreign_keys").fetchone()[0]
        if encendidas != 1:
            raise ErrorDeMigracion(
                "Las claves foráneas no se pudieron volver a encender después de "
                f"la migración (PRAGMA foreign_keys devolvió {encendidas})."
            )

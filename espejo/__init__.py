"""El espejo en Excel de la base: se regenera entero despues de cada guardado.

Este paquete LEE la base y ESCRIBE un `.xlsx`. Nunca al reves: el Excel no es la
fuente de verdad, es un espejo (`PENDIENTES.md`, FASE 4). Leer de vuelta lo que
Miguel corrija en el Excel es la FASE 6 y no esta aqui.

Tampoco dibuja nada en pantalla. Cuando tiene algo que decir —el archivo estaba
abierto en Excel y no se pudo actualizar— redacta el aviso en espanol y lo
devuelve; quien lo muestre es la interfaz, que es de otra fase.

Sin `pandas` (regla permanente 3): solo `openpyxl`.
"""

from espejo.escritura import (
    ResultadoDeGuardado,
    ResultadoDelEspejo,
    aviso_de_archivo_bloqueado,
    guardar_y_regenerar,
    regenerar_espejo,
)
from espejo.rutas import ruta_del_archivo_parcial, ruta_del_espejo

__all__ = (
    "ResultadoDeGuardado",
    "ResultadoDelEspejo",
    "aviso_de_archivo_bloqueado",
    "guardar_y_regenerar",
    "regenerar_espejo",
    "ruta_del_archivo_parcial",
    "ruta_del_espejo",
)

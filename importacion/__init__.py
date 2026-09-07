"""El puente entre lo que se lee de un PDF y lo que se guarda en la base.

Existe como paquete propio, y no dentro de `extraccion` ni dentro de `datos`,
porque es el unico sitio del programa que necesita conocer los dos: `extraccion`
no sabe que hay una base y `datos` no sabe que hay PDF. Meterlo en cualquiera de
los dos ataria uno al otro para siempre.

Hasta ahora este puente NO EXISTIA. La FASE 2 producia `FormularioExtraido` en
memoria y la FASE 1 sabia guardar casos, y no habia ni una linea que llevara lo
primero a lo segundo: `grep -rn alta_de_caso` sobre el codigo del programa —fuera
de `pruebas/`— no devolvia una sola llamada. Sin esto, la pantalla de correccion
no tiene nada que corregir.
"""

from importacion.documento import ResultadoDeImportacion, importar_documento
from importacion.guardado import ResultadoDeLaPagina, guardar_formulario

__all__ = (
    "ResultadoDeImportacion",
    "ResultadoDeLaPagina",
    "guardar_formulario",
    "importar_documento",
)

"""El paquete que se le entrega a un companero, y el Excel que devuelve.

Este paquete hace el viaje de ida y el de vuelta:

  - **Ida** (`exportacion`, `trabajo`, `columnas`): una carpeta con el PDF recortado
    de cada caso asignado y la hoja «Por verificar» —la del proyecto viejo, que es
    la que el dueno aprobo (`DECISIONES.md`, 2026-09-03)— con cinco filas de
    cabecera, `numero_caso` y `mrn` bloqueados, los **seis pasos** de «Preparacion
    para las ordenanzas» como listas desplegables de Sí/No, y la **clave a la
    vista** en la ultima columna.
  - **Vuelta** (`lectura`, `reconciliacion`): la hoja devuelta se casa por
    `numero_caso` + `mrn` —**nunca por nombre**—, leidos de la columna «clave», y lo
    que no casa **no se inserta**: va a una lista de descartados con su motivo. Una
    respuesta escrita a mano que no esta en el menu se lee igual y se avisa con su
    numero de fila: el menu es una ayuda, no una reja. Una celda en blanco es una
    pregunta sin contestar y **no** un «no».
  - **Vuelta de un Excel cualquiera** (`mapeo`): Miguel dice que columna es cada
    cosa y el archivo pasa por el mismo motor de reconciliacion, con sus mismas
    reglas.

Lo que entra por aqui es una **propuesta**, nunca una verificacion (regla
permanente 5): quien confirma el caso sigue siendo Miguel, en la pantalla de
correccion.

Este paquete no dibuja nada y no sabe SQL: las escrituras las hace `datos/`, y
quien las ensena es `interfaz/`. Sin `pandas` (regla permanente 3): solo
`openpyxl`, y `pypdf` para recortar las hojas del formulario.
"""

from paquete.exportacion import ResultadoDelPaquete, exportar_paquete
from paquete.reconciliacion import (
    ResultadoDeLaVuelta,
    reconciliar_excel,
    reconciliar_filas,
    resumen_de_la_vuelta,
)

__all__ = (
    "ResultadoDeLaVuelta",
    "ResultadoDelPaquete",
    "exportar_paquete",
    "reconciliar_excel",
    "reconciliar_filas",
    "resumen_de_la_vuelta",
)

"""Las dos pantallas del programa, en `tkinter` de la biblioteca estandar.

Se usa `tkinter` y no otra cosa por la regla permanente 2: un solo ejecutable que
se abre con doble clic. `tkinter` viene con Python y PyInstaller ya lo empaqueta;
cualquier otra biblioteca de interfaz anadiria megabytes y una dependencia mas
que puede fallar dentro del `.exe`.

Este paquete no sabe SQL ni sabe leer PDF. Pide los datos a `datos/`, la imagen a
`extraccion/` y guarda por `espejo/`, que es el punto unico por el que la base y
el Excel se mueven juntos.

Regla permanente 4: **todo lo que se ve en pantalla esta en espanol**, incluidos
los mensajes de error. Las unicas cadenas en ingles de estos modulos son las
etiquetas IMPRESAS EN EL FORMULARIO —«Date traveling to the temple»— que se
ensenan debajo de cada campo para poder localizarlo en el papel. Traducirlas seria
enganar: en el papel dicen eso.
"""

from interfaz.aplicacion import Aplicacion

__all__ = ("Aplicacion",)

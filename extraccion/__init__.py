"""Lectura de un formulario escaneado: anotaciones del PDF, rasterizado y OCR.

Este paquete no sabe nada de interfaz ni de Excel, y no escribe en la base: su
unica responsabilidad es convertir una pagina de PDF en datos con su procedencia.
Quien los guarde es cosa de `datos`.

Regla permanente 1, que gobierna todo lo de aqui: NO hay IA generativa en ningun
punto. Ni para leer, ni para «arreglar» lo leido, ni para adivinar un campo que
falta. Un MRN inventado manda a una persona al templo con la recomendacion mal.
Todo lo que este paquete hace es OCR determinista, expresiones regulares y
geometria.
"""

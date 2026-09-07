"""Como se nombra en pantalla un caso que todavia no tiene numero.

Desde la version 7 del esquema, `casos.numero_caso` puede ser NULL: una pagina cuyo
numero no se pudo leer se guarda igual, pendiente de identificar, en vez de tirarse
entera con todo lo que ya se le habia leido.

Eso deja un hueco en todas las listas, y este modulo existe para que ese hueco diga
algo. Sin el, cada pantalla que escribe `caso['numero_caso']` pinta la palabra
`None` en medio de la fila. Y `None` no es una etiqueta: no esta en espanol, no
dice que hay que hacer, y a quien la lee le parece que el programa se rompio —
cuando lo que ha pasado es justo lo contrario, que el programa acaba de salvar una
pagina que antes tiraba.

**Nunca se inventa un numero para tapar el hueco** (regla permanente 1). Ni un
correlativo, ni el nombre del archivo, ni un `SIN0001`. Se escribe que no se sabe.
"""

SIN_NUMERO = "(sin número de caso)"


def numero_de_caso_visible(caso):
    """El numero de caso para pintar, o la frase que dice que todavia no se sabe.

    Acepta la fila entera y no el campo suelto para que ninguna pantalla tenga que
    acordarse de sacarlo antes: se le pasa el caso y se pinta lo que devuelve.
    """
    if caso is None:
        return SIN_NUMERO
    return texto_del_numero(caso["numero_caso"])


def texto_del_numero(numero_caso):
    """Lo mismo, partiendo del valor suelto. Para donde no hay fila que pasar."""
    return numero_caso if numero_caso else SIN_NUMERO


# La marca que lleva en las listas un caso que repite a otro. Va **con el id del
# caso original y no con su numero**: desde la version 12 del esquema dos casos
# pueden llevar el mismo numero —es lo que este cambio existe para permitir—, asi
# que decir «duplicado de BALC2609» no llevaria a ninguna parte.
MARCA_DE_DUPLICADO = "⚠ DUPLICADO del caso n.º {original}"


def marca_de_duplicado(caso):
    """La marca de duplicado de ese caso, o cadena vacia si no repite a ninguno.

    Devuelve cadena vacia y no None para que quien pinta pueda pegarla sin
    preguntar: `f"{...}{marca_de_duplicado(caso)}"` sale igual de bien en los dos
    casos, y una pantalla que tiene que acordarse de preguntar es una pantalla que
    algun dia no pregunta.
    """
    if not caso or caso.get("duplicado_de") is None:
        return ""
    return "   ·   " + MARCA_DE_DUPLICADO.format(original=caso["duplicado_de"])

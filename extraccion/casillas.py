"""Las seis casillas de ordenanzas, y por que esta fase las entrega SIN leer.

Las casillas son marcas dibujadas a mano, no texto: el OCR no las lee de forma
fiable. El metodo previsto es recortar cada celda, binarizarla, contar el pixel
oscuro sobre el area total y comparar contra un umbral. Ese umbral hay que
CALIBRARLO sobre formularios reales; elegirlo a ojo es exactamente lo que la
fase prohibe.

Estado medido el 2026-09-02 sobre los cuatro documentos de referencia
(9 paginas). Lo que hace falta para calibrar y lo que hay:

  - Formularios con casillas que el sistema pueda localizar y a la vez una
    verdad conocida con la que comparar el conteo: **0**.
  - Minimo que exige el criterio de aceptacion de la fase: **3**.

El OCR detecta algunas marcas como caracteres sueltos —dos por pagina en dos de
las nueve, y con confianzas de 0.61 a 0.74—, pero eso no es una verdad conocida:
es otra lectura automatica, y calibrar un detector contra otro detector no mide
nada. Establecer la verdad exigiria que una persona marcase a mano, casilla por
casilla, formularios con nombres y MRN reales, y eso no es trabajo del
programador ni cabia en este pase.

Por lo tanto se aplica la rama 11b del criterio: **la lectura automatica NO se
entrega activa.** Las seis columnas vuelven vacias y marcadas para captura
manual. Un campo vacio que Miguel rellena cuesta un minuto; una casilla mal
leida que nadie revisa es el dano que este programa existe para evitar.

Para reactivarla hace falta, y en este orden:
  1. Tres o mas formularios con la verdad de sus casillas anotada por una persona.
  2. La medicion del pixel oscuro de las casillas marcadas y de las vacias.
  3. El umbral escrito aqui con ese numero y con cuantos formularios lo calibraron.
"""

# Las seis columnas en el orden literal de `DECISIONES.md`, que es el mismo que
# el de las columnas `ord_*` de la tabla `personas`.
CASILLAS_DE_ORDENANZAS = (
    "ord_recibir_propias",
    "ord_observar_sellamiento",
    "ord_traductor",
    "ord_investidura",
    "ord_sellamiento_esposos",
    "ord_sellamiento_hijo_padres",
)

# Cuantos formularios con verdad conocida hicieron falta y cuantos hubo.
FORMULARIOS_MINIMOS_PARA_CALIBRAR = 3
FORMULARIOS_CON_VERDAD_CONOCIDA = 0

LECTURA_DE_CASILLAS_ACTIVA = FORMULARIOS_CON_VERDAD_CONOCIDA >= FORMULARIOS_MINIMOS_PARA_CALIBRAR

# El umbral de pixel oscuro. Sigue en None a proposito: mientras no haya con que
# calibrarlo, cualquier numero aqui seria inventado. `None` no se puede usar por
# accidente; un 0.15 puesto «de momento» si.
UMBRAL_DE_PIXEL_OSCURO = None


def casillas_no_leidas():
    """Las seis casillas sin leer: `None` en cada una.

    `None` no es lo mismo que 0 en este esquema, y la diferencia importa. 0
    significa «se leyo y no estaba marcada»; `None` significa «no se leyo». Si se
    devolvieran ceros, Miguel veria seis ordenanzas negativas en firme y no
    tendria motivo para mirar el papel.
    """
    return {nombre: None for nombre in CASILLAS_DE_ORDENANZAS}

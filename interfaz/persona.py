"""El bloque de una persona del formulario, y como se anota una correccion a mano.

Vive aparte de `interfaz/correccion.py` porque es una pieza entera: el nombre, el
MRN y las seis casillas de UNA persona, con todo lo que hace falta para pintarlas,
contarlas y guardarlas. La pantalla monta uno por fila leida y no sabe por dentro
como esta hecho.

`_anotar_correccion_manual` esta aqui y no alli porque la usan los dos: la persona
para sus dos campos y la pantalla para los cuatro del caso. Es la funcion que
cumple el requisito literal del pase —`origen='manual'` conservando `valor_ocr`— y
tenerla en un solo sitio es lo que impide que una de las dos rutas se olvide.
"""

from tkinter import ttk

from datos.procedencia import (
    banda_de_la_procedencia,
    guardar_procedencia_de_campo,
    procedencia_por_campo,
)
from datos.ordenanzas import (
    ETIQUETAS_DE_LAS_ORDENANZAS as ETIQUETAS_DE_LAS_ORDENANZAS_DE_DATOS,
)
from datos.repositorio import CASILLAS_DE_ORDENANZAS, actualizar_datos_de_persona
from datos.validacion import validar_mrn
from extraccion.etiquetas import (
    CAMPO_DE_LOS_NOMBRES,
    CAMPO_DEL_MRN,
    forma_espanola_de,
)
from interfaz.campo import CampoCorregible
from interfaz.casilla import NO_MARCADA, CasillaDeOrdenanza
from interfaz.pasos import BloqueDeLosPasos

# Los rotulos ya no viven aqui: se movieron a `datos/ordenanzas.py` cuando la hoja
# del companero y el informe empezaron a necesitarlos. `paquete/` y `reportes/` no
# pueden importar de `interfaz/`, que es lo que dibuja ventanas, y tres copias de la
# misma lista acaban diciendo tres cosas distintas. Se reexporta con el mismo nombre
# para que nada de lo que ya lo importaba de aqui tenga que cambiar.
ETIQUETAS_DE_LAS_ORDENANZAS = ETIQUETAS_DE_LAS_ORDENANZAS_DE_DATOS


class BloqueDePersona(ttk.LabelFrame):
    """El nombre, el MRN y las seis casillas de una persona del formulario."""

    def __init__(
        self, padre, persona, pagina, procedencia, al_cambiar, con_tira=True,
        propuesta=None,
    ):
        titulo = persona["nombre"] or "— sin nombre leído —"
        super().__init__(
            padre, text=f"{persona['fila_formulario'] or '?'}  ·  {titulo}", padding=10
        )
        self.persona_id = persona["id"]
        # La hoja del PDF de la que salen las tiras de este bloque. Se toma de la
        # pagina que se recibe y no de `persona["pagina_pdf"]` a proposito: asi lo
        # que este bloque DICE que ensena y lo que de verdad recorta son el mismo
        # dato, y no dos que se puedan separar. Es lo que anuncia F2.
        self.pagina_pdf = pagina.pagina_pdf
        self._al_cambiar = al_cambiar
        self.columnconfigure(0, weight=1)

        self.campos = {}
        # La etiqueta del papel se dice en ESPANOL, sea cual sea el idioma de la
        # hoja (regla permanente 4). Aqui estaban escritas a mano «Full Name(s)» y
        # «Membership Record Number», que QA midio que no aparecen en los
        # formularios del dueno. Salen de `extraccion/etiquetas.py`, que es el
        # unico sitio donde se dice como se llama cada cosa en cada idioma.
        for nombre, etiqueta, del_papel, validar in (
            ("nombre", "Nombre", forma_espanola_de(CAMPO_DE_LOS_NOMBRES), None),
            ("mrn", "MRN", forma_espanola_de(CAMPO_DEL_MRN), validar_mrn),
        ):
            campo = CampoCorregible(
                self,
                nombre,
                etiqueta,
                etiqueta_del_papel=del_papel,
                validar=validar,
                al_cambiar=al_cambiar,
                con_tira=con_tira,
                pagina_pdf=self.pagina_pdf,
            )
            campo.grid(sticky="ew")
            fila = procedencia.get(nombre)
            # Sin tira no se recorta nada, y eso NO es una optimizacion escondida:
            # recortar obliga a rasterizar la hoja, y en la pantalla partida la
            # unica hoja que hay que rasterizar es la que el visor esta ensenando.
            # Un caso de seis hojas pagaba seis rasterizaciones al abrirse solo
            # para dibujar doce tiras que ahora no existen.
            campo.cargar(
                persona[nombre],
                fila,
                pagina.tira(banda_de_la_procedencia(fila)) if con_tira else None,
            )
            self.campos[nombre] = campo

        ttk.Label(self, text="Ordenanzas", style="Seccion.TLabel").grid(
            sticky="w", pady=(8, 4)
        )
        rejilla = ttk.Frame(self)
        rejilla.grid(sticky="ew")
        self.casillas = {}
        for indice, nombre in enumerate(CASILLAS_DE_ORDENANZAS):
            casilla = CasillaDeOrdenanza(
                rejilla,
                ETIQUETAS_DE_LAS_ORDENANZAS[nombre],
                valor=persona[nombre],
                al_cambiar=lambda valor: al_cambiar(),
            )
            casilla.grid(row=indice // 2, column=indice % 2, sticky="w", padx=(0, 24), pady=2)
            self.casillas[nombre] = casilla

        # ⚠️ Los seis pasos NO se cuentan con las seis casillas y no entran en
        # `casillas_sin_leer`. El contador del pie es el que bloquea «Todo
        # correcto», y meter ahi siete respuestas que Miguel no puede resolver
        # desde esta pantalla dejaria la verificacion trabada sin salida visible.
        # Los pasos se ensenan; las ordenanzas se resuelven.
        #
        # `pasos` queda en None cuando ningun companero ha contestado nada sobre
        # esta persona, y entonces la seccion no se dibuja: siete «sin contestar»
        # repetidos en cada persona de cada caso esconden las dos que si traen
        # algo, que es el mismo motivo por el que `propuestas_del_caso` filtra por
        # `propuesto_por IS NOT NULL`.
        self.pasos = None
        if propuesta is not None:
            self.pasos = BloqueDeLosPasos(self, propuesta)
            self.pasos.grid(sticky="ew", pady=(10, 0))

    def primer_control(self):
        """Por donde entra el foco en este bloque: el nombre."""
        return self.campos["nombre"].entrada

    def casillas_sin_leer(self):
        """Cuantas de las seis siguen sin resolver."""
        return sum(1 for casilla in self.casillas.values() if casilla.esta_sin_leer())

    def resolver_las_sin_leer_como_no_marcadas(self):
        """Deja en «no marcada» las que siguen sin leer. Devuelve cuantas movio.

        **No toca las que ya tienen valor.** Es la mitad que hace util al boton:
        Miguel marca a mano las dos o tres que el papel si trae, y con esto cierra
        el resto sin volver sobre ellas. Si tocara todas, marcar antes seria
        trabajo perdido y el boton no se podria usar mas que en el caso en que no
        hay ninguna marcada.

        No escribe en la base: eso lo hace `guardar`, por el mismo camino que
        cualquier otra correccion a mano, para que no exista una segunda puerta
        que se pueda olvidar de anotar la procedencia.
        """
        movidas = 0
        for casilla in self.casillas.values():
            if casilla.esta_sin_leer():
                casilla.fijar(NO_MARCADA)
                movidas += 1
        return movidas

    def campos_no_validos(self):
        """Los campos de esta persona que no pasan validacion."""
        return [campo for campo in self.campos.values() if not campo.es_valido()]

    def _valores_que_si_se_escriben(self):
        """El nombre y el MRN, y solo si valen.

        **Un campo que no vale no se pasa**, y omitirlo es exactamente lo que
        `actualizar_datos_de_persona` entiende por «esto no se toca»: se queda en
        la base lo que ya habia y en la pantalla lo tecleado, en rojo y con su
        motivo. Medido el 2026-09-04: pasandolos siempre, un MRN de tres digitos
        en la tercera de cuatro personas levantaba `ErrorDeValidacion` y la cuarta
        persona **no se guardaba**, aunque su MRN estuviera bien.
        """
        return {
            nombre: campo.valor()
            for nombre, campo in self.campos.items()
            if campo.es_valido()
        }

    def guardar(self, conexion):
        """Escribe el nombre, el MRN y las seis casillas de esta persona.

        ⚠️ **Una casilla que alguien resolvio deja fila de procedencia con
        `origen='manual'`, y una que nadie toco no.** Es lo que impide que
        «Miguel dijo que no esta marcada» y «la maquina la leyo sin marca» acaben
        pareciendose en la base: los dos guardan un 0 en la columna, y lo unico
        que los separa es esta fila. La importacion no escribe procedencia de
        casillas a proposito (`importacion/guardado.py:51-55`), asi que una fila
        aqui solo puede venir de una mano.
        """
        actualizar_datos_de_persona(
            conexion,
            self.persona_id,
            **self._valores_que_si_se_escriben(),
            **{nombre: casilla.valor for nombre, casilla in self.casillas.items()},
        )
        for nombre, campo in self.campos.items():
            if campo.cambio() and campo.es_valido():
                _anotar_correccion_manual(conexion, "personas", self.persona_id, nombre)
        for nombre, casilla in self.casillas.items():
            if casilla.cambio():
                _anotar_correccion_manual(conexion, "personas", self.persona_id, nombre)


def _anotar_correccion_manual(conexion, tabla, registro_id, campo):
    """Deja `origen='manual'` CONSERVANDO lo que el lector habia leido.

    Es el requisito literal del pase, y lo que hace posible comparar despues: si
    `valor_ocr` se pisara con lo tecleado, se perderia la unica prueba de que la
    maquina habia leido otra cosa — que es como se descubre que el OCR falla
    siempre en el mismo sitio.

    Por eso `valor_ocr` se lee de la fila que ya estaba y se vuelve a escribir tal
    cual. La banda tampoco se toca: `guardar_procedencia_de_campo` la conserva
    cuando no se le pasa ninguna, y la tira tiene que seguir viendose despues de
    teclear encima, porque es contra lo que se comprueba lo tecleado.
    """
    anterior = procedencia_por_campo(conexion, tabla, registro_id).get(campo)
    guardar_procedencia_de_campo(
        conexion,
        tabla,
        registro_id,
        campo,
        origen="manual",
        confianza=None,
        valor_ocr=anterior["valor_ocr"] if anterior else None,
        anulado_por_tachon=anterior["anulado_por_tachon"] if anterior else 0,
    )

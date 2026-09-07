"""La ventana: abre por el inicio, entra a un caso, y vuelve.

Una sola ventana y una sola conexion a la base para todo el programa. Las
pantallas se sustituyen dentro; no se abren ventanas encima. El motivo es la
regla permanente 2 llevada al uso: un solo ejecutable que se abre con doble clic
tiene que comportarse como un solo programa, y tres ventanas apiladas se pierden
detras de las demas del escritorio.

**Al volver del caso se refresca el inicio SIEMPRE.** Es lo que comprueba que la
correccion sirvio de algo: si un caso verificado sigue en «Pendientes» al volver,
el filtro esta mal, y eso hay que verlo en el acto y no tres dias despues.

**El OCR se carga una sola vez y solo cuando hace falta.** Arrancar el motor tarda
segundos y ocupa memoria; un Miguel que solo entra a mirar el calendario no tiene
por que pagarlo.

**La ruta de datos se DIBUJA, no se imprime** (FASE 9, criterio 6a). Empaquetado
con `console=False` —que es como se entrega, para que no salga una consola negra
detras de la ventana— PyInstaller deja `sys.stdout` en `None`, y `print` sobre
`None` no levanta nada: se traga la linea sin avisar. La ruta se seguiria
«mostrando» y nadie la veria jamas. El pie de la ventana es el unico sitio donde
Miguel puede leerla, asi que ahi va. Se sigue imprimiendo tambien: en desarrollo
hay consola y ahi tiene valor.
"""

import time
import tkinter as tk
from tkinter import ttk

from datos.arranque import preparar_base_de_datos
from datos.conexion import abrir_conexion
from datos.registro import ARRANQUE, CASO, apuntar, preparar_el_registro
from espejo.escritura import regenerar_espejo as espejo_al_dia
from interfaz import dialogos
from interfaz.archivar import PantallaDeArchivo
from interfaz.correccion import PantallaDeCorreccion
from interfaz.etiquetas import numero_de_caso_visible
from interfaz.inicio import PantallaDeInicio
from interfaz.reportes import PantallaDeReportes
from interfaz.tema import aplicar_tema

TITULO = "Fichas"
ANCHO_MINIMO = 1100
ALTO_MINIMO = 720


class Aplicacion:
    """Las dos pantallas dentro de una ventana, con una sola conexion."""

    def __init__(self, carpeta_de_datos=None, escribir=print, arrancado_en=None):
        # ⚠️ `arrancado_en` es el reloj del PROCESO, no el de esta clase, y por eso
        # se recibe en vez de tomarse aqui. Lo toma `fichas.py` en su primera linea:
        # para cuando se construye una `Aplicacion` ya se pagaron los imports, que
        # es justo donde el dueno dice que se va el tiempo —«el problema es cargar
        # la interfaz»—. Medido desde aqui, ese trozo no saldria.
        #
        # Admite None para las pruebas y para quien construya la ventana suelta: sin
        # reloj de partida no se inventa una cifra, simplemente no se dice ninguna.
        self._arrancado_en = arrancado_en
        self.segundos_hasta_verse = None
        self.carpeta_de_datos = carpeta_de_datos
        self.lineas_de_arranque = []
        self.ruta_de_la_base = preparar_base_de_datos(
            carpeta_de_datos=carpeta_de_datos, escribir=self._anotar_arranque(escribir)
        )
        self.conexion = abrir_conexion(self.ruta_de_la_base)
        # El registro se prepara en cuanto se sabe la carpeta y ANTES de dibujar
        # nada: lo primero que tiene que quedar apuntado es cuanto tarda en
        # dibujarse. Ver `datos/registro.py`.
        self.ruta_del_registro = preparar_el_registro(self.ruta_de_la_base.parent)
        self._motor_ocr = None

        self.raiz = tk.Tk()
        self.raiz.title(TITULO)
        self.raiz.minsize(ANCHO_MINIMO, ALTO_MINIMO)
        self.raiz.rowconfigure(0, weight=1)
        self.raiz.columnconfigure(0, weight=1)
        aplicar_tema(self.raiz)
        self._pantalla = None
        # Las pantallas que se conservan entre visitas, por su nombre. Son las tres
        # a las que se vuelve una y otra vez en una sesión —inicio, «Revisar» y
        # reportes—; las demás se abren para una cosa concreta y se destruyen al
        # salir, que es lo que hacía la ventana con todas hasta este pase.
        self._guardadas = {}
        self._construir_pie()
        self.mostrar_inicio()

    def _anotar_arranque(self, escribir):
        """Guarda cada linea del arranque ademas de pasarla a `escribir`.

        Se guardan porque el pie las necesita y `print` no las devuelve. Se siguen
        pasando porque en desarrollo hay consola y ahi valen.
        """

        def anotar(linea):
            self.lineas_de_arranque.append(linea)
            escribir(linea)

        return anotar

    def _construir_pie(self):
        """La franja de abajo con la ruta de datos que se resolvio.

        Va en la ventana y no en una pantalla porque no pertenece a ninguna de las
        dos: es de donde escribe el programa, y eso vale igual en el inicio que
        dentro de un caso.
        """
        pie = ttk.Frame(self.raiz, padding=(12, 4))
        pie.grid(row=1, column=0, sticky="ew")
        self._rotulo_del_pie = ttk.Label(
            pie, text=self._texto_del_pie(), style="Secundario.TLabel"
        )
        self._rotulo_del_pie.grid(row=0, column=0, sticky="w")

    def anotar_cuanto_tardo_en_abrir(self):
        """Escribe en el pie cuanto tardo el programa en abrir. Un dato, sin parrafo.

        **Existe porque no hay ningun numero del portatil del dueno.** El dice
        «lento» y «se está poniendo súper lento al abrir toda la interfaz», y de
        esta maquina no se puede deducir la suya: aqui hay otra cosa corriendo y
        alli hay un procesador distinto. Con la cifra en el pie, el manda un numero
        en vez de un adjetivo, y entonces se puede decidir algo.

        Se llama **despues** de que la ventana este pintada y respondiendo, que es
        el instante que el dueno cronometra con los ojos. Antes de eso la cifra
        seria de otra cosa.
        """
        if self._arrancado_en is None:
            return
        self.segundos_hasta_verse = time.perf_counter() - self._arrancado_en
        self._rotulo_del_pie.configure(text=self._texto_del_pie())
        apuntar(ARRANQUE, self.segundos_hasta_verse)

    # Lo que dice el pie cuando la carpeta no es la que resuelve Windows sino una
    # que se pidio con `--carpeta-de-datos`. Va SIEMPRE que se use esa opcion: un
    # programa escribiendo en otro sitio sin decirlo es como se pierde una tarde de
    # trabajo buscandolo en la carpeta de siempre.
    AVISO_DE_CARPETA_FORZADA = (
        "carpeta indicada con --carpeta-de-datos, NO es la de Documentos"
    )

    def _texto_del_pie(self):
        """Donde escribe el programa, y los avisos de por que es ahi.

        El aviso de OneDrive viaja entero: si la carpeta cayo dentro de OneDrive, la
        base con MRN de personas reales se sincroniza con la nube, y eso Miguel
        tiene que poder leerlo sin abrir una consola que no existe.

        Y desde el 2026-09-03 se dice tambien cuando la carpeta la forzo
        `--carpeta-de-datos`. Esa opcion existe para poder probar el `.exe` sin
        tocar la base real; lo que la hace segura es que se vea, porque el estado
        peligroso no es escribir en otra carpeta sino no saber en cual se escribe.
        """
        carpeta = self.ruta_de_la_base.parent
        avisos = [
            linea for linea in self.lineas_de_arranque if linea.startswith("AVISO")
        ]
        if self.carpeta_de_datos is not None:
            avisos.insert(0, self.AVISO_DE_CARPETA_FORZADA)
        # La cifra va DELANTE de los avisos y detras de la ruta: es lo que hay que
        # poder leer de un vistazo para decirlo por teléfono. Con coma decimal, que
        # es como se escribe en español, y con un solo decimal: la diferencia entre
        # 1,4 y 1,42 no cambia ninguna decisión.
        apertura = ""
        if self.segundos_hasta_verse is not None:
            cifra = f"{self.segundos_hasta_verse:.1f}".replace(".", ",")
            apertura = f"   ·   Abrió en {cifra} s"
        return (
            f"Datos en: {carpeta}"
            + apertura
            + "".join(f"   ·   {aviso}" for aviso in avisos)
        )

    # ---- cambio de pantalla ---------------------------------------------

    def _sustituir(self, nueva):
        """Quita la pantalla anterior con sus atajos y pone la nueva.

        Soltar los atajos es obligatorio, no higiene: los dos atan teclas al mismo
        toplevel, y sin soltarlos F5 seguiria intentando refrescar una pantalla de
        inicio que ya no esta dibujada.

        **La anterior se ESCONDE si es de las que se guardan, y se destruye si
        no.** Es el punto 4 del pase, y sale de una medicion del supervisor:
        destruir la pantalla de inicio son 186 `destroy` y 1,35 s en su perfil, y
        volver a construirla cuesta otro tanto. En Windows cada widget de Tk es una
        ventana del sistema; `grid_remove` no toca ninguna.
        """
        anterior = self._pantalla
        if anterior is not None and anterior is not nueva:
            anterior.soltar_atajos()
            anterior.grid_remove()
            if anterior not in self._guardadas.values():
                # Se esconde ya y se destruye **después**, con la nueva pantalla
                # delante. Medido con `cProfile` sobre esta máquina: destruir la
                # pantalla de corrección eran 77 ms de los 283 que costaba volver
                # al inicio, y son 77 ms que Miguel esperaba mirando el caso que
                # acababa de cerrar. `after_idle` los pone detrás.
                self.raiz.after_idle(lambda vieja=anterior: self._desechar(vieja))
        self._pantalla = nueva
        # `grid` y no `pack`: el pie de la ventana usa `grid`, y Tk no admite los
        # dos gestores en el mismo contenedor. La pantalla ocupa la fila 0, que es
        # la que crece; el pie se queda en la 1 con su alto natural.
        nueva.grid(row=0, column=0, sticky="nsew")
        nueva.atar_atajos()

    def _desechar(self, pantalla):
        """Destruye una pantalla que ya no se ve. Se llama con la nueva delante.

        Se comprueba que siga existiendo: entre el `after_idle` y esta llamada la
        ventana puede haberse cerrado, y `destroy` sobre lo ya destruido levanta un
        `TclError` que nadie ve venir.
        """
        try:
            if pantalla.winfo_exists():
                pantalla.destroy()
        except tk.TclError:
            # El intérprete se estaba cerrando. No queda nada que destruir.
            pass

    def _guardada(self, clave, construir):
        """La pantalla de esa clave, construida la primera vez y guardada despues.

        Se comprueba `winfo_exists()` ademas de tenerla en el diccionario: una
        pantalla puede haberse destruido por su cuenta —un error al construirla, la
        ventana cerrandose— y devolver un widget muerto pondria la ventana en un
        estado del que no se sale.
        """
        pantalla = self._guardadas.get(clave)
        if pantalla is None or not pantalla.winfo_exists():
            pantalla = construir()
            self._guardadas[clave] = pantalla
        return pantalla

    def mostrar_inicio(self):
        """Vuelve al inicio y lo relee entero con la fecha de hoy.

        La pantalla es **la misma de siempre**; lo que se rehace es su contenido,
        con `refrescar()`. Eso es lo que sigue cumpliendo la promesa de arriba —«al
        volver del caso se refresca el inicio SIEMPRE»— sin pagar la reconstruccion
        de los widgets.
        """
        inicio = self._guardada("inicio", self._construir_inicio)
        self._sustituir(inicio)
        self.raiz.title(f"{TITULO} — Inicio")
        inicio.refrescar()
        inicio.dar_el_foco_a_lo_mas_urgente()

    def _construir_inicio(self):
        """La pantalla de inicio, con sus siete destinos. Se construye una vez."""
        return PantallaDeInicio(
            self.raiz,
            self.conexion,
            self.abrir_caso,
            self.importar_pdf,
            al_ver_reportes=self.mostrar_reportes,
            al_ver_companeros=self.mostrar_companeros,
            al_asignar=self.mostrar_asignacion,
            al_ver_ilegibles=self.mostrar_ilegibles,
            # ⚠️ `al_revisar` estuvo en None con un motivo medido y bueno: no habia
            # forma de saber cual de los companeros es Miguel, y elegir uno
            # cualquiera pondria la firma de una persona real debajo de una marca
            # que no hizo. **Eso dejo de ser cierto el 2026-09-03**, cuando
            # `companero_que_firma` subio de la pantalla de correccion a
            # `datos/companeros.py`: ahi esta la respuesta, y es la MISMA que usa
            # «Verificar caso», asi que las dos pantallas firman como la misma
            # persona en vez de repartir las firmas entre dos.
            al_revisar=self.mostrar_revisar,
            al_bajar_excel=self.bajar_el_excel,
        )

    def mostrar_ilegibles(self):
        """Abre la lista de lo que no se pudo leer, que se queda entre sesiones.

        Es una ventana encima y no una pantalla que sustituye al inicio, al
        contrario que las demas: se abre para mirar una cosa y se cierra, y desde
        aqui no se cambia nada. Igual que la lista de descartados del Excel.
        """
        from interfaz.ilegibles import VentanaDeIlegibles

        VentanaDeIlegibles(self.raiz, self.conexion)

    def mostrar_revisar(self):
        """Abre «Revisar»: los documentos en tarjetas, para marcarlos en tandas.

        `companero_que_firma` es quien queda debajo de lo que se marque desde ahi,
        y es **la misma funcion** que usa «Verificar caso» en la pantalla de
        correccion. Que sea la misma no es comodidad: dos formas de responder «quien
        es Miguel» acabarian dando de alta dos companeros con el mismo nombre y
        repartiendo sus firmas entre los dos.
        """
        revisar = self._guardada("revisar", self._construir_revisar)
        self._sustituir(revisar)
        self.raiz.title(f"{TITULO} — Revisar")
        # La pantalla se conserva; sus documentos NO. Volver a «Revisar» después de
        # corregir un caso tiene que enseñar lo que ese caso quedó, no lo que era
        # cuando se abrió la pantalla por primera vez.
        revisar.refrescar()

    def _construir_revisar(self):
        """La pantalla de «Revisar». Se construye una vez y se conserva."""
        from datetime import date

        from datos.companeros import companero_que_firma
        from interfaz.revisar import PantallaDeRevisar

        return PantallaDeRevisar(
            self.raiz,
            self.conexion,
            date.today(),
            self.abrir_caso,
            companero_que_firma(self.conexion),
            al_volver=self.mostrar_inicio,
        )

    def mostrar_companeros(self):
        """Abre la pantalla de compañeros: alta, corrección del nombre y baja."""
        from interfaz.companeros import PantallaDeCompaneros

        self._sustituir(
            PantallaDeCompaneros(self.raiz, self.conexion, al_volver=self.mostrar_inicio)
        )
        self.raiz.title(f"{TITULO} — Compañeros")

    def mostrar_asignacion(self):
        """Abre la pantalla de asignación, paquete y vuelta del Excel."""
        from datetime import date

        from interfaz.asignacion import PantallaDeAsignacion

        self._sustituir(
            PantallaDeAsignacion(
                self.raiz,
                self.conexion,
                al_volver=self.mostrar_inicio,
                hoy=date.today(),
                carpeta_de_datos=self.carpeta_de_datos,
            )
        )
        self.raiz.title(f"{TITULO} — Asignar casos")

    def mostrar_reportes(self):
        """Abre la pantalla de reportes y el histórico de casos archivados."""
        reportes = self._guardada("reportes", self._construir_reportes)
        self._sustituir(reportes)
        self.raiz.title(f"{TITULO} — Reportes")
        # Un caso archivado desde otra pantalla tiene que salir en el histórico al
        # volver aquí, y los números del período tienen que contarlo.
        reportes.refrescar()

    def _construir_reportes(self):
        """La pantalla de reportes. Se construye una vez y se conserva."""
        return PantallaDeReportes(
            self.raiz,
            self.conexion,
            al_volver=self.mostrar_inicio,
            carpeta_de_datos=self.carpeta_de_datos,
        )

    def abrir_caso(self, caso):
        """Entra a corregir un caso. `caso` es la fila que trajo la lista.

        Deja su linea en `fichas.log` con lo que tardo en estar delante. Es la
        segunda de las cuatro cosas que el dueno nota lentas, y la unica forma de
        saber si su portatil tarda lo mismo que esta maquina.
        """
        desde = time.perf_counter()
        try:
            pantalla = PantallaDeCorreccion(
                self.raiz,
                self.conexion,
                caso["id"],
                al_volver=self.mostrar_inicio,
                carpeta_de_datos=self.carpeta_de_datos,
                al_archivar=self.archivar_caso,
            )
        except Exception as causa:
            dialogos.avisar_de_un_error(
                "No se pudo abrir el caso",
                f"El caso {numero_de_caso_visible(caso)} no se pudo abrir: {causa}",
                parent=self.raiz,
            )
            return
        self._sustituir(pantalla)
        self.raiz.title(f"{TITULO} — Corrección · {numero_de_caso_visible(caso)}")
        self.raiz.update_idletasks()
        apuntar(
            CASO,
            time.perf_counter() - desde,
            caso=caso["id"],
            numero=caso["numero_caso"] or "sin_numero",
        )

    def archivar_caso(self, caso_id, numero_caso=""):
        """Abre la pantalla que pregunta quién viajó y archiva el caso.

        Va aquí y no dentro de la pantalla de corrección por la misma razón que
        `abrir_caso`: quién sustituye a quién en la ventana lo decide la ventana, y
        una pantalla que se cambia a sí misma por otra deja dos dueños del mismo
        hueco.
        """
        try:
            pantalla = PantallaDeArchivo(
                self.raiz,
                self.conexion,
                caso_id,
                al_volver=self.mostrar_inicio,
                carpeta_de_datos=self.carpeta_de_datos,
            )
        except Exception as causa:
            dialogos.avisar_de_un_error(
                "No se pudo abrir el archivado",
                f"El caso {numero_caso} no se pudo archivar: {causa}",
                parent=self.raiz,
            )
            return
        self._sustituir(pantalla)
        self.raiz.title(f"{TITULO} — Archivar · {numero_caso}")

    def bajar_el_excel(self):
        """«Bajar la base en Excel»: reescribe el `.xlsx` espejo entero y dice donde.

        Es la unica de las cuatro acciones del panel que no vive detras de otra
        pantalla: `regenerar_espejo` es una funcion suelta de `espejo/escritura.py`
        y se llama tal cual. Las otras tres llevan a la pantalla que ya las hace
        —importar, Asignar casos y Reportes—, y ahi no se duplica nada.

        Los avisos de `regenerar_espejo` viajan a un dialogo y no a `print`:
        empaquetado con `console=False`, `sys.stdout` es `None` y un `print` se
        traga la linea sin levantar nada. El caso normal que hay que ver es el
        archivo abierto en Excel, que bloquea la escritura.
        """
        avisos = []
        try:
            resultado = espejo_al_dia(
                self.conexion,
                carpeta_de_datos=self.carpeta_de_datos,
                avisar=avisos.append,
            )
        except Exception as causa:
            dialogos.avisar_de_un_error(
                "No se pudo escribir el Excel",
                f"El Excel espejo no se pudo reescribir: {causa}",
                parent=self.raiz,
            )
            return None
        if avisos:
            dialogos.advertir(
                "El Excel no se pudo actualizar del todo",
                "\n".join(avisos),
                parent=self.raiz,
            )
        else:
            dialogos.informar(
                "Excel actualizado",
                f"Quedó escrito en:\n\n{resultado.ruta}",
                parent=self.raiz,
            )
        return resultado

    # ---- importacion -----------------------------------------------------

    def _motor(self):
        """El motor de OCR, cargado una sola vez y solo cuando se importa."""
        if self._motor_ocr is None:
            from extraccion.ocr import crear_motor_ocr

            self._motor_ocr = crear_motor_ocr()
        return self._motor_ocr

    def importar_pdf(self, origenes):
        """Importa lo que se haya elegido: archivos sueltos, carpetas, o las dos cosas.

        Recibe una LISTA y no una ruta, y ese cambio de forma es el que arregla lo
        que el dueno pidio: hasta hoy la pantalla de inicio solo dejaba elegir un
        archivo, porque el dialogo era `askopenfilename` en singular.

        Nada se lee aqui. Se resuelve la lista de PDF, se comprueba que hay alguno,
        y a partir de ahi manda `interfaz/importacion.py`: la lectura corre en su
        propio hilo y esta ventana sigue respondiendo. Antes toda la importacion
        pasaba dentro de este metodo, o sea dentro del bucle de eventos de Tk, y por
        eso «el programa se paraba» — no se paraba, es que Tk no puede repintar
        mientras una funcion suya no devuelve.
        """
        from importacion.tanda import rutas_de_pdf
        from interfaz.importacion import TandaEnMarcha

        rutas = rutas_de_pdf(origenes)
        if not rutas:
            dialogos.advertir(
                "No hay ningún PDF que importar",
                "En lo que ha elegido no hay ningún archivo .pdf. Si eligió una "
                "carpeta, se buscó también dentro de sus subcarpetas y no se "
                "encontró ninguno.",
                parent=self.raiz,
            )
            return

        tanda = TandaEnMarcha(
            self.raiz,
            self.conexion,
            rutas,
            self._motor,
            carpeta_de_datos=self.carpeta_de_datos,
            al_terminar=self._al_terminar_la_tanda,
            avisar=lambda aviso: dialogos.advertir(
                "Aviso al importar", aviso, parent=self.raiz
            ),
        )
        tanda.arrancar()

    def _al_terminar_la_tanda(self, resumen):
        """Ensena el resumen de la tanda y vuelve a leer el inicio desde cero."""
        dialogos.informar("Importación", resumen.texto(), parent=self.raiz)
        self.mostrar_inicio()

    # ---- ciclo de vida ---------------------------------------------------

    def ejecutar(self):
        """Arranca el bucle de la ventana y cierra la conexion al salir.

        La cifra de apertura se pide con `after_idle`: se escribe cuando ya no
        queda nada por dibujar, o sea justo cuando la ventana esta delante y
        responde. Escribirla antes seria medir otra cosa.
        """
        self.raiz.after_idle(self.anotar_cuanto_tardo_en_abrir)
        try:
            self.raiz.mainloop()
        finally:
            self.conexion.close()

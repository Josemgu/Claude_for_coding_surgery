using Fichas.App.Correccion;
using Fichas.Contratos.Modelos;
using Fichas.Datos.Falso;

namespace Fichas.Pruebas.App.Correccion;

/// <summary>
/// Los tres pasos de las bandas, y por que son tres y no uno.
/// </summary>
/// <remarks>
/// ⚠️ <b>Sale de una medicion, no de un gusto.</b> Con la ventana abierta sobre los siete
/// escaneos del dueno, entrar en Correccion costaba <b>8,3 s</b> con la ventana congelada:
/// 1,6 s en abrir el caso y el resto en preguntarle al documento donde estaba cada campo,
/// que es rasterizar otra vez y pasar el OCR entero, todo en el hilo de la ventana. Son las
/// palabras del dueno sobre el programa viejo: «conmigo es lento, se corta».
/// <para>
/// Partirlo en tres deja el paso caro —<see cref="ModeloDeCorreccion.LeerLasBandas"/>— fuera
/// del hilo de la ventana. Para eso tiene que cumplir dos cosas, y son las que se prueban
/// aqui: <b>no tocar nada del modelo</b> y <b>no tocar la base</b>.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLasBandasEnTresPasos
{
    private const long CasoDePrueba = 400;
    private const long PersonaDePrueba = 401;

    /// <summary>Monta un caso con ruta de PDF y una persona.</summary>
    private static ServiciosFalsos MontarConUnCasoConPdf()
    {
        var servicios = new ServiciosFalsos(2, 20260904, new RelojFijo("2026-09-04"));
        servicios.Almacen.Casos[CasoDePrueba] = new Caso
        {
            Id = CasoDePrueba,
            NumeroCaso = "CASP2609",
            UnidadNombre = "Castries Branch",
            FechaViaje = "2026-09-12",
            RutaPdf = @"C:\Users\josem\Documents\Fichas\pdf\lote-000.pdf",
            PaginaPdf = 1,
            CreadoEn = "2026-09-01",
        };
        servicios.Almacen.Personas[PersonaDePrueba] = new Persona
        {
            Id = PersonaDePrueba,
            CasoId = CasoDePrueba,
            Mrn = "055-1111-3853",
            Nombre = "Maria Anonimo",
            FilaFormulario = 1,
            PaginaPdf = 1,
        };
        return servicios;
    }

    /// <summary>Monta el modelo sobre ese almacen.</summary>
    private static ModeloDeCorreccion ModeloSobre(ServiciosFalsos servicios)
        => new(servicios.Casos, servicios.Personas, servicios.Procedencia,
               servicios.LecturaDePdf, servicios.Extraccion, servicios.Reloj, servicios.Companeros);

    /// <summary>Los tres pasos juntos hacen lo mismo que hacia el paso unico.</summary>
    [TestMethod]
    public void LosTresPasosJuntosRellenanLasBandasQueFaltan()
    {
        var modelo = ModeloSobre(MontarConUnCasoConPdf());
        modelo.Cargar(CasoDePrueba);

        var plan = modelo.PlanearLasBandas();
        Assert.IsNotNull(plan.Peticion, "Recien abierto y sin procedencia guardada, faltan bandas.");

        var leidas = modelo.LeerLasBandas(plan.Peticion);
        modelo.AplicarLasBandas(leidas);

        Assert.IsGreaterThan(0, modelo.Campos.Count(campo => campo.Banda is not null));
    }

    /// <summary>Leer el documento NO cambia ni una banda: eso es del tercer paso.</summary>
    /// <remarks>
    /// Es la condicion que permite llamarlo fuera del hilo de la ventana. Si este paso
    /// escribiera en <c>_campos</c>, moverlo a otro hilo seria una carrera silenciosa: la
    /// clase de fallo que no sale en ninguna prueba y aparece un martes en la maquina del
    /// dueno.
    /// </remarks>
    [TestMethod]
    public void LeerElDocumentoNoTocaNingunCampoDelModelo()
    {
        var modelo = ModeloSobre(MontarConUnCasoConPdf());
        modelo.Cargar(CasoDePrueba);
        modelo.LeerLasBandas(modelo.PlanearLasBandas().Peticion!);

        Assert.IsFalse(modelo.Campos.Any(campo => campo.Banda is not null));
    }

    /// <summary>Leer el documento tampoco escribe en el almacen.</summary>
    [TestMethod]
    public void LeerElDocumentoNoEscribeEnElAlmacen()
    {
        var servicios = MontarConUnCasoConPdf();
        var cuantas = servicios.Almacen.Procedencias.Count;
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        modelo.LeerLasBandas(modelo.PlanearLasBandas().Peticion!);

        Assert.HasCount(cuantas, servicios.Almacen.Procedencias);
        Assert.AreEqual(0, servicios.Procedencia.ContarVerificados(TablaDeProcedencia.Casos, CasoDePrueba));
    }

    /// <summary>
    /// Si NINGUN campo necesita banda, no se pide lectura ninguna.
    /// </summary>
    /// <remarks>
    /// Es donde esta el ahorro de verdad: cuando la importacion ya dejo la banda de cada
    /// campo, abrir el caso no tiene por que pagar ni el rasterizado ni el OCR.
    /// </remarks>
    [TestMethod]
    public void SiNoFaltaNingunaBandaNoSePideLeerElDocumento()
    {
        var servicios = MontarConUnCasoConPdf();
        foreach (var campo in new[] { "numero_caso", "unidad_numero", "unidad_nombre", "fecha_viaje", "templo_nombre" })
            AnotarConBanda(servicios, TablaDeProcedencia.Casos, CasoDePrueba, campo);
        foreach (var campo in new[] { "mrn", "nombre" })
            AnotarConBanda(servicios, TablaDeProcedencia.Personas, PersonaDePrueba, campo);

        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var plan = modelo.PlanearLasBandas();
        Assert.IsNull(plan.Peticion);
        Assert.IsEmpty(plan.Avisos);
    }

    /// <summary>Un caso sin ruta de PDF avisa en el primer paso y no manda leer nada.</summary>
    [TestMethod]
    public void UnCasoSinEscaneoAvisaEnElPrimerPasoYNoMandaLeer()
    {
        var servicios = MontarConUnCasoConPdf();
        servicios.Almacen.Casos[CasoDePrueba] = servicios.Almacen.Casos[CasoDePrueba] with { RutaPdf = null };
        var modelo = ModeloSobre(servicios);
        modelo.Cargar(CasoDePrueba);

        var plan = modelo.PlanearLasBandas();
        Assert.IsNull(plan.Peticion);
        Assert.HasCount(1, plan.Avisos);
        Assert.IsLessThanOrEqualTo(90, plan.Avisos[0].Linea.Length);
    }

    /// <summary>Anota una procedencia con banda para ese campo.</summary>
    private static void AnotarConBanda(ServiciosFalsos servicios, TablaDeProcedencia tabla, long registroId, string campo)
        => servicios.Procedencia.Anotar(new ProcedenciaDeCampo
        {
            Tabla = tabla,
            RegistroId = registroId,
            Campo = campo,
            Origen = OrigenDeCampo.Ocr,
            Confianza = 0.95,
            BandaX0 = 0.10,
            BandaY0 = 0.20,
            BandaX1 = 0.50,
            BandaY1 = 0.24,
        });
}

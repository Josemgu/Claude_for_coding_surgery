using Fichas.App.Vocabulario;
using Fichas.App.Inicio;

namespace Fichas.Pruebas.App.Inicio;

/// <summary>
/// FASE C20 — el Home cuenta PERSONAS del grupo que viene.
/// </summary>
/// <remarks>
/// <para>Es la peticion 7 del dueno, con sus palabras: <i>«En el Home debe decirme: las
/// personas del grupo del 17 de septiembre, faltan 3, 4 o 5 personas que la recomendación
/// para el templo no está confirmada. Ahí yo puedo verificarlos y ver en el sistema de la
/// Iglesia»</i>.</para>
///
/// <para>⛔ <b>Las dos cifras que el pidio el mismo dia —lo listo para asignar y lo
/// asignado— siguen donde estan y no se tocan</b> (C20-2). Esto es una tercera cosa.</para>
/// </remarks>
[TestClass]
public sealed class PruebasDelHomeQueCuentaPersonas
{
    /// <summary>
    /// C20-1. La frase es la suya y cuenta PERSONAS, con su denominador.
    /// </summary>
    /// <remarks>
    /// ⚠️ El documento trae 10 personas y es UNO solo: si el Home contara documentos, diria
    /// «1» donde tiene que decir «7». Esa es la comprobacion, y por eso el caso lleva diez
    /// personas y no una.
    /// </remarks>
    [TestMethod]
    public void ElHomeDiceCuantasPersonasFaltanPorConfirmarDelGrupoQueViene()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "SURB2609", "2026-09-17", cuantasPersonas: 10);
        for (var fila = 1; fila <= 3; fila++) BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila);

        var resumen = BaseDeInicio.LeerInicio(servicios);
        var grupo = resumen.ElSistemaDelObispo.ProximoGrupo;

        Assert.IsNotNull(grupo, "Hay un grupo con fecha por delante: tiene que salir.");
        Assert.AreEqual(10, grupo.CuantasPersonas);
        Assert.AreEqual(7, grupo.SinConfirmar);
        Assert.AreEqual(3, grupo.Confirmadas);

        StringAssert.Contains(grupo.Frase, "17 de septiembre", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Frase, "7 personas", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Frase, "de 10", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Frase, "confirmar", StringComparison.Ordinal);
        Assert.DoesNotContain("\n", grupo.Frase, "Ni un párrafo: una línea.");

        Assert.AreEqual(1, resumen.Denominadores.CasosNoArchivados, "Es UN documento.");
        Assert.AreNotEqual(
            resumen.Denominadores.CasosNoArchivados,
            grupo.SinConfirmar,
            "Si el número de personas coincidiera con el de documentos, no probaría nada.");
    }

    /// <summary>C20-1. Con una sola persona dice «1 persona», no «1 personas».</summary>
    [TestMethod]
    public void ConUnaSolaPersonaLoDiceEnSingular()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "UNAP2609", "2026-09-17", cuantasPersonas: 4);
        for (var fila = 1; fila <= 3; fila++) BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila);

        var grupo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.ProximoGrupo;

        Assert.IsNotNull(grupo);
        Assert.AreEqual(1, grupo.SinConfirmar);
        StringAssert.Contains(grupo.Frase, "1 persona ", StringComparison.Ordinal);
        Assert.DoesNotContain("1 personas", grupo.Frase);
    }

    /// <summary>
    /// C20-6. Un dia sin ninguna persona sin confirmar lo DICE, y no deja un hueco ni un cero mudo.
    /// </summary>
    [TestMethod]
    public void UnGrupoEnteroConfirmadoLoDiceConPalabras()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "TODO2609", "2026-09-17", cuantasPersonas: 3);
        for (var fila = 1; fila <= 3; fila++) BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila);

        var grupo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.ProximoGrupo;

        Assert.IsNotNull(grupo);
        Assert.AreEqual(0, grupo.SinConfirmar);
        Assert.IsGreaterThan(0, grupo.Frase.Length, "Un cero mudo no es una respuesta.");
        StringAssert.Contains(grupo.Frase, "17 de septiembre", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Frase, "confirmada", StringComparison.Ordinal);
        StringAssert.Contains(grupo.Frase, "3", StringComparison.Ordinal);
    }

    /// <summary>
    /// El proximo grupo es el PROXIMO: ni el que ya viajo, ni el de dentro de dos meses
    /// cuando hay uno antes.
    /// </summary>
    [TestMethod]
    public void ElProximoGrupoEsElPrimeroQueTodaviaNoHaViajado()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "YAVI2608", "2026-08-20", cuantasPersonas: 5);
        BaseDeInicio.MeterCaso(servicios, "PROX2609", "2026-09-17", cuantasPersonas: 2);
        BaseDeInicio.MeterCaso(servicios, "TARD2611", "2026-11-30", cuantasPersonas: 9);

        var grupo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.ProximoGrupo;

        Assert.IsNotNull(grupo);
        Assert.AreEqual(new DateOnly(2026, 9, 17), grupo.Fecha);
        Assert.AreEqual(2, grupo.CuantasPersonas);
    }

    /// <summary>Sin ningun grupo por delante, se dice; no se deja el recuadro en blanco.</summary>
    [TestMethod]
    public void SinNingunGrupoPorDelanteSeDice()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        BaseDeInicio.MeterCaso(servicios, "SOLO0000", fechaViaje: null, cuantasPersonas: 3);

        var loDelObispo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo;

        Assert.IsNull(loDelObispo.ProximoGrupo);
        Assert.IsGreaterThan(0, loDelObispo.FraseDelProximoGrupo.Length, "Un hueco no es una respuesta.");
    }

    /// <summary>
    /// C20-2. Las dos cifras que el dueno pidio el 2026-09-05 siguen donde estaban.
    /// </summary>
    [TestMethod]
    public void LasDosCifrasDeSiempreNoSeTocan()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var listo = BaseDeInicio.MeterCaso(servicios, "LIST2609", "2026-09-17", cuantasPersonas: 2);
        var asignado = BaseDeInicio.MeterCaso(servicios, "ASIG2609", "2026-09-17", cuantasPersonas: 3);
        BaseDeInicio.Asignar(servicios, asignado, BaseDeInicio.PrimerCompaneroActivo(servicios));
        BaseDeInicio.DejarNoListaParaViajar(servicios, listo, fila: 1);

        var resumen = BaseDeInicio.LeerInicio(servicios);

        Assert.AreEqual(1, resumen.Contadores.ListoParaAsignar);
        Assert.AreEqual(1, resumen.Contadores.AsignadoALosAgentes);
        Assert.AreEqual(2, resumen.Contadores.PersonasListas);
        StringAssert.Contains(resumen.DeQueVaLoListo, "2 personas", StringComparison.Ordinal);
    }

    /// <summary>
    /// C20-3. La pastilla del calendario dice «N de M confirmadas» en PERSONAS.
    /// </summary>
    /// <remarks>
    /// ⚠️ Antes decia «8 pers. · 0/4», donde el 0 y el 4 eran DOCUMENTOS completos. Con dos
    /// documentos de cinco y una persona, un «1/2» no le dice cuanta gente le falta por
    /// verificar, que es lo unico que el va a hacer con esa cifra.
    /// </remarks>
    [TestMethod]
    public void LaPastillaDelCalendarioCuentaPersonasConfirmadas()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var grande = BaseDeInicio.MeterCaso(servicios, "GRAN2609", "2026-09-17", unidadNumero: "700001", cuantasPersonas: 5);
        var chico = BaseDeInicio.MeterCaso(servicios, "CHIC2609", "2026-09-17", unidadNumero: "700001", cuantasPersonas: 1);

        BaseDeInicio.DejarListaParaViajar(servicios, grande, fila: 1);
        BaseDeInicio.DejarListaParaViajar(servicios, chico, fila: 1);

        var dia = BaseDeInicio.LeerInicio(servicios).Mes.Dias.Single(d => d.EsDelMes && d.Numero == 17);

        Assert.HasCount(1, dia.Pastillas, "Los dos documentos son de la misma unidad: una pastilla.");
        Assert.AreEqual(6, dia.Pastillas[0].CuantasPersonas);
        // ⚠️ La cifra sigue siendo de PERSONAS confirmadas y no de documentos, que es lo que
        // esta prueba defiende. La redacción cambió el 2026-09-07: decía «2 de 6 confirmadas».
        Assert.AreEqual(2, dia.Pastillas[0].CuantasPersonasConfirmadas);
        Assert.AreEqual("me falta 4 de 6", dia.Pastillas[0].Etiqueta);
        // En voz alta SÍ se dice «confirmadas»: el lector de pantalla lee la explicación entera,
        // que es el equivalente del detalle que en pantalla está a un clic.
        StringAssert.Contains(dia.Pastillas[0].ParaElLector, "confirmadas", StringComparison.Ordinal);
    }

    /// <summary>
    /// C12-2 sigue mandando: el numero de elementos visuales del calendario no depende de
    /// cuantos casos haya.
    /// </summary>
    /// <remarks>
    /// Contar personas confirmadas obliga a leer las personas de todo el mes; si eso hubiera
    /// hecho crecer las pastillas por dia, la pantalla dejaria de tener tope. Se comprueba
    /// sobre la base grande.
    /// </remarks>
    [TestMethod]
    public void ContarPersonasNoLeQuitaElTopeAlCalendario()
    {
        var servicios = BaseDeInicio.MontarServicios(BaseDeInicio.CasosDePrueba);

        var mes = BaseDeInicio.LeerInicio(servicios).Mes;

        Assert.HasCount(42, mes.Dias, "Las 42 celdas son fijas.");
        foreach (var dia in mes.Dias)
        {
            Assert.IsLessThanOrEqualTo(CalendarioDelMes.PastillasPorDia, dia.Pastillas.Count,
                $"El día {dia.Numero} pinta {dia.Pastillas.Count} pastillas y el tope es {CalendarioDelMes.PastillasPorDia}.");
        }
    }

    /// <summary>
    /// C20-4. El denominador se ve siempre: una cifra sin denominador no se puede comprobar.
    /// </summary>
    [TestMethod]
    public void LaCifraDePersonasSiempreLlevaSuDenominador()
    {
        var servicios = BaseDeInicio.MontarServicios(0);
        var casoId = BaseDeInicio.MeterCaso(servicios, "DENO2609", "2026-09-17", cuantasPersonas: 6);
        BaseDeInicio.DejarListaParaViajar(servicios, casoId, fila: 1);

        var grupo = BaseDeInicio.LeerInicio(servicios).ElSistemaDelObispo.ProximoGrupo;

        Assert.IsNotNull(grupo);
        StringAssert.Contains(grupo.Frase, "de 6", StringComparison.Ordinal);
    }

    /// <summary>
    /// C20-5. Con 3 000 documentos, Inicio lee la tabla de personas UNA sola vez.
    /// </summary>
    /// <remarks>
    /// <para>⛔ <b>Esta prueba cuenta consultas y NO mide milisegundos, y eso es deliberado.</b>
    /// Un tope en milisegundos dentro de esta suite no mide el codigo: mide la maquina. La
    /// suite corre en paralelo (<c>Parallelize(Workers = 0)</c>) y esta prueba comparte
    /// procesador con otras que montan bases de 3 000. Medido el mismo dia, la MISMA llamada
    /// dio <b>94-103 ms</b> corriendo sola y <b>200 y 312 ms</b> con la suite entera encima.
    /// Una prueba que se pone roja por eso deja de ser una red y pasa a ser ruido.</para>
    ///
    /// <para><b>Lo que si es determinista, y es lo que de verdad hace falta vigilar:</b>
    /// cuantas veces se va a la base. El riesgo real de contar personas en el calendario es
    /// leer la tabla dos veces —o una por documento—, y eso no depende de la maquina. Ya
    /// pasó en esta misma fase: al hacer que la pastilla contara personas confirmadas, Inicio
    /// leyó la tabla dos veces por pintado y el resumen pasó de <b>58 ms</b> a <b>362 ms</b>.
    /// Se arregló pasando las personas ya leídas a <c>LectorDeGrupos.PorDia</c>.</para>
    ///
    /// <para><b>Y lo que esta fase cuesta, medido corriendo sola y sin esconderlo:</b> antes
    /// de tocar nada, la mejor de cinco era <b>58,2 ms</b>; después, <b>94-103 ms</b>. Son
    /// unos <b>40 ms</b> más. Cabe en el tope, pero no es gratis.</para>
    ///
    /// <para>El tope de 0,2 s del criterio C20-5 es sobre la PANTALLA PINTADA y se demuestra
    /// con la ventana abierta, con <c>/medir-inicio</c>, sobre el paquete publicado.</para>
    /// </remarks>
    [TestMethod]
    public void ConTresMilDocumentosInicioLeeLasPersonasUnaSolaVez()
    {
        var servicios = BaseDeInicio.MontarServicios(3000);
        var contadas = new PersonasQueSeDejanContar(servicios.Personas);
        var lector = new LectorDelInicio(
            servicios.Casos, contadas, servicios.Companeros, servicios.Asignaciones,
            servicios.Reloj, servicios.Procedencia);

        lector.Leer();
        contadas.EmpezarDeCero();
        var resumen = lector.Leer();

        Assert.IsNotNull(resumen.ElSistemaDelObispo);
        Assert.AreEqual(1, contadas.CuantasVecesListo,
            $"Inicio listó las personas {contadas.CuantasVecesListo} veces para pintarse una.");
        Assert.AreEqual(0, contadas.CuantasVecesPidioLasDeUnCaso,
            "Preguntar documento a documento serían 3 000 idas a la base para pintar una pantalla.");
    }
}

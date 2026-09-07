using System.Reflection;
using Fichas.Contratos.Modelos;

namespace Fichas.Pruebas.Contratos;

/// <summary>
/// Los modelos tienen que tener las columnas del esquema al dia, ni una mas ni una menos.
/// </summary>
/// <remarks>
/// <para>
/// El criterio del que sale esta prueba es de docs/ARQUITECTURA.md §2: «Nueve tablas y
/// 104 columnas». Si manana entra una migracion y estos numeros no se tocan, la prueba
/// falla y obliga a mirar el esquema en vez de descubrirlo en la pantalla. Es lo que
/// paso con la 18, y funciono.
/// </para>
/// <para>
/// ⚠️ Son <b>108 y no 104</b> desde la migracion 18 (FASE C10): <c>casos</c> gana los
/// dos motivos —<c>motivo_no_completa</c> y <c>motivo_del_companero</c>— y
/// <c>companeros</c> gana <c>rol</c> y <c>categoria</c>. ARQUITECTURA §2 sigue diciendo
/// 104 y lo corrige su titular, el planificador; el numero que manda es el del esquema.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebasDeLosModelos
{
    /// <summary>Las nueve tablas del esquema al dia con las columnas que tiene cada una.</summary>
    private static readonly (Type Modelo, string Tabla, int Columnas)[] LasNueveTablas =
    [
        (typeof(VersionDeEsquema), "version_esquema", 3),
        (typeof(Caso), "casos", 22),
        (typeof(Persona), "personas", 25),
        (typeof(Companero), "companeros", 7),
        (typeof(Asignacion), "asignaciones", 6),
        (typeof(Contacto), "contactos", 12),
        (typeof(ProcedenciaDeCampo), "procedencia_campo", 16),
        (typeof(RenglonIlegible), "documentos_ilegibles", 8),
        (typeof(FilaDescartada), "filas_descartadas", 9),
    ];

    /// <summary>Cada modelo tiene exactamente las columnas de su tabla.</summary>
    [TestMethod]
    public void CadaModeloTieneLasColumnasDeSuTabla()
    {
        foreach (var (modelo, tabla, columnas) in LasNueveTablas)
        {
            Assert.AreEqual(columnas, ContarColumnas(modelo),
                $"El modelo {modelo.Name} no cuadra con las columnas de «{tabla}» en ARQUITECTURA §2.");
        }
    }

    /// <summary>Las nueve tablas suman las 108 columnas del esquema al dia.</summary>
    [TestMethod]
    public void LasNueveTablasSuman108Columnas()
    {
        var total = LasNueveTablas.Sum(t => ContarColumnas(t.Modelo));
        Assert.AreEqual(
            108,
            total,
            "El esquema al dia tiene 108 columnas repartidas en nueve tablas: las 104 " +
            "que declara ARQUITECTURA §2 mas las cuatro de la migracion 18.");
        Assert.HasCount(9, LasNueveTablas);
    }

    /// <summary>Toda columna que el esquema deja nula se puede dejar nula en el modelo.</summary>
    [TestMethod]
    public void LasColumnasQueAceptanNuloSeGuardanNulas()
    {
        var caso = new Caso { CreadoEn = "2026-09-04" };

        Assert.IsNull(caso.NumeroCaso, "Desde la migracion 7 un caso sin numero legible entra igual.");
        Assert.IsNull(caso.FechaViaje);
        Assert.IsNull(caso.UnidadNumero);
        Assert.IsNull(caso.TemploNombre);
        Assert.IsFalse(caso.Archivado);
        Assert.IsNull(caso.FechaArchivado, "Archivado y su fecha van juntos: o las dos o ninguna.");
    }

    /// <summary>Las casillas tienen tres valores, no dos: si, no y «nadie lo miro».</summary>
    [TestMethod]
    public void UnaCasillaSinMirarNoEsLoMismoQueUnaCasillaEnNo()
    {
        var persona = new Persona { CasoId = 1 };

        Assert.IsNull(persona.OrdRecibirPropias, "Nadie ha mirado esta casilla todavia.");
        Assert.IsNull(persona.PasoEntrevistas);
        Assert.AreNotEqual((bool?)false, persona.OrdRecibirPropias,
            "«No leida» y «no marcada» son cosas distintas y no se pueden confundir.");
    }

    /// <summary>Un campo se guarda sin firmar; la firma es otra cosa y va aparte.</summary>
    [TestMethod]
    public void UnCampoNaceSinFirmar()
    {
        var procedencia = new ProcedenciaDeCampo { Campo = "fecha_viaje", RegistroId = 1 };

        Assert.IsFalse(procedencia.Verificado, "Regla permanente 5: nada se marca verificado solo.");
        Assert.IsNull(procedencia.VerificadoPor);
        Assert.IsNull(procedencia.VerificadoEn);
    }

    /// <summary>Cuenta las columnas de un modelo: las propiedades que se pueden escribir al crearlo.</summary>
    private static int ContarColumnas(Type modelo)
        => modelo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(p => p.CanWrite);
}

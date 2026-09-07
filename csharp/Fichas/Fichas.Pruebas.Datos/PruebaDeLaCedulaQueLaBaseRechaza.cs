using Fichas.Datos.Esquema;
using Fichas.Datos.Validacion;
using Microsoft.Data.Sqlite;

namespace Fichas.Pruebas.Datos;

/// <summary>
/// Una cédula mal leída se GUARDA y se señala; no se rechaza. Aquí se pierde una PERSONA.
/// </summary>
/// <remarks>
/// <para>
/// Es el mismo caso por el que el dueño mandó quitar el <c>CHECK</c> del número de caso
/// (DECISIONES.md, «El CHECK del número de caso se quita en el programa nuevo», y su
/// requisito 9: «avisar, nunca impedir»), y aquí pesa más: lo que un <c>CHECK</c> tira no
/// es un número, es la fila de una persona que iba a viajar.
/// </para>
/// <para>
/// Medido por QA el 2026-09-04 insertando en una copia: una cédula de la forma
/// <c>066-2222-133A</c> —tres dígitos, guion, cuatro dígitos, guion, tres dígitos y una
/// LETRA— entra; <c>123</c> devuelve «CHECK constraint failed» y <c>''</c> también. La
/// migración 17 quita ese <c>CHECK</c>; lo que se conserva es la señal, que ya vive en
/// <see cref="ReglasDeFormato.RevisarMrn"/>: se guarda igual y se avisa.
/// </para>
/// <para>
/// ⚠️ El valor concreto que QA metió aquel día va SUSTITUIDO desde el 2026-09-07, como
/// todos los de este repositorio. Lo que se midió fue la forma, y la forma es la que esta
/// prueba sigue metiendo por el motor: si alguien la cambia por una que no termine en
/// letra, la prueba se queda verde y deja de vigilar. Ver `EN-CURSO.md`, «Los datos de
/// personas reales salen del repositorio».
/// </para>
/// <para>
/// ⚠️ La prueba se deriva del criterio y no del código: no mira el texto del DDL, mete
/// las filas por el motor y comprueba que están.
/// </para>
/// </remarks>
[TestClass]
public sealed class PruebaDeLaCedulaQueLaBaseRechaza
{
    /// <summary>Una cédula con la forma buena, con letra al final (migración 15).</summary>
    private const string CedulaBuena = "066-2222-133A";

    /// <summary>Lo que el OCR devuelve cuando solo pilló un trozo de la banda.</summary>
    private const string CedulaAMedias = "123";

    /// <summary>
    /// Dado un MRN que no tiene la forma esperada, cuando se guarda la persona, entonces
    /// la fila ENTRA.
    /// </summary>
    /// <remarks>
    /// Se prueban los tres casos que QA midió y uno más —una cédula con letras dentro—
    /// porque el papel del dueño trae cédulas raras y perder una persona por eso es
    /// exactamente el daño que este programa existe para evitar.
    /// </remarks>
    [TestMethod]
    [DataRow(CedulaBuena, DisplayName = "la que ya entraba")]
    [DataRow(CedulaAMedias, DisplayName = "solo un trozo de la banda")]
    [DataRow("", DisplayName = "vacía")]
    [DataRow("066-2222-13AB", DisplayName = "dos letras al final")]
    [DataRow("O66-2222-1334", DisplayName = "una O donde va un cero")]
    public void UnaCedulaRaraSeGuardaYNoSeRechaza(string mrn)
    {
        using var baseDeDatos = BaseDePrueba.Nueva();
        var caso = InsertarUnCaso(baseDeDatos.Conexion);

        InsertarUnaPersona(baseDeDatos.Conexion, caso, mrn);

        Assert.AreEqual(1, ContarPersonasCon(baseDeDatos.Conexion, mrn),
            $"la persona con la cédula «{mrn}» no entró: se perdió una persona.");
    }

    /// <summary>
    /// Y lo que se pierde en el motor se gana en la señal: una cédula rara deja aviso.
    /// </summary>
    /// <remarks>
    /// Quitar el <c>CHECK</c> sin dejar la señal sería cambiar «se pierde el dato» por
    /// «el dato malo pasa desapercibido», que no es lo que el dueño pidió.
    /// </remarks>
    [TestMethod]
    public void UnaCedulaRaraDejaAvisoYUnaBuenaNo()
    {
        Assert.IsNotNull(ReglasDeFormato.RevisarMrn(CedulaAMedias),
            "una cédula a medias tiene que quedar señalada.");
        Assert.IsNull(ReglasDeFormato.RevisarMrn(CedulaBuena),
            "una cédula con la forma buena no puede dar aviso; si diera siempre, dejaría "
            + "de significar nada.");
    }

    /// <summary>
    /// Una persona SIN nombre y SIN cédula sigue sin entrar: eso no es un dato raro, es
    /// una fila en blanco.
    /// </summary>
    /// <remarks>
    /// La migración 17 quita UN <c>CHECK</c>, el de la forma del MRN, y ni uno más. El
    /// requisito 9 habla de un valor raro que se guarda y se señala; una fila sin ningún
    /// dato no señala nada porque no hay nada que enseñar en pantalla.
    /// </remarks>
    [TestMethod]
    public void UnaPersonaSinNombreYSinCedulaSigueSinEntrar()
    {
        using var baseDeDatos = BaseDePrueba.Nueva();
        var caso = InsertarUnCaso(baseDeDatos.Conexion);

        var fallo = Assert.ThrowsExactly<SqliteException>(
            () => InsertarUnaPersona(baseDeDatos.Conexion, caso, null, null));

        StringAssert.Contains(fallo.Message, "CHECK constraint failed");
    }

    /// <summary>
    /// Una base que YA venía en la 16 —la del dueño hoy— también acaba aceptándolas.
    /// </summary>
    /// <remarks>
    /// Es la comprobación que faltó con el número de caso y costó la migración 16: una
    /// migración solo sirve si se aplica sobre la base que existe de verdad, no sobre una
    /// recién creada.
    /// </remarks>
    [TestMethod]
    public void UnaBaseQueVeniaDeLaVersion16TambienAcabaAceptandolas()
    {
        using var baseDeDatos = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDeDatos.Conexion, 16);
        var caso = InsertarUnCaso(baseDeDatos.Conexion);

        var rechazadaAntes = false;
        try
        {
            InsertarUnaPersona(baseDeDatos.Conexion, caso, CedulaAMedias);
        }
        catch (SqliteException)
        {
            rechazadaAntes = true;
        }

        AplicadorDeEsquema.Aplicar(baseDeDatos.Conexion);
        InsertarUnaPersona(baseDeDatos.Conexion, caso, CedulaAMedias);

        Assert.IsTrue(rechazadaAntes,
            "en la 16 la cédula a medias ya entraba: esta prueba no está midiendo nada.");
        Assert.AreEqual(1, ContarPersonasCon(baseDeDatos.Conexion, CedulaAMedias));
    }

    /// <summary>
    /// Migrar no se lleva por delante las personas que ya estaban, ni sus cédulas.
    /// </summary>
    [TestMethod]
    public void LasPersonasQueYaEstabanSobrevivenALaMigracion()
    {
        using var baseDeDatos = BaseDePrueba.SinEsquema();
        AplicadorDeEsquema.AplicarHasta(baseDeDatos.Conexion, 16);
        var caso = InsertarUnCaso(baseDeDatos.Conexion);
        InsertarUnaPersona(baseDeDatos.Conexion, caso, CedulaBuena, "Ana Prueba");

        AplicadorDeEsquema.Aplicar(baseDeDatos.Conexion);

        Assert.AreEqual(1, ContarPersonasCon(baseDeDatos.Conexion, CedulaBuena));
        using var orden = baseDeDatos.Conexion.CreateCommand();
        orden.CommandText = "SELECT nombre FROM personas WHERE mrn = $mrn";
        orden.Parameters.AddWithValue("$mrn", CedulaBuena);
        Assert.AreEqual("Ana Prueba", Convert.ToString(orden.ExecuteScalar()));
    }

    /// <summary>Un caso mínimo al que colgar las personas.</summary>
    private static long InsertarUnCaso(SqliteConnection conexion)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText =
            "INSERT INTO casos (numero_caso, creado_en) VALUES ('CASP2609', '2026-09-04 10:00:00'); "
            + "SELECT last_insert_rowid()";
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Mete una persona con la cédula que se le pase.</summary>
    private static void InsertarUnaPersona(
        SqliteConnection conexion, long caso, string? mrn, string? nombre = "Sin nombre")
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "INSERT INTO personas (caso_id, mrn, nombre) VALUES ($caso, $mrn, $nombre)";
        orden.Parameters.AddWithValue("$caso", caso);
        orden.Parameters.AddWithValue("$mrn", (object?)mrn ?? DBNull.Value);
        orden.Parameters.AddWithValue("$nombre", (object?)nombre ?? DBNull.Value);
        orden.ExecuteNonQuery();
    }

    /// <summary>Cuántas personas hay con esa cédula exacta.</summary>
    private static long ContarPersonasCon(SqliteConnection conexion, string mrn)
    {
        using var orden = conexion.CreateCommand();
        orden.CommandText = "SELECT COUNT(*) FROM personas WHERE mrn = $mrn";
        orden.Parameters.AddWithValue("$mrn", mrn);
        return Convert.ToInt64(orden.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

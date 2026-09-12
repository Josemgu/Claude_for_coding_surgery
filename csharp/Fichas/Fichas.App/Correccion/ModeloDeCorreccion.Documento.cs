using Fichas.Contratos.Lectura;
using Fichas.Contratos.Modelos;

namespace Fichas.App.Correccion;

/// <summary>Un campo al que le falta la banda, con lo justo para poder buscarla.</summary>
/// <remarks>
/// Es una COPIA de los datos de identidad del campo, no el campo. Existe para que el paso
/// caro pueda correr fuera del hilo de la ventana sin tocar ni un <see cref="CampoEnPantalla"/>.
/// </remarks>
/// <param name="Clave">Con que clave se devolvera la banda encontrada.</param>
/// <param name="Tabla">Si es del caso o de una persona.</param>
/// <param name="Campo">Nombre de la columna.</param>
/// <param name="Hoja">De que hoja del PDF salio, base 1.</param>
/// <param name="FilaFormulario">En que fila del formulario venia, si es de una persona.</param>
public sealed record CampoSinBanda(
    string Clave, TablaDeProcedencia Tabla, string Campo, int Hoja, int? FilaFormulario);

/// <summary>Lo que hay que ir a buscar al documento: que archivo y que campos.</summary>
/// <param name="RutaPdf">El escaneo del caso.</param>
/// <param name="Faltantes">Los campos sin banda, ya copiados.</param>
public sealed record PeticionDeBandas(string RutaPdf, IReadOnlyList<CampoSinBanda> Faltantes);

/// <summary>El primer paso: si hay que leer el documento, y que hay que decir si no.</summary>
/// <param name="Peticion">Lo que hay que buscar, o nulo si no hace falta leer nada.</param>
/// <param name="Avisos">Lo que hay que decir en la franja; vacio si no hay nada que decir.</param>
public sealed record PlanDeBandas(PeticionDeBandas? Peticion, IReadOnlyList<Aviso> Avisos);

/// <summary>El segundo paso: las bandas que se encontraron, por clave de campo.</summary>
/// <param name="PorClave">La banda de cada campo que se pudo situar.</param>
/// <param name="Avisos">Las hojas que no se pudieron abrir, con su motivo.</param>
public sealed record BandasLeidas(
    IReadOnlyDictionary<string, BandaDeLaPagina> PorClave, IReadOnlyList<Aviso> Avisos);

/// <summary>
/// La parte del modelo que pregunta al escaneo DONDE estaba cada campo, para que el visor
/// pueda iluminarlo. De aqui sale la banda y nada mas: ni un valor ni una confianza.
/// </summary>
/// <remarks>
/// Va en tres pasos —planear, leer, aplicar— porque el del medio es el caro y es el unico
/// que puede correr fuera del hilo de la ventana; el motivo medido esta en
/// <see cref="CompletarBandasDesdeElDocumento"/>.
/// </remarks>
public sealed partial class ModeloDeCorreccion
{
    /// <summary>Ancho al que se rasteriza para preguntar por las bandas; no se pinta con ella.</summary>
    private const int AnchoParaMirarLaHoja = 1700;

    /// <summary>
    /// Pregunta al documento donde estaba cada campo, en los tres pasos, de una vez.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Esto bloquea mientras lee, y por eso la pantalla NO lo llama.</b> Medido con la
    /// ventana abierta sobre los siete escaneos del dueno: entrar en Correccion costaba 8,3 s
    /// con la ventana congelada, y 6,6 de esos eran esto. Se queda porque es la forma comoda
    /// de probarlo sin ventana y sin hilos; quien tiene ventana usa los tres pasos.
    /// </remarks>
    /// <returns>Lo que hay que decir en la franja; vacio si no hay nada que decir.</returns>
    public IReadOnlyList<Aviso> CompletarBandasDesdeElDocumento()
    {
        var plan = PlanearLasBandas();
        if (plan.Peticion is null) return plan.Avisos;

        var leidas = LeerLasBandas(plan.Peticion);
        return [.. plan.Avisos, .. AplicarLasBandas(leidas)];
    }

    /// <summary>
    /// Primer paso, EN EL HILO DE LA VENTANA: dice si hay que leer el documento y que buscar.
    /// </summary>
    /// <remarks>
    /// Devuelve la peticion en nulo cuando no hay nada que buscar, y ese nulo es el ahorro de
    /// verdad: si la importacion ya dejo la banda de cada campo, abrir el caso no paga ni el
    /// rasterizado ni el OCR.
    /// </remarks>
    public PlanDeBandas PlanearLasBandas()
    {
        if (_caso is null) return new PlanDeBandas(null, []);

        var ruta = ReglasDeCampo.Limpiar(_caso.RutaPdf);
        if (ruta is null)
        {
            return new PlanDeBandas(null,
            [
                Aviso.Advierte(
                    "este caso no tiene escaneo: se corrige sin imagen al lado",
                    string.Empty,
                    "El caso no guarda ninguna ruta de PDF, asi que no hay documento que ensenar ni "
                    + "banda que iluminar. Los campos se corrigen igual, tecleando."),
            ]);
        }

        var faltantes = _campos
            .Where(campo => campo.Banda is null)
            .Select(campo => new CampoSinBanda(
                campo.Clave, campo.Tabla, campo.Campo, campo.PaginaPdf ?? 1, campo.FilaFormulario))
            .ToList();

        return faltantes.Count == 0
            ? new PlanDeBandas(null, [])
            : new PlanDeBandas(new PeticionDeBandas(ruta, faltantes), []);
    }

    /// <summary>
    /// Segundo paso, FUERA DEL HILO DE LA VENTANA: lee el documento y sitúa cada campo.
    /// </summary>
    /// <remarks>
    /// ⛔ <b>Este metodo no toca <c>_campos</c>, ni <c>_caso</c>, ni el almacen.</b> Esa es
    /// justo la condicion que permite llamarlo desde otro hilo, y hay pruebas que la fijan:
    /// si algun dia escribe algo aqui, se convierte en una carrera silenciosa.
    /// <para>
    /// ⚠️ Y de aqui se toma la BANDA y nada mas. Ni el valor —seria inventar, y la regla
    /// permanente 1 lo prohibe— ni la confianza —pegarle a un dato guardado la confianza de
    /// otra lectura seria decir que se leyo algo que no se leyo—.
    /// </para>
    /// </remarks>
    /// <param name="peticion">Que archivo abrir y que campos buscar, tal como lo dejo <see cref="PlanearLasBandas"/>.</param>
    /// <returns>Las bandas encontradas por clave, y un aviso por cada hoja que no se pudo abrir.</returns>
    /// <exception cref="ArgumentNullException">Si la peticion es nula.</exception>
    public BandasLeidas LeerLasBandas(PeticionDeBandas peticion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var encontradas = new Dictionary<string, BandaDeLaPagina>(StringComparer.Ordinal);
        var avisos = new List<Aviso>();

        foreach (var hoja in peticion.Faltantes.Select(campo => campo.Hoja).Distinct())
        {
            var imagen = _lecturaDePdf.RasterizarPagina(peticion.RutaPdf, hoja, AnchoParaMirarLaHoja);
            if (imagen is null)
            {
                avisos.Add(Aviso.Advierte(
                    $"la hoja {hoja} del escaneo no se pudo abrir: se corrige sin ella",
                    string.Empty,
                    $"No se pudo rasterizar la pagina {hoja} de «{peticion.RutaPdf}». Los campos de esa "
                    + "hoja se corrigen igual, pero sin poder iluminar donde estaban en el papel."));
                continue;
            }

            SituarLosCamposDeUnaHoja(peticion, hoja, imagen, encontradas);
        }

        return new BandasLeidas(encontradas, avisos);
    }

    /// <summary>
    /// Tercer paso, EN EL HILO DE LA VENTANA: reparte lo leido entre las fichas.
    /// </summary>
    /// <remarks>
    /// Una banda que YA venia guardada no se pisa: manda lo que hay en la base, no lo que se
    /// acaba de mirar. Y un campo que ya no esta —porque se cambio de caso mientras se leia—
    /// simplemente no se encuentra y no pasa nada.
    /// </remarks>
    /// <param name="leidas">Lo que devolvio <see cref="LeerLasBandas"/>.</param>
    /// <returns>Los avisos que venian en la lectura, tal cual, para que la pantalla los enseñe.</returns>
    /// <exception cref="ArgumentNullException">Si lo leido es nulo.</exception>
    public IReadOnlyList<Aviso> AplicarLasBandas(BandasLeidas leidas)
    {
        ArgumentNullException.ThrowIfNull(leidas);

        foreach (var campo in _campos.Where(campo => campo.Banda is null))
            if (leidas.PorClave.TryGetValue(campo.Clave, out var banda)) campo.Banda = banda;

        return leidas.Avisos;
    }

    /// <summary>Sitúa en una hoja los campos que la peticion pidió de esa hoja.</summary>
    /// <remarks>
    /// Corre el OCR y la extraccion enteros sobre la hoja —lo mismo que hace la importacion—
    /// y de lo propuesto se queda solo con la banda del campo que casa por tabla, columna y
    /// fila. Es lo caro del segundo paso.
    /// </remarks>
    /// <param name="peticion">La peticion entera; de ella se toman la ruta y los campos de esta hoja.</param>
    /// <param name="hoja">Que hoja se esta mirando, base 1.</param>
    /// <param name="imagen">La hoja ya rasterizada.</param>
    /// <param name="encontradas">Donde se van apuntando las bandas, por clave de campo.</param>
    private void SituarLosCamposDeUnaHoja(
        PeticionDeBandas peticion,
        int hoja,
        ImagenDePagina imagen,
        Dictionary<string, BandaDeLaPagina> encontradas)
    {
        var lineas = _lecturaDePdf.LeerConOcr(imagen);
        var anotaciones = _lecturaDePdf.LeerAnotaciones(peticion.RutaPdf, hoja);
        var deEstaHoja = peticion.Faltantes.Where(campo => campo.Hoja == hoja).ToList();

        foreach (var propuesto in _extraccion.ProponerCamposDelCaso(lineas, anotaciones).Campos)
            AnotarLaBanda(deEstaHoja, TablaDeProcedencia.Casos, propuesto, filaQueCasa: null, encontradas);

        foreach (var propuesto in _extraccion.ProponerCamposDePersonas(lineas, anotaciones).Campos)
            AnotarLaBanda(deEstaHoja, TablaDeProcedencia.Personas, propuesto, propuesto.FilaFormulario, encontradas);
    }

    /// <summary>Apunta la banda de un campo propuesto en la clave que le toca, si sigue libre.</summary>
    /// <remarks>
    /// Un propuesto sin banda no apunta nada, y una clave ya apuntada no se pisa: la primera
    /// propuesta que casa es la que vale, para que dos lineas de OCR sobre la misma columna
    /// no se roben la banda.
    /// </remarks>
    /// <param name="candidatos">Los campos sin banda de esta hoja.</param>
    /// <param name="tabla">Si lo propuesto es del caso o de una persona.</param>
    /// <param name="propuesto">Lo que la extraccion propuso; solo se usa su columna, su fila y su banda.</param>
    /// <param name="filaQueCasa">La fila del formulario que tiene que casar; nula para los campos del caso.</param>
    /// <param name="encontradas">Donde se apunta la banda, por clave de campo.</param>
    private static void AnotarLaBanda(
        List<CampoSinBanda> candidatos,
        TablaDeProcedencia tabla,
        CampoPropuesto propuesto,
        int? filaQueCasa,
        Dictionary<string, BandaDeLaPagina> encontradas)
    {
        if (propuesto.Banda is not BandaDeLaPagina banda) return;

        var cual = candidatos.FirstOrDefault(campo =>
            !encontradas.ContainsKey(campo.Clave)
            && campo.Tabla == tabla
            && campo.Campo == propuesto.Campo
            && (filaQueCasa is null || campo.FilaFormulario == filaQueCasa));

        if (cual is not null) encontradas[cual.Clave] = banda;
    }
}

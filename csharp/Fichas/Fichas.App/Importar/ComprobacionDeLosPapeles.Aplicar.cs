using Fichas.Contratos.Modelos;
using Fichas.Reportes.Reglas;

namespace Fichas.App.Importar;

/// <summary>
/// El tercer paso de la comprobación: escribir lo que las lecturas permiten decidir.
/// Corre en el hilo de la base.
/// </summary>
public sealed partial class ComprobacionDeLosPapeles
{
    /// <summary>Aplica las lecturas: vacía, conserva, recupera y marca; y cuenta cada cosa.</summary>
    /// <param name="lecturas">Lo que dejó <see cref="Leer"/>.</param>
    /// <returns>Las cifras, los avisos para la franja y los renglones para el cuaderno.</returns>
    public ResultadoDeLosPapeles Aplicar(LecturasDeLosPapeles lecturas)
    {
        ArgumentNullException.ThrowIfNull(lecturas);

        var cuenta = new Cuenta();
        foreach (var lectura in lecturas.Disputas) AplicarUnaDisputa(lectura, cuenta);
        foreach (var lectura in lecturas.Perdidos) AplicarUnPerdido(lectura, cuenta);
        foreach (var par in lecturas.Pares) AplicarUnPar(par, cuenta);

        return new ResultadoDeLosPapeles(
            cuenta.Vaciados, cuenta.Conservados, cuenta.Recuperados, cuenta.Marcados, cuenta.SinDecidir,
            cuenta.Avisos, cuenta.Renglones);
    }

    /// <summary>Lo que se va contando mientras se aplica.</summary>
    private sealed class Cuenta
    {
        /// <summary>Documentos que perdieron el papel porque no era suyo.</summary>
        public int Vaciados;
        /// <summary>Documentos que se quedaron con el papel en disputa, ya en su copia.</summary>
        public int Conservados;
        /// <summary>Papeles perdidos que se volvieron a encontrar.</summary>
        public int Recuperados;
        /// <summary>Documentos marcados como duplicado por tener el mismo papel byte a byte.</summary>
        public int Marcados;
        /// <summary>Documentos sobre los que no se pudo decidir; se dicen uno a uno.</summary>
        public int SinDecidir;
        /// <summary>Lo que se le dice al dueño en la franja.</summary>
        public List<Aviso> Avisos { get; } = [];
        /// <summary>Un renglón por cambio para <c>fichas.log</c>.</summary>
        public List<string> Renglones { get; } = [];
    }

    /// <summary>
    /// Quien no es del papel lo pierde; quien sí, se lo queda en su copia; si no se sabe,
    /// nadie se mueve.
    /// </summary>
    /// <remarks>
    /// ⛔ Solo se decide si el número leído es de UNO de los que disputan. Un número que no es
    /// de ninguno —un OCR que leyó «PULC26O9»— no puede quitarle el papel a nadie.
    /// <para>Pasa de las 30 líneas a propósito: son las cuatro salidas de UNA decisión —no
    /// existe, no se leyó o no es de nadie, conservar, vaciar— y partirla dejaría la regla
    /// repartida en cuatro métodos que solo se entienden juntos.</para>
    /// </remarks>
    /// <param name="lectura">La disputa con lo que dice hoy su archivo.</param>
    /// <param name="cuenta">Donde se suma.</param>
    private void AplicarUnaDisputa(LecturaDeUnaDisputa lectura, Cuenta cuenta)
    {
        var disputa = lectura.Disputa;
        var nombre = Path.GetFileName(disputa.RutaPdf);

        if (!lectura.ElArchivoExiste)
        {
            SinDecidir(cuenta, disputa.Casos.Count,
                $"«{nombre}», hoja {disputa.PaginaPdf}: el archivo ya no está, y {Plural.Con(disputa.Casos.Count, "documento sigue", "documentos siguen")} apuntando a él sin comprobar.",
                disputa.RutaPdf);
            return;
        }

        var reclamado = disputa.Casos.Any(caso => string.Equals(caso.NumeroCaso, lectura.NumeroLeido, StringComparison.Ordinal));
        if (lectura.NumeroLeido is null || !reclamado)
        {
            var dice = lectura.NumeroLeido is null ? "no se le pudo leer ningún número de caso" : $"dice «{lectura.NumeroLeido}», que no es de ninguno";
            SinDecidir(cuenta, disputa.Casos.Count,
                $"«{nombre}», hoja {disputa.PaginaPdf}: {dice} de los {disputa.Casos.Count} documentos que lo reclaman; no se decidió.",
                lectura.Error is null ? disputa.RutaPdf : $"{disputa.RutaPdf}. El lector falló: {lectura.Error}");
            return;
        }

        var papel = _copias.Guardar(disputa.RutaPdf);
        cuenta.Avisos.AddRange(papel.Avisos);
        foreach (var caso in disputa.Casos)
        {
            if (string.Equals(caso.NumeroCaso, lectura.NumeroLeido, StringComparison.Ordinal))
            {
                Conservar(caso, papel.RutaDelPapel, cuenta);
            }
            else
            {
                Vaciar(caso, disputa, lectura.NumeroLeido, cuenta);
            }
        }
    }

    /// <summary>El papel perdido vuelve si su único candidato dice su número; si no, se dice.</summary>
    /// <remarks>Mismo motivo que <see cref="AplicarUnaDisputa"/> para su largo: tres salidas de una sola decisión, con su frase cada una.</remarks>
    /// <param name="lectura">El caso, sus candidatos y lo leído en el único.</param>
    /// <param name="cuenta">Donde se suma.</param>
    private void AplicarUnPerdido(LecturaDeUnPerdido lectura, Cuenta cuenta)
    {
        var caso = lectura.Caso;
        var nombre = Path.GetFileName(caso.RutaPdf!);

        if (lectura.Candidatos.Count != 1)
        {
            var cuantos = lectura.Candidatos.Count == 0 ? "no hay ningún archivo con ese nombre en las carpetas de al lado" : $"hay {lectura.Candidatos.Count} archivos con ese nombre en las carpetas de al lado";
            SinDecidir(cuenta, 1, $"{caso.NumeroCaso ?? "sin número"}: su escaneo «{nombre}» ya no está y {cuantos}; sigue sin escaneo.", caso.RutaPdf!);
            return;
        }

        if (caso.NumeroCaso is null || !string.Equals(caso.NumeroCaso, lectura.NumeroLeido, StringComparison.Ordinal))
        {
            var dice = lectura.NumeroLeido is null ? "no se le pudo leer ningún número" : $"dice «{lectura.NumeroLeido}»";
            SinDecidir(cuenta, 1,
                $"{caso.NumeroCaso ?? "sin número"}: su escaneo «{nombre}» ya no está; el que hay en «{Path.GetDirectoryName(lectura.Candidatos[0])}» {dice}, así que no se tomó.",
                lectura.Error is null ? lectura.Candidatos[0] : $"{lectura.Candidatos[0]}. El lector falló: {lectura.Error}");
            return;
        }

        var papel = _copias.Guardar(lectura.Candidatos[0]);
        cuenta.Avisos.AddRange(papel.Avisos);
        var escrito = _casos.Guardar(caso with { RutaPdf = papel.RutaDelPapel });
        cuenta.Avisos.AddRange(escrito.Avisos);
        if (!escrito.SeEscribio) return;

        cuenta.Recuperados++;
        cuenta.Renglones.Add($"PAPELES  {caso.NumeroCaso} (caso {caso.Id}) recupera su escaneo: estaba en «{caso.RutaPdf}», se encontró en «{lectura.Candidatos[0]}», copiado a «{papel.RutaDelPapel}»");
    }

    /// <summary>Los que repiten el papel byte a byte quedan marcados como duplicado del más antiguo, con su renglón.</summary>
    /// <remarks>Pasa de las 30 líneas por el renglón y el aviso, que llevan la ruta de los dos y no caben en menos sin perder qué se marcó y contra quién.</remarks>
    /// <param name="par">El original y los que lo repiten sin marca.</param>
    /// <param name="cuenta">Donde se suma.</param>
    private void AplicarUnPar(ParDeIguales par, Cuenta cuenta)
    {
        foreach (var repetido in par.Repetidos)
        {
            var escrito = _casos.Guardar(repetido with { DuplicadoDe = par.Original.Id });
            cuenta.Avisos.AddRange(escrito.Avisos);
            if (!escrito.SeEscribio) continue;

            cuenta.Marcados++;
            _ilegibles.Registrar(new RenglonIlegible
            {
                RutaPdf = repetido.RutaPdf!,
                PaginaPdf = repetido.PaginaPdf,
                Motivo = MotivosDeIlegible.EntroComoDuplicado,
                Detalle = $"Es DUPLICADO del caso n.º {par.Original.Id} ({par.Original.NumeroCaso ?? "sin número"}): el mismo archivo byte a byte, "
                        + $"en «{repetido.RutaPdf}» y en «{par.Original.RutaPdf}». Lo marcó la comprobación de los papeles; nada se ha fundido.",
                CasoId = repetido.Id,
                RegistradoEn = _reloj.Ahora(),
            });
            cuenta.Renglones.Add($"PAPELES  {repetido.NumeroCaso ?? "sin número"} (caso {repetido.Id}) marcado como duplicado del caso {par.Original.Id}: mismo archivo byte a byte");
        }

        if (par.Repetidos.Count > 0)
        {
            cuenta.Avisos.Add(Aviso.Advierte(
                $"{par.Original.NumeroCaso ?? "Sin número"}: {Plural.Con(par.Repetidos.Count, "documento repite", "documentos repiten")} su mismo escaneo byte a byte; {(par.Repetidos.Count == 1 ? "quedó marcado" : "quedaron marcados")} como duplicado. Unifíquelos desde Revisar.",
                string.Empty,
                $"Original: caso {par.Original.Id}, «{par.Original.RutaPdf}». Repetidos: {string.Join("; ", par.Repetidos.Select(caso => $"caso {caso.Id}, «{caso.RutaPdf}»"))}."));
        }
    }

    /// <summary>El caso se queda con el papel, ya en su copia si hubo que copiarlo.</summary>
    /// <param name="caso">El caso cuyo número dice el archivo.</param>
    /// <param name="rutaDelPapel">La copia, o el original si no se pudo copiar.</param>
    /// <param name="cuenta">Donde se suma.</param>
    private void Conservar(Caso caso, string rutaDelPapel, Cuenta cuenta)
    {
        if (!string.Equals(caso.RutaPdf, rutaDelPapel, StringComparison.OrdinalIgnoreCase))
        {
            var escrito = _casos.Guardar(caso with { RutaPdf = rutaDelPapel });
            cuenta.Avisos.AddRange(escrito.Avisos);
            if (!escrito.SeEscribio) return;
        }

        cuenta.Conservados++;
        cuenta.Renglones.Add($"PAPELES  {caso.NumeroCaso} (caso {caso.Id}) conserva su escaneo, ahora en «{rutaDelPapel}»");
    }

    /// <summary>El caso pierde el papel que no es suyo, y queda dicho dónde estaba y qué hay ahora.</summary>
    /// <param name="caso">El caso cuyo número NO dice el archivo.</param>
    /// <param name="disputa">La hoja que reclamaba.</param>
    /// <param name="numeroLeido">Lo que dice hoy el archivo.</param>
    /// <param name="cuenta">Donde se suma.</param>
    private void Vaciar(Caso caso, PapelEnDisputa disputa, string numeroLeido, Cuenta cuenta)
    {
        var escrito = _casos.Guardar(caso with { RutaPdf = null, PaginaPdf = null });
        cuenta.Avisos.AddRange(escrito.Avisos);
        if (!escrito.SeEscribio) return;

        cuenta.Vaciados++;
        _ilegibles.Registrar(new RenglonIlegible
        {
            RutaPdf = disputa.RutaPdf,
            PaginaPdf = disputa.PaginaPdf,
            Motivo = MotivosDeIlegible.PapelPerdido,
            Detalle = $"El archivo «{disputa.RutaPdf}» (hoja {disputa.PaginaPdf}) contiene hoy el papel de {numeroLeido}, no el de este "
                    + $"documento ({caso.NumeroCaso ?? "sin número"}). Su escaneo ya no está en el disco; el documento se quedó sin escaneo "
                    + "y sus datos no se han tocado. Lo escribió la comprobación de los papeles.",
            CasoId = caso.Id,
            RegistradoEn = _reloj.Ahora(),
        });
        cuenta.Renglones.Add($"PAPELES  {caso.NumeroCaso ?? "sin número"} (caso {caso.Id}) pierde el escaneo «{disputa.RutaPdf}» hoja {disputa.PaginaPdf}: ahí hay hoy el papel de {numeroLeido}");
        cuenta.Avisos.Add(Aviso.Advierte(
            $"{caso.NumeroCaso ?? "Sin número"}: su escaneo «{Path.GetFileName(disputa.RutaPdf)}» contiene hoy el papel de {numeroLeido}; el documento se quedó sin escaneo.",
            string.Empty,
            "Sus datos no se han tocado: solo se le quitó la imagen que no era suya. Si tiene el papel, vuelva a escanearlo e impórtelo: "
            + "entrará como duplicado de este y podrá unificarlos desde Revisar."));
    }

    /// <summary>Suma lo que no se decidió y lo dice, con la ruta entera en el detalle.</summary>
    /// <param name="cuenta">Donde se suma.</param>
    /// <param name="cuantos">Cuántos documentos quedan sin decidir por esto.</param>
    /// <param name="linea">La línea para la franja.</param>
    /// <param name="detalle">Lo largo: la ruta, y el fallo del lector si lo hubo.</param>
    private static void SinDecidir(Cuenta cuenta, int cuantos, string linea, string detalle)
    {
        cuenta.SinDecidir += cuantos;
        cuenta.Avisos.Add(Aviso.Advierte(linea, string.Empty, detalle));
        cuenta.Renglones.Add($"PAPELES  sin decidir: {linea} ({detalle})");
    }
}

/// <summary>Lo que hizo la comprobación, con sus cifras.</summary>
/// <param name="Vaciados">Documentos que perdieron el papel porque no era suyo.</param>
/// <param name="Conservados">Documentos que se quedaron con el papel en disputa.</param>
/// <param name="Recuperados">Papeles perdidos que se volvieron a encontrar.</param>
/// <param name="Marcados">Documentos marcados como duplicado por repetir el papel byte a byte.</param>
/// <param name="SinDecidir">Documentos sobre los que no se pudo decidir.</param>
/// <param name="Avisos">Lo que se le dice al dueño.</param>
/// <param name="RenglonesParaElCuaderno">Un renglón por cambio para <c>fichas.log</c>.</param>
public sealed record ResultadoDeLosPapeles(
    int Vaciados, int Conservados, int Recuperados, int Marcados, int SinDecidir,
    IReadOnlyList<Aviso> Avisos, IReadOnlyList<string> RenglonesParaElCuaderno)
{
    /// <summary>Todo en una línea, con las cinco cifras aunque sean cero.</summary>
    public string Linea => "Papeles comprobados: " + string.Join(" · ",
        Plural.Con(Vaciados, "documento se quedó sin el escaneo que no era suyo", "documentos se quedaron sin el escaneo que no era suyo"),
        Plural.Con(Conservados, "lo conservó", "lo conservaron"),
        Plural.Con(Recuperados, "recuperado", "recuperados"),
        Plural.Con(Marcados, "marcado como duplicado", "marcados como duplicado"),
        Plural.Con(SinDecidir, "sin decidir", "sin decidir"));
}

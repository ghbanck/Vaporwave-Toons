using System.Globalization;

namespace VaporwaveToons;

/// <summary>
/// User-facing text, in English and Brazilian Portuguese. By default it follows the Windows
/// display language; the tray menu and --lang can pick one. Diagnostics stay in English.
/// </summary>
internal static class Strings
{
    public static bool Portuguese { get; private set; } = WindowsIsPortuguese;

    private static bool WindowsIsPortuguese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";

    /// <summary>"en", "pt", or anything else to follow the Windows display language.</summary>
    public static void Use(string language) => Portuguese = language switch
    {
        "en" => false,
        "pt" => true,
        _ => WindowsIsPortuguese,
    };

    private static string T(string en, string pt) => Portuguese ? pt : en;

    public const string AppName = "Vaporwave Toons";
    public const string ProjectUrl = "https://github.com/ghbanck/Vaporwave-Toons";

    // ----------------------------------------------------------------- command line

    public static string Usage => T(
        "VaporwaveToons.exe [options]\n\n" +
        "  -n, --toons N      how many toons (default: the theme's own, 18)\n" +
        "  --scale N          sprite size, 1 to 6 (default: automatic)\n" +
        "  --speed P          speed in percent, 100 = normal\n" +
        "  --squish           clicking a toon zaps it\n" +
        "  --no-blood         every death is the tame explosion\n" +
        "  --no-angels        no angels after deaths\n" +
        "  --theme FOLDER     use another XPenguins theme (a folder with a config file)\n" +
        "  --lang en|pt       interface language (default: the Windows display language)\n" +
        "  --seconds N        quit by itself after N seconds\n" +
        "  --log FILE         write diagnostic statistics\n" +
        "  --list-windows     list which windows count as floor and wall, then exit\n" +
        "  --selftest         validate the theme, then exit\n\n" +
        "Command-line options apply to this run only.",
        "VaporwaveToons.exe [opções]\n\n" +
        "  -n, --toons N      quantos toons (padrão: o do tema, 18)\n" +
        "  --scale N          tamanho dos sprites, de 1 a 6 (padrão: automático)\n" +
        "  --speed P          velocidade em %, 100 = normal\n" +
        "  --squish           clicar num toon o elimina\n" +
        "  --no-blood         toda morte vira a explosão mansa\n" +
        "  --no-angels        sem anjos depois das mortes\n" +
        "  --theme PASTA      usar outro tema do XPenguins (pasta com um arquivo config)\n" +
        "  --lang en|pt       idioma da interface (padrão: o idioma do Windows)\n" +
        "  --seconds N        sair sozinho depois de N segundos\n" +
        "  --log ARQUIVO      gravar estatísticas de diagnóstico\n" +
        "  --list-windows     listar quais janelas contam como chão e parede, e sair\n" +
        "  --selftest         validar o tema e sair\n\n" +
        "As opções da linha de comando valem só para esta execução.");

    public static string MissingValue(string option) =>
        string.Format(T("missing value for {0}", "falta o valor de {0}"), option);

    public static string UnknownOption(string option) =>
        string.Format(T("unknown option: {0}", "opção desconhecida: {0}"), option);

    public static string UnknownLanguage(string lang) =>
        string.Format(T("unknown language \"{0}\" (use en or pt)", "idioma desconhecido \"{0}\" (use en ou pt)"), lang);

    // ----------------------------------------------------------------- messages

    public static string ThemeLoadFailed(string why) =>
        T("Could not load the theme:\n", "Não consegui carregar o tema:\n") + why;

    public static string AlreadyRunning => T(
        "Vaporwave Toons is already running. Look for the CRT monitor icon in the notification area, " +
        "next to the clock.",
        "O Vaporwave Toons já está rodando. Procure o ícone do monitor CRT na área de notificação, " +
        "perto do relógio.");

    public static string Welcome => T(
        "The toons are loose on your desktop! Right-click this icon to choose which ones appear, " +
        "how many, how big, or to turn them off.",
        "Os toons estão soltos no seu desktop! Clique com o botão direito neste ícone para escolher " +
        "quais aparecem, quantos, o tamanho, ou para desligar.");

    public static string Crashed(string why) =>
        T("Vaporwave Toons ran into an error and will close:\n\n",
          "O Vaporwave Toons encontrou um erro e vai fechar:\n\n") + why;

    public static string StartupFailed(string why) =>
        T("Could not change the start-with-Windows setting:\n",
          "Não consegui alterar a inicialização automática:\n") + why;

    // ----------------------------------------------------------------- tray menu

    public static string Pause => T("Pause", "Pausar");
    public static string Hide => T("Hide", "Ocultar");
    public static string Count => T("Number of toons", "Quantidade");
    public static string ThemeDefault(int n) => string.Format(T("Theme default ({0})", "Padrão do tema ({0})"), n);
    public static string Toons => "Toons";
    public static string All => T("All", "Todos");
    public static string Size => T("Size", "Tamanho");
    public static string Automatic(int scale) => string.Format(T("Automatic ({0}×)", "Automático ({0}×)"), scale);
    public static string Speed => T("Speed", "Velocidade");
    public static string Slow => T("Slow", "Lenta");
    public static string Normal => "Normal";
    public static string Fast => T("Fast", "Rápida");
    public static string Turbo => "Turbo";
    public static string ClickToZap => T("Click a toon to zap it", "Clicar num toon elimina ele");
    public static string GentleDeaths => T("Gentle deaths (explosions only)", "Mortes mansas (só explosão)");
    public static string Angels => T("Angels rise to heaven", "Anjos sobem ao céu");
    public static string WalkOverMaximized => T("Walk in front of maximized and snapped windows",
                                                "Andar na frente de janelas maximizadas e encaixadas");
    public static string HideInFullscreen => T("Hide in full screen (games, videos)",
                                               "Esconder em tela cheia (jogos, vídeos)");
    public static string StartWithWindows => T("Start with Windows", "Iniciar com o Windows");
    public static string Language => T("Language", "Idioma");
    public static string LanguageAuto => T("Automatic (Windows)", "Automático (Windows)");
    public static string AboutMenu => T("About Vaporwave Toons…", "Sobre o Vaporwave Toons…");
    public static string Exit => T("Exit (power off the CRTs)", "Sair (desligar os CRTs)");

    public static string Tooltip(int toons, bool paused, bool hidden) =>
        $"{AppName} — {toons} toons" +
        (paused ? T(" (paused)", " (pausado)") : hidden ? T(" (hidden)", " (oculto)") : "");

    /// <summary>Display names for the Vaporwave theme's toons; other themes show their own names.</summary>
    public static string ToonName(string genus) => genus.ToLowerInvariant() switch
    {
        "terminal" => T("CRT terminal", "Terminal CRT"),
        "cassette" => T("Cassette tape", "Fita cassete"),
        "statue" => T("Greek statue", "Estátua grega"),
        "palm" => T("Palm tree", "Palmeira"),
        "dolphin" => T("Dolphin", "Golfinho"),
        "car" => T("80s car", "Carro anos 80"),
        "flamingo" => "Flamingo",
        "boombox" => "Boombox",
        _ => null,
    };

    // ----------------------------------------------------------------- about box

    public static string AboutTitle => T("About Vaporwave Toons", "Sobre o Vaporwave Toons");

    public static string About(string version, string toons, string artist, string maintainer, string license,
                               int live, int target, int scale, int sheets, string settingsFile) =>
        Portuguese
            ? $"{AppName} {version}\n" +
              "O tema \"Vaporwave\" do XPenguins, rodando nativamente no Windows.\n\n" +
              $"Toons: {toons}.\n\n" +
              "Eles caem do topo da tela, andam e correm sobre as barras de título, escalam as bordas " +
              "das janelas, despencam das beiradas e descem de paraquedas num disquete de 3,5\". " +
              "Uma janela arrastada em cima deles os esmaga, e cada um manda para o céu a parte " +
              "de si que tem alma (o carro manda o cheirinho de pinheiro).\n\n" +
              $"Arte: {artist}\n" +
              $"Tema mantido por: {maintainer}\n" +
              $"Licença do tema: {license}\n\n" +
              $"Agora: {live} de {target} toons, sprites em {scale}×, {sheets} sprite sheets.\n" +
              $"Preferências: {settingsFile}\n\n" +
              $"Software livre sob a licença MIT.\n{ProjectUrl}"
            : $"{AppName} {version}\n" +
              "The XPenguins \"Vaporwave\" theme, running natively on Windows.\n\n" +
              $"Toons: {toons}.\n\n" +
              "They fall from the top of the screen, walk and run along title bars, climb window " +
              "edges, tumble off ledges and parachute down under a 3.5\" floppy disk. A window " +
              "dragged on top of them squashes them, and each one sends the part of itself that " +
              "has a soul up to heaven (the car sends its pine-tree air freshener).\n\n" +
              $"Art: {artist}\n" +
              $"Theme maintained by: {maintainer}\n" +
              $"Theme license: {license}\n\n" +
              $"Now: {live} of {target} toons, sprites at {scale}×, {sheets} sprite sheets.\n" +
              $"Settings: {settingsFile}\n\n" +
              $"Free software under the MIT License.\n{ProjectUrl}";
}

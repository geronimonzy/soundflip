// UI string catalog. Every user-facing string in the tray app (menu, toasts,
// dialogs, startup messages) goes through here; CLI output and the hotkey
// grammar (ctrl+alt+o) stay English on purpose so scripts are stable.
//
// The tables live in code rather than .resx satellite assemblies because the
// project runs with InvariantGlobalization, so .NET's culture-based resource
// lookup never follows the Windows display language. Language detection is
// done with a single Win32 call instead (see Loc.SystemLanguage).

enum Str
{
    // Tray menu
    MenuOutputCurrent, MenuInputCurrent, NoDefaultOutput, NoDefaultInput,
    CycleOutput, CycleInput, Output, Input, NoActiveDevices,
    Hotkeys, Language, LanguageSystem, StartWithWindows, About, Exit, Unknown,

    // Toasts
    HotkeysInUseTitle, HotkeysInUseText,
    AutostartEnabledTitle, AutostartEnabledText, AutostartDisabledTitle, AutostartDisabledText,
    AutostartUnavailable,
    NoOutputsConfigured, NoInputsConfigured, TickDevicesHint,
    NothingToSwitch, NoOutputsActive, NoInputsActive, AudioOutput, AudioInput,

    // Hotkey dialogs
    SetHotkeyTitle, PressNewShortcut, CurrentHotkey, HotkeyHint, WaitingForShortcut, NeedModifier,
    HotkeysTitle, CycleOutputs, CycleInputs, Save, Cancel, Clear, None,

    // About dialog
    AboutVersion, AppDescription, Publisher, Copyright, Settings, Close, Support, Homepage, NotConfigured,

    // Startup (autostart) status texts
    StartupChecking, StartupEnableHint, StartupEnableFromTray, StartupDisabledByUser, StartupDisabledByPolicy,
    StartupTaskUnavailable, StartupRegistryUnavailable, StartupStateUnknown, StartupStatusUnavailable,
    ExePathUnknown,

    // Bootstrap
    SettingsUnreadable,
}

static class Loc
{
    public const string Auto = "auto";
    public const string Default = "en";

    // Order is the order shown in the tray Language submenu.
    public static readonly string[] Supported = { "en", "de", "es", "fr", "ru" };

    public static string Current { get; private set; } = Default;

    // Pick the effective language from the settings value ("auto" or a code)
    // and the Windows display language. Called once at startup and whenever the
    // user picks a language in the tray.
    public static void Apply(string? setting) => Current = Resolve(setting, SystemLanguage());

    internal static string Resolve(string? setting, string systemLanguage)
    {
        string chosen = Normalize(setting);
        if (chosen != Auto) return chosen;

        string system = Normalize(systemLanguage);
        return system == Auto ? Default : system;
    }

    // "auto" for anything that is not a supported code (null, empty, unknown).
    public static string Normalize(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting)) return Auto;
        string code = setting.Trim().ToLowerInvariant();
        return Array.IndexOf(Supported, code) >= 0 ? code : Auto;
    }

    public static string SystemLanguage()
    {
        try
        {
            return FromLangId(NativeMethods.GetUserDefaultUILanguage());
        }
        catch
        {
            return Default;
        }
    }

    // Primary language ID (low 10 bits of a Windows LANGID) -> catalog code.
    internal static string FromLangId(ushort langId) => (langId & 0x3FF) switch
    {
        0x07 => "de",
        0x0A => "es",
        0x0C => "fr",
        0x19 => "ru",
        _ => Default,
    };

    // Language names are shown in their own language and never translated.
    public static string NativeName(string code) => code switch
    {
        "de" => "Deutsch",
        "es" => "Español",
        "fr" => "Français",
        "ru" => "Русский",
        _ => "English",
    };

    public static string T(this Str key) => Get(Current, key);

    public static string T(this Str key, params object?[] args) => string.Format(Get(Current, key), args);

    internal static string Get(string code, Str key)
    {
        if (Tables.TryGetValue(code, out var table) && table.TryGetValue(key, out var text))
            return text;
        return Tables[Default].TryGetValue(key, out var fallback) ? fallback : key.ToString();
    }

    internal static IReadOnlyDictionary<Str, string> Table(string code) => Tables[code];

    static readonly Dictionary<string, Dictionary<Str, string>> Tables = new()
    {
        ["en"] = new()
        {
            [Str.MenuOutputCurrent] = "Output:  {0}",
            [Str.MenuInputCurrent] = "Input:   {0}",
            [Str.NoDefaultOutput] = "No default output",
            [Str.NoDefaultInput] = "No default input",
            [Str.CycleOutput] = "Cycle output",
            [Str.CycleInput] = "Cycle input",
            [Str.Output] = "Output",
            [Str.Input] = "Input",
            [Str.NoActiveDevices] = "(no active devices)",
            [Str.Hotkeys] = "Hotkeys...",
            [Str.Language] = "Language",
            [Str.LanguageSystem] = "System default",
            [Str.StartWithWindows] = "Start with Windows",
            [Str.About] = "About {0}",
            [Str.Exit] = "Exit",
            [Str.Unknown] = "unknown",

            [Str.HotkeysInUseTitle] = "Some hotkeys are in use",
            [Str.HotkeysInUseText] = "Could not register: {0}",
            [Str.AutostartEnabledTitle] = "Start with Windows enabled",
            [Str.AutostartEnabledText] = "{0} will start when you sign in.",
            [Str.AutostartDisabledTitle] = "Start with Windows disabled",
            [Str.AutostartDisabledText] = "{0} will no longer start automatically.",
            [Str.AutostartUnavailable] = "Autostart unavailable",
            [Str.NoOutputsConfigured] = "No outputs configured",
            [Str.NoInputsConfigured] = "No inputs configured",
            [Str.TickDevicesHint] = "Tick devices to cycle in the tray menu.",
            [Str.NothingToSwitch] = "Nothing to switch to",
            [Str.NoOutputsActive] = "None of the configured outputs are currently active.",
            [Str.NoInputsActive] = "None of the configured inputs are currently active.",
            [Str.AudioOutput] = "Audio output",
            [Str.AudioInput] = "Audio input",

            [Str.SetHotkeyTitle] = "Set hotkey",
            [Str.PressNewShortcut] = "Press a new shortcut",
            [Str.CurrentHotkey] = "Current: {0}",
            [Str.HotkeyHint] = "Use Ctrl, Alt, Shift, or Win together with a letter, digit, or F1-F12. Press Esc to cancel.",
            [Str.WaitingForShortcut] = "Waiting for a valid shortcut...",
            [Str.NeedModifier] = "Need a modifier plus a letter, digit, or F1-F12. Try again.",
            [Str.HotkeysTitle] = "Hotkeys",
            [Str.CycleOutputs] = "Cycle outputs",
            [Str.CycleInputs] = "Cycle inputs",
            [Str.Save] = "Save",
            [Str.Cancel] = "Cancel",
            [Str.Clear] = "Clear",
            [Str.None] = "(none)",

            [Str.AboutVersion] = "Version {0}",
            [Str.AppDescription] = "Minimal Windows audio device switcher with tray-based output and input toggling.",
            [Str.Publisher] = "Publisher",
            [Str.Copyright] = "Copyright",
            [Str.Settings] = "Settings",
            [Str.Close] = "Close",
            [Str.Support] = "Support",
            [Str.Homepage] = "Homepage",
            [Str.NotConfigured] = "Not configured",

            [Str.StartupChecking] = "Checking startup support...",
            [Str.StartupEnableHint] = "Enable startup from the tray menu or Windows Startup Apps.",
            [Str.StartupEnableFromTray] = "Enable startup from the tray menu.",
            [Str.StartupDisabledByUser] = "Startup was disabled by the user. Re-enable it from Windows Startup Apps.",
            [Str.StartupDisabledByPolicy] = "Startup is disabled by system policy on this device.",
            [Str.StartupTaskUnavailable] = "Startup task is unavailable: {0}",
            [Str.StartupRegistryUnavailable] = "Startup registry entry is unavailable: {0}",
            [Str.StartupStateUnknown] = "Startup state could not be determined.",
            [Str.StartupStatusUnavailable] = "Startup status is unavailable.",
            [Str.ExePathUnknown] = "Could not determine the path of the running executable.",

            [Str.SettingsUnreadable] = "Your settings could not be read, so defaults are being used.\n\n{0}",
        },

        ["de"] = new()
        {
            [Str.MenuOutputCurrent] = "Ausgabe:  {0}",
            [Str.MenuInputCurrent] = "Eingabe:   {0}",
            [Str.NoDefaultOutput] = "Kein Standard-Ausgabegerät",
            [Str.NoDefaultInput] = "Kein Standard-Eingabegerät",
            [Str.CycleOutput] = "Ausgabe wechseln",
            [Str.CycleInput] = "Eingabe wechseln",
            [Str.Output] = "Ausgabe",
            [Str.Input] = "Eingabe",
            [Str.NoActiveDevices] = "(keine aktiven Geräte)",
            [Str.Hotkeys] = "Tastenkürzel...",
            [Str.Language] = "Sprache",
            [Str.LanguageSystem] = "Systemstandard",
            [Str.StartWithWindows] = "Mit Windows starten",
            [Str.About] = "Über {0}",
            [Str.Exit] = "Beenden",
            [Str.Unknown] = "unbekannt",

            [Str.HotkeysInUseTitle] = "Einige Tastenkürzel sind belegt",
            [Str.HotkeysInUseText] = "Konnte nicht registriert werden: {0}",
            [Str.AutostartEnabledTitle] = "Mit Windows starten aktiviert",
            [Str.AutostartEnabledText] = "{0} startet bei der Anmeldung.",
            [Str.AutostartDisabledTitle] = "Mit Windows starten deaktiviert",
            [Str.AutostartDisabledText] = "{0} startet nicht mehr automatisch.",
            [Str.AutostartUnavailable] = "Autostart nicht verfügbar",
            [Str.NoOutputsConfigured] = "Keine Ausgabegeräte konfiguriert",
            [Str.NoInputsConfigured] = "Keine Eingabegeräte konfiguriert",
            [Str.TickDevicesHint] = "Geräte zum Wechseln im Tray-Menü anhaken.",
            [Str.NothingToSwitch] = "Kein Gerät zum Wechseln",
            [Str.NoOutputsActive] = "Keines der konfigurierten Ausgabegeräte ist derzeit aktiv.",
            [Str.NoInputsActive] = "Keines der konfigurierten Eingabegeräte ist derzeit aktiv.",
            [Str.AudioOutput] = "Audioausgabe",
            [Str.AudioInput] = "Audioeingabe",

            [Str.SetHotkeyTitle] = "Tastenkürzel festlegen",
            [Str.PressNewShortcut] = "Neues Tastenkürzel drücken",
            [Str.CurrentHotkey] = "Aktuell: {0}",
            [Str.HotkeyHint] = "Strg, Alt, Umschalt oder Win zusammen mit einem Buchstaben, einer Ziffer oder F1-F12 drücken. Esc bricht ab.",
            [Str.WaitingForShortcut] = "Warte auf ein gültiges Tastenkürzel...",
            [Str.NeedModifier] = "Es braucht eine Zusatztaste plus Buchstabe, Ziffer oder F1-F12. Bitte erneut versuchen.",
            [Str.HotkeysTitle] = "Tastenkürzel",
            [Str.CycleOutputs] = "Ausgabe wechseln",
            [Str.CycleInputs] = "Eingabe wechseln",
            [Str.Save] = "Speichern",
            [Str.Cancel] = "Abbrechen",
            [Str.Clear] = "Löschen",
            [Str.None] = "(keins)",

            [Str.AboutVersion] = "Version {0}",
            [Str.AppDescription] = "Minimaler Audiogeräte-Umschalter für Windows mit Ausgabe- und Eingabewechsel aus dem Tray.",
            [Str.Publisher] = "Herausgeber",
            [Str.Copyright] = "Copyright",
            [Str.Settings] = "Einstellungen",
            [Str.Close] = "Schließen",
            [Str.Support] = "Support",
            [Str.Homepage] = "Website",
            [Str.NotConfigured] = "Nicht konfiguriert",

            [Str.StartupChecking] = "Autostart-Unterstützung wird geprüft...",
            [Str.StartupEnableHint] = "Autostart im Tray-Menü oder unter Windows-Autostart-Apps aktivieren.",
            [Str.StartupEnableFromTray] = "Autostart im Tray-Menü aktivieren.",
            [Str.StartupDisabledByUser] = "Autostart wurde vom Benutzer deaktiviert. Unter Windows-Autostart-Apps wieder aktivieren.",
            [Str.StartupDisabledByPolicy] = "Autostart ist auf diesem Gerät per Systemrichtlinie deaktiviert.",
            [Str.StartupTaskUnavailable] = "Autostart-Aufgabe nicht verfügbar: {0}",
            [Str.StartupRegistryUnavailable] = "Autostart-Registrierungseintrag nicht verfügbar: {0}",
            [Str.StartupStateUnknown] = "Autostart-Status konnte nicht ermittelt werden.",
            [Str.StartupStatusUnavailable] = "Autostart-Status ist nicht verfügbar.",
            [Str.ExePathUnknown] = "Der Pfad der laufenden Anwendung konnte nicht ermittelt werden.",

            [Str.SettingsUnreadable] = "Die Einstellungen konnten nicht gelesen werden, daher werden Standardwerte verwendet.\n\n{0}",
        },

        ["es"] = new()
        {
            [Str.MenuOutputCurrent] = "Salida:  {0}",
            [Str.MenuInputCurrent] = "Entrada:   {0}",
            [Str.NoDefaultOutput] = "Sin salida predeterminada",
            [Str.NoDefaultInput] = "Sin entrada predeterminada",
            [Str.CycleOutput] = "Cambiar salida",
            [Str.CycleInput] = "Cambiar entrada",
            [Str.Output] = "Salida",
            [Str.Input] = "Entrada",
            [Str.NoActiveDevices] = "(no hay dispositivos activos)",
            [Str.Hotkeys] = "Atajos de teclado...",
            [Str.Language] = "Idioma",
            [Str.LanguageSystem] = "Predeterminado del sistema",
            [Str.StartWithWindows] = "Iniciar con Windows",
            [Str.About] = "Acerca de {0}",
            [Str.Exit] = "Salir",
            [Str.Unknown] = "desconocido",

            [Str.HotkeysInUseTitle] = "Algunos atajos ya están en uso",
            [Str.HotkeysInUseText] = "No se pudo registrar: {0}",
            [Str.AutostartEnabledTitle] = "Inicio con Windows activado",
            [Str.AutostartEnabledText] = "{0} se iniciará al iniciar sesión.",
            [Str.AutostartDisabledTitle] = "Inicio con Windows desactivado",
            [Str.AutostartDisabledText] = "{0} ya no se iniciará automáticamente.",
            [Str.AutostartUnavailable] = "Inicio automático no disponible",
            [Str.NoOutputsConfigured] = "No hay salidas configuradas",
            [Str.NoInputsConfigured] = "No hay entradas configuradas",
            [Str.TickDevicesHint] = "Marca los dispositivos a alternar en el menú de la bandeja.",
            [Str.NothingToSwitch] = "Nada a lo que cambiar",
            [Str.NoOutputsActive] = "Ninguna de las salidas configuradas está activa en este momento.",
            [Str.NoInputsActive] = "Ninguna de las entradas configuradas está activa en este momento.",
            [Str.AudioOutput] = "Salida de audio",
            [Str.AudioInput] = "Entrada de audio",

            [Str.SetHotkeyTitle] = "Definir atajo",
            [Str.PressNewShortcut] = "Pulsa un nuevo atajo",
            [Str.CurrentHotkey] = "Actual: {0}",
            [Str.HotkeyHint] = "Usa Ctrl, Alt, Mayús o Win junto con una letra, un dígito o F1-F12. Pulsa Esc para cancelar.",
            [Str.WaitingForShortcut] = "Esperando un atajo válido...",
            [Str.NeedModifier] = "Se necesita un modificador más una letra, un dígito o F1-F12. Inténtalo de nuevo.",
            [Str.HotkeysTitle] = "Atajos de teclado",
            [Str.CycleOutputs] = "Cambiar salida",
            [Str.CycleInputs] = "Cambiar entrada",
            [Str.Save] = "Guardar",
            [Str.Cancel] = "Cancelar",
            [Str.Clear] = "Borrar",
            [Str.None] = "(ninguno)",

            [Str.AboutVersion] = "Versión {0}",
            [Str.AppDescription] = "Conmutador minimalista de dispositivos de audio para Windows con cambio de salida y entrada desde la bandeja.",
            [Str.Publisher] = "Editor",
            [Str.Copyright] = "Copyright",
            [Str.Settings] = "Configuración",
            [Str.Close] = "Cerrar",
            [Str.Support] = "Soporte",
            [Str.Homepage] = "Sitio web",
            [Str.NotConfigured] = "No configurado",

            [Str.StartupChecking] = "Comprobando la compatibilidad con el inicio automático...",
            [Str.StartupEnableHint] = "Activa el inicio automático desde el menú de la bandeja o desde Aplicaciones de inicio de Windows.",
            [Str.StartupEnableFromTray] = "Activa el inicio automático desde el menú de la bandeja.",
            [Str.StartupDisabledByUser] = "El usuario desactivó el inicio automático. Vuelve a activarlo en Aplicaciones de inicio de Windows.",
            [Str.StartupDisabledByPolicy] = "El inicio automático está desactivado por directiva del sistema en este dispositivo.",
            [Str.StartupTaskUnavailable] = "La tarea de inicio no está disponible: {0}",
            [Str.StartupRegistryUnavailable] = "La entrada de inicio en el registro no está disponible: {0}",
            [Str.StartupStateUnknown] = "No se pudo determinar el estado del inicio automático.",
            [Str.StartupStatusUnavailable] = "El estado del inicio automático no está disponible.",
            [Str.ExePathUnknown] = "No se pudo determinar la ruta del ejecutable en ejecución.",

            [Str.SettingsUnreadable] = "No se pudo leer la configuración, así que se usan los valores predeterminados.\n\n{0}",
        },

        ["fr"] = new()
        {
            [Str.MenuOutputCurrent] = "Sortie :  {0}",
            [Str.MenuInputCurrent] = "Entrée :   {0}",
            [Str.NoDefaultOutput] = "Aucune sortie par défaut",
            [Str.NoDefaultInput] = "Aucune entrée par défaut",
            [Str.CycleOutput] = "Changer de sortie",
            [Str.CycleInput] = "Changer d'entrée",
            [Str.Output] = "Sortie",
            [Str.Input] = "Entrée",
            [Str.NoActiveDevices] = "(aucun périphérique actif)",
            [Str.Hotkeys] = "Raccourcis clavier...",
            [Str.Language] = "Langue",
            [Str.LanguageSystem] = "Langue du système",
            [Str.StartWithWindows] = "Lancer avec Windows",
            [Str.About] = "À propos de {0}",
            [Str.Exit] = "Quitter",
            [Str.Unknown] = "inconnu",

            [Str.HotkeysInUseTitle] = "Certains raccourcis sont déjà utilisés",
            [Str.HotkeysInUseText] = "Impossible d'enregistrer : {0}",
            [Str.AutostartEnabledTitle] = "Lancement avec Windows activé",
            [Str.AutostartEnabledText] = "{0} se lancera à l'ouverture de session.",
            [Str.AutostartDisabledTitle] = "Lancement avec Windows désactivé",
            [Str.AutostartDisabledText] = "{0} ne se lancera plus automatiquement.",
            [Str.AutostartUnavailable] = "Lancement automatique indisponible",
            [Str.NoOutputsConfigured] = "Aucune sortie configurée",
            [Str.NoInputsConfigured] = "Aucune entrée configurée",
            [Str.TickDevicesHint] = "Cochez les périphériques à alterner dans le menu de la zone de notification.",
            [Str.NothingToSwitch] = "Rien vers quoi basculer",
            [Str.NoOutputsActive] = "Aucune des sorties configurées n'est active actuellement.",
            [Str.NoInputsActive] = "Aucune des entrées configurées n'est active actuellement.",
            [Str.AudioOutput] = "Sortie audio",
            [Str.AudioInput] = "Entrée audio",

            [Str.SetHotkeyTitle] = "Définir le raccourci",
            [Str.PressNewShortcut] = "Appuyez sur un nouveau raccourci",
            [Str.CurrentHotkey] = "Actuel : {0}",
            [Str.HotkeyHint] = "Utilisez Ctrl, Alt, Maj ou Win avec une lettre, un chiffre ou F1-F12. Appuyez sur Échap pour annuler.",
            [Str.WaitingForShortcut] = "En attente d'un raccourci valide...",
            [Str.NeedModifier] = "Il faut un modificateur plus une lettre, un chiffre ou F1-F12. Réessayez.",
            [Str.HotkeysTitle] = "Raccourcis clavier",
            [Str.CycleOutputs] = "Changer de sortie",
            [Str.CycleInputs] = "Changer d'entrée",
            [Str.Save] = "Enregistrer",
            [Str.Cancel] = "Annuler",
            [Str.Clear] = "Effacer",
            [Str.None] = "(aucun)",

            [Str.AboutVersion] = "Version {0}",
            [Str.AppDescription] = "Commutateur minimaliste de périphériques audio pour Windows, avec changement de sortie et d'entrée depuis la zone de notification.",
            [Str.Publisher] = "Éditeur",
            [Str.Copyright] = "Copyright",
            [Str.Settings] = "Paramètres",
            [Str.Close] = "Fermer",
            [Str.Support] = "Assistance",
            [Str.Homepage] = "Site web",
            [Str.NotConfigured] = "Non configuré",

            [Str.StartupChecking] = "Vérification de la prise en charge du lancement automatique...",
            [Str.StartupEnableHint] = "Activez le lancement automatique depuis le menu de la zone de notification ou les Applications de démarrage de Windows.",
            [Str.StartupEnableFromTray] = "Activez le lancement automatique depuis le menu de la zone de notification.",
            [Str.StartupDisabledByUser] = "Le lancement automatique a été désactivé par l'utilisateur. Réactivez-le dans les Applications de démarrage de Windows.",
            [Str.StartupDisabledByPolicy] = "Le lancement automatique est désactivé par une stratégie système sur cet appareil.",
            [Str.StartupTaskUnavailable] = "La tâche de démarrage est indisponible : {0}",
            [Str.StartupRegistryUnavailable] = "L'entrée de démarrage dans le registre est indisponible : {0}",
            [Str.StartupStateUnknown] = "Impossible de déterminer l'état du lancement automatique.",
            [Str.StartupStatusUnavailable] = "L'état du lancement automatique est indisponible.",
            [Str.ExePathUnknown] = "Impossible de déterminer le chemin de l'exécutable en cours.",

            [Str.SettingsUnreadable] = "Vos paramètres n'ont pas pu être lus, les valeurs par défaut sont donc utilisées.\n\n{0}",
        },

        ["ru"] = new()
        {
            [Str.MenuOutputCurrent] = "Вывод:  {0}",
            [Str.MenuInputCurrent] = "Ввод:   {0}",
            [Str.NoDefaultOutput] = "Нет устройства вывода по умолчанию",
            [Str.NoDefaultInput] = "Нет устройства ввода по умолчанию",
            [Str.CycleOutput] = "Переключить вывод",
            [Str.CycleInput] = "Переключить ввод",
            [Str.Output] = "Вывод",
            [Str.Input] = "Ввод",
            [Str.NoActiveDevices] = "(нет активных устройств)",
            [Str.Hotkeys] = "Горячие клавиши...",
            [Str.Language] = "Язык",
            [Str.LanguageSystem] = "Как в системе",
            [Str.StartWithWindows] = "Запускать вместе с Windows",
            [Str.About] = "О программе {0}",
            [Str.Exit] = "Выход",
            [Str.Unknown] = "неизвестно",

            [Str.HotkeysInUseTitle] = "Некоторые горячие клавиши заняты",
            [Str.HotkeysInUseText] = "Не удалось зарегистрировать: {0}",
            [Str.AutostartEnabledTitle] = "Автозапуск включён",
            [Str.AutostartEnabledText] = "{0} будет запускаться при входе в систему.",
            [Str.AutostartDisabledTitle] = "Автозапуск выключен",
            [Str.AutostartDisabledText] = "{0} больше не будет запускаться автоматически.",
            [Str.AutostartUnavailable] = "Автозапуск недоступен",
            [Str.NoOutputsConfigured] = "Устройства вывода не настроены",
            [Str.NoInputsConfigured] = "Устройства ввода не настроены",
            [Str.TickDevicesHint] = "Отметьте устройства для переключения в меню в трее.",
            [Str.NothingToSwitch] = "Не на что переключиться",
            [Str.NoOutputsActive] = "Ни одно из настроенных устройств вывода сейчас не активно.",
            [Str.NoInputsActive] = "Ни одно из настроенных устройств ввода сейчас не активно.",
            [Str.AudioOutput] = "Аудиовывод",
            [Str.AudioInput] = "Аудиоввод",

            [Str.SetHotkeyTitle] = "Назначить горячую клавишу",
            [Str.PressNewShortcut] = "Нажмите новое сочетание клавиш",
            [Str.CurrentHotkey] = "Сейчас: {0}",
            [Str.HotkeyHint] = "Используйте Ctrl, Alt, Shift или Win вместе с буквой, цифрой или F1-F12. Esc — отмена.",
            [Str.WaitingForShortcut] = "Ожидание допустимого сочетания...",
            [Str.NeedModifier] = "Нужен модификатор плюс буква, цифра или F1-F12. Попробуйте ещё раз.",
            [Str.HotkeysTitle] = "Горячие клавиши",
            [Str.CycleOutputs] = "Переключить вывод",
            [Str.CycleInputs] = "Переключить ввод",
            [Str.Save] = "Сохранить",
            [Str.Cancel] = "Отмена",
            [Str.Clear] = "Очистить",
            [Str.None] = "(нет)",

            [Str.AboutVersion] = "Версия {0}",
            [Str.AppDescription] = "Минималистичный переключатель аудиоустройств для Windows: смена вывода и ввода прямо из трея.",
            [Str.Publisher] = "Издатель",
            [Str.Copyright] = "Авторские права",
            [Str.Settings] = "Настройки",
            [Str.Close] = "Закрыть",
            [Str.Support] = "Поддержка",
            [Str.Homepage] = "Сайт",
            [Str.NotConfigured] = "Не настроено",

            [Str.StartupChecking] = "Проверка поддержки автозапуска...",
            [Str.StartupEnableHint] = "Включите автозапуск в меню в трее или в разделе «Автозагрузка» Windows.",
            [Str.StartupEnableFromTray] = "Включите автозапуск в меню в трее.",
            [Str.StartupDisabledByUser] = "Автозапуск отключён пользователем. Включите его снова в разделе «Автозагрузка» Windows.",
            [Str.StartupDisabledByPolicy] = "Автозапуск отключён системной политикой на этом устройстве.",
            [Str.StartupTaskUnavailable] = "Задача автозапуска недоступна: {0}",
            [Str.StartupRegistryUnavailable] = "Запись автозапуска в реестре недоступна: {0}",
            [Str.StartupStateUnknown] = "Не удалось определить состояние автозапуска.",
            [Str.StartupStatusUnavailable] = "Состояние автозапуска недоступно.",
            [Str.ExePathUnknown] = "Не удалось определить путь к запущенному исполняемому файлу.",

            [Str.SettingsUnreadable] = "Не удалось прочитать настройки, поэтому используются значения по умолчанию.\n\n{0}",
        },
    };
}

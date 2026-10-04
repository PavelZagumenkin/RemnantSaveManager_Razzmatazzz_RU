using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using RemnantSaveManager;
using Localization = RemnantSaveManager.Localization;

internal static class LocalizationChecks
{
    private static int checks;
    private static readonly Assembly assembly = typeof(Localization).Assembly;
    private static readonly Type gameInfo = assembly.GetType("RemnantSaveManager.GameInfo");
    private static readonly BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture");
    private const string events =
        "/Game/World_City/Templates/Template_City_Overworld_Zone1 " +
        "/Game/World_City/Quests/Quest_Mini_RootBrute " +
        "/Game/World_City/Quests/Quest_Church " +
        "/Game/World_City/Templates/Template_City_Overworld_Zone2 " +
        "/Game/World_City/Quests/Quest_Boss_RootDragon " +
        "/Game/World_Swamp/Templates/Template_Swamp_Overworld_Zone2 " +
        "/Game/World_Swamp/Quests/Quest_Boss_SwampGuardian " +
        "/Game/World_Jungle/Templates/Template_Jungle_Overworld_Zone2 " +
        "/Game/World_Jungle/Quests/Quest_Boss_Wolf";
    private const string campaign =
        "/Game/Campaign_Main/Quest_Campaign_City.Quest_Campaign_City " + events +
        " /Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";
    private const string profile =
        "/Game/_Core/Archetypes/Archetype_Hunter " +
        "/Game/Characters/Player/Base/Character_Master_Player.Character_Master_Player_C " +
        "/Items/Weapons/Basic/LongGuns/HuntingRifle/Weapon_HuntingRifle Character_Master_Player_C";

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static void Russian(string value, string context)
    {
        Check(!string.IsNullOrEmpty(value) && Regex.IsMatch(value, "[А-Яа-яЁё]") &&
              !value.Contains("Неизвестн"), context + ": " + value);
    }

    private static object Invoke(object target, string method, params object[] args)
    {
        return target.GetType().GetMethods(instance).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    }

    private static T Control<T>(Window window, string name) where T : class
    {
        return window.FindName(name) as T;
    }

    private static void Render(Window window, string name, int width, int height)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name + ".png")))
            encoder.Save(stream);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameInfo.xml")),
                "The test must exercise the embedded fallback without an external catalog.");
            var app = new App();
            app.InitializeComponent();
            Invoke(app, "OnStartup", app, null);
            Check(CultureInfo.CurrentCulture.Name == "ru-RU", "Russian date/number culture");
            Check(CultureInfo.CurrentUICulture.Name == "ru-RU", "Russian dialog culture");
            string threadCulture = null;
            var thread = new Thread(() => threadCulture = CultureInfo.CurrentUICulture.Name);
            thread.Start();
            thread.Join();
            Check(threadCulture == "ru-RU", "Background thread culture");

            gameInfo.GetMethod("RefreshGameInfo").Invoke(null, null);
            var allItems = (Dictionary<string, RemnantItem[]>)gameInfo.GetProperty("EventItem").GetValue(null);
            XDocument catalog;
            using (var stream = assembly.GetManifestResourceStream("RemnantSaveManager.GameInfo.xml"))
                catalog = XDocument.Load(stream);
            foreach (var node in catalog.Descendants("Event"))
                Russian(Localization.EventName((string)node.Attribute("name"), (string)node.Attribute("altname")), "Event");
            foreach (var node in catalog.Descendants("SubLocation"))
                Russian(Localization.Location((string)node.Attribute("location")), "Sublocation");
            foreach (var node in catalog.Descendants("MainLocation"))
                Russian(Localization.Location((string)node.Attribute("name")), "Main location");
            foreach (var node in catalog.Descendants("Zone"))
                Russian(Localization.Location((string)node.Attribute("name")), "World");
            foreach (var node in catalog.Descendants("Archetype"))
                Russian(Localization.Text((string)node.Attribute("name")), "Archetype");
            foreach (var item in allItems.Values.SelectMany(x => x))
            {
                Russian(item.ItemName, item.GetKey());
                Russian(item.ItemType, "Item category");
                if (!string.IsNullOrEmpty(item.ItemNotes)) Russian(item.ItemNotes, "Item note");
                Check(!Regex.IsMatch(item.WikiName, "[А-Яа-яЁё]"), "Wiki still uses its English title");
            }
            Check(allItems.Values.SelectMany(x => x).Select(x => x.GetKey()).Distinct().Count() == 294,
                "Full item catalog");
            Check(allItems["BrainBug"][0].WikiName == "Gift of the Iskal", "Explicit wiki title");
            Check(allItems["BrainBug"][1].WikiName == "Carapace Set", "Item altname must not leak to the next item");
            Russian(new RemnantItem("/UnknownPotentialLoot").ItemName, "Unknown loot placeholder");
            Check(new RemnantItem("/Items/Traits/Trait_Spirit").Equals("/Items/Traits/Trait_Spirit"),
                "Save key equality remains unchanged");

            var character = new RemnantCharacter { Archetype = "Hunter" };
            character.processSaveData(campaign);
            Check(character.CampaignEvents.Count >= 10, "Synthetic campaign parsed");
            foreach (var worldEvent in character.CampaignEvents)
            {
                Russian(worldEvent.Name, worldEvent.getKey());
                Russian(worldEvent.Type, "Event type");
                Russian(worldEvent.Location, "Event location");
            }
            var keys = character.CampaignEvents.Select(x => x.getKey()).ToList();
            Check(keys.IndexOf("RootMother") >= 0 && keys.IndexOf("RootMother") < keys.IndexOf("RootDragon"),
                "Church events remain before the Westcourt boss");
            Check(keys.IndexOf("IskalQueen") >= 0 && keys.IndexOf("IskalQueen") < keys.IndexOf("SwampGuardian"),
                "Queen insertion remains before the Corsus boss");
            Check(keys.IndexOf("SlaveRevolt") >= 0 && keys.IndexOf("SlaveRevolt") < keys.IndexOf("Wolf"),
                "Rebel insertion remains before the Yaesha boss");
            var dlcCharacter = new RemnantCharacter();
            RemnantWorldEvent.ProcessEvents(dlcCharacter,
                "/Game/World_Rural/Templates/Template_Rural_Overworld_01 " +
                "/Game/World_Rural/Quests/Quest_Siege_BarnSiege " +
                "/Game/World_Snow/Templates/Template_Snow_Overworld_Zone1 " +
                "/Game/World_Snow/Quests/Quest_Mini_BlizzardMage", RemnantWorldEvent.ProcessMode.Subject2923);
            Check(dlcCharacter.CampaignEvents.Any(x => x.getKey() == "WardPrime" && x.Name == "Главный блок"),
                "Subject 2923: Ward Prime");
            Check(dlcCharacter.CampaignEvents.Last().Name == "Харсгаард", "Subject 2923 final boss");
            foreach (var worldEvent in dlcCharacter.CampaignEvents)
            {
                Russian(worldEvent.Name, "DLC event");
                Russian(worldEvent.Location, "DLC location");
            }
            var adventure = new RemnantCharacter();
            RemnantWorldEvent.ProcessEvents(adventure, events, RemnantWorldEvent.ProcessMode.Adventure);
            Check(adventure.AdventureEvents.Count > 0 && adventure.CampaignEvents.Count == 0, "Adventure routing");

            Directory.CreateDirectory(fixture);
            string saveDir = Path.Combine(fixture, "saves");
            string backupDir = Path.Combine(fixture, "backups");
            string backupSave = Path.Combine(backupDir, "test");
            Directory.CreateDirectory(saveDir);
            Directory.CreateDirectory(backupSave);
            File.WriteAllText(Path.Combine(saveDir, "profile.sav"), profile);
            File.WriteAllText(Path.Combine(saveDir, "save_0.sav"), campaign);
            File.Copy(Path.Combine(saveDir, "profile.sav"), Path.Combine(backupSave, "profile.sav"), true);
            File.Copy(Path.Combine(saveDir, "save_0.sav"), Path.Combine(backupSave, "save_0.sav"), true);
            var settingsType = assembly.GetType("RemnantSaveManager.Properties.Settings");
            var settings = (ApplicationSettingsBase)settingsType.GetProperty("Default").GetValue(null);
            settings["SaveFolder"] = saveDir;
            settings["BackupFolder"] = backupDir;
            settings["GameFolder"] = fixture;
            settings["UpgradeRequired"] = false;
            settings["AutoBackup"] = false;
            settings["AutoCheckUpdate"] = false;
            settings["CreateLogFile"] = false;
            settings["ShowPossibleItems"] = true;

            var main = new MainWindow();
            Invoke(main, "Window_Loaded", main, null);
            Check(main.Title == "Remnant — менеджер сохранений", "Compiled main window title");
            var backups = Control<DataGrid>(main, "dataBackups");
            Render(main, "backups", 1000, 500);
            Check(backups.Columns.Any(x => (string)x.Header == "Дата сохранения"), "Backup date column");
            Check(backups.Columns.Any(x => (string)x.Header == "Название"), "Backup name column");
            var tabs = (TabControl)Control<TabItem>(main, "tabBackups").Parent;
            tabs.SelectedIndex = 1;
            Render(main, "settings", 1000, 500);
            Check(Control<CheckBox>(main, "chkAutoBackup").Content.ToString().Contains("Автоматически"), "Settings BAML");

            var analyzer = new SaveAnalyzer(main);
            analyzer.LoadData(new List<RemnantCharacter> { character });
            Render(analyzer, "campaign", 1150, 600);
            var grid = Control<DataGrid>(analyzer, "dgCampaign");
            foreach (var column in grid.Columns) Russian(column.Header.ToString(), "Analyzer column");
            string report = (string)Invoke(analyzer, "ExportCampaign", false);
            Check(report.Contains("## Земля") && report.Contains("**Тип**") && !report.Contains("Type"), "Russian campaign export");
            string missing = (string)Invoke(analyzer, "ExportMissingItems");
            Check(missing.Contains("## Обычный режим") && missing.Contains("## Хардкор") &&
                  missing.Contains("## Выживание") && !missing.Contains("## Normal"), "Russian modes in export");
            Check(missing.Contains("Награда") && missing.Contains("Оружие"), "Russian item notes/categories in export");
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Кампания.md"), report);
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Недостающие предметы.md"), missing);
            var analyzerTabs = Control<TabControl>(analyzer, "tabAnalyzer");
            analyzerTabs.SelectedIndex = 3;
            Render(analyzer, "missing-items", 1150, 600);
            var modes = Control<TreeView>(analyzer, "treeMissingItems");
            Check(((TreeViewItem)modes.Items[1]).Header.ToString() == "Хардкор", "Compiled missing items tree");
            var hardcore = (TreeViewItem)modes.Items[1];
            hardcore.IsExpanded = !hardcore.IsExpanded;
            Check((bool)settings["HardcoreExpanded"] == hardcore.IsExpanded, "Localized mode keeps expansion setting");
            var red = Control<ComboBox>(main, "cmbMissingItemColor");
            red.SelectedIndex = 1;
            Check((string)settings["MissingItemColor"] == "White", "Russian color label keeps original setting value");

            var dialog = new RestoreDialog(main, new SaveBackup(backupSave), new RemnantSave(saveDir));
            Render(dialog, "restore", 680, 150);
            Check(Control<Label>(dialog, "txtSave").Content.ToString().Contains("Название:"), "Restore dialog data");
            Check(Control<Button>(dialog, "btnWorld").Content.ToString() == "Мир", "Restore choice label");
            var watcher = (FileSystemWatcher)typeof(MainWindow).GetField("saveWatcher", instance).GetValue(main);
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            var timer = (System.Timers.Timer)typeof(MainWindow).GetField("saveTimer", instance).GetValue(main);
            timer.Dispose();
            Console.WriteLine("PASS: " + checks + " checks; embedded catalog, compiled WPF, campaign/DLC/adventure, wiki titles and Russian exports.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}




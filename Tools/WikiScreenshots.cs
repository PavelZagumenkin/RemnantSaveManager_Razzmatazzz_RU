using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemnantSaveManager;

// Render the compiled WPF controls using demonstration saves, never the player's saves.
internal static class WikiScreenshots
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Assembly ProgramAssembly = typeof(MainWindow).Assembly;
    private const string Events =
        "/Game/World_City/Templates/Template_City_Overworld_Zone1 " +
        "/Game/World_City/Quests/Quest_Mini_RootBrute " +
        "/Game/World_City/Quests/Quest_Church " +
        "/Game/World_City/Templates/Template_City_Overworld_Zone2 " +
        "/Game/World_City/Quests/Quest_Boss_RootDragon " +
        "/Game/World_Swamp/Templates/Template_Swamp_Overworld_Zone2 " +
        "/Game/World_Swamp/Quests/Quest_Boss_SwampGuardian " +
        "/Game/World_Jungle/Templates/Template_Jungle_Overworld_Zone2 " +
        "/Game/World_Jungle/Quests/Quest_Boss_Wolf";
    private const string Campaign =
        "/Game/Campaign_Main/Quest_Campaign_City.Quest_Campaign_City " + Events +
        " /Game/Campaign_Main/Quest_Campaign_Main.Quest_Campaign_Main_C";

    private static object Invoke(object target, string method, params object[] args)
    {
        return target.GetType().GetMethods(Hidden)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length)
            .Invoke(target, args);
    }

    private static T Control<T>(Window window, string name) where T : class
    {
        return (T)window.FindName(name);
    }

    private static void Render(Window window, string output, string name, int width, int height)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name + ".png")))
            encoder.Save(stream);
    }

    private static void WriteSave(string path, string profile, DateTime date)
    {
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "profile.sav"), profile);
        File.WriteAllText(Path.Combine(path, "save_0.sav"), Campaign);
        File.SetLastWriteTime(Path.Combine(path, "profile.sav"), date);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        MainWindow main = null;
        try
        {
            if (args.Length != 1) throw new ArgumentException("Specify the screenshots output directory.");
            string output = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(output);
            var app = new App();
            app.InitializeComponent();
            Invoke(app, "OnStartup", app, null);

            var gameInfo = ProgramAssembly.GetType("RemnantSaveManager.GameInfo");
            var allItems = (Dictionary<string, RemnantItem[]>)gameInfo.GetProperty("EventItem").GetValue(null);
            var missingKeys = new HashSet<string>(
                new[] { "RootBrute", "RootDragon", "SwampGuardian", "Wolf" }
                .SelectMany(key => allItems[key]).Select(item => item.GetKey()));
            var inventory = allItems.Values.SelectMany(items => items)
                .Select(item => item.GetKey()).Distinct().Where(key => !missingKeys.Contains(key)).ToList();
            string profile = "/Game/_Core/Archetypes/Archetype_Hunter " +
                "/Game/Characters/Player/Base/Character_Master_Player.Character_Master_Player_C " +
                string.Join(" ", inventory) + " Character_Master_Player_C";

            string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixture");
            string saves = Path.Combine(fixture, "saves");
            string backups = Path.Combine(fixture, "backups");
            Directory.CreateDirectory(backups);
            var date = new DateTime(2026, 10, 4, 15, 30, 0);
            WriteSave(saves, profile, date);
            for (int i = 0; i < 3; i++)
                WriteSave(Path.Combine(backups, date.AddHours(-i).Ticks.ToString()), profile, date.AddHours(-i));

            var settingsType = ProgramAssembly.GetType("RemnantSaveManager.Properties.Settings");
            var settings = (ApplicationSettingsBase)settingsType.GetProperty("Default").GetValue(null);
            settings["SaveFolder"] = saves;
            settings["BackupFolder"] = backups;
            settings["GameFolder"] = fixture;
            settings["UpgradeRequired"] = false;
            settings["AutoBackup"] = false;
            settings["AutoCheckUpdate"] = false;
            settings["CreateLogFile"] = false;
            settings["ShowPossibleItems"] = false;
            settings["AnalyzerFontSize"] = 18d;
            settings["MissingItemColor"] = "Red";
            settings["NormalExpanded"] = true;
            settings["HardcoreExpanded"] = false;
            settings["SurvivalExpanded"] = false;
            settings["BackupMinutes"] = 15;
            settings["BackupLimit"] = 50;

            main = new MainWindow();
            Invoke(main, "Window_Loaded", main, null);
            var grid = Control<DataGrid>(main, "dataBackups");
            var list = (ObservableCollection<SaveBackup>)typeof(MainWindow).GetField("listBackups", Hidden).GetValue(main);
            string[] names = { "Текущая кампания", "Перед боссом", "Начало приключения" };
            for (int i = 0; i < list.Count; i++)
            {
                int slot = (int)(date - list[i].SaveDate).TotalHours;
                list[i].Name = names[slot];
                list[i].Keep = slot == 1;
            }
            grid.Items.Refresh();
            Render(main, output, "backups", 1000, 420);

            // These display-only paths make the illustrative settings easy to read.
            Control<TextBox>(main, "txtSaveFolder").Text = @"C:\Users\Игрок\AppData\Local\Remnant\Saved\SaveGames";
            Control<TextBox>(main, "txtGameFolder").Text = @"C:\Games\Remnant";
            Control<TextBox>(main, "txtBackupFolder").Text = @"C:\Users\Игрок\AppData\Local\Remnant\Saved\Backups";
            ((TabControl)Control<TabItem>(main, "tabBackups").Parent).SelectedIndex = 1;
            Render(main, output, "settings", 1000, 520);

            var character = new RemnantCharacter { Archetype = "Hunter", Inventory = inventory };
            character.processSaveData(Campaign);
            RemnantWorldEvent.ProcessEvents(character, Events, RemnantWorldEvent.ProcessMode.Adventure);
            var analyzer = new SaveAnalyzer(main);
            analyzer.LoadData(new List<RemnantCharacter> { character });
            Render(analyzer, output, "campaign", 1250, 650);
            Control<TabControl>(analyzer, "tabAnalyzer").SelectedIndex = 3;
            Render(analyzer, output, "missing-items", 1000, 650);

            var selected = list.Single(backup => backup.Keep);
            var restore = new RestoreDialog(main, selected, new RemnantSave(saves));
            Render(restore, output, "restore", 680, 150);
            Console.WriteLine("Created 5 Russian WPF screenshots: " + output);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            if (main != null)
            {
                var watcher = (FileSystemWatcher)typeof(MainWindow).GetField("saveWatcher", Hidden).GetValue(main);
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                ((System.Timers.Timer)typeof(MainWindow).GetField("saveTimer", Hidden).GetValue(main)).Dispose();
            }
        }
    }
}

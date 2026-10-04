using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Net;
using System.IO;

namespace RemnantSaveManager
{
    class GameInfo
    {
        public static event EventHandler<GameInfoUpdateEventArgs> GameInfoUpdate;
        private static readonly string gameInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameInfo.xml");
        private static readonly string tempGameInfoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempGameInfo.xml");
        private static Dictionary<string, string> zones = new Dictionary<string, string>();
        private static Dictionary<string, string> events = new Dictionary<string, string>();
        private static Dictionary<string, RemnantItem[]> eventItem = new Dictionary<string, RemnantItem[]>();
        private static Dictionary<string, string> subLocations = new Dictionary<string, string>();
        private static Dictionary<string, string> mainLocations = new Dictionary<string, string>();
        private static Dictionary<string, string> archetypes = new Dictionary<string, string>();
        public static Dictionary<string, string> Events
        {
            get
            {
                if (events.Count == 0)
                {
                    RefreshGameInfo();
                }

                return events;
            }
        }
        public static Dictionary<string, RemnantItem[]> EventItem
        {
            get
            {
                if (eventItem.Count == 0)
                {
                    RefreshGameInfo();
                }

                return eventItem;
            }
        }
        public static Dictionary<string,string> Zones
        {
            get
            {
                if (zones.Count == 0)
                {
                    RefreshGameInfo();
                }

                return zones;
            }
        }
        public static Dictionary<string, string> SubLocations
        {
            get {
                if (subLocations.Count == 0)
                {
                    RefreshGameInfo();
                }

                return subLocations;
            }
        }
        public static Dictionary<string, string> MainLocations
        {
            get
            {
                if (mainLocations.Count == 0)
                {
                    RefreshGameInfo();
                }

                return mainLocations;
            }
        }

        public static Dictionary<string, string> Archetypes
        {
            get
            {
                if (archetypes.Count == 0)
                {
                    RefreshGameInfo();
                }

                return archetypes;
            }
        }

        public static void RefreshGameInfo()
        {
            zones.Clear();
            events.Clear();
            eventItem.Clear();
            subLocations.Clear();
            mainLocations.Clear();
            archetypes.Clear();
            string eventName = null;
            string altEventName = null;
            string itemMode = null;
            string itemNotes = null;
            string itemAltName = null;
            List<RemnantItem> eventItems = new List<RemnantItem>();
            Stream catalog = File.Exists(gameInfoPath) ? File.OpenRead(gameInfoPath) :
                typeof(GameInfo).Assembly.GetManifestResourceStream("RemnantSaveManager.GameInfo.xml");
            if (catalog == null) throw new InvalidOperationException("Игровой справочник не найден.");
            XmlTextReader reader = new XmlTextReader(catalog);
            reader.DtdProcessing = DtdProcessing.Prohibit;
            reader.WhitespaceHandling = WhitespaceHandling.None;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name.Equals("Event"))
                        {
                            eventName = reader.GetAttribute("name");
                            altEventName = reader.GetAttribute("altname");
                            if (altEventName == null)
                            {
                                altEventName = eventName;
                            }
                            events.Add(eventName, altEventName);
                        }
                        else if (reader.Name.Equals("Item"))
                        {
                            itemMode = reader.GetAttribute("mode");
                            itemNotes = reader.GetAttribute("notes");
                            itemAltName = reader.GetAttribute("altname");
                        } else if (reader.Name.Equals("Zone"))
                        {
                            zones.Add(reader.GetAttribute("key"), reader.GetAttribute("name"));
                        }
                        else if (reader.Name.Equals("SubLocation"))
                        {
                            subLocations.Add(reader.GetAttribute("eventName"), reader.GetAttribute("location"));
                        }
                        else if (reader.Name.Equals("MainLocation"))
                        {
                            mainLocations.Add(reader.GetAttribute("key"), reader.GetAttribute("name"));
                        }
                        else if (reader.Name.Equals("Archetype"))
                        {
                            archetypes.Add(reader.GetAttribute("key"), reader.GetAttribute("name"));
                        }
                            break;
                    case XmlNodeType.Text:
                        if (eventName != null)
                        {
                            RemnantItem rItem = new RemnantItem(reader.Value);
                            if (itemMode != null)
                            {
                                if (itemMode.Equals("hardcore"))
                                {
                                    rItem.ItemMode = RemnantItem.RemnantItemMode.Hardcore;
                                } else if (itemMode.Equals("survival"))
                                {
                                    rItem.ItemMode = RemnantItem.RemnantItemMode.Survival;
                                }
                            }
                            if (itemNotes != null)
                            {
                                rItem.ItemNotes = itemNotes;
                            }
                            if (itemAltName != null)
                            {
                                rItem.ItemAltName = itemAltName;
                            }
                            eventItems.Add(rItem);
                            itemMode = null;
                            itemNotes = null;
                            itemAltName = null;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        if (reader.Name.Equals("Event"))
                        {
                            eventItem.Add(eventName, eventItems.ToArray());
                            eventName = null;
                            eventItems.Clear();
                        }
                        break;
                }
            }
            reader.Close();
        }

        public static void CheckForNewGameInfo()
        {
            GameInfoUpdateEventArgs args = new GameInfoUpdateEventArgs();
            try
            {
                WebClient client = new WebClient();
                client.DownloadFile("https://raw.githubusercontent.com/Razzmatazzz/RemnantSaveManager/master/Resources/GameInfo.xml", tempGameInfoPath);

                XmlTextReader reader = new XmlTextReader(tempGameInfoPath);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                int remoteversion = 0;
                int localversion = 0;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name.Equals("GameInfo"))
                        {
                            remoteversion = int.Parse(reader.GetAttribute("version"));
                            break;
                        }
                    }
                }
                args.RemoteVersion = remoteversion;
                reader.Close();
                if (File.Exists(gameInfoPath))
                {
                    reader = new XmlTextReader(gameInfoPath);
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name.Equals("GameInfo"))
                            {
                                localversion = int.Parse(reader.GetAttribute("version"));
                                break;
                            }
                        }
                    }
                    reader.Close();
                    args.LocalVersion = localversion;

                    if (remoteversion > localversion)
                    {
                        File.Delete(gameInfoPath);
                        File.Move(tempGameInfoPath, gameInfoPath);
                        RefreshGameInfo();
                        args.Result = GameInfoUpdateResult.Updated;
                        args.Message = "Игровой справочник обновлён с версии "+localversion+" до версии "+remoteversion+".";
                    }
                    else
                    {
                        File.Delete(tempGameInfoPath);
                    }
                } else
                {
                    File.Move(tempGameInfoPath, gameInfoPath);
                    RefreshGameInfo();
                    args.Result = GameInfoUpdateResult.Updated;
                    args.Message = "Локальный справочник не найден; загружена версия "+remoteversion+".";
                }
            } catch (Exception ex)
            {
                args.Result = GameInfoUpdateResult.Failed;
                args.Message = "Ошибка проверки обновления игрового справочника: " + ex.Message;
            }

            OnGameInfoUpdate(args);
        }

        protected static void OnGameInfoUpdate(GameInfoUpdateEventArgs e)
        {
            EventHandler<GameInfoUpdateEventArgs> handler = GameInfoUpdate;
            handler?.Invoke(typeof(GameInfo), e);
        }
    }
    public class GameInfoUpdateEventArgs : EventArgs
    {
        public int LocalVersion { get; set; }
        public int RemoteVersion { get; set; }
        public string Message { get; set; }
        public GameInfoUpdateResult Result { get; set; }

        public GameInfoUpdateEventArgs()
        {
            this.LocalVersion = 0;
            this.RemoteVersion = 0;
            this.Message = "Обновлений игрового справочника нет.";
            this.Result = GameInfoUpdateResult.NoUpdate;
        }
    }

    public enum GameInfoUpdateResult
    {
        Updated,
        Failed,
        NoUpdate
    }
}

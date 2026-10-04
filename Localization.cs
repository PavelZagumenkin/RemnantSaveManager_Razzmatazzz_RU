using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RemnantSaveManager
{
    // This embedded overlay uses save identifiers and survives GameInfo.xml updates.
    public static class Localization
    {
        private static readonly XDocument document = Load();
        private static readonly Dictionary<string, string> events = Read("Events");
        private static readonly Dictionary<string, string> items = Read("Items");
        private static readonly Dictionary<string, string> locations = Read("Locations", true);
        private static readonly Dictionary<string, string> texts = Read("Texts");

        private static XDocument Load()
        {
            using (var stream = typeof(Localization).Assembly.GetManifestResourceStream("RemnantSaveManager.Localization.ru.xml"))
            {
                if (stream == null) throw new InvalidOperationException("Встроенный русский перевод не найден.");
                return XDocument.Load(stream);
            }
        }

        private static Dictionary<string, string> Read(string group, bool normalize = false)
        {
            return document.Root.Element(group).Elements("Entry").ToDictionary(
                entry => normalize ? Normalize((string)entry.Attribute("key")) : (string)entry.Attribute("key"),
                entry => (string)entry.Attribute("value"), StringComparer.OrdinalIgnoreCase);
        }

        private static string Normalize(string value)
        {
            return Regex.Replace(value ?? "", @"\s+", "");
        }

        public static string Text(string key)
        {
            string value;
            return key != null && texts.TryGetValue(key, out value) ? value : key;
        }

        public static string EventName(string key, string fallback)
        {
            string value;
            if (key != null && events.TryGetValue(key, out value)) return value;
            return string.IsNullOrEmpty(fallback) ? "Неизвестное событие" : "Неизвестное событие (" + key + ")";
        }

        public static string ItemName(string key)
        {
            string value;
            return items.TryGetValue(key, out value) ? value : "Неизвестный предмет (" + key + ")";
        }

        public static string Location(string location)
        {
            if (string.IsNullOrEmpty(location)) return location;
            return string.Join(": ", location.Split(':').Select(part => {
                string value;
                return locations.TryGetValue(Normalize(part), out value) ? value : "Неизвестная локация (" + part.Trim() + ")";
            }));
        }
    }
}

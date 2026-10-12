using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace FocusSearch
{
    internal sealed class SettingsStore
    {
        private readonly string _baseDirectory;
        private readonly string _settingsPath;
        private readonly string _historyPath;
        private readonly string _logPath;

        public SettingsStore(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory is required.", "baseDirectory");
            }

            _baseDirectory = baseDirectory;
            _settingsPath = Path.Combine(baseDirectory, "settings.xml");
            _historyPath = Path.Combine(baseDirectory, "history.xml");
            _logPath = Path.Combine(baseDirectory, "errors.log");
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            try
            {
                string xml = File.ReadAllText(_settingsPath);
                Match rootMatch = Regex.Match(xml, @"<settings\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!rootMatch.Success || xml.IndexOf("</settings>", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidDataException("Invalid settings root element.");
                }

                var settings = new AppSettings();
                string attributes = rootMatch.Groups["attributes"].Value;
                settings.CombinedMode = ReadBoolean(attributes, "combinedMode", settings.CombinedMode);
                settings.AppGridEnabled = ReadBoolean(attributes, "appGridEnabled", settings.AppGridEnabled);
                settings.FileSearchEnabled = ReadBoolean(attributes, "fileSearchEnabled", settings.FileSearchEnabled);
                settings.StartWithWindows = ReadBoolean(attributes, "startWithWindows", settings.StartWithWindows);
                settings.HideOnDeactivate = ReadBoolean(attributes, "hideOnDeactivate", settings.HideOnDeactivate);
                settings.AnimationsEnabled = ReadBoolean(attributes, "animationsEnabled", settings.AnimationsEnabled);
                settings.SearchHotkey = ReadHotkey(ReadElementAttributes(xml, "searchHotkey"), settings.SearchHotkey);
                settings.GridHotkey = ReadHotkey(ReadElementAttributes(xml, "gridHotkey"), settings.GridHotkey);
                return settings;
            }
            catch (Exception exception)
            {
                Log("读取设置失败，已恢复默认值。", exception);
                return new AppSettings();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            EnsureDirectory();
            string temporaryPath = _settingsPath + ".tmp";
            var writerSettings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
            using (XmlWriter writer = XmlWriter.Create(temporaryPath, writerSettings))
            {
                writer.WriteStartElement("settings");
                writer.WriteAttributeString("combinedMode", XmlConvert.ToString(settings.CombinedMode));
                writer.WriteAttributeString("appGridEnabled", XmlConvert.ToString(settings.AppGridEnabled));
                writer.WriteAttributeString("fileSearchEnabled", XmlConvert.ToString(settings.FileSearchEnabled));
                writer.WriteAttributeString("startWithWindows", XmlConvert.ToString(settings.StartWithWindows));
                writer.WriteAttributeString("hideOnDeactivate", XmlConvert.ToString(settings.HideOnDeactivate));
                writer.WriteAttributeString("animationsEnabled", XmlConvert.ToString(settings.AnimationsEnabled));
                WriteHotkey(writer, "searchHotkey", settings.SearchHotkey ?? new AppSettings().SearchHotkey);
                WriteHotkey(writer, "gridHotkey", settings.GridHotkey ?? new AppSettings().GridHotkey);
                writer.WriteEndElement();
            }

            ReplaceAtomically(temporaryPath, _settingsPath);
        }

        public IDictionary<string, UsageRecord> LoadHistory()
        {
            var history = new Dictionary<string, UsageRecord>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_historyPath))
            {
                return history;
            }

            try
            {
                string xml = File.ReadAllText(_historyPath);
                if (xml.IndexOf("<history", StringComparison.OrdinalIgnoreCase) < 0 || xml.IndexOf("</history>", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    throw new InvalidDataException("Invalid history root element.");
                }

                MatchCollection items = Regex.Matches(xml, @"<item\b(?<attributes>[^>]*)/>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                foreach (Match item in items)
                {
                    string attributes = item.Groups["attributes"].Value;
                    string path = ReadAttribute(attributes, "path");
                    int count;
                    DateTime lastLaunched;
                    if (string.IsNullOrWhiteSpace(path) || !int.TryParse(ReadAttribute(attributes, "count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    {
                        continue;
                    }

                    DateTime.TryParse(ReadAttribute(attributes, "lastUtc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastLaunched);
                    history[path] = new UsageRecord { Path = path, LaunchCount = Math.Max(0, count), LastLaunchedUtc = lastLaunched };
                }
            }
            catch (Exception exception)
            {
                Log("读取历史失败，已忽略损坏的数据。", exception);
                history.Clear();
            }

            return history;
        }

        public void RecordLaunch(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var history = LoadHistory();
            UsageRecord record;
            if (!history.TryGetValue(path, out record))
            {
                record = new UsageRecord { Path = path };
                history[path] = record;
            }

            record.LaunchCount++;
            record.LastLaunchedUtc = DateTime.UtcNow;
            SaveHistory(history.Values);
        }

        private void SaveHistory(IEnumerable<UsageRecord> records)
        {
            EnsureDirectory();
            string temporaryPath = _historyPath + ".tmp";
            var writerSettings = new XmlWriterSettings { Indent = true, Encoding = new System.Text.UTF8Encoding(false) };
            using (XmlWriter writer = XmlWriter.Create(temporaryPath, writerSettings))
            {
                writer.WriteStartElement("history");
                foreach (UsageRecord record in records)
                {
                    writer.WriteStartElement("item");
                    writer.WriteAttributeString("path", record.Path);
                    writer.WriteAttributeString("count", record.LaunchCount.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("lastUtc", record.LastLaunchedUtc.ToString("o", CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            ReplaceAtomically(temporaryPath, _historyPath);
        }

        private static bool ReadBoolean(string attributes, string attribute, bool defaultValue)
        {
            bool value;
            return bool.TryParse(ReadAttribute(attributes, attribute), out value) ? value : defaultValue;
        }

        private static HotkeySpec ReadHotkey(string attributes, HotkeySpec defaultValue)
        {
            uint modifiers;
            uint virtualKey;
            if (string.IsNullOrEmpty(attributes) || !uint.TryParse(ReadAttribute(attributes, "modifiers"), NumberStyles.Integer, CultureInfo.InvariantCulture, out modifiers) ||
                !uint.TryParse(ReadAttribute(attributes, "key"), NumberStyles.Integer, CultureInfo.InvariantCulture, out virtualKey))
            {
                return defaultValue.Clone();
            }

            return new HotkeySpec(modifiers, virtualKey);
        }

        private static string ReadElementAttributes(string xml, string elementName)
        {
            Match match = Regex.Match(xml, "<" + Regex.Escape(elementName) + @"\b(?<attributes>[^>]*)/>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["attributes"].Value : string.Empty;
        }

        private static string ReadAttribute(string attributes, string attributeName)
        {
            Match match = Regex.Match(attributes, @"(?:^|\s)" + Regex.Escape(attributeName) + @"\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return string.Empty;
            }

            return DecodeXmlAttribute(match.Groups["value"].Value);
        }

        private static string DecodeXmlAttribute(string value)
        {
            return value.Replace("&quot;", "\"")
                        .Replace("&apos;", "'")
                        .Replace("&lt;", "<")
                        .Replace("&gt;", ">")
                        .Replace("&amp;", "&");
        }

        private static void WriteHotkey(XmlWriter writer, string elementName, HotkeySpec hotkey)
        {
            writer.WriteStartElement(elementName);
            writer.WriteAttributeString("modifiers", hotkey.Modifiers.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("key", hotkey.VirtualKey.ToString(CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private void ReplaceAtomically(string temporaryPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                string backupPath = destinationPath + ".bak";
                try { File.Replace(temporaryPath, destinationPath, backupPath, true); }
                finally { try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch { } }
            }
            else
            {
                File.Move(temporaryPath, destinationPath);
            }
        }

        private void EnsureDirectory()
        {
            Directory.CreateDirectory(_baseDirectory);
        }

        private void Log(string message, Exception exception)
        {
            try
            {
                EnsureDirectory();
                File.AppendAllText(_logPath, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine + exception + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}

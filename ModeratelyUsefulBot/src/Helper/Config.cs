using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Schema;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot.Helper
{
    internal static class Config
    {
        private const string Tag = "Config";
        private const string DefaultDoc = "config";
        private const string Dir = "data/";
        private const char Slash = '/';
        private const string NamespacePrefix = "a";
#if DEBUG
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string> { { "config", Dir + "debugConfig.xml" }, { "credentials", Dir + "debugCredentials.xml" } };
#else
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string> { { "config", Dir + "config.xml" }, { "credentials", Dir + "credentials.xml" } };
#endif
        private static readonly Dictionary<string, (XmlDocument, XmlNamespaceManager)> Docs = new Dictionary<string, (XmlDocument, XmlNamespaceManager)>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static Config()
        {
            var success = true;
            foreach (var file in Files)
            {
                var events = new List<ValidationEventArgs>();

                var settings = new XmlReaderSettings();
                settings.Schemas.Add(file.Key, Dir + file.Key + ".xsd");
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += (s, e) => events.Add(e);

                var doc = new XmlDocument();
                XmlReader reader = null;
                try
                {
                    reader = XmlReader.Create(file.Value, settings);
                }
                catch
                {
                    // ignored
                }

                doc.Load(reader);
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace(NamespacePrefix, file.Key);

                if (events.Any())
                {
                    var message = string.Join('\n', events.Select(e => (e.Severity == XmlSeverityType.Warning ? "Warning" : "Error") + " at line " + e.Exception.LineNumber + ": " + e.Message));
                    Console.WriteLine("Invalid XML: " + file.Value);
                    Console.WriteLine(message);
                    success = false;
                }

                Docs.Add(file.Key, (doc, nsmgr));
            }

            if (success)
                return;

            Console.WriteLine("Problems with config files found. Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(-1);
        }

        private static XmlNode _selectSingleNode((XmlDocument, XmlNamespaceManager) pair, string path) => pair.Item1.SelectSingleNode(path.Replace(Slash.ToString(), Slash + NamespacePrefix + ':'), pair.Item2);

        internal static bool DoesPropertyExist(string property, string file = DefaultDoc) => Docs.TryGetValue(file, out var pair) && _selectSingleNode(pair, Slash + file + Slash + property) != null;

        internal static bool Get<T>(string property, out T value, string file = DefaultDoc)
        {
            value = default(T);

            if (!Docs.TryGetValue(file, out var pair))
            {
                Log.Warn(Tag, "Could not find file: " + file);
                return false;
            }

            var node = _selectSingleNode(pair, Slash + file + Slash + property);

            if (node == null)
            {
                Log.Warn(Tag, "Tried to read property which doesn't exist: " + property);
                return false;
            }

            var valueString = node.InnerText;

            try
            {
                value = (T)Convert.ChangeType(valueString, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidCastException) && 
                    !(ex is FormatException) && 
                    !(ex is OverflowException) &&
                    !(ex is ArgumentNullException))
                    throw;
                Log.Error(Tag, "Could not convert property " + property + " with value " + valueString + " to type " + typeof(T).Name + ".");
                return false;

            }
        }

        internal static T GetDefault<T>(string property, T defaultValue, string file = DefaultDoc)
        {
            if (!DoesPropertyExist(property, file))
            {
                Log.Info(Tag, "Could not find property " + property + ". Using default value (" + defaultValue + ").");
                return defaultValue;
            }
            Get<T>(property, out var outValue, file);
            return outValue;
        }

        internal static bool Set(string property, object value, string file = DefaultDoc)
        {
            if (!Docs.TryGetValue(file, out var pair))
            {
                Log.Warn(Tag, "Could not find file: " + file);
                return false;
            }

            if (!Files.TryGetValue(file, out var fileName))
            {
                Log.Warn(Tag, "Could not find file location for file: " + file);
                return false;
            }

            var valueString = value.ToString();

            var node = _traveseOrBuildHierarchy(pair, Slash + file + Slash + property);

            node.InnerText = valueString;

            try
            {
                File.WriteAllText(fileName, Beautify(pair.Item1));
            }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException) && 
                    !(ex is PathTooLongException) &&
                    !(ex is DirectoryNotFoundException) && 
                    !(ex is IOException) &&
                    !(ex is UnauthorizedAccessException) && 
                    !(ex is NotSupportedException) &&
                    !(ex is SecurityException))
                    throw;
                Log.Error(Tag, "Could not set property " + property + ":\n" + ex.Message);
                return false;

            }

            return true;
        }

        private static XmlNode _traveseOrBuildHierarchy((XmlDocument, XmlNamespaceManager) pair, string path)
        {
            var split = path.Trim('/').Split('/');

            var doc = pair.Item1;
            var parent = doc as XmlNode;

            foreach (var node in split)
            {
                var current = parent.SelectSingleNode(NamespacePrefix + ":" + node, pair.Item2) ?? parent.AppendChild(doc.CreateElement(node));

                parent = current;
            }

            return parent;
        }

        private static string Beautify(XmlDocument doc)
        {
            var stringBuilder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (var writer = XmlWriter.Create(stringBuilder, settings))
            {
                doc.Save(writer);
            }
            return stringBuilder + Environment.NewLine;
        }
    }
}

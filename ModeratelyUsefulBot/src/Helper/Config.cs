using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace ModeratelyUsefulBot
{
    internal static class Config
    {
        private static string _tag = "Config";
        private const string _defaultDoc = "config";
        private const string _dir = "data/";
        private const char _slash = '/';
        private const string _namespacePrefix = "a";
#if DEBUG
        private static Dictionary<string, string> _files = new Dictionary<string, string> { { "config", _dir + "debugConfig.xml" }, { "credentials", _dir + "debugCredentials.xml" } };
#else
        private static Dictionary<string, string> _files = new Dictionary<string, string> { { "config", _dir + "config.xml" }, { "credentials", _dir + "credentials.xml" } };
#endif
        private static Dictionary<string, (XmlDocument, XmlNamespaceManager)> _docs = new Dictionary<string, (XmlDocument, XmlNamespaceManager)>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static Config()
        {
            bool success = true;
            foreach(var file in _files)
            {
                var events = new List<ValidationEventArgs>();

                var settings = new XmlReaderSettings();
                settings.Schemas.Add(file.Key, _dir + file.Key + ".xsd");
                settings.ValidationType = ValidationType.Schema;
                settings.ValidationEventHandler += (s, e) => events.Add(e);

                var doc = new XmlDocument();
                XmlReader reader = null;
                try
                {
                    reader = XmlReader.Create(file.Value, settings);
                }
                catch (Exception) { }
                doc.Load(reader);
                var nsmgr = new XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace(_namespacePrefix, file.Key);

                if(events.Count() > 0)
                {
                    var message = string.Join('\n', events.Select(e => (e.Severity == XmlSeverityType.Warning ? "Warning" : "Error") + " at line " + e.Exception.LineNumber + ": " + e.Message));
                    Console.WriteLine("Invalid XML: " + file.Value);
                    Console.WriteLine(message);
                    success = false;
                }

                _docs.Add(file.Key, (doc, nsmgr));
            }

            if (!success)
            {
                Console.WriteLine("Problems with config files found. Press any key to exit.");
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }

        private static XmlNode _selectSingleNode((XmlDocument, XmlNamespaceManager) pair, string path) => pair.Item1.SelectSingleNode(path.Replace(_slash.ToString(), _slash + _namespacePrefix + ':'), pair.Item2);

        internal static bool DoesPropertyExist(string property, string file = _defaultDoc) => _docs.TryGetValue(file, out var pair) && _selectSingleNode(pair, _slash + file + _slash + property) != null;

        internal static bool Get<T>(string property, out T value, string file = _defaultDoc)
        {
            value = default(T);
            var valueString = "";

            if (!_docs.TryGetValue(file, out var pair))
            {
                Log.Warn(_tag, "Could not find file: " + file);
                return false;
            }

            var node = _selectSingleNode(pair, _slash + file + _slash + property);

            if (node == null)
            {
                Log.Warn(_tag, "Tried to read property which doesn't exist: " + property);
                return false;
            }

            valueString = node.InnerText;

            try
            {
                value = (T)Convert.ChangeType(valueString, typeof(T), CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                if (ex is InvalidCastException
                    || ex is FormatException
                    || ex is OverflowException
                    || ex is ArgumentNullException)
                {
                    Log.Error(_tag, "Could not convert property " + property + " with value " + valueString + " to type " + typeof(T).Name + ".");
                    return false;
                }

                throw;
            }
        }

        internal static T GetDefault<T>(string property, T defaultValue, string file = _defaultDoc)
        {
            if (!DoesPropertyExist(property, file))
            {
                Log.Info(_tag, "Could not find property " + property + ". Using default value (" + defaultValue.ToString() + ").");
                return defaultValue;
            }
            Get<T>(property, out var outValue, file);
            return outValue;
        }

        internal static bool Set(string property, object value, string file = _defaultDoc)
        {
            if (!_docs.TryGetValue(file, out var pair))
            {
                Log.Warn(_tag, "Could not find file: " + file);
                return false;
            }

            if (!_files.TryGetValue(file, out var fileName))
            {
                Log.Warn(_tag, "Could not find file location for file: " + file);
                return false;
            }

            var value_string = value.ToString();

            var node = _traveseOrBuildHierarchy(pair, _slash + file + _slash + property);

            node.InnerText = value_string;

            try
            {
                File.WriteAllText(fileName, Beautify(pair.Item1));
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException
                    || ex is ArgumentNullException
                    || ex is PathTooLongException
                    || ex is DirectoryNotFoundException
                    || ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is NotSupportedException
                    || ex is SecurityException)
                {
                    Log.Error(_tag, "Could not set property " + property + ":\n" + ex.Message);
                    return false;
                }

                throw;
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
                var current = parent.SelectSingleNode(_namespacePrefix + ":" + node, pair.Item2);
                if (current == null)
                    current = parent.AppendChild(doc.CreateElement(node));

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
            return stringBuilder.ToString() + Environment.NewLine;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace ModeratelyUsefulBot
{
    internal static class Config
    {
        private static string _tag = "Config";
        private const string _defaultDoc = "config";
        private const string _dir = "data/";
        private const char _slash = '/';
#if DEBUG
        private static Dictionary<string, string> _files = new Dictionary<string, string> { { "config", _dir + "debugConfig.xml" }, { "credentials", _dir + "debugCredentials.xml" } };
#else
        private static Dictionary<string, string> _files = new Dictionary<string, string> { { "config", _dir + "config.xml" }, { "credentials", _dir + "credentials.xml" } };
#endif
        private static Dictionary<string, XmlDocument> _docs = new Dictionary<string, XmlDocument>();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static Config()
        {
            foreach(var file in _files)
            {
                var doc = new XmlDocument();
                doc.Load(file.Value);
                _docs.Add(file.Key, doc);
            }
        }

        public static bool DoesPropertyExist(string property, string file = _defaultDoc) => _docs.TryGetValue(file, out var doc) && doc.SelectSingleNode(_slash + file + _slash + property) != null;

        public static bool Get<T>(string property, out T value, string file = _defaultDoc)
        {
            value = default(T);
            var valueString = "";

            if (!_docs.TryGetValue(file, out var doc))
            {
                Log.Warn(_tag, "Could not find file: " + file);
                return false;
            }

            var node = doc.SelectSingleNode(_slash + file + _slash + property);

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

        public static T GetDefault<T>(string property, T defaultValue, string file = _defaultDoc)
        {
            if (!DoesPropertyExist(property, file))
            {
                Log.Info(_tag, "Could not find property " + property + ". Using default value (" + defaultValue.ToString() + ").");
                return defaultValue;
            }
            Get<T>(property, out var outValue, file);
            return outValue;
        }

        public static bool Set(string property, object value, string file = _defaultDoc)
        {
            if (!_docs.TryGetValue(file, out var doc))
            {
                Log.Warn(_tag, "Could not find file: " + file);
                return false;
            }

            if(!_files.TryGetValue(file, out var fileName))
            {
                Log.Warn(_tag, "Could not find file location for file: " + file);
                return false;
            }

            var value_string = value.ToString();

            var node = _traveseOrBuildHierarchy(doc, _slash + file + _slash + property);

            node.InnerText = value_string;

            try
            {
                File.WriteAllText(fileName, Beautify(doc));
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

        private static XmlNode _traveseOrBuildHierarchy(XmlDocument doc, string path)
        {
            var split = path.Trim('/').Split('/');

            var parent = doc as XmlNode;

            foreach (var node in split)
            {
                var current = parent.SelectSingleNode(node);
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

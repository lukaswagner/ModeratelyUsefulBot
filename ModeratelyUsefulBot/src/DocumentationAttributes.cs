using System;

namespace ModeratelyUsefulBot
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class CommandAttribute : Attribute
    {
        public string Name;
        public string ShortDescription;
        public string Description;

        public string Documentation => Name + " - " + Description;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    internal class ArgumentAttribute : Attribute
    {
        public string Name;
        public string Description;
        public bool Optional = false;
        public string DefaultValue;
        public Type Type;

        public string Documentation
        {
            get
            {
                var result = "" + Name + " - ";

                result +=
                    Type == typeof(string) ? "Text" :
                    Type == typeof(int) ? "Integer" :
                    Type == typeof(float) ? "Decimal" :
                    Type == typeof(bool) ? "Boolean" : "Unknown type";

                if (Optional)
                    result += ", optional";
                if (DefaultValue != null)
                    result += ", default: " + DefaultValue;
                result += ".\n  " + Description;
                return result;
            }
        }
    }
}

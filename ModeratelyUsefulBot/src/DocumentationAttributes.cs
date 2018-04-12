﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ModeratelyUsefulBot
{
    [AttributeUsage(AttributeTargets.Method)]
    class CommandAttribute : Attribute
    {
        public string Name;
        public string ShortDescription;
        public string Description;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class ArgumentAttribute : Attribute
    {
        public string Name;
        public string Description;
        public bool Optional = false;
        public string DefaultValue;
    }
}

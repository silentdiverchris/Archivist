﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivist.Helpers
{
    internal static class IntHelpers
    {
        internal static string PluralSuffix(this int number)
        {
            return number == 1
                ? ""
                : "s";
        }
    }
}

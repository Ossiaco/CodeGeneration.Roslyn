﻿namespace CodeGeneration.Chorus
{
    using System.Collections.Generic;
    using System.Data.Entity.Design.PluralizationServices;
    using System.Globalization;

    internal static class Utilities
    {
        internal static readonly PluralizationService PluralizationService = PluralizationService.CreateService(new CultureInfo("en-US"));

        internal static string ToPascalCase(this string name)
        {
            // Requires.NotNullOrEmpty(name, "name");
            return name.Substring(0, 1).ToUpperInvariant() + name.Substring(1);
        }

        internal static string ToCamelCase(this string name)
        {
            // Requires.NotNullOrEmpty(name, "name");
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }

        internal static string ToPlural(this string word)
        {
            return PluralizationService.Pluralize(word);
        }

        internal static string ToSingular(this string word)
        {
            return PluralizationService.Singularize(word);
        }
    }
}
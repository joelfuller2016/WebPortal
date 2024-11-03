using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Reflection;

namespace WebAI.Models
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            FieldInfo field = value.GetType().GetField(value.ToString());
            if (field == null)
                return value.ToString();

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);

            if (attributes != null && attributes.Length > 0)
                return attributes[0].Description;

            return value.ToString();
        }

        public static T ParseEnum<T>(string value) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException("value");

            T result;
            if (Enum.TryParse<T>(value, true, out result))
                return result;

            throw new ArgumentException(string.Format("Cannot parse '{0}' to enum {1}", value, typeof(T).Name));
        }

        public static bool IsValidEnumValue<T>(string value) where T : struct
        {
            if (string.IsNullOrEmpty(value))
                return false;

            T result;
            return Enum.TryParse<T>(value, true, out result);
        }

        public static T[] GetAllValues<T>() where T : struct
        {
            return (T[])Enum.GetValues(typeof(T));
        }

        public static string[] GetAllDescriptions<T>() where T : struct
        {
            List<string> descriptions = new List<string>();
            T[] values = (T[])Enum.GetValues(typeof(T));

            foreach (T value in values)
            {
                Enum enumValue = value as Enum;
                if (enumValue != null)
                {
                    descriptions.Add(GetDescription(enumValue));
                }
            }

            return descriptions.ToArray();
        }
    }
}
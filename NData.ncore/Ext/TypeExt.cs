using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NData
{
    internal static class TypeExt
    {
        public static string TableName(this Type type)
        {
            string tableName = type.Name;
            MethodInfo? tableMethod = type.GetMethod("Table", BindingFlags.Static | BindingFlags.Public);
            if (tableMethod != null)
                tableName = tableMethod.Invoke(null, null).ToString();
            return tableName;
        }

        public static PropertyInfo FindPK(this Type type)
        {
            foreach (PropertyInfo p in type.GetProperties())
            {
                if (p.Name.ToLower().Equals("id") ||
                    p.GetCustomAttribute<KeyAttribute>() != null)
                    return p;
            }
            return null;
        }

        public static List<PropertyInfo> FindPropForType(this Type type, Type typeFindedProp)
        {
            List<PropertyInfo> res = new List<PropertyInfo>();
            foreach (PropertyInfo p in type.GetProperties())
                if (p.PropertyType == typeFindedProp)
                    res.Add(p);
            return res;
        }

        public static PropertyInfo FindPropForName(this Type type, string propName)
        {
            foreach (PropertyInfo p in type.GetProperties())
                if (p.Name.ToLower().Equals(propName.ToLower()))
                    return p;
            return null;
        }

        public static bool IsSimpleType(this Type type)
        {
            return type.IsPrimitive
              || type.Equals(typeof(string)) ||


              type.Equals(typeof(System.Nullable<Decimal>)) ||
              type.Equals(typeof(System.Nullable<Int32>)) ||
              type.Equals(typeof(System.Nullable<Int16>)) ||
              type.Equals(typeof(System.Nullable<Int64>)) ||
              type.Equals(typeof(System.Nullable<Double>)) ||
              type.Equals(typeof(System.Nullable<Guid>)) ||
              type.Equals(typeof(System.Nullable<DateTime>)) ||
              type.Equals(typeof(System.Nullable<Byte>)) ||

              type.Equals(typeof(System.Decimal)) ||
              type.Equals(typeof(System.Int32)) ||
              type.Equals(typeof(System.String)) ||
              type.Equals(typeof(System.Int16)) ||
              type.Equals(typeof(System.Int64)) ||
              type.Equals(typeof(System.Double)) ||
              type.Equals(typeof(System.Guid)) ||
              type.Equals(typeof(System.DateTime)) ||
              type.Equals(typeof(System.Byte)

              );
        }

    }
}

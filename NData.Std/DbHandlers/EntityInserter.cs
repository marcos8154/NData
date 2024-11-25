using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using NData.Providers;

namespace NData.DbHandlers
{
    public class EntityInserter
    {
        private readonly DbScope scope;
        private Type eType;
        private bool insertWithIdVal;
        private string[] specificFields;

        internal EntityInserter(DbScope scope)
        {
            this.scope = scope;
        }

        public EntityInserter Into<T>(params string[] fields)
        {
            insertWithIdVal = false;
            eType = typeof(T);
            specificFields = fields;
            return this;
        }

        public EntityInserter IncludeKeyValue()
        {
            insertWithIdVal = true;
            return this;
        }

        public void Values<T>(T data)
        {
            if (specificFields != null && specificFields.Length == 0)
                specificFields = null;
            Type type = eType;
            string tableName = type.Name;

            string sql = BuildSql(type, tableName);
            sql += $"\n{scope.SelectInsertedKey()}";

            using (IDbCommand cmd = scope.BuildDbCommand(sql))
            {
                PropertyInfo idProperty = FillParameters(data, insertWithIdVal, type, cmd);

                object insertedId = cmd.ExecuteScalar();

                if (idProperty != null)
                    if (insertedId != null && insertedId != DBNull.Value)
                        idProperty.SetValue(data, Convert.ChangeType(insertedId, idProperty.PropertyType));
            }
        }

        private PropertyInfo FillParameters<T>(T data, bool withId, Type type, IDbCommand cmd)
        {
            PropertyInfo idProperty = null;
            foreach (PropertyInfo p in data.GetType().GetProperties())
            {
                if (p.PropertyType.IsSimpleType())
                {
                    if (specificFields?.Contains(p.Name) == false)
                        continue;

                    string pName = p.Name;
                    object pVal = p.GetValue(data);

                    if ((p.Name.ToLower().Equals("id") || p.GetCustomAttribute<KeyAttribute>() != null))
                    {
                        idProperty = p;
                        if (withId)
                            cmd.Parameters.Add(scope.BuildParameter($"@{pName.ToLower()}", pVal));
                    }
                    else
                        cmd.Parameters.Add(scope.BuildParameter($"@{pName.ToLower()}", pVal));
                }
            }
            return idProperty;
        }

        private string BuildSql(Type type, string tableName)
        {
            string ins = $"insert into {tableName} (";
            string vals = "values (";

            foreach (PropertyInfo p in type.GetProperties())
            {
                if (p.PropertyType.IsSimpleType())
                {
                    if (specificFields?.Contains(p.Name) == false)
                        continue;

                    if ((p.Name.ToLower().Equals("id") || p.GetCustomAttribute<KeyAttribute>() != null))
                    {
                        if (insertWithIdVal)
                        {
                            ins += $"{p.Name}, ";
                            vals += $"@{p.Name.ToLower()}, ";
                        }
                    }
                    else
                    {
                        ins += $"{p.Name}, ";
                        vals += $"@{p.Name.ToLower()}, ";
                    }
                }
            }

            ins = ins.Substring(0, ins.Length - 2);
            vals = vals.Substring(0, vals.Length - 2);

            return $@"{ins}) {vals});";
        }
    }
}

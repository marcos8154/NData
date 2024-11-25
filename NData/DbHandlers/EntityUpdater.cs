using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NData.DbHandlers
{
    public class EntityUpdater
    {
        private readonly DbScope scope;
        private string customWhere;
        private string[] onlyTheseChangedFields = null;
        private Type type;
        internal EntityUpdater(DbScope scope)
        {
            this.scope = scope;
        }

        public EntityUpdater Set<T>(string customWhere = null,
            params string[] onlyTheseChangedFields)
        {
            if ((customWhere != null) && !customWhere.ToLower().StartsWith("where"))
                customWhere = $"where {customWhere}";

            this.customWhere = customWhere;
            if (onlyTheseChangedFields != null)
                if (onlyTheseChangedFields.Length > 0)
                    this.onlyTheseChangedFields = onlyTheseChangedFields;

            this.type = typeof(T);
            return this;
        }

        public void Values<T>(T data)
        {

            string tableName = type.Name;
            string sql = BuildSql(tableName, customWhere, onlyTheseChangedFields);
            if (!sql.ToLower().Contains("where"))
                throw new Exception("Not secure UPDATE operation: WHERE clause is not defined");

            using (IDbCommand cmd = scope.BuildDbCommand(sql))
            {
                FillParameters(data, cmd, onlyTheseChangedFields);

                cmd.ExecuteNonQuery();
            }
        }

        private void FillParameters<T>(T data, IDbCommand cmd, string[] specificFields)
        {
            foreach (PropertyInfo p in type.GetProperties())
            {
                if (p.PropertyType.IsSimpleType() || p.PropertyType.Name.Contains("Nullable"))
                {
                    string pName = p.Name;
                    object pVal = p.GetValue(data);

                    if (p.Name.ToLower().Equals("id") || 
                        p.GetCustomAttribute<KeyAttribute>() != null || 
                        cmd.CommandText.ToLower().Contains(p.Name.ToLower()))
                    {
                        cmd.Parameters.Add(scope.BuildParameter($"@{pName.ToLower()}", pVal));
                        continue;
                    }
                    else if ((specificFields != null) && (specificFields.Contains(p.Name) == false))
                        continue;

                    cmd.Parameters.Add(scope.BuildParameter($"@{pName.ToLower()}", pVal));
                }
            }
        }

        private string BuildSql(string tableName, string customWhere, string[] onlyTheseChangedFields)
        {
            string where = customWhere == null ? "" : customWhere;
            string sql = $"update {tableName} SET \n";
            foreach (PropertyInfo p in type.GetProperties())
            {
                if (p.PropertyType.IsSimpleType() || p.PropertyType.Name.Contains("Nullable"))
                {
                    if ((p.Name.ToLower().Equals("id") || p.GetCustomAttribute<KeyAttribute>() != null))
                    {
                        if (customWhere == null)
                            where = $@"WHERE {p.Name} = @{p.Name.ToLower()}";
                        continue;
                    }

                    if (onlyTheseChangedFields != null)
                    {
                        if (!onlyTheseChangedFields.Contains(p.Name))
                            continue;
                    }

                    string set = $"\n{p.Name} = @{p.Name.ToLower()},";
                    sql += set;
                }
            }
            if (sql.EndsWith(","))
                sql = sql.Substring(0, sql.Length - 1);
            sql += $"\n{where}";

            return sql;
        }
    }
}

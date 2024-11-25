using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NData.DbHandlers
{
    public class EntityDeletor
    {
        private DbScope scope;
        private Type eType; //esse type é a entidade
        private string customWhere;
        internal EntityDeletor(DbScope scope)
        {
            this.scope = scope;
        }
        public EntityDeletor From<T>() where T : class
        {
            customWhere = null;
            eType = typeof(T);
            return this;
        }

        public EntityDeletor Where(string customWhereClause)
        {
            customWhere = customWhereClause;
            return this;
        }

        
        public void Values<T>(T data) // esse T pode NAO SER o type da entidade armazenada em eType. pode ser um obj de dados anonimo
        {
            string tableName = eType.Name;

            string sql = BuildSql(tableName);
            if (!sql.ToLower().Contains("where"))
                throw new Exception("Not secure DELETE operation: WHERE clause is not defined");

            using (IDbCommand cmd = scope.BuildDbCommand(sql))
            {
                if(string.IsNullOrEmpty(customWhere))
                {
                    PropertyInfo idProp = typeof(T).FindPK();
                    object idVal = idProp.GetValue(data);

                    cmd.Parameters.Add(scope.BuildParameter("@id", idVal));
                }
                else
                {
                    foreach(PropertyInfo pi in typeof(T).GetProperties())
                        cmd.Parameters.Add(scope.BuildParameter($"@{pi.Name}", pi.GetValue(data)));
                }

                cmd.ExecuteNonQuery();
            }
        }

        private string BuildSql(string tableName)
        {
            string sql = $@"delete from {tableName} ";
            if (string.IsNullOrEmpty(customWhere))
            {
                PropertyInfo id = eType.FindPK();
                if (id == null)
                    throw new Exception($"The type '{eType.Name}' does not have an Id property. Ensure that a property marked [Key] or named 'Id' in classs-type '{eType.FullName}'. Alternatively, you can call 'Where(string)' method in this EntityDeletor to provide a custom where sql clause as string.");

                sql += $"where {id.Name} = @id";
            }
            else sql += customWhere;
            return sql;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NData.Caching;

namespace NData.DbHandlers
{
    public interface IEntitySelector
    {
        object ToListObj();
        IEntitySelector WhereObj(string where_sql, object paramsObj = null);
        void Disposed();
    }
    public sealed class EntitySelectorT<T> : IEntitySelector where T : class
    {
        private Type type;
        private DbScope scope;
        private int cacheTimeSeconds = 10;
        private bool isCacheShared;

        public string Sql => sb.ToString();

        private List<string> fieldList = new List<string>();
        private List<JoinOneCatalog> joinOnes = new List<JoinOneCatalog>();
        private List<JoinManyCatalog> joinManies = new List<JoinManyCatalog>();
        private StringBuilder sb = new StringBuilder();
        private object Params;

        public void Disposed()
        {
            CacheRepository<List<T>>.ExpireAll(scope.Id);
        }

        public EntitySelectorT(DbScope scope, string[] customFields)
        {
            this.type = typeof(T);
            this.scope = scope;

            BeginSelect(customFields);
        }

        public EntitySelectorT(DbScope scope, string rawSql)
        {
            this.type = typeof(T);
            this.scope = scope;

            sb.Append(rawSql);
        }

        private void BeginSelect(string[] customFields)
        {
            if ((customFields != null) && (customFields.Length > 0))
            {
                fieldList.AddRange(customFields);
            }
            else
                foreach (PropertyInfo p in type.GetProperties())
                {
                    if (p.PropertyType.IsSimpleType())
                        fieldList.Add($"{type.TableName()}.{p.Name}");
                }

            sb = new StringBuilder();
            sb.AppendLine("SELECT ");
            sb.AppendLine("$field_list ");
            sb.AppendLine($"FROM {type.TableName()}");
        }

        private JoinOneCatalog JoinOneInternal<TTarget>(
            Expression<Func<T, object>> joinFkProperty, //ex: Product_id (int)
            Expression<Func<T, TTarget>> customNavigationProperty = null, //ex: Product (obj), 
            string joinAlias = null,
            string[] customFields = null,
            Join join = Join.Inner) where TTarget : class
        {
            string fkName = joinFkProperty.Body.GetType().GetProperty("Operand").GetValue(joinFkProperty.Body).ToString();
            fkName = fkName.Substring(fkName.LastIndexOf('.') + 1);

            string targetNavigationPropertyName = null;
            if (customNavigationProperty != null)
            {
                var t = customNavigationProperty.Body.ToString();
                t = t.Substring(t.IndexOf('.'), t.Length - t.IndexOf('.')).Replace(".", "");
                targetNavigationPropertyName = t;
            }

            Type navigationType = typeof(TTarget);
            string joinSelectName = (joinAlias ?? navigationType.TableName());

            var joinPk = navigationType.FindPK();
            if (joinPk == null)
                throw new Exception($"Entity type '{navigationType.FullName}' does not have an Primary Key. Ensure that a property named 'Id' or [Key] attribute in an class-property.");

            sb.AppendLine($"{(join == Join.Inner ? "INNER" : "LEFT")} JOIN {navigationType.TableName()} {joinAlias} ON {type.TableName()}.{fkName} = {joinSelectName}.{joinPk.Name}");

            List<PropertyInfo> propNavigation = type.FindPropForType(navigationType);

            if (targetNavigationPropertyName == null)
            {
                if (propNavigation.Count == 0)
                    throw new Exception($"An navigation-property for '{navigationType.Name}' was not found in type '{type.FullName}'. Ensure that a public-property for type '{navigationType.FullName}' was defined in class '{type.Name}'.");
                if (propNavigation.Count > 1)
                    throw new Exception($"Detected multiple navigation-properties for '{navigationType.Name}' declared in type '{type.Name}'. Ensure to pass parameter 'customNavigationProperty' on this method to define a target navigation-property in type '{type.Name}'.");
                targetNavigationPropertyName = propNavigation[0].Name;
            }

            JoinOneCatalog catalog = new JoinOneCatalog(targetNavigationPropertyName,
                $"{joinSelectName}_",
                fieldList.Count);

            if ((customFields == null) || (customFields.Length == 0))
            {
                foreach (PropertyInfo p in navigationType.GetProperties())
                    if (p.PropertyType.IsSimpleType())
                    {
                        fieldList.Add($"{joinSelectName}.{p.Name}  '{joinSelectName}_{p.Name}'");
                        catalog.ResultsEndPosition += 1;
                    }
            }
            else
            {
                foreach (var cf in customFields)
                {
                    string fName = cf;

                    if (!fName.StartsWith(navigationType.TableName()))
                    {
                        if (!fName.Contains("."))
                            fName = $"{navigationType.TableName()}.{fName}";
                    }

                    if (fName.Contains("'"))
                        fieldList.Add(fName);
                    else
                        fieldList.Add($"{fName} '{fName.Replace(".", "_")}'");

                    catalog.ResultsEndPosition += 1;
                }
            }
            return catalog;
        }

        public EntitySelectorT<T> JoinOne<TTarget>(
            Expression<Func<T, object>> joinFkProperty, //ex: Product_id (int)
            Expression<Func<T, TTarget>> customNavigationProperty = null, //ex: Product (obj), 
            string joinAlias = null,
            string[] customFields = null,
            Join join = Join.Inner) where TTarget : class
        {
            JoinOneCatalog catalog = JoinOneInternal<TTarget>(joinFkProperty, customNavigationProperty, joinAlias, customFields, join);
            joinOnes.Add(catalog);
            return this;
        }

        public EntitySelectorT<T> ThenJoinOne<TSource, TTarget>(
            Expression<Func<TSource, object>> joinFkProperty, //ex: Product_id (int)
            Expression<Func<TSource, TTarget>> customNavigationProperty = null, //ex: Product (obj), 
            string joinAlias = null,
            string[] customFields = null,
            Join join = Join.Inner) where TTarget : class where TSource : class
        {
            (joinManies.Last().Selector as EntitySelectorT<TSource>).JoinOne<TTarget>(joinFkProperty, customNavigationProperty, joinAlias, customFields, join);
            return this;
        }

        public EntitySelectorT<T> JoinMany<TTarget>(
            Expression<Func<TTarget, object>> targetFkProperty,
            Expression<Func<T, ICollection<TTarget>>> navigationListProperty,
            string[] customFields = null,
            string bodyOrWhereClause = null) where TTarget : class
        {
            string fkName = targetFkProperty.Body.GetType().GetProperty("Operand").GetValue(targetFkProperty.Body).ToString();
            fkName = fkName.Substring(fkName.LastIndexOf('.') + 1);

            string navListName = navigationListProperty.Body.ToString();
            navListName = navListName.Substring(navListName.LastIndexOf('.') + 1);

            var es = scope.Select.From<TTarget>(customFields).Cache(10, false);

            joinManies.Add(new JoinManyCatalog(fkName, navListName, bodyOrWhereClause).Set(es));
            return this;
        }

        public EntitySelectorT<T> Body(string where_or_any_sql, object paramsObj = null)
        {
            sb.AppendLine(where_or_any_sql.Trim());
            this.Params = paramsObj;
            return this;
        }

        public EntitySelectorT<T> Cache(int cacheSeconds = 10, bool shared = false)
        {
            cacheTimeSeconds = cacheSeconds;
            isCacheShared = shared;
            return this;
        }

        public EntitySelectorT<T> Where(string where_sql, object paramsObj = null)
        {
            if (!Sql.ToLower().Contains("where"))
            {
                if (!where_sql.Trim().ToLower().StartsWith("where"))
                    sb.AppendLine("WHERE");
                sb.AppendLine(where_sql.Trim());
            }

            this.Params = paramsObj;
            return this;
        }

        public IEntitySelector WhereObj(string where_sql, object paramsObj = null)
        {
            return Where(where_sql, paramsObj);
        }
        public object ToListObj()
        {
            return ToList();
        }

        public T FirstOrDefault()
        {
            first = true;
            return ToList().FirstOrDefault();
        }

        private bool first = false;

        public string SqlFull { get; set; }

        public async Task<List<T>> ToListAsync()
        {
            return await Task.Run(() => ToList());
        }

        public List<T> ToList()
        {
            StringBuilder ck = new StringBuilder();//cache-key

            if (isCacheShared == false)
                ck.Append($"{scope.Id}-");

            string sql = Sql;
            string fList = "";
            for (int i = 0; i < fieldList.Count; i++)
            {
                if (i == fieldList.Count - 1) 
                    fList += $" {fieldList[i]}\n";
                else
                    fList += $" {fieldList[i]},\n";
            }
            sql = sql.Replace("$field_list", fList);
            ck.Append($"{sql.Replace(" ", "")}-");

            List<T> result = new List<T>();
            List<IDataParameter> pars = new List<IDataParameter>();

            if (Params != null)
                foreach (PropertyInfo field in Params.GetType().GetProperties())
                {
                    object val = field.GetValue(Params);
                    pars.Add(
                           scope.BuildParameter(
                       $"@{field.Name}", val
                   ));

                    ck.Append($"{val}-".Replace(" ", ""));
                }

            string key = null;

            if (cacheTimeSeconds > 0)
            {
                key = ck.ToString().Replace("\r", "").Replace("\n", "").Replace("\t", "");
                Cache<List<T>> cached = CacheRepository<List<T>>.Get(key);
                if (cached != null)
                    return cached.Value;
            }

            SqlFull = sql;

            using (DbScope joinScp = new DbScope())
            {
                using (IDbCommand cmd = joinScp.BuildDbCommand(sql))
                {
                    pars.ForEach(p => cmd.Parameters.Add(p));
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();

                    using (IDataReader dr = cmd.ExecuteReader())
                    {
                        if (fieldList.Count == 0)
                        {
                            DataTable schema = dr.GetSchemaTable();
                            if (schema != null)
                                foreach (DataRow row in schema.Rows)
                                    if (row != null)
                                        fieldList.Add(row[0].ToString());
                        }

                        while (dr.Read())
                        {
                            object rootObj = Activator.CreateInstance<T>();

                            int rootEnd = (joinOnes.Count > 0 ?  joinOnes[0].ResultsStartPosition : fieldList.Count);

                            for (int i = 0; i < rootEnd; i++)
                            {
                                string fName = fieldList[i];
                                if (fName.Contains("."))
                                    fName = fName.Substring(fName.IndexOf('.'), fName.Length - fName.IndexOf('.')).Replace(".", "");
                                PropertyInfo p = rootObj.GetType().FindPropForName(fName);
                                if (p == null) continue;

                                if (dr.IsDBNull(i))
                                    p.SetValue(rootObj, null);
                                else
                                {
                                    if (p.PropertyType.FullName.Contains("Nullable"))
                                        p.SetValue(rootObj, Convert.ChangeType(dr.GetValue(i), Nullable.GetUnderlyingType(p.PropertyType)));
                                    else
                                        p.SetValue(rootObj, Convert.ChangeType(dr.GetValue(i), p.PropertyType));
                                }
                            }

                            foreach (JoinOneCatalog one in joinOnes)
                            {
                                PropertyInfo pNav = rootObj.GetType().FindPropForName(one.PropertyName);
                                object childOneObj = Activator.CreateInstance(pNav.PropertyType);

                                for (int i = one.ResultsStartPosition; i < one.ResultsEndPosition; i++)
                                {
                                    try
                                    {
                                        string fName = fieldList[i];
                                        fName =
                                            fName.Substring(
                                                    fName.IndexOf("'"), fName.Length - fName.IndexOf("'"));

                                        fName = fName.Replace($"{one.Alias}", "")
                                             .Replace("'", "");

                                        PropertyInfo p = childOneObj.GetType().FindPropForName(fName);
                                        if (p == null) continue;

                                        if (dr.IsDBNull(i)) p.SetValue(childOneObj, null);
                                        else p.SetValue(childOneObj, Convert.ChangeType(dr.GetValue(i), p.PropertyType));
                                    }
                                    catch { }
                                }

                                pNav.SetValue(rootObj, childOneObj);
                            }

                            foreach (JoinManyCatalog many in joinManies)
                            {
                                HandleManies(rootObj, many);
                            }

                            result.Add((T)rootObj);

                            if (first) break;
                        }
                    }
                }
            }


            first = false;

            if (cacheTimeSeconds > 0)
                CacheRepository<List<T>>.Set(key, result, cacheTimeSeconds);
            return result;
        }

        private static void HandleManies(object rootObj, JoinManyCatalog many)
        {
            var pkProp = rootObj.GetType().FindPK();
            if (pkProp == null)
                throw new Exception($"Entity type '{rootObj.GetType().FullName}' does not have an Primary Key. Ensure that a property named 'Id' or [Key] attribute in an class-property.");

            object list = many.Selector.WhereObj($"where {many.FKPropName} = @fkValue \n {many.BodyOrWhereClause}", new
            {
                fkValue = pkProp.GetValue(rootObj)
            }).ToListObj();

            PropertyInfo navListProp = rootObj.GetType().GetProperty(many.NavListPropName);
            navListProp.SetValue(rootObj, list);
        }

        public EntitySelectorT<T> WhereId(object idValue)
        {
            var pkProp = typeof(T).FindPK();
            if (pkProp == null)
                throw new Exception($"Entity type '{type.FullName}' does not have an Primary Key. Ensure that a property named 'Id' or [Key] attribute in an class-property.");

            return Where($"{type.TableName()}.{pkProp.Name} = @valueId", new
            {
                valueId = idValue
            });
        }

        public EntitySelectorT<T> Excep(params string[] exceptFieldNames)
        {
            foreach (var f in exceptFieldNames)
            {
                var ex = fieldList.FirstOrDefault(e => e.ToLower().Contains(f.ToLower()));
                if (ex != null)
                    fieldList.Remove(ex);
            }

            return this;
        }
    }
}
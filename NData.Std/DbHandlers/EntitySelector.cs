using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace NData.DbHandlers
{
    public sealed class EntitySelector
    {
        private List<IEntitySelector> sels = new List<IEntitySelector>();

        private DbScope scope;
        internal EntitySelector(DbScope scope)
        {
            this.scope = scope;
        }

        internal void Disposed()
        {
            sels.ForEach(es => es.Disposed());
            sels.Clear();
        }

        public EntitySelectorT<T> From<T>(params string[] customFields) where T : class
        {
            var es = new EntitySelectorT<T>(scope, customFields);
            sels.Add(es);
            return es;
        }

        public EntitySelectorT<T> FromSql<T>(string sql) where T : class
        {
            var es = new EntitySelectorT<T>(scope, sql);
            sels.Add(es);
            return es;
        }
    }
}

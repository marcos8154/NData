using System;
using System.Data;
using NData.Caching;
using NData.DbHandlers;
using NData.Providers;

namespace NData
{
    public sealed class DbScope : IDisposable
    {
        internal static IDbScopeProvider _provider { get; private set; }

        public static void DefineProvider(IDbScopeProvider provider)
        {
            _provider = provider;
        }

        private IDbConnection conn;
        public string Name { get; private set; }

        public DbScope(string name = null)
        {
            Id = Guid.NewGuid().ToString().Split('-')[0];
            this.Name = name;
            conn = _provider.BuildConnection(name);

            Insert = new EntityInserter(this);
            Update = new EntityUpdater(this);
            Select = new EntitySelector(this);
            Delete = new EntityDeletor(this);
        }


        public void Dispose()
        {
            try
            {
                conn.Close();
                conn.Dispose();
                Select.Disposed();
            }
            catch { }
        }

        internal IDbCommand BuildDbCommand(string sql) => _provider.BuildDbCommand(sql, conn, Name);
        internal IDataParameter BuildParameter(string pName, object pVal) => _provider.BuildDbParameter(pName, pVal, Name);
        internal string SelectInsertedKey() => _provider.SelectInsertedKey(Name);
        public IDbConnection DbConnection() => conn;

        public EntityInserter Insert { get; private set; }
        public EntityUpdater Update { get; private set; }
        public EntitySelector Select { get; private set; }
        public EntityDeletor Delete { get; private set; }
        public string Id { get; private set; }
    }
}
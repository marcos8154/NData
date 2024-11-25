using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NData.DbHandlers
{
    internal class JoinManyCatalog
    {
        public string BodyOrWhereClause { get; private set; }

        public string FKPropName { get; private set; }
        public string NavListPropName { get; private set; }
        public IEntitySelector Selector { get; private set; }

        private List<JoinOneCatalog> _joins = new List<JoinOneCatalog>();
        public IReadOnlyCollection<JoinOneCatalog> JoinOnes  => _joins.AsReadOnly();

        public JoinManyCatalog(string fKPropName, string navListPropName)
        {
            FKPropName = fKPropName;
            NavListPropName = navListPropName;
        }

        public JoinManyCatalog(string fKPropName, string navListPropName, string bodyOrWhereClause = "") : this(fKPropName, navListPropName)
        {
            this.BodyOrWhereClause = bodyOrWhereClause;
        }

        public JoinManyCatalog Set<T>(EntitySelectorT<T> es) where T:class
        {
            this.Selector = es;
            return this;
        }

        internal void AddJoinOne(JoinOneCatalog catalog)
        {
            _joins.Add(catalog);
        }
    }
}

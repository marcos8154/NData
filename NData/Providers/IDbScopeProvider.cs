﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NData.Providers
{
    public interface IDbScopeProvider
    {
        IDbConnection BuildConnection(string scopeName = null);
        IDbCommand BuildDbCommand(string sql, IDbConnection conn, string scopeName = null);
        IDbDataParameter BuildDbParameter(string parName, object parValue, string scopeName = null);
        string SelectInsertedKey(string scopeName = null);
    }
}
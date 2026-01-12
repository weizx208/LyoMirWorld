using System.Data;
using System.Data.SqlClient;

namespace MirCommon.Database
{
    
    
    
    public class SqlServerDatabase : BaseDatabase
    {
        public SqlServerDatabase(string connectionString) : base(connectionString)
        {
        }
        
        protected override IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
        
        protected override IDbDataParameter CreateParameter(string name, object value)
        {
            return new SqlParameter(name, value);
        }
        
        
        public override SERVER_ERROR OpenDataBase()
        {
            try
            {
                _connection = CreateConnection();
                _connection.Open();
                return SERVER_ERROR.SE_OK;
            }
            catch (System.Exception)
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
        
        
        public override SERVER_ERROR DelCharacter(string account, string serverName, string name)
        {
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = @"UPDATE TBL_CHARACTER SET FLD_DELETED = 1, FLD_DELETEDATE = GETDATE() 
                                  WHERE FLD_ACCOUNT = @account AND FLD_SERVERNAME = @server AND FLD_NAME = @name";
                cmd.Parameters.Add(CreateParameter("@account", account));
                cmd.Parameters.Add(CreateParameter("@server", serverName));
                cmd.Parameters.Add(CreateParameter("@name", name));
                
                return cmd.ExecuteNonQuery() > 0 ? SERVER_ERROR.SE_OK : SERVER_ERROR.SE_FAIL;
            }
            catch
            {
                return SERVER_ERROR.SE_FAIL;
            }
        }
    }
}

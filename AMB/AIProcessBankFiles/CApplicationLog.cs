using System;
using System.Data.SqlClient;

namespace CincApplicationLog
{
	/// <summary>
	/// Summary description for CApplicationLog.
	/// </summary>
	public class CApplicationLog
	{
		public SqlConnection m_conn;
		string m_app;
		public string m_database;
		public string m_server;
        public int m_groupid;

		public CApplicationLog(string sDatabase, string sAppName,string sServer)
		{
            m_groupid = 0;
			m_database = sDatabase;
			m_server = sServer;
			m_app = sAppName;
			m_conn = new SqlConnection(string.Format("Server={0};Database=CincCustomer;UID=CincUser;PWD=0tat0pay$A;",sServer));
			m_conn.Open();
            using(SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = m_conn;
                cmd.CommandText = "select max(numapplogid) from applicationlog";
                object applogid = cmd.ExecuteScalar();
                m_groupid = (applogid == DBNull.Value) ? 0 : Convert.ToInt32(applogid);
            }
		}

		public void LogApp()
		{
			LogApp("");
		}

        public void LogApp(string sMessage)
		{
			SqlCommand cmdLog = new SqlCommand(string.Format("insert into applicationlog (txtdatabase,txtapplication,numresult,txtclass,txtfunction,txtmessage,numgroupid) values('{0}','{1}',1,'','','{2}',{3})",m_database.Replace("'","''"),m_app.Replace("'","''"),sMessage.Replace("'","''"),m_groupid),m_conn);
			cmdLog.ExecuteNonQuery();
		}

		public void LogErr(string sClass,string sFunction,string sMessage)
		{
			SqlCommand cmdLog = new SqlCommand(string.Format("insert into applicationlog (txtdatabase,txtapplication,numresult,txtclass,txtfunction,txtmessage,numgroupid) values('{0}','{1}',0,'{2}','{3}','{4}',{5})",m_database.Replace("'","''"),m_app.Replace("'","''"),sClass.Replace("'","''"),sFunction.Replace("'","''"),sMessage.Replace("'","''"),m_groupid),m_conn);
			cmdLog.ExecuteNonQuery();
		}

		public SqlConnection GetDBConnection()
		{
			SqlConnection conn = new SqlConnection(string.Format("Server={0};Database={1};UID=CincUser;PWD=0tat0pay$A;",m_server,m_database));
			conn.Open();
			return conn;
		}
	}
}

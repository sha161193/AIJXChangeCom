using System;
using System.Collections.Generic;
using System.Text;
using Rebex.Net;
using CincApplicationLog;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using CincFileDelivery;
using Chilkat;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using System.Globalization;
using ax9LibIF;
using System.Windows.Forms;
using InquiryMaster;
using System.ServiceModel;
using AIJXChangeCom.JXChangeClass;

namespace AIProcessBankFiles
{
    public struct FileStruct
    {
        public bool IsDirectory;
        public DateTime CreateTime;
        public int Size;
        public string Name;
    }

    public enum DateInterval
    {
        Second, Minute, Hour, Day, Week, Month, Quarter, Year
    }

    class ProcessBankFiles : IDisposable
    {
        public int _IFaceTypeID { get; private set; }
        public CApplicationLog _Log;
        public string _TranDate { get; set; }
        public string _AccountNumber { get; set; }
        private List<SqlConnection> _Connections = new List<SqlConnection>();
        private int _PrimaryConnection { get; set; }
        protected JXChangeCom jXChangeCom = null;
        protected JXChangeComCredentialsModel jxChangeComModel;
        public string _ImageFormat = "TIFF";
        public string _CheckSide = "Both";

        public ProcessBankFiles(int iFaceTypeID)
        {
            _IFaceTypeID = iFaceTypeID;
            _Log = new CApplicationLog("CincCustomer", "AIProcessBankFiles", GetAppSetting("server"));
            SetupConnections();            
        }

        public void ProcessFiles()
        {
             ImportFiles();
             ImportTransactions();
             ImportImages();
             ProcessTransactions();
             ProcessImages();
        }

        private void SetupConnections()
        {
            string PrimaryServer = GetAppSetting("server");
            List<string> Servers = new List<string>();
            using (SqlConnection conn = new SqlConnection(string.Format("server={0};database=fcbinterface;uid=CincUser;pwd=0tat0pay$A", PrimaryServer)))
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select distinct txtserver from {0}.cinccustomer.dbo.customerfull where dtedeleted is null and numlive = 1 or txtDatabase = 'cma'  order by txtserver", PrimaryServer);

                    using (SqlDataReader Reader = cmd.ExecuteReader())
                    {
                        while (Reader.Read())
                            Servers.Add(Reader["txtserver"].ToString());
                    }
                    for (int i = 0; i < Servers.Count; i++)
                    {
                        if (PrimaryServer.ToLower() == Servers[i].ToLower())
                            _PrimaryConnection = i;
                        SqlConnection TempConnection = new SqlConnection(string.Format("server={0};database=fcbinterface;uid=CincUser;pwd=0tat0pay$A", Servers[i]));
                        TempConnection.Open();
                        _Connections.Add(TempConnection);
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "SetupConnections", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                }
            }
        }

        protected void GetInterfaceTypeCredentials()
        {
            string PrimaryServer = GetAppSetting("server");
            List<string> Servers = new List<string>();
            SqlCommand objSqlCommand = null;
            using (SqlConnection conn = new SqlConnection(string.Format("server={0};database=cincinternal;uid=CincUser;pwd=0tat0pay$A", PrimaryServer)))
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    objSqlCommand = new SqlCommand("sp_getInterfaceTypeCredentials", cmd.Connection);
                    objSqlCommand.CommandType = CommandType.StoredProcedure;
                    objSqlCommand.Parameters.Add("@ifaceTypeID", SqlDbType.VarChar).Value = _IFaceTypeID.ToString();

                    using (SqlDataReader Reader = objSqlCommand.ExecuteReader())
                    {
                        while (Reader.Read())
                        {
                            jxChangeComModel.EndPointURL = Reader["txtURL"].ToString();
                            jxChangeComModel.AuditUserId = Reader["txtAuditUsrId"].ToString();
                            jxChangeComModel.AuditWsId = Reader["txtAuditWsId"].ToString();
                            jxChangeComModel.InstEnv = Reader["txtInstEnv"].ToString();
                            jxChangeComModel.InstRtId = Reader["txtInstRtId"].ToString();
                            jxChangeComModel.ValidConsName = Reader["txtValidConsName"].ToString();
                            jxChangeComModel.ValidConsProd = Reader["txtValidConsProd"].ToString();
                            jxChangeComModel.ClientCredentialsUserName = Reader["txtClientCredentialsUserName"].ToString();
                            jxChangeComModel.ClientCredentialsPassword = Reader["txtClientCredentialsPassword"].ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetInterfaceTypeCredentials", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                }
            }
            jXChangeCom = new JXChangeCom(jxChangeComModel);
        }

        public virtual void ImportFiles()
        {

        }

        public virtual void ImportTransactions()
        {

        }

        public virtual void ImportImages()
        {

        }

        public void ImportBAITransactions()
        {
            ImportBAITransactions("%bai%");
        }

        public void ImportBAITransactions(string filePattern)
        {
            string Record = "";
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            using (DataTable BankFiles = new DataTable())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select numfileid, txtfilename, txtfile, dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '{1}' order by numfileid", _IFaceTypeID, filePattern);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(BankFiles);
                    }
                    foreach (DataRow BankFile in BankFiles.Rows)
                    {
                        Dictionary<string, string> BankAccts = new Dictionary<string, string>();
                        int BankFileID = Convert.ToInt32(BankFile["numfileid"].ToString());
                        string BankFileName = BankFile["txtfilename"].ToString();
                        string BankFileContents = BankFile["txtfile"].ToString();
                        string LastAccount = "";
                        bool SkipAccount = false;
                        using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(BankFileContents)))
                        using (StreamReader sr = new StreamReader(ms))
                        {
                            while (!sr.EndOfStream)
                            {
                                string Line = sr.ReadLine();
                                if (Line.Trim().Length <= 3)
                                    continue;
                                //remove double quotes from beginning and end of line if they are there
                                if (Line[0] == '\"')
                                    Line = Line.Remove(0, 1);
                                if (Line[Line.Length - 1] == '\"')
                                    Line = Line.Remove(Line.Length - 1, 1);

                                if (Line.Substring(0, 2) == "03")
                                {
                                    string[] Fields = Line.Split(',');
                                    if (LastAccount != "")
                                    {
                                        if (LastAccount != Fields[1])
                                        {

                                            if (!BankAccts.ContainsKey(Fields[1]))
                                            {
                                                ;
                                                SkipAccount = false;
                                                BankAccts.Add(Fields[1], Fields[1]);
                                            }
                                            else
                                                SkipAccount = true;
                                        }
                                    }
                                    LastAccount = Fields[1];
                                }
                                if (SkipAccount)
                                    continue;

                                if (Line.Substring(0, 2) == "88")
                                {
                                    Record = Record.Replace("/", "") + Line.Substring(2, Line.Length - 2);
                                    continue;
                                }
                                else if (Record != "")
                                    ProcessBAIRecord(Record, BankFileID);
                                Record = Line;
                            }
                            ProcessBAIRecord(Record, BankFileID);
                        }
                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", BankFileID);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "ImportBAITransactions", ex.Message);
                }
            }
        }

        public virtual void ProcessBAIRecord(string record, int bankFileID)
        {
            string[] Fields = record.Split(',');
            string TranCode = "";
            string TranDescr = "";
            string CheckNumber = "";
            string TraceNumber = "";
            decimal TranAmount = 0;
            string CreditDebit = "";
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 120;
                cmd.Connection = conn;
                try
                {

                    switch (Fields[0])
                    {
                        case "02": //group header
                            _TranDate = string.Format("20{0}-{1}-{2}", Fields[4].Substring(0, 2), Fields[4].Substring(2, 2), Fields[4].Substring(4, 2));
                            break;
                        case "03": //account information
                            TranCode = "";
                            CheckNumber = "";
                            TraceNumber = "";
                            TranDescr = "";
                            _AccountNumber = Fields[1];
                            for (int iPos = 0; iPos < Fields.Length - 1; iPos++)
                            {
                                //ending balance
                                if (Fields[iPos].Trim() == "015")
                                {
                                    TranCode = "EBAL";
                                    if (IsNumeric(Fields[iPos + 1].Trim()))
                                        TranAmount = FormatCurrency(Fields[iPos + 1].Trim());
                                }
                                else if (Fields[iPos].Trim().Length >= 3)
                                {
                                    if (Fields[iPos].Trim().Substring(0, 3) == "015" && Fields[iPos].Trim().Length == 14)
                                    {
                                        string sTemp = Fields[iPos].Trim().Substring(3, 11);
                                        TranCode = "EBAL";
                                        if (IsNumeric(sTemp.Trim()))
                                            TranAmount = FormatCurrency(sTemp.Trim());
                                    }
                                }

                            }
                            if (TranCode != "")
                            {
                                cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9})", sqlStr(_AccountNumber), sqlStr(TranCode), TranAmount, sqlStr(CheckNumber), _TranDate, sqlStr(TraceNumber), sqlStr(TranDescr.Replace("/", "")), CreditDebit, _IFaceTypeID, bankFileID);
                                cmd.ExecuteNonQuery();
                            }
                            break;
                        case "16": //transaction detail
                            TranCode = Fields[1].Trim();
                            if (String.IsNullOrEmpty(TranCode))
                                break;
                            TranAmount = FormatCurrency(Fields[2].Trim());
                            if (TranAmount == 0)
                                break;
                            CheckNumber = Fields[5].Trim().TrimStart(new char[] { '0' });

                            if (Fields.Length >= 8)
                            {
                                string CKCheckNumber = Fields[7].Trim().TrimStart(new char[] { '0' });
                                if (CKCheckNumber.Length > 2)
                                {
                                    if ((!String.IsNullOrEmpty(CKCheckNumber)) && (_IFaceTypeID == 33) && (CKCheckNumber.Substring(0, 2).ToUpper().Equals("CK")))
                                        CheckNumber = CKCheckNumber.Remove(0, 2).Replace("/", "");
                                }
                            }

                            TraceNumber = Fields[4].Trim();
                            CreditDebit = "";

                            if (!IsNumeric(CheckNumber))
                                CheckNumber = "";
                            string sTempDate = "";
                            if (IsNumeric(Fields[6].Trim()) && Fields[6].Trim().Length == 6)
                                sTempDate = string.Format("20{0}-{1}-{2}", Fields[6].Substring(4, 2), Fields[6].Substring(0, 2), Fields[6].Substring(2, 2));
                            if (sTempDate != "" && IsDate(sTempDate))
                            {
                                _TranDate = sTempDate;
                                TranDescr = Fields[7].Trim();
                            }
                            else
                                TranDescr = Fields[6].Trim();

                            CreditDebit = GetTranCode(TranCode, TranDescr.Replace("/", "").Trim());

                            if (CreditDebit == "X")
                                break;

                            if (TranDescr.Trim() == "" && Fields.Length >= 8)
                                TranDescr = Fields[7].Trim();

                            if (TranCode == "354")
                            {
                                if (Fields[4].Trim() != "152")
                                {
                                    cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9}); select @@identity", sqlStr(_AccountNumber), sqlStr(TranCode), TranAmount, sqlStr(CheckNumber), _TranDate, sqlStr(TraceNumber), sqlStr("INTEREST PAYMENT"), "C", _IFaceTypeID, bankFileID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                if (_IFaceTypeID != 33)
                                {
                                    cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9}); select @@identity", sqlStr(_AccountNumber), sqlStr(TranCode), TranAmount, sqlStr(CheckNumber), _TranDate, sqlStr(TraceNumber), sqlStr(TranDescr.Replace("/", "").Trim()), CreditDebit, _IFaceTypeID, bankFileID);
                                    cmd.ExecuteNonQuery();
                                }
                                else
                                {
                                    if (TranCode != "000" || (Fields[4].Trim() != "151" && Fields[4].Trim() != "152"))
                                    {
                                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9}); select @@identity", sqlStr(_AccountNumber), sqlStr(TranCode), TranAmount, sqlStr(CheckNumber), _TranDate, sqlStr(TraceNumber), sqlStr(TranDescr.Replace("/", "")), CreditDebit, _IFaceTypeID, bankFileID);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "ProcessBAIRecord", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                }
            }
        }

        public virtual void ProcessTransactions()
        {
            int BankID = 0;
            string CheckNo = "";
            string TranCode = "";
            string MatchSQL = "";
            int TransID = 0;
            int CheckBookID = 0;
            int CBTransID = 0;
            string PrimaryServer = GetAppSetting("server");
            foreach (SqlConnection conn in GetConnections())
            {
                using (SqlCommand cmd = new SqlCommand())
                using (DataTable Customers = new DataTable())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = "update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and dtetrandate >= cast(dateadd(d, -10, getdate()) as date)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "select txtdatabase, right('0000' + isnull(txtmgmtcode,'0000'), 4) as [txtmgmtcode], txtcustomertype from cinccustomer..customer where dtedeleted is null and numlive = 1 and txtdatabase not like '%test%' and txtcustomertype in ('HOA','AIONLY') and right('0000' + isnull(txtmgmtcode,'0000'), 4) <> '0000' order by txtdatabase";
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(Customers);
                    }
                    foreach (DataRow Customer in Customers.Rows)
                    {
                        cmd.CommandText = string.Format("use {0}", Customer["txtdatabase"].ToString());
                        cmd.ExecuteNonQuery();
                        try
                        {
                            cmd.CommandText = string.Format("select isnull(max(numbankid),0) from interfaces where numifacetypeid = {0}", _IFaceTypeID);
                            if ((BankID = Convert.ToInt32(cmd.ExecuteScalar())) != 0)
                            {
                                using (DataTable Accounts = new DataTable())
                                {
                                    using (SqlDataAdapter da = new SqlDataAdapter(string.Format("select cb.numAccountNo from checkbook cb inner join association a on a.associd = cb.numassocid and a.status = 1 where numbankid = {0} order by cb.numAccountNo", BankID), conn))
                                    {
                                        da.Fill(Accounts);
                                    }
                                    foreach (DataRow Account in Accounts.Rows)
                                    {
                                        cmd.CommandText = string.Format("select numtranid, txtCheckNumber, txtCode, txtDescr, txtCreditDebit, mnyAmount, mnyInterest, dteTranDate, numIfaceTypeId, txtAccountNumber from [{0}].fcbinterface.dbo.banktransaction b " +
                                            "where dtetrandate >= '{1:d}' and txtaccountnumber = '{2}' and numifacetypeid = {3} " +
                                            "and not exists (select b2.numbtranid from banktransactions b2 where b2.numbanktranid = b.numtranid) " +
                                            "and not exists (select b3.numbtranid from banktransactions b3 where b3.numbankid = {4} and b3.dtetrandate = b.dtetrandate and b3.txttrancode not like '%ebal%' and txtaccountno = '{2}') " +
                                            "order by numtranid", sqlStr(PrimaryServer), DateTime.Now.AddDays(-10), Account["numAccountNo"].ToString().Replace("'", "''").TrimStart(new char[] { '0' }), _IFaceTypeID, BankID);
                                        using (DataTable Transactions = new DataTable())
                                        {
                                            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                                            {
                                                da.Fill(Transactions);
                                            }
                                            foreach (DataRow Tran in Transactions.Rows)
                                            {
                                                CheckNo = Tran["txtchecknumber"].ToString().Trim();
                                                if (CheckNo == "")
                                                    CheckNo = "null";
                                                if (Tran["txtCode"].ToString().Length >= 4 && Tran["txtCode"].ToString().ToUpper().Substring(0, 4) == "EBAL")
                                                    TranCode = "EBAL";
                                                else
                                                {
                                                    if (Tran["txtDescr"].ToString().ToLower().IndexOf("interest") != -1 ||
                                                          ((_IFaceTypeID == 8 || _IFaceTypeID == 37) && Tran["txtCreditDebit"].ToString().ToUpper() == "C" && Tran["txtDescr"].ToString().ToLower().IndexOf("cdc i") != -1))                                                       
                                                        TranCode = "I";
                                                    else
                                                        TranCode = Tran["txtCreditDebit"].ToString();
                                                }
                                                cmd.CommandText = string.Format("insert into banktransactions (dteImportDate,txtFileName,txtAccountNo,txtTranCode,dteTranDate,mnyAmount,numCheckNo,numBankID,numbanktranid) values (getdate(),'{0}',dbo.TrimZeros('{1}'),'{2}','{3}',{4},{5},{6},{7})", sqlStr(Tran["txtDescr"].ToString()), Tran["txtaccountnumber"], TranCode, Tran["dtetrandate"], Tran["mnyamount"], CheckNo, BankID, Tran["numtranid"]);
                                                cmd.ExecuteNonQuery();
                                                if (TranCode == "EBAL")
                                                {
                                                    //update ending balance
                                                    cmd.CommandText = string.Format("update checkbook set mnyBankBalance = {0}, dteBankDate = '{1}' where dbo.TrimZeros(numAccountNo) = dbo.TrimZeros('{2}') and (dteBankDate is null or dteBankDate < '{1}')", Tran["mnyamount"], Tran["dtetrandate"], Tran["txtaccountnumber"]);
                                                    cmd.ExecuteNonQuery();
                                                }
                                                if (Convert.ToDouble(Tran["mnyinterest"]) != 0)
                                                {
                                                    //insert and update accrued interest
                                                    TranCode = "AINT";
                                                    cmd.CommandText = string.Format("insert into banktransactions (dteImportDate,txtFileName,txtAccountNo,txtTranCode,dteTranDate,mnyAmount,numCheckNo,numBankID,numbanktranid) values (getdate(),'',dbo.TrimZeros('{0}'),'{1}','{2}',{3},{4},{5},{6})", Tran["txtaccountnumber"], TranCode, Tran["dtetrandate"], Tran["mnyinterest"], CheckNo, BankID, Tran["numtranid"]);
                                                    cmd.ExecuteNonQuery();
                                                    cmd.CommandText = string.Format("update checkbook set mnyAccruedInt = {0} where dbo.TrimZeros(numAccountNo) = dbo.TrimZeros('{1}')", Tran["mnyinterest"], Tran["txtaccountnumber"]);
                                                    cmd.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }
                                if (Customer["txtcustomertype"].ToString().ToUpper() == "HOA")
                                {
                                    // Perform Auto Matching
                                    // Transfers - Match on Acct No, Date, Amount, TranType
                                    // Check - Match on Acct No, Check No, Amount, TranType
                                    // Deposit - Match on Acct No, Amount, TranType
                                    // Interest - Match on Acct No, Amount, TranType
                                    cmd.CommandText = string.Format("select * from banktransactions where numMatchTranID = 0 and txttrancode not in ('Ebal','Aint') and numbankid = {0} and txttrancode <> '' order by dteTranDate, numBTranID", BankID);
                                    using (DataTable Transactions = new DataTable())
                                    {
                                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                                        {
                                            da.Fill(Transactions);
                                        }
                                        foreach (DataRow Tran in Transactions.Rows)
                                        {
                                            MatchSQL = string.Format("select isnull(max(numTransID),0) from checkbooktransactions t inner join checkbook c on t.numcheckbookid = c.numcheckbookid where dteReconcileDate is null and isnull(numbtranid,0) = 0 and t.dteVoidDate is null and dbo.TrimZeros(numAccountNo) = dbo.TrimZeros('{0}')", Tran["txtAccountNo"]);
                                            switch (Tran["txtTranCode"].ToString().Substring(0, 1))
                                            {
                                                case "D": // Debit / Check
                                                    if (Tran["numcheckno"].ToString() != null && Tran["numcheckno"].ToString().Trim() != "" && Tran["numcheckno"].ToString().Trim() != "0")
                                                        MatchSQL += string.Format(" and ABS(mnyAmount) = {0} and txtTransType like 'CK' and numCheckNo = {1}", Tran["mnyamount"], Tran["numCheckNo"]);
                                                    else
                                                        MatchSQL += string.Format(" and ABS(mnyAmount) = {0} and txtTransType in ('CK','TD')", Tran["mnyamount"]);
                                                    break;
                                                case "C": // Credit / Deposit
                                                    MatchSQL += string.Format(" and ABS(mnyAmount) = {0} and txtTransType in ('DP','TC', 'D')", Tran["mnyamount"]);
                                                    break;
                                                case "I": // Interest
                                                    MatchSQL += string.Format(" and ABS(mnyAmount) = {0} and txtTransType like 'I'", Tran["mnyamount"]);
                                                    break;
                                                default:
                                                    MatchSQL += " and 1=2";
                                                    break;
                                            }
                                            cmd.CommandText = MatchSQL;
                                            if ((TransID = Convert.ToInt32(cmd.ExecuteScalar())) != 0)
                                            {
                                                cmd.CommandText = string.Format("update banktransactions set numMatchTranID = {0} where numBTranID = {1}", TransID, Tran["numBTranID"]);
                                                cmd.ExecuteNonQuery();
                                                cmd.CommandText = string.Format("update checkbooktransactions set numBTranID = {0}, dtereconciledate = getdate() where numTransID = {1}", Tran["numBTranID"], TransID);
                                                cmd.ExecuteNonQuery();
                                            }
                                            else
                                            {
                                                if (Tran["txtTranCode"].ToString().ToUpper().Substring(0, 1) == "I")
                                                {
                                                    // Post Interest Automatically
                                                    cmd.CommandText = string.Format("select isnull(max(c.numCheckBookID),0) from checkbook c where dbo.TrimZeros(numAccountNo) = dbo.TrimZeros('{0}')", Tran["txtAccountNo"]);
                                                    if ((CheckBookID = Convert.ToInt32(cmd.ExecuteScalar())) != 0)
                                                    {
                                                        cmd.CommandText = string.Format("exec sp_AddCheckBookInterest '{0}',{1},{2}", Tran["dteTranDate"], CheckBookID, Tran["mnyAmount"]);
                                                        CBTransID = Convert.ToInt32(cmd.ExecuteScalar());
                                                        cmd.CommandText = string.Format("update banktransactions set numMatchTranID = {0} where numBTranID = {1}", CBTransID, Tran["numBTranID"]);
                                                        cmd.ExecuteNonQuery();
                                                        cmd.CommandText = string.Format("update checkbooktransactions set numBTranID = {0}, dtereconciledate = getdate() where numTransID = {1}", Tran["numBTranID"], CBTransID);
                                                        cmd.ExecuteNonQuery();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("ProcessBankFiles", "ProcessTransactions", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                        }
                    }
                }
            }
        }

        public virtual void ProcessImages()
        {
            int BankID = 0;
            string LastAccountNumber = "";
            bool SkipAccount = false;
            int CheckbookID = 0;
            int ImageLinkID = 0;
            int CBImageLinkID = 0;
            int TransID = 0;
            string PrimaryServer = GetAppSetting("server");
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                //skip importing images if there aren't any available for this interface
                cmd.CommandText = string.Format("select count(*) from [{0}].fcbinterface.dbo.bankimage where dteprocessed is null and numifacetypeid = {1}", PrimaryServer, _IFaceTypeID);
                if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                    return;
            }

            foreach (SqlConnection tempConn in GetConnections())
            {
                using (SqlCommand cmd = new SqlCommand())
                using (DataTable Customers = new DataTable())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = tempConn;
                    cmd.CommandText = "select txtdatabase, right('0000' + isnull(txtmgmtcode,'0000'), 4) as [txtmgmtcode], txtcustomertype from cinccustomer..customer where dtedeleted is null and numlive = 1 and txtdatabase not like '%test%' and txtcustomertype in ('HOA','AIONLY') and right('0000' + isnull(txtmgmtcode,'0000'), 4) <> '0000' order by txtdatabase";
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(Customers);
                    }
                    foreach (DataRow Customer in Customers.Rows)
                    {
                        cmd.CommandText = string.Format("use [{0}]", Customer["txtdatabase"].ToString());
                        cmd.ExecuteNonQuery();
                        try
                        {
                            cmd.CommandText = string.Format("select isnull(max(numbankid),0) from interfaces where numifacetypeid = {0}", _IFaceTypeID);
                            if ((BankID = Convert.ToInt32(cmd.ExecuteScalar())) != 0)
                            {
                                cmd.CommandText = string.Format("select numbankimageid,txtaccount,txtrouting,txtchecknumber,txtimagenumber,mnyamount " +
                                    "from [{0}].fcbinterface.dbo.bankimage where dteprocessed is null and numbackflag = 0 and numifacetypeid = {1} " +
                                    "order by txtaccount,txtimagenumber", PrimaryServer, _IFaceTypeID);
                                using (DataTable imageTable = new DataTable())
                                {
                                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                                    {
                                        da.Fill(imageTable);
                                    }
                                    LastAccountNumber = "";
                                    SkipAccount = false;
                                    foreach (DataRow checkImage in imageTable.Rows)
                                    {
                                        if (LastAccountNumber == checkImage["txtaccount"].ToString() && SkipAccount)
                                            continue;
                                        SkipAccount = false;
                                        if (LastAccountNumber != checkImage["txtaccount"].ToString())
                                        {
                                            LastAccountNumber = checkImage["txtaccount"].ToString();
                                            cmd.CommandText = string.Format("select isnull(max(numcheckbookid),0) from checkbook where numbankid = {0} and dbo.trimzeros(numaccountno) = dbo.trimzeros('{1}')", BankID, sqlStr(LastAccountNumber));
                                            CheckbookID = Convert.ToInt32(cmd.ExecuteScalar());
                                            if (CheckbookID == 0)
                                            {
                                                SkipAccount = true;
                                                continue;
                                            }
                                        }
                                        //insert image link
                                        cmd.CommandText = string.Format("insert into imagelink (numimagetypeid,numkeyid,txtfilename,dtefiledate,numfilesize,txtdatabase) " +
                                            "values (4,0,'',getdate(),0,'{0}{1}'); select @@identity", Customer["txtdatabase"], DateTime.Now.Year);
                                        ImageLinkID = Convert.ToInt32(cmd.ExecuteScalar());
                                        //insert images
                                        cmd.CommandText = string.Format("insert into [{0}{1}]..miscbankimage (numimagelinkid,numbackflag,numsize,imgimage) " +
                                            "select {2},numbackflag,datalength(imgimage),imgimage " +
                                            "from [{3}].fcbinterface.dbo.bankimage " +
                                            "where txtimagenumber = '{4}' and numifacetypeid = {5}", Customer["txtdatabase"], DateTime.Now.Year, ImageLinkID, PrimaryServer, checkImage["txtimagenumber"], _IFaceTypeID);
                                        cmd.ExecuteNonQuery();
                                        //insert checkbook image link
                                        cmd.CommandText = string.Format("insert into checkbooktransactionimagelink (numtransid,numimagelinkid,mnyamount,txtaccount,txtchecknumber,txtrouting,txtimagenumber) " +
                                            "values ({0},{1},{2},'{3}','{4}','{5}','{6}'); select @@identity", 0, ImageLinkID, checkImage["mnyamount"],
                                            sqlStr(checkImage["txtaccount"].ToString()), sqlStr(checkImage["txtchecknumber"].ToString()), sqlStr(checkImage["txtrouting"].ToString()), checkImage["txtimagenumber"]);
                                        CBImageLinkID = Convert.ToInt32(cmd.ExecuteScalar());

                                        if (Customer["txtcustomertype"].ToString().ToUpper() == "HOA")
                                        {
                                            //try to match image
                                            cmd.CommandText = string.Format("select isnull(max(numtransid),0) from checkbooktransactions t " +
                                                "where txttranstype = 'CK' and numcheckbookid = {0} and abs(mnyamount) = {1} " +
                                                "and dbo.trimzeros(numcheckno) = dbo.trimzeros('{2}')",
                                                CheckbookID, checkImage["mnyamount"], sqlStr(checkImage["txtchecknumber"].ToString()));
                                            if ((TransID = Convert.ToInt32(cmd.ExecuteScalar())) != 0)
                                            {
                                                cmd.CommandText = string.Format("update checkbooktransactionimagelink set numtransid = {0} where numcbimagelinkid = {1}",
                                                    TransID, CBImageLinkID);
                                                cmd.ExecuteNonQuery();
                                                cmd.CommandText = string.Format("update imagelink set numkeyid = {0} where numimagelinkid = {1}",
                                                    CBImageLinkID, ImageLinkID);
                                                cmd.ExecuteNonQuery();
                                            }
                                        }
                                        //mark records as processed
                                        cmd.CommandText = string.Format("update [{0}].fcbinterface.dbo.bankimage set dteprocessed = getdate() " +
                                            "where txtimagenumber = '{1}' and numifacetypeid = {2}", PrimaryServer, sqlStr(checkImage["txtimagenumber"].ToString()), _IFaceTypeID);
                                        cmd.ExecuteScalar();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("ProcessImages", "ProcessImages", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                        }
                    }
                }
            }
        }

        public int AddBinaryFileToDatabase(string fileName, bool allowDuplicates = true)
        {
            int FileID = 0;
            int readSize = 250000;
            byte[] buffer = new byte[readSize];
            try
            {
                if (!allowDuplicates && DuplicateFileExists(fileName))
                    return 0;

                FileInfo BankFileInfo = new FileInfo(fileName);
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where txtfilename = '{0}' and dtefiledate = '{1}' and numifacetypeid = {2}", sqlStr(BankFileInfo.Name), BankFileInfo.CreationTime, _IFaceTypeID);
                    if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                    {
                        BinaryReader br = new BinaryReader(File.Open(BankFileInfo.FullName, FileMode.Open));
                        for (int bytesRead = 0; (bytesRead = br.Read(buffer, 0, readSize)) != 0;)
                        {
                            if (bytesRead < readSize)
                                Array.Resize<byte>(ref buffer, bytesRead);
                            if (FileID == 0)
                            {
                                cmd.CommandText = string.Format("insert into fcbinterface..bankfile (txtfilename,dtefiledate,dteprocessdate,numifacetypeid,imgfile) " +
                                    "values ('{0}','{1}',getdate(),{2},@Data); select @@identity", sqlStr(BankFileInfo.Name), BankFileInfo.CreationTime, _IFaceTypeID);
                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, buffer.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, buffer);
                                cmd.Parameters.Add(param);
                                FileID = Convert.ToInt32(cmd.ExecuteScalar());
                                cmd.Parameters.Clear();
                                param = null;
                            }
                            else
                            {
                                if (bytesRead < readSize)
                                    Array.Resize<byte>(ref buffer, bytesRead);
                                cmd.CommandText = string.Format("DECLARE @ptrval binary(16),@length int " +
                                    "SELECT @ptrval = TEXTPTR(imgfile), @length = datalength(imgfile) " +
                                    "FROM fcbinterface..bankfile " +
                                    "WHERE numfileid = {0} " +
                                    "UPDATETEXT fcbinterface..bankfile.imgfile @ptrval @length 0 @Data", FileID);
                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, buffer.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, buffer);
                                cmd.Parameters.Add(param);
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                                param = null;
                            }
                            if (bytesRead < readSize)
                                break;
                        }
                        br.Close();
                    }
                }
                BankFileInfo.Delete();
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "AddBinaryFileToDatabase", string.Format("ERR:{0}", ex.Message));
            }
            return FileID;
        }

        public int AddTextFileToDatabase(string fileName)
        {
            int FileID = 0;
            int readSize = 250000;
            char[] buffer = new char[readSize];
            try
            {
                FileInfo BankFileInfo = new FileInfo(fileName);
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    string FileName = BankFileInfo.Name;

                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where txtfilename = '{0}' and dtefiledate = '{1}' and numifacetypeid = {2}", sqlStr(BankFileInfo.Name), BankFileInfo.CreationTime, _IFaceTypeID);
                    if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                    {
                        using (StreamReader sr = new StreamReader(File.Open(BankFileInfo.FullName, FileMode.Open)))
                        {
                            for (int bytesRead = 0; (bytesRead = sr.Read(buffer, 0, buffer.Length)) != 0;)
                            {
                                if (bytesRead < buffer.Length)
                                    Array.Resize(ref buffer, bytesRead);
                                if (0 == FileID)
                                {
                                    cmd.CommandText = string.Format("insert into fcbinterface..bankfile (txtfilename,dtefiledate,dteprocessdate,txtfile,numifacetypeid) " +
                                        "values ('{0}','{1}',getdate(),'{2}',{3}); select @@identity", sqlStr(BankFileInfo.Name), BankFileInfo.CreationTime, sqlStr(new string(buffer)), _IFaceTypeID);
                                    FileID = Convert.ToInt32(cmd.ExecuteScalar());
                                }
                                else
                                {
                                    cmd.CommandText = string.Format("DECLARE @ptrval binary(16),@length int " +
                                        "SELECT @ptrval = TEXTPTR(txtfile), @length = datalength(txtfile) " +
                                        "FROM fcbinterface..bankfile " +
                                        "WHERE numfileid = {0} " +
                                        "UPDATETEXT fcbinterface..bankfile.txtfile @ptrval @length 0 '{1}'", FileID, sqlStr(new string(buffer)));
                                    cmd.ExecuteNonQuery();
                                }
                                if (bytesRead < buffer.Length)
                                    break;
                            }
                        }
                    }
                }
                BankFileInfo.Delete();
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "AddTextFileToDatabase", string.Format("ERR:{0}", ex.Message));
            }
            return FileID;
        }

        /// <summary>
        /// Added by Invezza Team 
        /// To add dummy entry into BankFile table.
        /// </summary>
        /// <param name="fileName">string fileName</param>
        public int AddStubFileRecordToDatabase(string fileName)
        {
            int fileId = 0;
            try
            {
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("insert into fcbinterface..bankfile (txtfilename,dtefiledate,dteprocessdate,numifacetypeid) " +
                        "values ('{0}',getdate(),getdate(), {1}); select @@identity", fileName, _IFaceTypeID);
                    fileId = Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "AddStubFileRecordToDatabase", string.Format("ERR:{0}", ex.Message));
            }

            return fileId;
        }

        public bool DuplicateFileExists(string fileName)
        {
            int readSize = 250000;
            char[] buffer = new char[readSize];

            try
            {
                FileInfo BankFileInfo = new FileInfo(fileName);
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    string FileName = BankFileInfo.Name;

                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where txtfilename = '{0}' and numifacetypeid = {1} and datalength(txtfilename) = {2}", sqlStr(BankFileInfo.Name), _IFaceTypeID, BankFileInfo.Length);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    {
                        BankFileInfo.Delete();
                        return true;
                    }
                }

            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "AddTextFileToDatabase", string.Format("ERR:{0}", ex.Message));
            }
            return false;
        }

        public virtual void ImportJHXMLImages()
        {
            string FileName = "";
            int FileID = 0;
            string FileText = "";
            string AccountNumber = "";
            decimal CheckAmount = 0;
            string CheckNumber = "";
            string RoutingNumber = "";
            string ImageNumber = "";
            DateTime ProcessingDate = DateTime.Now;
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            using (DataTable BankFiles = new DataTable())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    int RemainingImagesFiles = 0;
                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '%xml%'", _IFaceTypeID);
                    RemainingImagesFiles = Convert.ToInt32(cmd.ExecuteScalar().ToString());
                    for (int i = 0; i < 50 && RemainingImagesFiles > 0; i++)
                    {
                        cmd.CommandText = string.Format("select top 1 numfileid, txtfilename, txtfile, dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '%xml%' order by numfileid", _IFaceTypeID);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(BankFiles);
                        }
                        foreach (DataRow bankFile in BankFiles.Rows)
                        {
                            FileID = Convert.ToInt32(bankFile["numfileid"].ToString());
                            FileName = bankFile["txtfilename"].ToString();
                            FileText = bankFile["txtfile"].ToString();

                            XmlDataDocument xmlDoc = new XmlDataDocument();
                            using (MemoryStream ms = new MemoryStream(ASCIIEncoding.ASCII.GetBytes(FileText)))
                            using (DataSet dsXml = new DataSet())
                            {
                                dsXml.ReadXml(ms);
                                if (dsXml.Tables.IndexOf("FrontImage") == -1)
                                {
                                    cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                                    cmd.ExecuteNonQuery();
                                    continue;
                                }
                                if (dsXml.Tables.IndexOf("JHA4Sight") == -1)
                                {
                                    cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                                    cmd.ExecuteNonQuery();
                                    continue;
                                }
                                if (dsXml.Tables.IndexOf("Item") == -1)
                                {
                                    cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                                    cmd.ExecuteNonQuery();
                                    continue;
                                }
                                if (dsXml.Tables.IndexOf("BackImage") == -1)
                                {
                                    cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                                    cmd.ExecuteNonQuery();
                                    continue;
                                }
                                foreach (DataRow image in dsXml.Tables["FrontImage"].Rows)
                                {
                                    DataRow[] aJHA = dsXml.Tables["JHA4Sight"].Select("FrontImage_ID = " + image["FrontImage_ID"].ToString());
                                    if (aJHA.Length == 1)
                                    {
                                        DataRow jha = aJHA[0];
                                        DataRow[] aItem = dsXml.Tables["Item"].Select("JHA4Sight_ID = " + jha["JHA4Sight_ID"].ToString());
                                        if (aItem.Length == 1)
                                        {
                                            DataRow item = aItem[0];
                                            //process details about check
                                            ImageNumber = "";
                                            AccountNumber = "";
                                            CheckAmount = 0;
                                            CheckNumber = "";
                                            RoutingNumber = "";
                                            ProcessingDate = DateTime.Now;
                                            for (int k = 0; k < dsXml.Tables["Item"].Columns.Count; k++)
                                            {
                                                DataColumn col = dsXml.Tables["Item"].Columns[k];
                                                switch (col.ColumnName.ToLower())
                                                {
                                                    case "hostimagenumber":
                                                        if (!item.IsNull(k))
                                                            ImageNumber = item[k].ToString();
                                                        break;
                                                    case "account":
                                                        if (!item.IsNull(k))
                                                            AccountNumber = item[k].ToString();
                                                        break;
                                                    case "amount":
                                                        if (!item.IsNull(k))
                                                            CheckAmount = Convert.ToDecimal(item[k]) / 100;
                                                        break;
                                                    case "serial":
                                                        if (!item.IsNull(k))
                                                            CheckNumber = item[k].ToString();
                                                        break;
                                                    case "tranrouting":
                                                        if (!item.IsNull(k))
                                                            RoutingNumber = item[k].ToString();
                                                        break;
                                                    case "processingdate":
                                                        if (!item.IsNull(k))
                                                        {
                                                            if (!string.IsNullOrEmpty(item[k].ToString()) && item[k].ToString().Length == 8)
                                                                ProcessingDate = Convert.ToDateTime(item[k].ToString().Substring(4, 2) + "/" + item[k].ToString().Substring(6, 2) + "/" + item[k].ToString().Substring(0, 4));
                                                        }
                                                        break;
                                                }
                                            }
                                            cmd.CommandText = string.Format("select count(*) from fcbinterface..bankimage where txtimagenumber = '{0}' and numbackflag = 0", sqlStr(ImageNumber));
                                            if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                                            {
                                                //insert front of check
                                                byte[] FrontImage = Convert.FromBase64String(jha["ImageData"].ToString());
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','{5}',0,{6},'{7:d}',{8},@Data)", FileID, CheckAmount, sqlStr(AccountNumber), sqlStr(RoutingNumber), sqlStr(CheckNumber), sqlStr(ImageNumber), FrontImage.Length, ProcessingDate, _IFaceTypeID);
                                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, FrontImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, FrontImage);
                                                cmd.Parameters.Add(param);
                                                cmd.ExecuteNonQuery();
                                                cmd.Parameters.Clear();
                                                param = null;
                                            }
                                        }
                                    }
                                }

                                foreach (DataRow image in dsXml.Tables["BackImage"].Rows)
                                {
                                    DataRow[] aJHA = dsXml.Tables["JHA4Sight"].Select("BackImage_ID = " + image["BackImage_ID"].ToString());
                                    if (aJHA.Length == 1)
                                    {
                                        DataRow jha = aJHA[0];
                                        DataRow[] aItem = dsXml.Tables["Item"].Select("JHA4Sight_ID = " + jha["JHA4Sight_ID"].ToString());
                                        if (aItem.Length == 1)
                                        {
                                            DataRow item = aItem[0];
                                            //process details about check
                                            ImageNumber = "";
                                            AccountNumber = "";
                                            CheckAmount = 0;
                                            CheckNumber = "";
                                            RoutingNumber = "";
                                            for (int k = 0; k < dsXml.Tables["Item"].Columns.Count; k++)
                                            {
                                                DataColumn col = dsXml.Tables["Item"].Columns[k];
                                                switch (col.ColumnName.ToLower())
                                                {
                                                    case "hostimagenumber":
                                                        if (!item.IsNull(k))
                                                            ImageNumber = item[k].ToString();
                                                        break;
                                                    case "account":
                                                        if (!item.IsNull(k))
                                                            AccountNumber = item[k].ToString();
                                                        break;
                                                    case "amount":
                                                        if (!item.IsNull(k))
                                                            CheckAmount = Convert.ToDecimal(item[k]) / 100;
                                                        break;
                                                    case "serial":
                                                        if (!item.IsNull(k))
                                                            CheckNumber = item[k].ToString();
                                                        break;
                                                    case "tranrouting":
                                                        if (!item.IsNull(k))
                                                            RoutingNumber = item[k].ToString();
                                                        break;
                                                }
                                            }
                                            cmd.CommandText = string.Format("select count(*) from fcbinterface..bankimage where txtimagenumber = '{0}' and numbackflag = 1", sqlStr(ImageNumber));
                                            if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                                            {
                                                //insert back of check
                                                byte[] BackImage = Convert.FromBase64String(jha["ImageData"].ToString());
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','{5}',1,{6},'{7:d}',{8},@Data)", FileID, CheckAmount, sqlStr(AccountNumber), sqlStr(RoutingNumber), sqlStr(CheckNumber), sqlStr(ImageNumber), BackImage.Length, ProcessingDate, _IFaceTypeID);
                                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, BackImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, BackImage);
                                                cmd.Parameters.Add(param);
                                                cmd.ExecuteNonQuery();
                                                cmd.Parameters.Clear();
                                                param = null;
                                            }
                                        }
                                    }
                                }
                            }
                            cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                            cmd.ExecuteNonQuery();
                        }
                        BankFiles.Clear();
                        cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '%xml%'", _IFaceTypeID);
                        RemainingImagesFiles = Convert.ToInt32(cmd.ExecuteScalar().ToString());
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "ImportJHXMLImages", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                    if (FileID != 0)
                    {
                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public virtual void ImportXMLImages()
        {
            string FileName = "";
            int FileID = 0;
            string FileText = "";
            string AccountNumber = "";
            decimal CheckAmount = 0;
            string CheckNumber = "";
            string RoutingNumber = "";
            string ImageNumber = "";
            Root rootXmlImage;
            DateTime ProcessingDate = DateTime.Now;
            byte[] imgFileData = null;

            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            using (DataTable BankFiles = new DataTable())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    int RemainingImagesFiles = 0;
                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like 'CK_%.zip'", _IFaceTypeID);
                    RemainingImagesFiles = Convert.ToInt32(cmd.ExecuteScalar().ToString());
                    for (int i = 0; i < 50 && RemainingImagesFiles > 0; i++)
                    {
                        cmd.CommandText = string.Format("select top 1 numfileid, txtfilename, txtfile, dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like 'CK_%.zip' order by numfileid", _IFaceTypeID);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(BankFiles);
                        }
                        foreach (DataRow bankFile in BankFiles.Rows)
                        {
                            FileID = Convert.ToInt32(bankFile["numfileid"].ToString());
                            FileName = bankFile["txtfilename"].ToString();
                            FileText = bankFile["txtfile"].ToString();

                            cmd.CommandText = string.Format("select imgFile from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like 'CK_%.zip' order by numfileid", _IFaceTypeID);
                            imgFileData = (byte[])cmd.ExecuteScalar();
                            cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                            cmd.ExecuteNonQuery();

                            string extractPath = CreateFolder("extract");
                            using (FileStream fs = new FileStream(extractPath + FileName, FileMode.OpenOrCreate, System.IO.FileAccess.Write))
                            {
                                using (BinaryWriter bw = new BinaryWriter(fs))
                                {
                                    bw.Write(imgFileData);
                                    bw.Flush();
                                }
                            }

                            //extract and log file in database              
                            RemoveAttribute(extractPath + FileName, FileAttributes.ReadOnly);
                            Zip zip = new Zip();
                            zip.UnlockComponent("1FCBUSAZIP_kGv5CoMw7l6V");
                            zip.OpenZip(extractPath + FileName);
                            zip.ExtractInto(extractPath);
                            zip.CloseZip();
                            zip = null;
                            foreach (string xmlCheckFile in Directory.GetFiles(extractPath, "*.xml"))
                            {
                                RemoveAttribute(xmlCheckFile, FileAttributes.ReadOnly);
                                XmlSerializer serializer = new XmlSerializer(typeof(Root));
                                using (TextReader reader = new StreamReader(xmlCheckFile))
                                {

                                    try
                                    {
                                        rootXmlImage = (Root)serializer.Deserialize(reader);
                                    }
                                    catch (Exception ex)
                                    {
                                        // catch bad xml file
                                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                                        cmd.ExecuteNonQuery();
                                        _Log.LogErr("ProcessBankFiles", "ImportXMLImages", ex.Message);
                                        continue;
                                    }

                                    foreach (var image in rootXmlImage.ImageRecord)
                                    {
                                        if (image.FrontImage.Item != null)
                                        {
                                            ImageNumber = "";
                                            AccountNumber = "";
                                            CheckAmount = 0;
                                            CheckNumber = "";
                                            RoutingNumber = "";
                                            ProcessingDate = DateTime.Now;
                                            ImageNumber = image.FrontImage.Item.HostImageNumber.ToString();
                                            AccountNumber = image.FrontImage.Item.Account.ToString();
                                            CheckAmount = Convert.ToDecimal(image.FrontImage.Item.Amount.ToString());
                                            CheckNumber = image.FrontImage.Item.Serial.ToString();
                                            RoutingNumber = image.FrontImage.Item.TranRouting.ToString();
                                            ProcessingDate = Convert.ToDateTime(string.Format("{0}/{1}/{2}", image.FrontImage.Item.ProcessingDate.ToString().Substring(6, 2), image.FrontImage.Item.ProcessingDate.ToString().Substring(4, 2), image.FrontImage.Item.ProcessingDate.ToString().Substring(0, 4)));

                                            cmd.CommandText = string.Format("select count(*) from fcbinterface..bankimage where txtimagenumber = '{0}' and numbackflag = 0", sqlStr(ImageNumber));
                                            if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                                            {
                                                //insert front of check
                                                byte[] FrontImage = Convert.FromBase64String(image.FrontImage.ImageData.ToString());
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','{5}',0,{6},'{7:d}',{8},@Data)", FileID, CheckAmount, sqlStr(AccountNumber), sqlStr(RoutingNumber), sqlStr(CheckNumber), sqlStr(ImageNumber), FrontImage.Length, ProcessingDate, _IFaceTypeID);
                                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, FrontImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, FrontImage);
                                                cmd.Parameters.Add(param);
                                                cmd.ExecuteNonQuery();
                                                cmd.Parameters.Clear();
                                                param = null;

                                                //insert back of check
                                                byte[] BackImage = Convert.FromBase64String(image.BackImage.ImageData.ToString());
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','{5}',1,{6},'{7:d}',{8},@Data)", FileID, CheckAmount, sqlStr(AccountNumber), sqlStr(RoutingNumber), sqlStr(CheckNumber), sqlStr(ImageNumber), BackImage.Length, ProcessingDate, _IFaceTypeID);
                                                SqlParameter param2 = new SqlParameter("@Data", SqlDbType.VarBinary, BackImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, BackImage);
                                                cmd.Parameters.Add(param2);
                                                cmd.ExecuteNonQuery();
                                                cmd.Parameters.Clear();
                                                param = null;
                                            }
                                        }

                                    }
                                }
                                File.Delete(xmlCheckFile);
                            }
                            File.Delete(extractPath + FileName);
                            cmd.CommandText = string.Format("select count(*) from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like 'CK_%.zip'", _IFaceTypeID);
                            RemainingImagesFiles = Convert.ToInt32(cmd.ExecuteScalar().ToString());

                        }
                        BankFiles.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "ImportXMLImages", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                    if (FileID != 0)
                    {
                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void Parsex937NSFBankImages(string fileExtension)
        {
            string FileName = "";
            int BankFileID = 0;
            string FileText = "";
            string TempPath = CreateTempFolder();
            DateTime ProcessingDate = DateTime.Now;
            string checknumber = string.Empty;
            string sAcount = string.Empty;
            string checkamount = string.Empty;
            string chkfrontimage = string.Empty;
            string chkbackimage = string.Empty;
            string chkdate = string.Empty;
            string BankRoutingNumber = string.Empty;
            byte[] imgFileData = null;
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                cmd.CommandText = string.Format("select isnull(max(txtbankrouting),'') from cincinternal..interfacetype where numifacetypeid = {0}", _IFaceTypeID);
                BankRoutingNumber = cmd.ExecuteScalar().ToString();
            }
            using (SqlCommand cmd = new SqlCommand())
            using (DataTable BankFiles = new DataTable())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    cmd.CommandText = string.Format("select numfileid, txtfilename, txtfile,  dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '{1}' order by numfileid", _IFaceTypeID, fileExtension);
                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                    {
                        da.Fill(BankFiles);
                    }
                    ax9LibClass ax = new ax9LibClass();
                    ax.WorkingDirectory = TempPath;

                    foreach (DataRow bankFile in BankFiles.Rows)
                    {
                        BankFileID = Convert.ToInt32(bankFile["numfileid"].ToString());
                        FileName = bankFile["txtfilename"].ToString();
                        FileText = bankFile["txtfile"].ToString();
                        cmd.CommandText = string.Format("select imgFile from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '{1}' order by numfileid", _IFaceTypeID, fileExtension);
                        imgFileData = (byte[])cmd.ExecuteScalar();
                        using (FileStream fs = new FileStream(TempPath + FileName, FileMode.OpenOrCreate, System.IO.FileAccess.Write))
                        {
                            using (BinaryWriter bw = new BinaryWriter(fs))
                            {
                                bw.Write(imgFileData);
                                bw.Flush();
                            }
                        }

                        for (int i = 0; i < 10; i++)
                        {
                            int nReturn = ax.ConvertToNsfSit(TempPath + FileName);

                            if (nReturn == 96)
                                continue;
                            if (nReturn == 0)
                                break;
                            if (nReturn != 0 && i == 9)
                            {
                                _Log.LogErr("ProcessBankFiles", "Parsex937NSFBankImages", string.Format("i:{0}::Return Error:{1}", i, nReturn));
                            }
                        }
                        foreach (string nsfFile in Directory.GetFiles(TempPath, "*.nsf"))
                        {
                            checknumber = string.Empty;
                            sAcount = string.Empty;
                            checkamount = string.Empty;
                            chkfrontimage = string.Empty;
                            chkbackimage = string.Empty;
                            chkdate = string.Empty;

                            try
                            {
                                using (StreamReader sr = new StreamReader(nsfFile))
                                {
                                    for (int i = 0; !sr.EndOfStream; i++)
                                    {
                                        string sLine = sr.ReadLine();
                                        if (i < 1)
                                            continue;
                                        sLine = sLine.Replace("'", "");
                                        if (!string.IsNullOrEmpty(sLine))
                                        {
                                            string[] aFields = ParseFields(sLine);
                                            string[] chknumberLine = aFields[8].Split('C');
                                            sAcount = chknumberLine[0].Replace(" ", "");
                                            checknumber = chknumberLine[1].ToString().Replace(" ", "");

                                            if (string.IsNullOrEmpty(checknumber))
                                                checknumber = aFields[5].Replace(" ", "").ToString();

                                            checkamount = (Convert.ToDecimal(aFields[9]) / 100).ToString();
                                            chkdate = aFields[11]; // checkdate
                                            chkfrontimage = aFields[3];

                                            int ImageNumber = 0;
                                            if (File.Exists(chkfrontimage))
                                            {
                                                byte[] frontImage = File.ReadAllBytes(chkfrontimage);
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','',0,{5},'{6:d}',{7},@Data); select @@identity", BankFileID, checkamount, sqlStr(sAcount), sqlStr(BankRoutingNumber), sqlStr(checknumber), frontImage.Length, DateTime.ParseExact(chkdate, "yyyyMMdd", CultureInfo.InvariantCulture).ToString(), _IFaceTypeID);
                                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, frontImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, frontImage);
                                                cmd.Parameters.Add(param);
                                                ImageNumber = Convert.ToInt32(cmd.ExecuteScalar());
                                                cmd.Parameters.Clear();
                                                param = null;
                                                File.Delete(chkfrontimage);
                                                cmd.CommandText = string.Format("update fcbinterface..bankimage set txtimagenumber = '{0}' where numBankImageID = {0}", ImageNumber);
                                                cmd.ExecuteNonQuery();
                                            }

                                            chkbackimage = aFields[4];
                                            if (File.Exists(chkbackimage))
                                            {
                                                byte[] backImage = File.ReadAllBytes(chkbackimage);
                                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                    "values ({0},{1},'{2}','{3}','{4}','{5}',1,{6},'{7:d}',{8},@Data)", BankFileID, checkamount, sqlStr(sAcount), sqlStr(BankRoutingNumber), sqlStr(checknumber), ImageNumber, backImage.Length, DateTime.ParseExact(chkdate, "yyyyMMdd", CultureInfo.InvariantCulture), _IFaceTypeID);
                                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, backImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, backImage);
                                                cmd.Parameters.Add(param);
                                                cmd.ExecuteNonQuery();
                                                cmd.Parameters.Clear();
                                                param = null;
                                                File.Delete(chkbackimage);
                                            }
                                        }
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                File.Delete(nsfFile);
                                _Log.LogErr("BankFiles", "Parsex937BankImage", ex.Message);
                            }
                            File.Delete(nsfFile);
                        }
                        File.Delete(FileName);

                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "Parsex937NSFBankImages", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                    if (BankFileID != 0)
                    {
                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            RemoveTempFolder();
        }

        public SqlConnection[] GetConnections()
        {
            try
            {
                return _Connections.ToArray();
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "GetConnections", string.Format("ERR:{0}", ex.Message));
            }
            return null;
        }

        public SqlConnection GetPrimaryConnection()
        {
            try
            {
                return _Connections[_PrimaryConnection];
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "GetPrimaryConnection", string.Format("ERR:{0}", ex.Message));
            }
            return null;
        }

        public string GetAppSetting(string appSetting)
        {
            return System.Configuration.ConfigurationManager.AppSettings[string.Format("{0}{1}", appSetting, _IFaceTypeID)];
        }

        public string sqlStr(string sBuf)
        {
            if (sBuf == null)
                sBuf = "";
            return sBuf.Replace("'", "''");
        }

        public virtual void Dispose()
        {
            foreach (SqlConnection conn in _Connections)
                conn.Dispose();
        }

        public string CreateTempFolder()
        {
            string TempPath = string.Format("{0}\\temp{1}\\", Directory.GetCurrentDirectory(), _IFaceTypeID);
            if (!Directory.Exists(TempPath))
                Directory.CreateDirectory(TempPath);
            return TempPath;
        }

        public void RemoveTempFolder()
        {
            string TempPath = string.Format("{0}\\temp{1}\\", Directory.GetCurrentDirectory(), _IFaceTypeID);
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);
        }

        public bool IsNumeric(string sBuf)
        {
            double dTemp;
            try
            {
                dTemp = Convert.ToDouble(sBuf);
            }
            catch
            {
                return false;
            }
            return true;
        }

        public bool IsDate(string sBuf)
        {
            try
            {
                DateTime dt = DateTime.Parse(sBuf);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private decimal FormatCurrency(string input)
        {
            input = input.Replace("+", "");
            if (input.IndexOf('-') == -1)
                input = input.Insert(0, "0000000000");
            else
                input = input.Insert(1, "0000000000");

            if (IsNumeric(input))
                return Convert.ToDecimal(input.Insert(input.Length - 2, "."));
            else
                return 0;
        }

        public string[] ParseFields(string sBuf)
        {
            try
            {
                string sReturn = "";
                bool bInStr = false;
                for (int i = 0; i < sBuf.Length; i++)
                {
                    if (!bInStr)
                    {
                        if (sBuf[i] == ',')
                            sReturn += '\v';
                        else if (sBuf[i] == '\"')
                            bInStr = true;
                        else
                            sReturn += sBuf[i];
                    }
                    else
                    {
                        if (sBuf[i] == '\"')
                            bInStr = false;
                        else
                            sReturn += sBuf[i];
                    }
                }
                return sReturn.Split('\v');
            }
            catch { }
            return null;
        }

        public string GetTranCode(string bankTranCode, string bankTranDescr)
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("exec fcbinterface..sp_GetBankTranCode {0},{1},'{2}'", _IFaceTypeID, bankTranCode, sqlStr(bankTranDescr));
                    return cmd.ExecuteScalar().ToString();
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetTranCode", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
            return "";
        }

        public FileStruct[] FTP_GetDirectoryListing(string remoteFolder)
        {
            string s = "";
            try
            {
                List<FileStruct> StructArray = new List<FileStruct>();
                string UserName = GetAppSetting("ftp_username");
                string Password = GetAppSetting("ftp_password");
                string ftpURI = GetAppSetting("ftp_uri");
                ftpURI += remoteFolder;

                FtpWebRequest ftp = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpURI));
                ftp.Credentials = new NetworkCredential(UserName, Password);
                ftp.KeepAlive = false;
                ftp.UseBinary = true;
                ftp.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                FtpWebResponse response = (FtpWebResponse)ftp.GetResponse();
                using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                {
                    s = sr.ReadToEnd();
                }
                response.Close();

                s = s.Replace('\r', '\n');
                s = s.Replace("\n\n", "\n");
                string[] aLines = s.Split('\n');
                for (int l = 0; l < aLines.Length; l++)
                {
                    string sLine = aLines[l].Replace("  ", " ");
                    while (sLine.IndexOf("  ") != -1)
                        sLine = sLine.Replace("  ", " ");
                    string[] aFields = sLine.Split(' ');
                    if (aFields.Length >= 4 && aFields[2].ToUpper().IndexOf("<DIR>") == -1)
                    {
                        FileStruct fs = new FileStruct();
                        fs.CreateTime = Convert.ToDateTime(aFields[0] + " " + aFields[1]);
                        fs.Size = Convert.ToInt32(aFields[2]);
                        fs.Name = aFields[3];
                        StructArray.Add(fs);
                    }
                }
                return StructArray.ToArray();
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "FTP_GetDirectoryListing", string.Format("ERR:{0}", ex.Message));
            }
            return null;
        }

        public bool FTP_DownloadFile(string localPath, string remoteFolder, string fileName)
        {
            try
            {
                string UserName = GetAppSetting("ftp_username");
                string Password = GetAppSetting("ftp_password");
                string ftpURI = GetAppSetting("ftp_uri");
                ftpURI += remoteFolder + "/" + fileName;
                FtpWebRequest ftp = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpURI));
                ftp.Credentials = new NetworkCredential(UserName, Password);
                ftp.KeepAlive = false;
                ftp.UseBinary = true;
                ftp.Method = WebRequestMethods.Ftp.DownloadFile;
                using (WebResponse response = ftp.GetResponse())
                using (BinaryReader reader = new BinaryReader(response.GetResponseStream()))
                using (BinaryWriter writer = new BinaryWriter(File.Open(localPath + fileName, FileMode.Create)))
                {
                    byte[] buffer = new byte[2048];
                    int count = reader.Read(buffer, 0, buffer.Length);
                    while (count != 0)
                    {
                        writer.Write(buffer, 0, count);
                        count = reader.Read(buffer, 0, buffer.Length);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "FTP_DownloadFile", string.Format("ERR:{0}", ex.Message));
            }
            return false;
        }

        public static Int32 DateDiff(DateInterval Interval, DateTime dFrom, DateTime dTo)
        {
            TimeSpan ts = new TimeSpan(dTo.Ticks - dFrom.Ticks);
            switch (Interval)
            {
                case DateInterval.Day:
                    return (Int32)ts.Days;
                case DateInterval.Hour:
                    return (Int32)ts.TotalHours;
                case DateInterval.Minute:
                    return (Int32)ts.TotalMinutes;
                case DateInterval.Month:
                    return (Int32)(ts.Days / 30);
                case DateInterval.Quarter:
                    return (Int32)((ts.Days / 30) / 3);
                case DateInterval.Second:
                    return (Int32)ts.TotalSeconds;
                case DateInterval.Week:
                    return (Int32)(ts.Days / 7);
                case DateInterval.Year:
                    return (Int32)(ts.Days / 365);
            }
            return 0;
        }

        public void ImportTextFiles(string filePattern)
        {
            try
            {
                if (string.IsNullOrEmpty(filePattern))
                    filePattern = "*.*";
                else
                    filePattern = filePattern.Replace("%", "*");
                string path = GetAppSetting("path");
                foreach (string BankFile in Directory.GetFiles(path, filePattern))
                    AddTextFileToDatabase(BankFile);
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "ImportTextFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public void RemoveAttribute(string filePath, FileAttributes attributesToRemove)
        {
            FileAttributes attributes = File.GetAttributes(filePath);
            if ((attributes & attributesToRemove) == attributesToRemove)
                File.SetAttributes(filePath, attributes & ~attributesToRemove);
        }

        public string CreateFolder(string dir)
        {
            string TempPath = string.Format("{0}\\{1}{2}\\", Directory.GetCurrentDirectory(), dir, _IFaceTypeID);
            if (!Directory.Exists(TempPath))
                Directory.CreateDirectory(TempPath);
            return TempPath;
        }

        public void RemoveFolder(string dir)
        {
            string TempPath = string.Format("{0}\\{1}{2}\\", Directory.GetCurrentDirectory(), dir, _IFaceTypeID);
            if (Directory.Exists(TempPath))
                Directory.Delete(TempPath, true);
        }

        public List<string> SCBCheckCompanyTaxID()
        {
            List<string> taxidList = new List<string>();
            //Query customers for the interface SCB
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select txtserver, txtdatabase from cinccustomer..customerfull where numcustomerid in ( select numcustomerid from cincdb2.cinccustomer.dbo.CustomerInterface where numifacetypeid = {0}" +
                                      "and dteDeleted is null union all select numcustomerid from cincdb3.cinccustomer.dbo.CustomerInterface where numifacetypeid = {0} and dteDeleted is null) and numlive = 1 order by txtdatabase", _IFaceTypeID);
                    using (DataTable dtCustomer = new DataTable())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtCustomer);
                        }

                        foreach (DataRow customer in dtCustomer.Rows)
                        {
                            using (SqlCommand cmd2 = new SqlCommand())
                            {
                                cmd2.CommandTimeout = 1200;
                                cmd2.Connection = conn;
                                cmd2.CommandText = string.Format("select isnull(max(replace(txttaxid,'-','')),'') from {0}.{1}.dbo.companyprofile", customer["txtserver"].ToString(), customer["txtdatabase"].ToString());
                                //get each taxid and add to List                               
                                taxidList.Add(cmd2.ExecuteScalar().ToString());
                            }
                           
                        }
                    }                                     
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetTranCode", ex.Message + " SQL:" + cmd.CommandText);
                    return null;
                }
            }

            return taxidList;
        }

        public void ImportFISERVTransactions()
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    string sBuf = "";
                    string sAccount = "";
                    string sCheckNumber = "";
                    string sTranCode = "";
                    string sDescription = "";
                    decimal dAmount = 0;
                    string sDate = "";
                    string sTraceNumber = "";
                    string sCD = "";
                    string sTranDate = "";

                    List<string> taxidList = SCBCheckCompanyTaxID();

                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select numfileid,txtfile,txtfilename from fcbinterface..bankfile where numifacetypeid = {0} and txtfilename like '%.dat' and dteparsedate is null order by numfileid", _IFaceTypeID);
                    using (DataTable dtFiles = new DataTable())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtFiles);
                        }
                        foreach (DataRow row in dtFiles.Rows)
                        {
                            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(row["txtfile"].ToString())))
                            using (StreamReader sr = new StreamReader(ms))
                            {
                                string sFileName = row["txtfilename"].ToString().ToUpper();
                                if (sFileName.Substring(0, 3) == "IB2")
                                    sFileName = sFileName.Remove(0, 3);
                                if (sFileName.Substring(0, 2) == "IB")
                                    sFileName = sFileName.Remove(0, 2);
                                sBuf = sr.ReadToEnd().Replace("\r", "").Replace("\n", "");
                                if (sBuf.Length >= 13)
                                    sBuf = sBuf.Remove(0, 13);
                                else
                                    continue;
                                switch (sFileName)
                                {
                                    case "CD.DAT":
                                        for (; sBuf.Length >= 263;)
                                        {
                                            sAccount = sBuf.Substring(14, 10);
                                            dAmount = Convert.ToDecimal(sBuf.Substring(53, 12));
                                            if ('-' == sBuf[65])
                                                dAmount *= -1;
                                            sDate = sTranDate;
                                            if (!IsDate(sDate))
                                            {
                                                //get previous business day if we haven't parsed any transactions yet
                                                cmd.CommandText = string.Format("select cincinternal.dbo.GetPreviousBusinessDay('{0:d}')", DateTime.Now);
                                                sDate = string.Format("{0:d}", Convert.ToDateTime(cmd.ExecuteScalar().ToString()));
                                            }
                                            cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                                "values('{0}','EBAL',{1},'','{2}','','','',0,{3},{4})",
                                                sqlStr(sAccount), dAmount, sqlStr(sDate), _IFaceTypeID, row["numfileid"]);
                                            cmd.ExecuteNonQuery();
                                            sBuf = sBuf.Remove(0, 263);
                                        }
                                        break;
                                    case "DDA.DAT":
                                    case "SVG.DAT":
                                        for (; sBuf.Length >= 434;)
                                        {
                                            sAccount = sBuf.Substring(14, 10);
                                            dAmount = Convert.ToDecimal(sBuf.Substring(43, 12));
                                            if ('-' == sBuf[55])
                                                dAmount *= -1;
                                            sDate = sTranDate;
                                            if (!IsDate(sDate))
                                            {
                                                //get previous business day if we haven't parsed any transactions yet
                                                cmd.CommandText = string.Format("select cincinternal.dbo.GetPreviousBusinessDay('{0:d}')", DateTime.Now);
                                                sDate = string.Format("{0:d}", Convert.ToDateTime(cmd.ExecuteScalar().ToString()));
                                            }
                                            cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                                "values('{0}','EBAL',{1},'','{2}','','','',0,{3},{4})",
                                                sqlStr(sAccount), dAmount, sqlStr(sDate), _IFaceTypeID, row["numfileid"]);
                                            cmd.ExecuteNonQuery();
                                            sBuf = sBuf.Remove(0, 434);
                                        }
                                        break;
                                    case "DTRAN.DAT":
                                    case "STRAN.DAT":
                                    case "CTRAN.DAT":
                                        for (; sBuf.Length >= 178;)
                                        {
                                            //if (_IFaceTypeID == 71 && sBuf.Substring(4, 9) != "208283261" && sBuf.Substring(4, 9) != "870656190")
                                            if (_IFaceTypeID == 71 &&  (!taxidList.Contains(sBuf.Substring(4, 9))))
                                            {
                                                sBuf = sBuf.Remove(0, 178);
                                                continue;
                                            }
                                            sAccount = sBuf.Substring(14, 10);
                                            sCheckNumber = sBuf.Substring(24, 10);
                                            sDate = sBuf.Substring(34, 10);
                                            sTranDate = sDate;
                                            dAmount = Convert.ToDecimal(sBuf.Substring(44, 11));
                                            if ('-' == sBuf[55])
                                                dAmount *= -1;
                                            sTranCode = sBuf.Substring(56, 3);
                                            sDescription = sBuf.Substring(59, 20);
                                            sTraceNumber = sBuf.Substring(128, 20);
                                            cmd.CommandText = string.Format("select isnull(max(txtcreditdebit),'C') from fcbinterface..banktrancode " +
                                                "where numifacetypeid = {0} and numtrancode = {1}", _IFaceTypeID, sqlStr(sTranCode));
                                            sCD = cmd.ExecuteScalar().ToString();
                                            cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                                "values('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9})",
                                                sqlStr(sAccount), sqlStr(sTranCode), dAmount, sqlStr(sCheckNumber), sqlStr(sDate), sqlStr(sTraceNumber), sqlStr(sDescription), sCD, _IFaceTypeID, row["numfileid"]);
                                            cmd.ExecuteNonQuery();
                                            sBuf = sBuf.Remove(0, 178);
                                        }
                                        break;
                                }
                            }
                            cmd.CommandText = String.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("QBProcessBankFiles", "ImportTransactions", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }


        /// <summary>
        /// Added by Invezza Team 
        /// To Get Transactions from JXChange API
        /// </summary>
        /// <param name="fileID">int fileID</param>
        public void ImportJXChangeTransactions(int fileID)
        {
            GetJXChangeAccountHistoryTrans(fileID);
        }

        /// <summary>
        /// Added by Invezza Team 
        /// Get Account details from JXChange API
        /// </summary>
        /// <param name="fileID">int fileID</param>
        public void GetJXChangeAccountHistoryTrans(int fileID)
        {
            ///get previous business day if we haven't parsed any transactions yet
            DateTime transDate = DateTime.Now;           
            string prevAccountNumber = string.Empty;

            transDate = GetPreviousBusinessDay();
            JXChangeComAcctSrchModel jxChangeComAcctSrchModel = new JXChangeComAcctSrchModel();
            jxChangeComAcctSrchModel.StartDate = transDate;
            jxChangeComAcctSrchModel.EndDate = transDate;
            jxChangeComAcctSrchModel.MaxRecords = Convert.ToInt32(GetAppSetting("MaxRecords"));
            jxChangeComAcctSrchModel.JXChangeComModel = this.jxChangeComModel;

            foreach (SqlConnection conn in GetConnections())
            {
                using (SqlCommand cmd = new SqlCommand())
                {
                    int bankID = 0;
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    try
                    {
                        cmd.CommandText = "select txtdatabase from cinccustomer..customer where numlive = 1 and txtcustomertype in ('AIONLY','HOA') order by txtdatabase";
                        using (DataTable dtCustomer = new DataTable())
                        {
                            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                            {
                                da.Fill(dtCustomer);
                            }

                            foreach (DataRow customer in dtCustomer.Rows)
                            {
                                cmd.CommandText = string.Format("use [{0}]", customer["txtdatabase"]);
                                cmd.ExecuteNonQuery();
                                bankID = 0;
                                cmd.CommandText = string.Format("select numbankid from interfaces where numifacetypeid = {0}", _IFaceTypeID);
                                using (SqlDataReader rdr = cmd.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        bankID = Convert.ToInt32(rdr["numbankid"]);
                                    }
                                    else
                                        continue;
                                }
                                cmd.CommandText = string.Format("select distinct dbo.trimzeros(cb.numaccountno) as [numaccountno], cb.numCBAccountType as [numcbaccounttype], cb.numassocid " +
                                    "from checkbook cb inner join association a on a.associd = cb.numassocid " +
                                    "inner join bank b on b.numbankid = cb.numbankid " +
                                    "where cb.numbankid = {0} and a.status = 1 and cb.numstatus = 1 " +
                                    "order by cb.numassocid,cb.numcbaccounttype desc", bankID);

                                using (DataTable dtAccount = new DataTable())
                                {
                                    using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                                    {
                                        da.Fill(dtAccount);
                                    }

                                    foreach (DataRow acct in dtAccount.Rows)
                                    {
                                        try
                                        {
                                            if (acct != null)
                                            {
                                                if (string.IsNullOrEmpty(acct["numaccountno"].ToString()))
                                                    continue;                                                
                                                if (DuplicateTransactionsExists(acct["numaccountno"].ToString(), transDate.ToString()))
                                                    continue;                                                      
                                                jxChangeComAcctSrchModel.AccountId = acct["numaccountno"].ToString();
                                                string accountStatus = string.Empty;
                                                string accountType = string.Empty;
                                                //training people/customers set up the accounts wrong. so we are getting the account type to deposit                                                
                                                GetJXChangeEBalTran(acct["numaccountno"].ToString(), ref accountType, ref accountStatus, fileID, transDate, GetAppSetting("MaxRecords"));                                                                                                                                          
                                                jxChangeComAcctSrchModel.AccountType = accountType;
                                                jXChangeCom.InitializeAccountHistSrch(jxChangeComAcctSrchModel);
                                                JXChangeComReqRespModel jxChangeComReqRespModel = new JXChangeComReqRespModel();
                                                AcctHistSrchResponse aresp = jXChangeCom.GetAccountHistory(0, ref jxChangeComReqRespModel);

                                                //This method is used to maintain request and response log of Jack Henry service operation
                                                if (jxChangeComReqRespModel.LogType.ToLower().Equals("error"))
                                                    LogJackHenryDetails(jxChangeComReqRespModel, jxChangeComAcctSrchModel.AccountId + ": " + jxChangeComAcctSrchModel.AccountType);

                                                int currentCursor = 0;
                                                if (aresp == null)
                                                {
                                                    continue;
                                                }
												while (aresp != null && ((aresp.SrchMsgRsHdr != null && aresp.SrchMsgRsHdr.MoreRec != null
													 && Convert.ToBoolean(aresp.SrchMsgRsHdr.MoreRec.Value)) || aresp.AcctHistSrchRecArray != null))
												{
													if (((jxChangeComAcctSrchModel.AccountType == "D")|| (jxChangeComAcctSrchModel.AccountType == "S")) && ((accountStatus == "1") || (accountStatus == "2") ||
                                                        (accountStatus == "3") || (accountStatus == "4") || (accountStatus == "5") || (accountStatus == "6")))
                                                    {
														GetDepHistSrchRecValues(aresp.AcctHistSrchRecArray, fileID);
													}
													else
													if ((jxChangeComAcctSrchModel.AccountType == "T") && ((accountStatus == "A")||(accountStatus == "M")|| (accountStatus == "R") || (accountStatus == "N")|| (accountStatus == "D")))
													{
														GetTimeDepHistSrchRecValues(aresp.AcctHistSrchRecArray, fileID);
													}													
													if (jxChangeComAcctSrchModel.MaxRecords == aresp.AcctHistSrchRecArray.Length)
													{
														currentCursor += aresp.SrchMsgRsHdr.SentRec.Value;
														aresp = jXChangeCom.GetAccountHistory(currentCursor, ref jxChangeComReqRespModel);
													}
													else
														aresp = null;
												}
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _Log.LogErr("ProcessBankFiles", "GetJXChangeAccountHistoryTrans", ex.Message + " SQL:" + cmd.CommandText);
                                        }

                                    }
                                }
                            }
                        }
                        cmd.CommandText = String.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", fileID);
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", fileID);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _Log.LogErr("ProcessBankFiles", "GetJXChangeAccountHistoryTrans", ex.Message + " SQL:" + cmd.CommandText);
                    }
                }
            }
        }

        DateTime GetPreviousBusinessDay()
        {
            DateTime prevDay = DateTime.Now.AddDays(-1).Date;
            DateTime transDate = DateTime.Now;

            SqlConnection primaryconn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = primaryconn;
                    cmd.CommandText = string.Format("select cincinternal.dbo.GetPreviousBusinessDay(CONVERT(date, GETDATE()))");
                    transDate = Convert.ToDateTime(string.Format("{0:d}", Convert.ToDateTime(cmd.ExecuteScalar().ToString())));
                    //previous day is end of the month, then we take , previous day for trandate since interest transactions are posted at the end of the month
                    if (prevDay.Month != transDate.Month)
                        transDate = prevDay;
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetJXChangeAccountHistoryTrans", ex.Message + " SQL:" + cmd.CommandText);
                    return prevDay;
                }
            }
            return transDate;
        }
		/// <summary>
		/// Added by Invezza Team
		/// GetDepHistSrchRecValues method is used when AcctType = "D"
		/// </summary>
		/// <param name="acctHistSrchRecArray"></param>
		/// <param name="cmd"></param>
		/// <param name="fileID"></param>
		public void GetDepHistSrchRecValues(AcctHistSrchRec_CType[] acctHistSrchRecArray, int fileID)
		{
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    foreach (AcctHistSrchRec_CType acctHistory in acctHistSrchRecArray)
                    {
                        string accountNumber = (acctHistory.DepHistSrchRec.DepAcctId.AcctId == null)
                                                                        ? string.Empty : acctHistory.DepHistSrchRec.DepAcctId.AcctId.Value;
                        string transactionDate = (acctHistory.DepHistSrchRec.EffDt == null)
                        ? string.Empty : Convert.ToDateTime(acctHistory.DepHistSrchRec.EffDt.Value).ToString("yyyy-MM-dd");
                        decimal transactionAmount = (acctHistory.DepHistSrchRec.Amt == null)
                            ? decimal.Round(0, 2, MidpointRounding.AwayFromZero) : Convert.ToDecimal(acctHistory.DepHistSrchRec.Amt.Value);
                        if ((transactionAmount == Convert.ToDecimal(0.00)) || (transactionAmount == Convert.ToDecimal(0)))
                            continue;
                        string checkNumber = (acctHistory.DepHistSrchRec.ChkNum == null)
                            ? string.Empty : acctHistory.DepHistSrchRec.ChkNum.Value;
                        string traceNumber = (acctHistory.DepHistSrchRec.SeqNum == null)
                            ? string.Empty : Convert.ToString(acctHistory.DepHistSrchRec.SeqNum.Value);
                        string transactionDescription = (acctHistory.DepHistSrchRec.EftDescArray == null)
                          ? string.Empty : acctHistory.DepHistSrchRec.EftDescArray[0].EftDesc.Value.ToString();
                        if (string.IsNullOrEmpty(transactionDescription) == true)
                            transactionDescription = acctHistory.DepHistSrchRec.TrnCodeDesc.Value;

                        if (string.IsNullOrEmpty(checkNumber))
                        {
                            string extradescr = ((string.IsNullOrEmpty(checkNumber) || (checkNumber.Equals("0"))) && (acctHistory.DepHistSrchRec.EftDescArray[2].EftDesc != null))
                           ? acctHistory.DepHistSrchRec.EftDescArray[2].EftDesc.Value.ToString() : string.Empty;
                            transactionDescription = transactionDescription + " " + extradescr;
                        }
                        string tranType = (acctHistory.DepHistSrchRec.TrnType == null)
                            ? string.Empty : acctHistory.DepHistSrchRec.TrnType.Value;
                        string creditDebit = string.Empty;
                        if (tranType == "D")
                            creditDebit = tranType;
                        else
                        {
                            cmd.CommandText = string.Format("exec fcbinterface..sp_aiGetJXChangeBankTranCode {0},{1},'{2}'",
                                _IFaceTypeID, acctHistory.DepHistSrchRec.TrnCodeCode.Value, acctHistory.DepHistSrchRec.TrnCodeDesc.Value);
                            creditDebit = Convert.ToString(cmd.ExecuteScalar());
                        }
                        string transactionCode = (acctHistory.DepHistSrchRec.TrnCodeCode == null)
                            ? string.Empty : acctHistory.DepHistSrchRec.TrnCodeCode.Value.ToString();

                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction " +
                            "(txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit," +
                                "mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9})",
                                sqlStr(accountNumber), sqlStr(transactionCode), transactionAmount, sqlStr(checkNumber), transactionDate,
                                sqlStr(traceNumber), sqlStr(transactionDescription.Replace("/", string.Empty)), sqlStr(creditDebit), _IFaceTypeID, fileID);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetDepHistSrchRecValues", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
		}

        /// <summary>
        /// Added by Invezza Team
        /// GetTimeDepHistSrchRecValues method is used when AcctType = "T"
        /// </summary>
        /// <param name="acctHistSrchRecArray"></param>
        /// <param name="cmd"></param>
        /// <param name="fileID"></param>
        public void GetTimeDepHistSrchRecValues(AcctHistSrchRec_CType[] acctHistSrchRecArray, int fileID)
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    foreach (AcctHistSrchRec_CType acctHistory in acctHistSrchRecArray)
                    {
                        string accountNumber = (acctHistory.TimeDepHistSrchRec.TimeDepAcctId == null || acctHistory.TimeDepHistSrchRec.TimeDepAcctId.AcctId == null)
                                                                        ? string.Empty : acctHistory.TimeDepHistSrchRec.TimeDepAcctId.AcctId.Value;
                        string transactionDate = (acctHistory.TimeDepHistSrchRec.EffDt == null)
                        ? string.Empty : Convert.ToDateTime(acctHistory.TimeDepHistSrchRec.EffDt.Value).ToString("yyyy-MM-dd");
                        decimal transactionAmount = (acctHistory.TimeDepHistSrchRec.Amt == null)
                            ? decimal.Round(0, 2, MidpointRounding.AwayFromZero) : Convert.ToDecimal(acctHistory.TimeDepHistSrchRec.Amt.Value);
                        if ((transactionAmount == Convert.ToDecimal(0.00)) || (transactionAmount == Convert.ToDecimal(0)))
                            continue;
                        string checkNumber = (acctHistory.TimeDepHistSrchRec.ChkNum == null)
                            ? string.Empty : acctHistory.TimeDepHistSrchRec.ChkNum.Value;
                        string traceNumber = (acctHistory.TimeDepHistSrchRec.SeqNum == null)
                            ? string.Empty : Convert.ToString(acctHistory.TimeDepHistSrchRec.SeqNum.Value);

                        string transactionDescription = acctHistory.TimeDepHistSrchRec.TrnCodeDesc == null ? string.Empty : acctHistory.TimeDepHistSrchRec.TrnCodeDesc.Value;
                        if (string.IsNullOrEmpty(checkNumber))
                        {
                            string extradescr = ((string.IsNullOrEmpty(checkNumber) || (checkNumber.Equals("0"))) && (acctHistory.TimeDepHistSrchRec.AffCodeDesc != null))
                           ? acctHistory.TimeDepHistSrchRec.AffCodeDesc.Value.ToString() : string.Empty;
                            transactionDescription = transactionDescription + " " + extradescr;
                        }
                        string tranType = (acctHistory.TimeDepHistSrchRec.TrnType == null)
                            ? string.Empty : acctHistory.TimeDepHistSrchRec.TrnType.Value;
                        string creditDebit = string.Empty;
                        if (tranType == "D")
                            creditDebit = tranType;
                        else
                        {
                            cmd.CommandText = string.Format("exec fcbinterface..sp_aiGetJXChangeBankTranCode {0},{1},'{2}'",
                                _IFaceTypeID, acctHistory.TimeDepHistSrchRec.TrnCodeCode.Value, acctHistory.TimeDepHistSrchRec.TrnCodeDesc.Value);
                            creditDebit = Convert.ToString(cmd.ExecuteScalar());
                        }
                        string transactionCode = (acctHistory.TimeDepHistSrchRec.TrnCodeCode == null)
                            ? string.Empty : acctHistory.TimeDepHistSrchRec.TrnCodeCode.Value.ToString();

                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction " +
                            "(txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit," +
                                "mnyinterest,numifacetypeid,numfileid) values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9})",
                                sqlStr(accountNumber), sqlStr(transactionCode), transactionAmount, sqlStr(checkNumber), transactionDate,
                                sqlStr(traceNumber), sqlStr(transactionDescription.Replace("/", string.Empty)), sqlStr(creditDebit), _IFaceTypeID, fileID);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch(Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetDepHistSrchRecValues", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }

	    /// <summary>
		/// Added by Invezza Team 
		/// To Check Duplicate Records
		/// </summary>
		/// <param name="accountNumber">string accountNumber</param>
		/// <param name="transactionDate">string transactionDate</param>
		public bool DuplicateTransactionsExists(string accountNumber, string transDate)
        {
            bool isDuplicate = true;
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;

                try
                {
                    cmd.CommandText = string.Format("select count(*) from fcbinterface..banktransaction  bt where bt.numIFaceTypeID = {0} and bt.txtAccountNumber = '{1}' and bt.dteTranDate = '{2}'",
                        _IFaceTypeID, accountNumber, Convert.ToDateTime(transDate).ToString("yyyy-MM-dd"));
                    if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                    {
                        isDuplicate = false;
                    }
                    _Log.LogApp("ProcessBankFiles: DuplicateTransactions: " + accountNumber + "  SQL:" + cmd.CommandText);
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "DuplicateTransactions", ex.Message + " SQL:" + cmd.CommandText);
                }
            }

            return isDuplicate;
        }

        /// <summary>
        /// Added by Invezza Team 
        /// To Get End of Balance
        /// </summary>
        /// <param name="fileID">int fileID</param>
        public bool GetJXChangeEBalTran(string acctid, ref string accountType, ref string accountStatus, int fileID, DateTime transDate, string maxRecords)
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    JXChangeComReqRespModel jxChangeBankTrans = new JXChangeComReqRespModel();
                    AcctSrchResponse aresp = jXChangeCom.GetAccountSrch(acctid, ref accountType, jxChangeComModel.ClientCredentialsUserName, jxChangeComModel.ClientCredentialsPassword, maxRecords, ref jxChangeBankTrans);
                    //This method is used to maintain request and response log of Jack Henry service operation
                    if (jxChangeBankTrans.LogType.ToLower().Equals("error"))
                        LogJackHenryDetails(jxChangeBankTrans, acctid);
                    if (aresp == null)
                    {
                        return false;
                    }
                    else if (aresp.AcctSrchRecArray != null)
                    {
                        for (int i = 0; i < aresp.AcctSrchRecArray.Length; i++)
                        {
                            if ((((aresp.AcctSrchRecArray[i].AccountId.AcctType.Value == "D") || ((aresp.AcctSrchRecArray[i].AccountId.AcctType.Value == "S"))) && ((aresp.AcctSrchRecArray[i].AcctStat.Value == "1")|| (aresp.AcctSrchRecArray[i].AcctStat.Value == "2") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "3") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "4") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "5") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "6")))
                                || ((aresp.AcctSrchRecArray[i].AccountId.AcctType.Value == "T") && ((aresp.AcctSrchRecArray[i].AcctStat.Value == "A") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "M")||(aresp.AcctSrchRecArray[i].AcctStat.Value == "R") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "N") || (aresp.AcctSrchRecArray[i].AcctStat.Value == "D"))))
                            {
                                string accountNumber = (aresp.AcctSrchRecArray[i].AccountId == null) ? string.Empty : aresp.AcctSrchRecArray[i].AccountId.AcctId.Value;
                                string transactionCode = "EBAL";
                                string transactionDescription = (aresp.AcctSrchRecArray[i].ProdDesc == null) ? string.Empty : aresp.AcctSrchRecArray[i].ProdDesc.Value;
                                decimal transactionAmount = (aresp.AcctSrchRecArray[i].Amt == null)
                                    ? decimal.Round(0, 2, MidpointRounding.AwayFromZero) : Convert.ToDecimal(aresp.AcctSrchRecArray[i].Amt.Value);
                                cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount, txtchecknumber, dtetrandate, txttracenumber, txtdescr, txtcreditdebit, mnyinterest, numifacetypeid,numfileid) " +
                                    "values ('{0}','{1}',{2},'','{3}','','{4}','', 0, {5}, {6})",
                                    sqlStr(accountNumber), sqlStr(transactionCode), transactionAmount, transDate.ToString("yyyy-MM-dd"), sqlStr(transactionDescription.Replace("/", string.Empty)), _IFaceTypeID, fileID);
                                cmd.ExecuteNonQuery();
                                accountType = aresp.AcctSrchRecArray[i].AccountId.AcctType.Value;
                                accountStatus = aresp.AcctSrchRecArray[i].AcctStat.Value;                               
                                return true;
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetJXChangeEBalTran", ex.Message + " SQL:" + cmd.CommandText);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Added by Invezza Team 
        /// To add request and response log to Database
        /// </summary>
        /// <param name="requestdata">string requestdata</param>
        /// <param name="responsedata">string responsedata</param>
        /// <param name="exception">string exception</param>
        /// <param name="errortype">string errortype</param>
        /// <param name="accountNumber">string accountNumber</param>
        /// <param name="logTrackingID">string logTrackingID</param>
        private void LogJackHenryDetails(JXChangeComReqRespModel jxChangeComReqRespModel, string accountNumber)
        {
            try
            {
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("exec cinccustomer..sp_AI_Insert_AI_JackhenryLog  @LogTrackingGUID='{0}', @Request= '{1}', " +
                        "@Response='{2}', @exception='{3}', @errortype= '{4}', @accountnumber = '{5}'", sqlStr(jxChangeComReqRespModel.LogTrackingId), sqlStr(jxChangeComReqRespModel.Request),
                        sqlStr(jxChangeComReqRespModel.Response), sqlStr(jxChangeComReqRespModel.ExceptionMessage), sqlStr(jxChangeComReqRespModel.LogType), sqlStr(accountNumber));
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "LogJackHenryDetails", ex.Message);
            }
        }
        private void LogJackHenryDetails(string msg, string accountNumber)
        {
            try
            {
                SqlConnection conn = GetPrimaryConnection();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("exec cinccustomer..sp_AI_Insert_AI_JackhenryLog  @LogTrackingGUID='{0}', @Request= '{1}', " +
                        "@Response='{2}', @exception='{3}', @errortype= '{4}', @accountnumber = '{5}'", string.Empty, sqlStr(msg),
                        "", "", "Info", sqlStr(accountNumber));
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                _Log.LogErr("ProcessBankFiles", "LogJackHenryDetails", ex.Message);
            }
        }

        /// <summary>
        ///  Added by Invezza Team 
        ///  To Get Transactions from JXChange API
        /// </summary>
        public void ImportJXChangeImages()
        {
            int FileID = 0;
            string AccountNumber = "";
            decimal TrnAmount = 0;
            string CheckNumber = "";          
            string AccountType = "";
            DateTime TrnDate = DateTime.Now;             
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            using (DataTable BankFiles = new DataTable())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    DateTime transDate = GetPreviousBusinessDay();                                      
                    using (DataTable Accounts = new DataTable())
                    {
                        cmd.CommandText = string.Format("select * from fcbinterface..banktransaction where txtCreditDebit='D' and txtCheckNumber is not null and txtCheckNumber <> '' and txtCheckNumber != '0' and numifacetypeid = {0}  and dteTranDate = '{1}'", _IFaceTypeID, transDate);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(Accounts);
                        }
                        foreach (DataRow accounts in Accounts.Rows)
                        {
                            AccountNumber = accounts["txtAccountNumber"].ToString();
                            AccountType = accounts["txtCreditDebit"].ToString();
                            CheckNumber = accounts["txtCheckNumber"].ToString();
                            TrnAmount = Convert.ToDecimal(accounts["mnyAmount"]);
                            TrnDate = Convert.ToDateTime(accounts["dteTranDate"]);
                            FileID = Convert.ToInt32(accounts["numfileid"].ToString());
                            GetJXChangeAccountHistoryForCheckImage(AccountNumber, AccountType, CheckNumber, TrnAmount, TrnDate, jxChangeComModel.InstRtId, TrnDate, FileID);
                        }
                    }                           
                                       
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "ImportJXChangeImages", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                    if (FileID != 0)
                    {
                        cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", FileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Added by Invezza Team 
        /// To Get Check Images from JXChange API
        /// </summary>
        /// <param name="accountNumber"></param>
        /// <param name="accountType"></param>
        /// <param name="checkNumber"></param>
        /// <param name="trnAmount"></param>
        /// <param name="transDate"></param>
        /// <param name="routingNumber"></param>
        /// <param name="processingDate"></param>
        /// <param name="fileID"></param>
        public void GetJXChangeAccountHistoryForCheckImage(string accountNumber, string accountType, string checkNumber, decimal trnAmount, DateTime transDate, string routingNumber, DateTime processingDate, int fileID)
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;
                try
                {
                    JXChangeComReqRespModel jxChangeBankTrans = new JXChangeComReqRespModel();
                    AcctSrchResponse actsrchresp = jXChangeCom.GetAccountSrch(accountNumber, ref accountType, jxChangeComModel.ClientCredentialsUserName, jxChangeComModel.ClientCredentialsPassword, GetAppSetting("MaxRecords"), ref jxChangeBankTrans);
                    //This method is used to maintain request and response log of Jack Henry service operation
                    if (jxChangeBankTrans.LogType.ToLower().Equals("error"))
                        LogJackHenryDetails(jxChangeBankTrans, accountNumber);
                    if (actsrchresp == null)
                    {
                        return;
                    }
                    else if (actsrchresp != null && actsrchresp.AcctSrchRecArray != null && actsrchresp.AcctSrchRecArray.Length > 0)
                    {
                        if ((((actsrchresp.AcctSrchRecArray[0].AccountId.AcctType.Value == "D") || ((actsrchresp.AcctSrchRecArray[0].AccountId.AcctType.Value == "S"))) && ((actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "1") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "2") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "3") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "4") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "5") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "6")))
                                   || ((actsrchresp.AcctSrchRecArray[0].AccountId.AcctType.Value == "T") && ((actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "A") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "M") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "R") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "N") || (actsrchresp.AcctSrchRecArray[0].AcctStat.Value == "D"))))
                        {
                            accountType = actsrchresp.AcctSrchRecArray[0].AccountId != null ? actsrchresp.AcctSrchRecArray[0].AccountId.AcctType.Value : string.Empty;
                        }
                    }
                    JXChangeComAcctSrchModel jxChangeComAcctSrchModel = new JXChangeComAcctSrchModel();
                    jxChangeComAcctSrchModel.AccountId = accountNumber;
                    jxChangeComAcctSrchModel.AccountType = accountType;
                    jxChangeComAcctSrchModel.StartDate = transDate;
                    jxChangeComAcctSrchModel.EndDate = transDate;
                    jxChangeComAcctSrchModel.MaxRecords = Convert.ToInt32(GetAppSetting("MaxRecords"));
                    jxChangeComAcctSrchModel.TransactionAmount = Math.Round(trnAmount, 2);
                    jxChangeComAcctSrchModel.CheckNumber = checkNumber;
                    jxChangeComAcctSrchModel.JXChangeComModel = jxChangeComModel;
                    jXChangeCom.InitializeAccountHistSrchForCheckImage(jxChangeComAcctSrchModel);
                    JXChangeComReqRespModel jxChangeComReqRespModel = new JXChangeComReqRespModel();
                    AcctHistSrchResponse aresp = jXChangeCom.GetAccountHistory(0, ref jxChangeComReqRespModel);
                    //This method is used to maintain request and response log of Jack Henry service operation
                    if (jxChangeComReqRespModel.LogType.ToLower().Equals("error"))
                        LogJackHenryDetails(jxChangeComReqRespModel, jxChangeComAcctSrchModel.AccountId + ": " + jxChangeComAcctSrchModel.AccountType);

                    if (aresp == null)
                    {
                        return;
                    }
                    else if (aresp.AcctHistSrchRecArray != null && aresp.AcctHistSrchRecArray.Length > 0)
                    {
                        foreach (InquiryMaster.AcctHistSrchRec_CType sHistory in aresp.AcctHistSrchRecArray)
                        {
                            JXChangeComCheckImageModel checkImageModel = new JXChangeComCheckImageModel();
                            checkImageModel.CheckImageFormat = _ImageFormat;
                            if (!string.IsNullOrEmpty(accountType))
                            {
                                if (accountType == "D" || accountType == "S")
                                    checkImageModel.CheckImageID = sHistory.DepHistSrchRec.ImgNum.Value;
                                else if (accountType == "T")
                                    checkImageModel.CheckImageID = sHistory.TimeDepHistSrchRec.ImgNum.Value;
                            }
                            checkImageModel.CheckSide = _CheckSide;
                            checkImageModel.JXChangeComModel = jxChangeComModel;
                            Image.ChkImgInqResponse chkImgResponse = jXChangeCom.DownloadCheckImage(checkImageModel, ref jxChangeComReqRespModel);
                            if (chkImgResponse != null && !string.IsNullOrEmpty(chkImgResponse.ChkImgId.Value) && !string.IsNullOrEmpty(_CheckSide))
                            {
                                if (!CheckDuplicateBankImageIDExists(chkImgResponse.ChkImgId.Value))
                                {
                                    if (_CheckSide.ToLower() == "both")
                                    {
                                        if (chkImgResponse.FrontChkImg != null && chkImgResponse.FrontChkImg.Value != null)
                                        {                                           
                                            byte[] checkImageFront = chkImgResponse.FrontChkImg.Value;
                                            cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                "values ({0},{1},'{2}','{3}','{4}','{5}',0,{6},'{7:d}',{8},@Data)", fileID, trnAmount, sqlStr(accountNumber), sqlStr(routingNumber), sqlStr(checkNumber), sqlStr(chkImgResponse.ChkImgId.Value), checkImageFront.Length, processingDate, _IFaceTypeID);
                                            SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, checkImageFront.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, checkImageFront);
                                            cmd.Parameters.Add(param);
                                            cmd.ExecuteNonQuery();
                                            cmd.Parameters.Clear();
                                            param = null;
                                        }
                                        if (chkImgResponse.BackChkImg != null && chkImgResponse.BackChkImg.Value != null)
                                        {
                                            byte[] checkImageBack = chkImgResponse.BackChkImg.Value;
                                            cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                "values ({0},{1},'{2}','{3}','{4}','{5}',1,{6},'{7:d}',{8},@Data)", fileID, trnAmount, sqlStr(accountNumber), sqlStr(routingNumber), sqlStr(checkNumber), sqlStr(chkImgResponse.ChkImgId.Value), checkImageBack.Length, processingDate, _IFaceTypeID);
                                            SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, checkImageBack.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, checkImageBack);
                                            cmd.Parameters.Add(param);
                                            cmd.ExecuteNonQuery();
                                            cmd.Parameters.Clear();
                                            param = null;
                                        }
                                    }
                                    else if (_CheckSide.ToLower() == "front")
                                    {
                                        if (chkImgResponse.FrontChkImg != null && chkImgResponse.FrontChkImg.Value != null)
                                        {
                                            byte[] checkImageFront = chkImgResponse.FrontChkImg.Value;
                                            cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                "values ({0},{1},'{2}','{3}','{4}','{5}',0,{6},'{7:d}',{8},@Data)", fileID, trnAmount, sqlStr(accountNumber), sqlStr(routingNumber), sqlStr(checkNumber), sqlStr(chkImgResponse.ChkImgId.Value), checkImageFront.Length, processingDate, _IFaceTypeID);
                                            SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, checkImageFront.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, checkImageFront);
                                            cmd.Parameters.Add(param);
                                            cmd.ExecuteNonQuery();
                                            cmd.Parameters.Clear();
                                            param = null;
                                        }
                                    }
                                    else if (_CheckSide.ToLower() == "back")
                                    {
                                        if (chkImgResponse.BackChkImg != null && chkImgResponse.BackChkImg.Value != null)
                                        {
                                            byte[] checkImageBack = chkImgResponse.BackChkImg.Value;
                                            cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                                "values ({0},{1},'{2}','{3}','{4}','{5}',0,{6},'{7:d}',{8},@Data)", fileID, trnAmount, sqlStr(accountNumber), sqlStr(routingNumber), sqlStr(checkNumber), sqlStr(chkImgResponse.ChkImgId.Value), checkImageBack.Length, processingDate, _IFaceTypeID);
                                            SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, checkImageBack.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, checkImageBack);
                                            cmd.Parameters.Add(param);
                                            cmd.ExecuteNonQuery();
                                            cmd.Parameters.Clear();
                                            param = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "GetJXChangeAccountHistoryForCheckImage", string.Format("ERR:{0} SQL:{1}", ex.Message, cmd.CommandText));
                }
            }
        }

        /// <summary>
        /// Added by Invezza Team 
        /// To Check Duplicate Image ID 
        /// </summary>
        /// <param name="imageId">string imageId</param>
        /// <returns></returns>
        public bool CheckDuplicateBankImageIDExists(string imageId)
        {
            bool isDuplicate = true;
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandTimeout = 1200;
                cmd.Connection = conn;

                try
                {
                    cmd.CommandText = string.Format("select count(*) from fcbinterface..bankimage where txtimagenumber = '{0}' and numbackflag = 0", sqlStr(imageId));
                    if (0 == Convert.ToInt32(cmd.ExecuteScalar()))
                    {
                        isDuplicate = false;
                    }
                    _Log.LogApp("ProcessBankFiles: BankImageId: " + imageId + "  SQL:" + cmd.CommandText);
                }
                catch (Exception ex)
                {
                    _Log.LogErr("ProcessBankFiles", "BankImageId", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
            return isDuplicate;
        }
    }


    class CWBProcessBankFiles : ProcessBankFiles
    {
        public CWBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            try
            {
                string pathBAI66 = GetAppSetting("pathBAI");
                string filePattern = GetAppSetting("imagefilepattern");
                string path66 = GetAppSetting("path");

                //import BAI files
                foreach (string existingFile in Directory.GetFiles(pathBAI66))
                {
                    if (existingFile.ToLower().IndexOf("bai") >= 0)
                    {
                        try
                        {
                            AddTextFileToDatabase(existingFile);
                            File.Delete(existingFile);
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("CWBProcessBankFiles", "ImportFiles", string.Format("DOWNLOAD_ERROR:{0}", ex.Message));
                            continue;
                        }
                    }
                }

                //import x937 files for check images
                foreach (string fileName in Directory.GetFiles(path66, filePattern))
                {
                    int BankFileID = AddBinaryFileToDatabase(fileName);

                    File.Delete(fileName);
                }
            }
            catch (Exception ex)
            {
                _Log.LogErr("CWBPProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }

        public override void ImportImages()
        {
            Parsex937NSFBankImages("%937%");
        }
    }

    class BPOPProcessBankFiles : ProcessBankFiles
    {
        public BPOPProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            try
            {
                SftpItemCollection BankFiles = new SftpItemCollection();
                string TempPath = CreateTempFolder();
                string Site = "sftp.metavante.com";
                int Port = 22;
                string Uid = @"excust\r493110";
                string Pwd = "Qwerty#4";
                using (Sftp sftp = new Sftp())
                {
                    sftp.Connect(Site, Port);
                    sftp.Login(Uid, Pwd);
                    sftp.ChangeDirectory("/users/r493110");
                    BankFiles = sftp.GetList();
                    foreach (SftpItem bankFile in BankFiles)
                    {
                        try
                        {
                            if (bankFile.Name.ToLower().IndexOf("bai") >= 0)
                            {
                                try
                                {
                                    sftp.GetFile(bankFile.Name, TempPath + bankFile.Name);
                                    AddTextFileToDatabase(TempPath + bankFile.Name);
                                    sftp.DeleteFile(bankFile.Name);
                                }
                                catch (Exception ex)
                                {
                                    _Log.LogErr("BPOPProcessBankFiles", "ImportFiles", string.Format("DOWNLOAD_ERROR:{0}", ex.Message));
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("BPOPProcessBankFiles", "ImportFiles", string.Format("LOOP_ERROR:{0}", ex.Message));
                        }
                    }
                }
                RemoveTempFolder();
            }
            catch (Exception ex)
            {
                _Log.LogErr("BPOPProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }
    }

    class MBProcessBankFiles : ProcessBankFiles
    {
        public MBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            try
            {
                string remotePath = GetAppSetting("remotepath");
                string filePattern = GetAppSetting("filepattern");
                string TempPath = CreateTempFolder();
                using (CincSFTP sftp = new CincSFTP(ref _Log))
                {
                    sftp.GetFiles(TempPath, remotePath, filePattern, true);
                }
                foreach (string fileName in Directory.GetFiles(TempPath, filePattern))
                {
                    AddTextFileToDatabase(fileName);
                }
                RemoveTempFolder();
            }
            catch (Exception ex)
            {
                _Log.LogErr("MBProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }

        public override void ImportImages()
        {
            string BankRoutingNumber = "";
            string remotePath = GetAppSetting("imageremotepath");
            string filePattern = GetAppSetting("imagefilepattern");
            string TempPath = CreateTempFolder();
            string ckfileName = string.Empty;

            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select isnull(max(txtbankrouting),'') from cincinternal..interfacetype where numifacetypeid = {0}", _IFaceTypeID);
                    BankRoutingNumber = cmd.ExecuteScalar().ToString();

                    using (CincSFTP sftp = new CincSFTP(ref _Log))
                    {
                        sftp.GetFiles(TempPath, remotePath, filePattern, true);
                    }
                    foreach (string fileName in Directory.GetFiles(TempPath, filePattern))
                    {
                        FileInfo fi = new FileInfo(fileName);
                        Zip zip = new Zip();
                        zip.UnlockComponent("1FCBUSAZIP_kGv5CoMw7l6V");
                        zip.DiscardPaths = true;
                        zip.OpenZip(fi.FullName);
                        zip.ExtractInto(TempPath);
                        zip.CloseZip();
                        zip = null;
                        int BankFileID = AddBinaryFileToDatabase(fi.FullName);
                        UpdateBankImage(conn, fileName, TempPath, BankFileID, BankRoutingNumber);
                    }
                    ImportNonParsedImages(TempPath);
                    RemoveTempFolder();
                }
                catch (Exception ex)
                {
                    _Log.LogErr("MBProcessBankFiles", "ImportImages", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }

        public void UpdateBankImage(SqlConnection conn, string zipFile, string TempPath, int BankFileID, string BankRoutingNumber)
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.Connection = conn;

                foreach (string xmlImageFile in Directory.GetFiles(TempPath, "*.xml"))
                {
                    try
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(ITEMS));
                        FileStream xmlImageStream = new FileStream(xmlImageFile, FileMode.Open, System.IO.FileAccess.Read);
                        ITEMS loadedObject = (ITEMS)serializer.Deserialize(xmlImageStream);
                        xmlImageStream.Close();
                        if (loadedObject.Items == null)
                        {
                            File.Delete(xmlImageFile);
                            _Log.LogErr("MBProcessBankFiles", "UpdateBankImage", string.Format("The zip file: {0} included invalid Xml file: {1}. It does not any check images to import, Please contact the Bank!", zipFile, xmlImageFile));
                            continue;
                        }
                        foreach (ITEMSItem checkImage in loadedObject.Items)
                        {
                            int ImageNumber = 0;
                            if (File.Exists(TempPath + checkImage.frontimage))
                            {
                                byte[] frontImage = File.ReadAllBytes(TempPath + checkImage.frontimage);
                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                    "values ({0},{1},'{2}','{3}','{4}','',0,{5},'{6:d}',{7},@Data); select @@identity", BankFileID, checkImage.amount, sqlStr(checkImage.account), sqlStr(BankRoutingNumber), sqlStr(checkImage.checknumber), frontImage.Length, checkImage.date, _IFaceTypeID);
                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, frontImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, frontImage);
                                cmd.Parameters.Add(param);
                                ImageNumber = Convert.ToInt32(cmd.ExecuteScalar());
                                cmd.Parameters.Clear();
                                param = null;
                                File.Delete(TempPath + checkImage.frontimage);
                                cmd.CommandText = string.Format("update fcbinterface..bankimage set txtimagenumber = '{0}' where numBankImageID = {0}", ImageNumber);
                                cmd.ExecuteNonQuery();
                            }
                            if (File.Exists(TempPath + checkImage.backimage))
                            {
                                byte[] backImage = File.ReadAllBytes(TempPath + checkImage.backimage);
                                cmd.CommandText = string.Format("insert into fcbinterface..bankimage (numfileid,mnyamount,txtaccount,txtrouting,txtchecknumber,txtimagenumber,numbackflag,numsize,dteprocessingdate,numifacetypeid,imgimage) " +
                                    "values ({0},{1},'{2}','{3}','{4}','{5}',1,{6},'{7:d}',{8},@Data)", BankFileID, checkImage.amount, sqlStr(checkImage.account), sqlStr(BankRoutingNumber), sqlStr(checkImage.checknumber), ImageNumber, backImage.Length, checkImage.date, _IFaceTypeID);
                                SqlParameter param = new SqlParameter("@Data", SqlDbType.VarBinary, backImage.Length, ParameterDirection.Input, true, 0, 0, null, DataRowVersion.Current, backImage);
                                cmd.Parameters.Add(param);
                                cmd.ExecuteNonQuery();
                                cmd.Parameters.Clear();
                                param = null;
                                File.Delete(TempPath + checkImage.backimage);
                            }
                        }
                        File.Delete(xmlImageFile);
                    }
                    catch (Exception ex)
                    {
                        File.Delete(xmlImageFile);
                        _Log.LogErr("MBProcessBankFiles", "UpdateBankImage", ex.Message);
                    }
                }

                cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                cmd.ExecuteNonQuery();
            }
        }

        public void ImportNonParsedImages(string TempPath)
        {
            string BankRoutingNumber = "";
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select isnull(max(txtbankrouting),'') from cincinternal..interfacetype where numifacetypeid = {0}", _IFaceTypeID);
                    BankRoutingNumber = cmd.ExecuteScalar().ToString();

                    cmd.CommandText = string.Format("select  numfileid,txtfilename, imgFile from fcbinterface..bankfile where numifacetypeid = {0} and dteparsedate is null", _IFaceTypeID);

                    string sZipFile = "";
                    int BankFileID = 0;

                    if (!Directory.Exists(TempPath))
                        Directory.CreateDirectory(TempPath);

                    using (DataSet dsBankFile = new DataSet())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dsBankFile, "BankFile");
                        }
                        foreach (DataRow row in dsBankFile.Tables["BankFile"].Rows)
                        {
                            if ((string.IsNullOrEmpty(row["txtfilename"].ToString())) || (string.IsNullOrEmpty(row["numfileid"].ToString())) || (string.IsNullOrEmpty(row["imgFile"].ToString())))
                                continue;

                            sZipFile = TempPath + row["txtfilename"].ToString();

                            BankFileID = Convert.ToInt32(row["numfileid"].ToString());

                            using (FileStream fs = new FileStream(sZipFile, FileMode.OpenOrCreate, System.IO.FileAccess.Write))
                            {
                                using (BinaryWriter bw = new BinaryWriter(fs))
                                {
                                    bw.Write((byte[])row["imgFile"]);
                                    bw.Flush();
                                }
                            }
                            Zip zip = new Zip();
                            zip.UnlockComponent("UCINCSysZIP_4UHXoRCaDQPf");
                            zip.DiscardPaths = true;
                            zip.OpenZip(sZipFile);
                            zip.ExtractInto(TempPath);
                            zip.CloseZip();
                            zip = null;

                            UpdateBankImage(conn, sZipFile, TempPath, BankFileID, BankRoutingNumber);

                            File.Delete(sZipFile);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("MBProcessBankFiles", "ImportNonParsedImages", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }
    }

    class NSBProcessBankFiles : ProcessBankFiles
    {
        public NSBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        {
            jxChangeComModel = new JXChangeComCredentialsModel();
            //Get Interface Type Connection for JXChange operations
            GetInterfaceTypeCredentials();           
        }

        public override void ImportTransactions()
        {
            string fileName = "AccountHistory_" + _IFaceTypeID + "_" + DateTime.Now.ToString().Replace("-", string.Empty).Replace("/", string.Empty) + ".txt";
            int fileID = AddStubFileRecordToDatabase(fileName);
            ImportJXChangeTransactions(fileID);
        }
        public override void ImportImages()
        {
            ImportJXChangeImages();
        }
    }

    class BNCProcessBankFiles : ProcessBankFiles
    {
        public BNCProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles("");
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }

        public override void ImportImages()
        {
            ImportJHXMLImages();
        }
    }

    class CFBDCProcessBankFiles : ProcessBankFiles
    {
        public CFBDCProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles("*.csv");
        }

        public override void ImportTransactions()
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select numfileid,txtfile,txtfilename from fcbinterface..bankfile where numifacetypeid = {0} and txtfilename like '%.csv' and dteparsedate is null order by numfileid", _IFaceTypeID);
                    using (DataTable dtFiles = new DataTable())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtFiles);
                        }
                        foreach (DataRow row in dtFiles.Rows)
                        {
                            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(row["txtfile"].ToString())))
                            using (StreamReader sr = new StreamReader(ms))
                            {
                                if (row["txtfilename"].ToString().ToLower().IndexOf("bal") != -1)
                                {
                                    for (int i = 0; !sr.EndOfStream; i++)
                                    {
                                        string sLine = sr.ReadLine();
                                        if (i < 1)
                                            continue;
                                        if (!string.IsNullOrEmpty(sLine))
                                        {
                                            string[] aFields = ParseFields(sLine);
                                            if (aFields.Length != 5)
                                                continue;
                                            if (string.IsNullOrEmpty(aFields[1]) || string.IsNullOrEmpty(aFields[3].Replace("$", "").Replace(",", "")) || !IsNumeric(aFields[3].Replace("$", "").Replace(",", "")))
                                                continue;
                                            cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                                "values('{0}','EBAL',{1},'','{2}','','','',0,{3},{4})",
                                                sqlStr(aFields[1]), sqlStr(aFields[3].Replace("$", "").Replace(",", "")), sqlStr(aFields[4]), _IFaceTypeID, row["numfileid"]);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                                else if (row["txtfilename"].ToString().ToLower().IndexOf("tran") != -1)
                                {
                                    for (int i = 0; !sr.EndOfStream; i++)
                                    {
                                        string sLine = sr.ReadLine();
                                        if (i < 1)
                                            continue;
                                        if (!string.IsNullOrEmpty(sLine))
                                        {
                                            string[] aFields = ParseFields(sLine);
                                            if (aFields.Length != 13)
                                                continue;
                                            string sCD = "";
                                            if (aFields[4].Length >= 1)
                                                sCD = aFields[4].Substring(0, 1).ToUpper();
                                            if (string.IsNullOrEmpty(aFields[3]) || string.IsNullOrEmpty(aFields[8].Replace("$", "").Replace(",", "")) || !IsNumeric(aFields[8].Replace("$", "").Replace(",", "")))
                                                continue;
                                            cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                                "values('{0}','{1}',{2},'{3}','{4}','','{5}','{6}',0,{7},{8})",
                                                sqlStr(aFields[3]), sqlStr(aFields[7]), sqlStr(aFields[8].Replace("$", "").Replace(",", "")), sqlStr(aFields[6]), sqlStr(aFields[1]), sqlStr(aFields[5]), sCD, _IFaceTypeID, row["numfileid"]);
                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                            cmd.CommandText = String.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("CFBDCProcessBankFiles", "ImportTransactions", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }
    }

    class GBProcessBankFiles : ProcessBankFiles
    {
        public GBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            try
            {
                SftpItemCollection BankFiles = new SftpItemCollection();
                string TempPath = CreateTempFolder();

                //import BAI files
                string Site = "transfer.greenbank.com";
                int Port = 992;
                string Uid = @"CINC2";
                string Pwd = "LMy6KrFi07";
                using (Sftp sftp = new Sftp())
                {
                    sftp.Connect(Site, Port);
                    sftp.Login(Uid, Pwd);
                    sftp.ChangeDirectory("/BAI2");
                    BankFiles = sftp.GetList();
                    foreach (SftpItem bankFile in BankFiles)
                    {
                        try
                        {
                            if (bankFile.Name.ToLower().IndexOf("ba") >= 0)
                            {
                                try
                                {
                                    sftp.GetFile(bankFile.Name, TempPath + bankFile.Name);
                                    AddTextFileToDatabase(TempPath + bankFile.Name);
                                    sftp.DeleteFile(bankFile.Name);
                                }
                                catch (Exception ex)
                                {
                                    _Log.LogErr("GBProcessBankFiles", "ImportFiles", string.Format("DOWNLOAD_ERROR:{0}", ex.Message));
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("GBProcessBankFiles", "ImportFiles", string.Format("LOOP_ERROR:{0}", ex.Message));
                        }
                    }
                }

                //import x937 check image files
                Site = "secure.jhahosted.com";

                Port = 22;
                Uid = @"otlcinc";
                Pwd = "h*2U2h63";
                using (Sftp sftp = new Sftp())

                {
                    sftp.Connect(Site, Port);
                    sftp.Login(Uid, Pwd);
                    sftp.ChangeDirectory("/Home/Outlink CINC Systems/AllenIP/Outgoing");
                    BankFiles = sftp.GetList();
                    foreach (SftpItem bankFile in BankFiles)
                    {
                        try
                        {
                            if (bankFile.Name.ToLower().IndexOf("x937") >= 0)
                            {
                                try
                                {
                                    sftp.GetFile(bankFile.Name, TempPath + bankFile.Name);
                                    AddBinaryFileToDatabase(TempPath + bankFile.Name);
                                    sftp.DeleteFile(bankFile.Name);
                                }
                                catch (Exception ex)
                                {
                                    _Log.LogErr("GBProcessBankFiles", "ImportFiles", string.Format("DOWNLOAD_ERROR:{0}", ex.Message));
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("GBProcessBankFiles", "ImportFiles", string.Format("LOOP_ERROR:{0}", ex.Message));
                        }
                    }
                }
                RemoveTempFolder();
            }
            catch (Exception ex)
            {
                _Log.LogErr("GBProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions("BA%.txt");
        }

        public override void ImportImages()
        {
            Parsex937NSFBankImages("%x937%");
        }
    }

    class MPBProcessBankFiles : ProcessBankFiles
    {
        public MPBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles(GetAppSetting("transFilePattern"));
            ImportTextFiles(GetAppSetting("balFilePattern"));
        }

        public override void ImportTransactions()
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    DateTime TranDate = DateTime.MinValue;
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select numfileid, txtfilename, txtfile, dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '{1}' order by numfileid", _IFaceTypeID, GetAppSetting("transFilePattern"));
                    using (DataTable BankFiles = new DataTable())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(BankFiles);
                        }
                        foreach (DataRow row in BankFiles.Rows)
                        {
                            int BankFileID = Convert.ToInt32(row["numfileid"].ToString());
                            string FileContents = row["txtfile"].ToString();
                            using (MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(FileContents)))
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                string Line = string.Empty;
                                for (int i = 0; !reader.EndOfStream; i++)
                                {
                                    Line = reader.ReadLine();
                                    if (i == 0)
                                        continue;
                                    //sRecord: Account #,Account Name,Tran Date,Trans Description,Check Number,Tran Amount,Tran Code
                                    string[] Fields = ParseFields(Line);
                                    string TranCode = GetTranCode(Fields[6].Trim(), Fields[3].Replace("/", "").Trim());
                                    TranDate = Convert.ToDateTime(Fields[2].Trim());
                                    cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                        "values ('{0}','{1}',{2},'{3}','{4}','{5}','{6}','{7}',0,{8},{9}); select @@identity",
                                        sqlStr(Fields[0].Trim()), sqlStr(Fields[6].Trim()), decimal.Parse(Fields[5].Trim()), sqlStr(Fields[4].Trim()), Fields[2].Trim(), "", sqlStr(Fields[3].Replace("/", "").Trim()), TranCode, _IFaceTypeID, BankFileID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", BankFileID);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    cmd.CommandText = string.Format("select numfileid, txtfilename, txtfile, dtefiledate from fcbinterface..bankfile where dteparsedate is null and numifacetypeid = {0} and txtfilename like '{1}' order by numfileid", _IFaceTypeID, GetAppSetting("balFilePattern"));
                    using (DataTable BankFiles = new DataTable())
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(BankFiles);
                        }
                        foreach (DataRow row in BankFiles.Rows)
                        {
                            int BankFileID = Convert.ToInt32(row["numfileid"].ToString());
                            string FileName = row["txtfilename"].ToString();
                            string FileContents = row["txtfile"].ToString();
                            using (MemoryStream stream = new MemoryStream(Encoding.ASCII.GetBytes(FileContents)))
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    DateTime BalDate = TranDate.AddDays(1);
                                    if (FileName.Length >= 18)
                                    {
                                        string Temp = string.Format("{0}/{1}/{2}", FileName.Substring(FileName.Length - 8, 2), FileName.Substring(FileName.Length - 6, 2), FileName.Substring(FileName.Length - 12, 4));
                                        if (IsDate(Temp))
                                            BalDate = DateTime.Parse(Temp);
                                    }
                                    for (int i = 0; !reader.EndOfStream; i++)
                                    {
                                        string Line = reader.ReadLine();
                                        if (i == 0)
                                            continue;
                                        //Account #,Account Name,Current Balance
                                        string[] Fields = ParseFields(Line);
                                        cmd.CommandText = string.Format("select cincinternal.dbo.GetPreviousBusinessDay('{0:d}')", BalDate);
                                        DateTime TempDate = Convert.ToDateTime(cmd.ExecuteScalar().ToString());
                                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                            "values ('{0}','EBAL',{1},'','{2}','','{3}','',0,{4},{5}); select @@identity",
                                            sqlStr(Fields[0].Trim()), decimal.Parse(Fields[2].Trim()), TempDate, sqlStr(Fields[1].Trim().Replace("/", "")), _IFaceTypeID, BankFileID);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            cmd.CommandText = string.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", BankFileID);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", BankFileID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("MPBProcessBankFiles", "ImportTransactions", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }
    }

    class QBProcessBankFiles : ProcessBankFiles
    {
        public QBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles("*.dat");
        }

        public override void ImportTransactions()
        {
            ImportFISERVTransactions();
        }
    }

    class VNBProcessBankFiles : ProcessBankFiles
    {
        public VNBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            try
            {
                //import BAI files
                foreach (string existingFile in Directory.GetFiles(GetAppSetting("pathBAI")))
                {
                    if (existingFile.ToLower().IndexOf("bai") >= 0)
                    {
                        try
                        {
                            AddTextFileToDatabase(existingFile);
                            File.Delete(existingFile);
                        }
                        catch (Exception ex)
                        {
                            _Log.LogErr("VNBProcessBankFiles", "ImportFiles", string.Format("DOWNLOAD_ERROR:{0}", ex.Message));
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _Log.LogErr("VNBProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }

            try
            {
                string filePattern = GetAppSetting("imagefilepattern");
                string extractPath = CreateFolder("extract");
                string zipCheckPath = GetAppSetting("pathXML");
                string ckfileName = string.Empty;

                foreach (string fileName in Directory.GetFiles(zipCheckPath, filePattern))
                {
                    FileInfo info = new FileInfo(fileName);
                    ckfileName = "CK_" + Path.GetFileName(fileName);
                    info.MoveTo(zipCheckPath + ckfileName);
                    AddBinaryFileToDatabase(zipCheckPath + ckfileName, false);
                    File.Delete(fileName);
                }

                RemoveFolder("extract");
            }
            catch (Exception ex)
            {
                _Log.LogErr("VNBProcessBankFiles", "ImportFiles", string.Format("ERR:{0}", ex.Message));
            }
        }

        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }

        public override void ImportImages()
        {
            ImportXMLImages();
        }

    }

    class FCBProcessBankFiles : ProcessBankFiles
    {
        public FCBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        /* No functions overridden, trans are imported through web service
        and ProcessTransactions will be called in base class. */
    }

    class FCNBProcessBankFiles : ProcessBankFiles
    {
        public FCNBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        /* No functions overridden, trans are imported through web service
        and ProcessTransactions will be called in base class. */
    }

    class AMBProcessBankFiles : ProcessBankFiles
    {
        public AMBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        {
            jxChangeComModel = new JXChangeComCredentialsModel();
            //Get Interface Type Connection for JXChange operations
            GetInterfaceTypeCredentials();            
        }

        /// <summary>
        /// This method is used to imort transactions
        /// </summary>
        public override void ImportTransactions()
        {
            string fileName = "AccountHistory_" + _IFaceTypeID + "_" + DateTime.Now.ToString().Replace("-", string.Empty).Replace("/", string.Empty) + ".txt";
            int fileID = AddStubFileRecordToDatabase(fileName);
            ImportJXChangeTransactions(fileID);
        }

        /// <summary>
        /// This method is used to imort import images
        /// </summary>
        public override void ImportImages()
        {
            ImportJXChangeImages();
        }
    }

    class PCBProcessBankFiles : ProcessBankFiles
    {
        public PCBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles("*.csv");
        }

        public override void ImportTransactions()
        {
            SqlConnection conn = GetPrimaryConnection();
            using (SqlCommand cmd = new SqlCommand())
            {
                try
                {
                    cmd.CommandTimeout = 1200;
                    cmd.Connection = conn;
                    cmd.CommandText = string.Format("select cincinternal.dbo.GetPreviousBusinessDay('{0:d}')", DateTime.Now);
                    DateTime TransDate = Convert.ToDateTime(string.Format("{0:d}", Convert.ToDateTime(cmd.ExecuteScalar().ToString())));

                    using (DataTable dtFiles = new DataTable())
                    {
                        cmd.CommandText = string.Format("select numfileid,txtfile,txtfilename from fcbinterface..bankfile where numifacetypeid = {0} and txtfilename like '%trans%.csv' and dteparsedate is null order by numfileid", _IFaceTypeID);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtFiles);
                        }
                        foreach (DataRow row in dtFiles.Rows)
                        {
                            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(row["txtfile"].ToString())))
                            using (StreamReader sr = new StreamReader(ms))
                            {
                                for (int i = 0; !sr.EndOfStream; i++)
                                {
                                    string sLine = sr.ReadLine();
                                    if (i < 1) //skip file header
                                        continue;
                                    if (!string.IsNullOrEmpty(sLine))
                                    {
                                        string[] aFields = ParseFields(sLine);
                                        if (aFields.Length != 7)
                                            continue;
                                        TransDate = Convert.ToDateTime(aFields[0]);
                                        string AccountNumber = aFields[1];
                                        string Description = aFields[2];
                                        string TranCode = aFields[3];
                                        string Amount = aFields[4];
                                        string CheckNumber = aFields[5];
                                        string sCD = GetTranCode(TranCode, Description);
                                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                            "values('{0}','{1}',{2},'{3}','{4:d}','','{5}','{6}',0,{7},{8})",
                                            sqlStr(AccountNumber), sqlStr(TranCode), sqlStr(Amount.Replace("$", "").Replace(",", "")), sqlStr(CheckNumber), TransDate, sqlStr(Description), sCD, _IFaceTypeID, row["numfileid"]);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            cmd.CommandText = String.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();

                        }
                        dtFiles.Clear();

                        cmd.CommandText = string.Format("select numfileid,txtfile,txtfilename from fcbinterface..bankfile where numifacetypeid = {0} and txtfilename like '%bal%.csv' and dteparsedate is null order by numfileid", _IFaceTypeID);
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dtFiles);
                        }
                        foreach (DataRow row in dtFiles.Rows)
                        {
                            using (MemoryStream ms = new MemoryStream(Encoding.ASCII.GetBytes(row["txtfile"].ToString())))
                            using (StreamReader sr = new StreamReader(ms))
                            {
                                for (int i = 0; !sr.EndOfStream; i++)
                                {
                                    string sLine = sr.ReadLine();
                                    if (i < 1)
                                        continue;
                                    if (!string.IsNullOrEmpty(sLine))
                                    {
                                        string[] aFields = ParseFields(sLine);
                                        if (aFields.Length != 3)
                                            continue;
                                        string AccountNumber = aFields[0];
                                        string Balance = aFields[1];
                                        cmd.CommandText = string.Format("insert into fcbinterface..banktransaction (txtaccountnumber,txtcode,mnyamount,txtchecknumber,dtetrandate,txttracenumber,txtdescr,txtcreditdebit,mnyinterest,numifacetypeid,numfileid) " +
                                            "values('{0}','EBAL',{1},'','{2:d}','','','',0,{3},{4})",
                                            sqlStr(AccountNumber), sqlStr(Balance.Replace("$", "").Replace(",", "")), TransDate, _IFaceTypeID, row["numfileid"]);
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            cmd.CommandText = String.Format("update fcbinterface..bankfile set dteparsedate = getdate() where numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                            cmd.CommandText = string.Format("update fcbinterface..banktransaction set txtAccountNumber = dbo.TrimZeros(txtAccountNumber) where txtAccountNumber LIKE '0%' and numfileid = {0}", row["numfileid"]);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Log.LogErr("PCBProcessBankFiles", "ImportTransactions", ex.Message + " SQL:" + cmd.CommandText);
                }
            }
        }
    }

    class SCBProcessBankFiles : ProcessBankFiles
    {
        public SCBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            ImportTextFiles("*.dat");
        }

        public override void ImportTransactions()
        {
            ImportFISERVTransactions();
        }
    }
    class IBProcessBankFiles : ProcessBankFiles
    {
        public IBProcessBankFiles(int iFaceTypeID)
            : base(iFaceTypeID)
        { }

        public override void ImportFiles()
        {
            string filePattern = GetAppSetting("filepattern");
            ImportTextFiles(filePattern);
        }
        public override void ImportTransactions()
        {
            ImportBAITransactions();
        }
    }
}
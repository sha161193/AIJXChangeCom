// ***********************************************************************
// Assembly         : AIJXChangeCom
// Author           : Invezza Team (Sagar Shinde)
// Created          : 07-18-2018
// Last Modified By : Invezza Team (Sagar Shinde)
// Last Modified On : 07-23-2018
// Last Modified Comments : 
// ***********************************************************************
// <copyright file="JXChangeCom.cs" company="CINC Systems">
//     Copyright ©  2018
// </copyright>
// <summary></summary>
// ***********************************************************************

using InquiryMaster;
using System;
using System.IO;
using System.ServiceModel;
using System.Xml.Serialization;

namespace AIJXChangeCom.JXChangeClass
{
	/// <summary>
	/// JXChangeCom Class
	/// </summary>
	public class JXChangeCom
	{
		AcctHistSrchRequest request;
		InquiryServiceClient proxy;
		SrchMsgRqHdr_CType srchMsgRqHdr;
		public string LogTrackingID { get; set; }
		TransactionServiceClient transactionProxy;
        JXChangeComCredentialsModel jxChangeComModel;

        /// <summary>
        /// JXChangeCom Construtor
        /// </summary>
        /// <param name="jxChangeComModel">JXChangeComModel jxChangeComModel</param>
        public JXChangeCom(JXChangeComCredentialsModel jxChangeComModel)
		{
			EndpointIdentity spn = EndpointIdentity.CreateSpnIdentity("host/mikev-ws");
			Uri uri = new Uri(jxChangeComModel.EndPointURL);
			var address = new EndpointAddress(uri, spn);
			var binding = new BasicHttpBinding("InquiryServiceSoapBinding");
			proxy = new InquiryServiceClient(binding, address);
			transactionProxy = new TransactionServiceClient(binding, address);
            request = new AcctHistSrchRequest();
			srchMsgRqHdr = new SrchMsgRqHdr_CType();
            this.jxChangeComModel = jxChangeComModel;            
        }

		/// <summary>
		/// Added by Invezza Team 
		/// InitializeAccountHistSrch method to Initlalize request
		/// </summary>
		/// <param name="jxChangeComModel">JXChangeComModel jxChangeComModel</param>
		public void InitializeAccountHistSrch(JXChangeComAcctSrchModel jxChangeComAcctSrchModel)
		{
			////request header
			srchMsgRqHdr.MaxRec = new MaxRec_Type
			{
				Value = jxChangeComAcctSrchModel.MaxRecords
			};
			request.SrchMsgRqHdr = srchMsgRqHdr;
			request.SrchMsgRqHdr.jXchangeHdr = CreateJXChangeInquiryHeader();

			request.InAcctId = new InquiryMaster.AccountId_CType
			{
				AcctId = new InquiryMaster.AcctId_Type()
			};
			request.InAcctId.AcctId.Value = jxChangeComAcctSrchModel.AccountId;// "1881";
			request.InAcctId.AcctType = new InquiryMaster.AcctType_Type
			{
				Value = jxChangeComAcctSrchModel.AccountType
			};

			request.StartDt = new StartDt_Type
			{
				Value = jxChangeComAcctSrchModel.StartDate// new DateTime(2000, 02, 24);
			};
			request.EndDt = new EndDt_Type
			{
				Value = jxChangeComAcctSrchModel.EndDate//new DateTime(2018, 03, 19);
			};

			//proxy credentials
			if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.UserName))
				proxy.ClientCredentials.UserName.UserName = jxChangeComAcctSrchModel.JXChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";

			if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.Password))
				proxy.ClientCredentials.UserName.Password = jxChangeComAcctSrchModel.JXChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ";

			request.ChkNumStart = new InquiryMaster.ChkNumStart_Type();
			request.ChkNumEnd = new InquiryMaster.ChkNumEnd_Type();
			request.HighAmt = new InquiryMaster.HighAmt_Type();
			request.LowAmt = new InquiryMaster.LowAmt_Type();

			request.Ver_1 = new InquiryMaster.Ver_1_CType();
			request.Ver_2 = new InquiryMaster.Ver_2_CType();
			request.Ver_3 = new InquiryMaster.Ver_3_CType();
			request.Ver_4 = new InquiryMaster.Ver_4_CType();
			request.SrchMsgRqHdr.Cursor = new Cursor_Type();
		}

		/// <summary>
		/// Added by Invezza Team
		/// GetAccountHistory method to Get Account details from JXChange API
		/// </summary>
		/// <param name="currentCursor"></param>
		/// <returns></returns>
		public AcctHistSrchResponse GetAccountHistory(int currentCursor, ref JXChangeComReqRespModel jxChangeBankTrans)
		{
			AcctHistSrchResponse aresp = null;

			try
			{
				if (currentCursor != 0)
					request.SrchMsgRqHdr.Cursor.Value = currentCursor.ToString();

				aresp = proxy.AcctHistSrch(request);

				////These fields are used to maintain Jack Henry Log Details
				jxChangeBankTrans.Request = ObjectToXml(request);
                if (aresp == null)
                {
                    jxChangeBankTrans.Response = String.Format("Missing Account, AccountId: {0}", request.InAcctId.AcctId.Value);
                }
                else
                    jxChangeBankTrans.Response = ObjectToXml(aresp);

				jxChangeBankTrans.LogTrackingId = LogTrackingID;
				jxChangeBankTrans.LogType = "Info";
			}
			catch (Exception ex)
			{
                ////This field is used to maintain Jack Henry Exception Log Details
                jxChangeBankTrans.ExceptionMessage = ObjectToXml(ex);
				jxChangeBankTrans.LogType = "Error";
			}
			return aresp;
		}
        /// <summary>
		/// Added by Invezza Team
		/// Create Inquiry Header.
		/// </summary>
		/// <param name="jxChangeComModel">JXChangeComModel jxChangeComModel</param>
		/// <returns>returns jXchangeHdr_CType</returns>
		public InquiryMaster.jXchangeHdr_CType CreateJXChangeInquiryHeader()
        {
            string LogTrackingID = Guid.NewGuid().ToString();
            InquiryMaster.jXchangeHdr_CType jXchangeHdr = new InquiryMaster.jXchangeHdr_CType
            {
                AuditUsrId = new InquiryMaster.AuditUsrId_Type
                {
                    Value = jxChangeComModel.AuditUserId//Test";
                },
                AuditWsId = new InquiryMaster.AuditWsId_Type()
            };
            jXchangeHdr.AuditWsId.Value = jxChangeComModel.AuditWsId;//"Test";
            jXchangeHdr.Ver_1 = new InquiryMaster.Ver_1_CType();
            jXchangeHdr.jXLogTrackingId = new InquiryMaster.jXLogTrackingId_Type
            {
                Value = LogTrackingID
            };
            jXchangeHdr.Ver_2 = new InquiryMaster.Ver_2_CType();
            jXchangeHdr.InstRtId = new InquiryMaster.InstRtId_Type
            {
                Value = jxChangeComModel.InstRtId//"011001276";
            };
            jXchangeHdr.InstEnv = new InquiryMaster.InstEnv_Type
            {
                Value = jxChangeComModel.InstEnv//"TEST";
            };
            jXchangeHdr.Ver_3 = new InquiryMaster.Ver_3_CType();
            jXchangeHdr.BusCorrelId = new InquiryMaster.BusCorrelId_Type
            {
                Value = Guid.NewGuid().ToString()
            };
            jXchangeHdr.Ver_4 = new InquiryMaster.Ver_4_CType();
            jXchangeHdr.Ver_5 = new InquiryMaster.Ver_5_CType();
            jXchangeHdr.ValidConsmName = new InquiryMaster.ValidConsmName_Type
            {
                Value = jxChangeComModel.ValidConsName//"AccountingIntegrators";
            };
            jXchangeHdr.ValidConsmProd = new InquiryMaster.ValidConsmProd_Type
            {
                Value = jxChangeComModel.ValidConsProd //"AccountingIntegrators";
            };

            return jXchangeHdr;
        }

        /// <summary>
		/// Added by Invezza Team
		/// ObjectToXml method to create XML from Object
		/// </summary>
		/// <param name="output">object output</param>
		/// <returns>returns xml in string</returns>
		private string ObjectToXml(object output)
        {
            string objectAsXmlString;

            using (StringWriter sw = new StringWriter())
            {
                try
                {
                    XmlSerializer xs = new XmlSerializer(output.GetType());
                    xs.Serialize(sw, output);
                    objectAsXmlString = sw.ToString();
                }
                catch (Exception ex)
                {
                    objectAsXmlString = ex.ToString();
                }
            }

            return objectAsXmlString;
        }
       
        /// <summary>
        /// Added by Invezza Team
        /// GetAccountSearch method to get End of Balance
        /// </summary>
        /// <param name="jxChangeComModel"></param>
        /// <returns></returns>
        public AcctSrchResponse GetAccountSrch(string acctid, ref string accttype, string clientUserName, string clientPwd, string maxRecords, ref JXChangeComReqRespModel jxChangeBankTrans)
        {
            try
            {
                EndpointIdentity spn = EndpointIdentity.CreateSpnIdentity("host/mikev-ws");
                Uri uri = new Uri(this.jxChangeComModel.EndPointURL);
                var address = new EndpointAddress(uri, spn);
                var binding = new BasicHttpBinding("InquiryServiceSoapBinding");
                InquiryServiceClient proxy = new InquiryServiceClient(binding, address);
				//proxy credentials
				if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.UserName))
					proxy.ClientCredentials.UserName.UserName = clientUserName;//"AccountingIntegrators@jxtest.local";             
				if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.Password))
					proxy.ClientCredentials.UserName.Password = clientPwd;//"!Eto4N6CjQ"; 

                //request header
                InquiryMaster.AcctSrchRequest request = new InquiryMaster.AcctSrchRequest();
                request.AcctType = new InquiryMaster.AcctType_Type();
                request.AcctId = new InquiryMaster.AcctId_Type();
                request.SrchMsgRqHdr = new InquiryMaster.SrchMsgRqHdr_CType();
                request.SrchMsgRqHdr.jXchangeHdr = CreateJXChangeInquiryHeader();
                request.SrchMsgRqHdr.MaxRec = new InquiryMaster.MaxRec_Type();
                request.SrchMsgRqHdr.MaxRec.Value =  Convert.ToInt32(maxRecords);
                request.AcctId.Value = acctid;
              //  request.AcctType.Value = accttype;

                request.Ver_1 = new InquiryMaster.Ver_1_CType();
                request.Ver_2 = new InquiryMaster.Ver_2_CType();
                request.Ver_3 = new InquiryMaster.Ver_3_CType();            

                //send request
                AcctSrchResponse aresp = proxy.AcctSrch(request);
                ///These fields are used to maintain Jack Henry Log Details
                 jxChangeBankTrans.Request = ObjectToXml(request);
                 if (aresp == null)
                {
                    jxChangeBankTrans.Response = String.Format("Missing Account, AccountId: {0}", request.AcctId.Value);
                }
                else
                    jxChangeBankTrans.Response = ObjectToXml(aresp);

                  jxChangeBankTrans.LogTrackingId = LogTrackingID;
                  jxChangeBankTrans.LogType = "Info";
                                  
                return aresp;
            }
            catch (Exception ex)
            {
                jxChangeBankTrans.ExceptionMessage = ObjectToXml(ex);
                jxChangeBankTrans.LogType = "Error";
            }

            return null;
        }

       
        /// <summary>
        /// Added by Invezza Team
        /// AddTransaction method to add new transaction
        /// </summary>
        /// <param name="jxChangeComModel"></param>
        /// <returns></returns>
        public XferAddResponse XferAdd(JXChangeComTransferModel jxChangeComModel, ref JXChangeComReqRespModel jxChangeBankTrans)
		{
			XferAddResponse response = null;

			try
			{
				//request header
				XferAddRequest request = new XferAddRequest
				{
					MsgRqHdr = new MsgRqHdr_CType()
				};
				request.MsgRqHdr.jXchangeHdr = CreateJXChangeTransactionHeader(jxChangeComModel);
				request.Ver_1 = new Ver_1_CType();
				request.Ver_2 = new Ver_2_CType();
				request.Ver_3 = new Ver_3_CType();

				request.AcctIdFrom = new AcctIdFrom_CType
				{
					FromAcctId = new AcctId_Type()
				};
				request.AcctIdFrom.FromAcctId.Value = jxChangeComModel.FromAccountId;
				request.AcctIdFrom.FromAcctType = new AcctType_Type
				{
					Value = jxChangeComModel.FromAccountType
				};

				request.AcctIdTo = new AcctIdTo_CType
				{
					ToAcctId = new AcctId_Type()
				};
				request.AcctIdTo.ToAcctId.Value = jxChangeComModel.ToAccountId;
				request.AcctIdTo.ToAcctType = new AcctType_Type
				{
					Value = jxChangeComModel.ToAccountType
				};

				request.XferType = new XferType_Type();
				request.XferType.Value = jxChangeComModel.XferType;

				request.ErrOvrRdInfoArray = new ErrOvrRd_CType[3];
				request.ErrOvrRdInfoArray[0] = new ErrOvrRd_CType();
				request.ErrOvrRdInfoArray[1] = new ErrOvrRd_CType();
				request.ErrOvrRdInfoArray[2] = new ErrOvrRd_CType();

				request.ErrOvrRdInfoArray[0].ErrCode = new ErrCode_Type();
				request.ErrOvrRdInfoArray[1].ErrCode = new ErrCode_Type();
				request.ErrOvrRdInfoArray[2].ErrCode = new ErrCode_Type();

				request.ErrOvrRdInfoArray[0].ErrCode.Value = "500019";
				request.ErrOvrRdInfoArray[1].ErrCode.Value = "500008";
				request.ErrOvrRdInfoArray[2].ErrCode.Value = "500025";
				//'500021, 500003

				request.ErrOvrRdInfoArray[0].Ver_1 = new Ver_1_CType();
				request.ErrOvrRdInfoArray[1].Ver_1 = new Ver_1_CType();
				request.ErrOvrRdInfoArray[2].Ver_1 = new Ver_1_CType();

				if (jxChangeComModel.XferType == "Xfer" || jxChangeComModel.XferType == "ACH")
				{
					CreateXferTypeRequest(request, jxChangeComModel);
				}

				if (jxChangeComModel.XferType == "ACH")
				{
					CreateACHTypeRequest(request, jxChangeComModel);
				}
				else
				if (jxChangeComModel.XferType == "Fut")
				{
					CreateFUTTypeRequest(request, jxChangeComModel);
				}

				//proxy credentials
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.UserName))
					transactionProxy.ClientCredentials.UserName.UserName = jxChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.Password))
					transactionProxy.ClientCredentials.UserName.Password = jxChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ";

				var validateTransactionResponse = XferAddValidate(jxChangeComModel);
				if (validateTransactionResponse.RsStat != null && validateTransactionResponse.RsStat.Value == "Success")
				{
					response = transactionProxy.XferAdd(request);
				}
				string xmlResponse = ObjectToXml(response);
			}
			catch (Exception ex)
			{
				var exObject = ((System.ServiceModel.FaultException<HdrFault_MType>)ex).Detail.FaultRecInfoArray[0];

			}
			return response;
		}

		/// <summary>
		/// Added by Invezza Team
		/// To create Xfer Type Request
		/// </summary>
		/// <param name="request"></param>
		/// <param name="jxChangeComModel"></param>
		/// <returns></returns>
		public void CreateXferTypeRequest(XferAddRequest request, JXChangeComTransferModel jxChangeComModel)
		{
			request.XferRec = new XferRec_CType();
			request.XferRec.Amt = new Amt_Type();
			request.XferRec.Amt.Value = jxChangeComModel.Amount;
			request.XferRec.Ver_1 = new Ver_1_CType();
			request.XferRec.Ver_2 = new Ver_2_CType();
			request.XferRec.Ver_3 = new Ver_3_CType();
			request.XferRec.Ver_4 = new Ver_4_CType();
			request.XferRec.Ver_5 = new Ver_5_CType();
			request.XferRec.AvlBalCalcCode = new AvlBalCalcCode_Type();
			request.XferRec.RedPrinc = new RedPrinc_Type { Value = jxChangeComModel.RedPrinc };
			request.XferRec.XferSrcType = new XferSrcType_Type();
			request.XferRec.XferSrcType.Value = "Intnet";
			request.XferRec.Fee = new Fee_Type() { Value = jxChangeComModel.ACHFeeAmt };
		}

		/// <summary>
		/// Added by Invezza Team
		/// To create ACH Type Request
		/// </summary>
		/// <param name="request"></param>
		/// <param name="jxChangeComModel"></param>
		/// <returns></returns>
		public void CreateACHTypeRequest(XferAddRequest request, JXChangeComTransferModel jxChangeComModel)
		{
			request.ACHXferRec = new ACHXferRec_CType();
			request.ACHXferRec.Ver_1 = new Ver_1_CType();
			request.ACHXferRec.Ver_2 = new Ver_2_CType();
			request.ACHXferRec.Ver_3 = new Ver_3_CType();
			request.ACHXferRec.Ver_4 = new Ver_4_CType();
			request.ACHXferRec.Ver_5 = new Ver_5_CType();

			request.ACHXferRec.RedPrinc = new RedPrinc_Type();
			request.ACHXferRec.RedPrinc.Value = jxChangeComModel.RedPrinc;

			request.ACHXferRec.XferBalType = new XferBalType_Type();

			request.ACHXferRec.ACHXferAmt = new ACHXferAmt_Type();
			request.ACHXferRec.ACHXferAmt.Value = jxChangeComModel.Amount;

			request.ACHXferRec.ACHDrRtNum = new ACHDrRtNum_Type() { Value = jxChangeComModel.ACHDrRtNum };
			request.ACHXferRec.ACHDrName = new ACHDrName_Type() { Value = jxChangeComModel.ACHDrName };
			request.ACHXferRec.ACHNextXferDt = new ACHNextXferDt_Type() { Value = DateTime.Now };

			request.ACHXferRec.ACHDrAcctId = new ACHDrAcctId_Type() { Value = jxChangeComModel.FromAccountId };
			request.ACHXferRec.ACHDrAcctType = new ACHDrAcctType_Type() { Value = jxChangeComModel.FromAccountType };

			request.ACHXferRec.ACHCrAcctId = new ACHCrAcctId_Type() { Value = jxChangeComModel.ToAccountId };
			request.ACHXferRec.ACHCrAcctType = new ACHCrAcctType_Type() { Value = jxChangeComModel.ToAccountType };

			request.ACHXferRec.ACHFeeDrAcctId = new ACHFeeDrAcctId_Type() { Value = jxChangeComModel.ToAccountId };
			request.ACHXferRec.ACHFeeDrAcctType = new ACHFeeDrAcctType_Type() { Value = jxChangeComModel.ToAccountType };
			request.ACHXferRec.ACHFeeAmt = new ACHFeeAmt_Type() { Value = jxChangeComModel.ACHFeeAmt };
		}

		/// <summary>
		/// Added by Invezza Team
		/// To create FUT Type Request
		/// </summary>
		/// <param name="request"></param>
		/// <param name="jxChangeComModel"></param>
		/// <returns></returns>
		public void CreateFUTTypeRequest(XferAddRequest request, JXChangeComTransferModel jxChangeComModel)
		{
			request.FutXferRec = new FutXferRec_CType();
			request.FutXferRec.Ver_1 = new Ver_1_CType();
			request.FutXferRec.Ver_2 = new Ver_2_CType();
			request.FutXferRec.Ver_3 = new Ver_3_CType();
			request.FutXferRec.Ver_4 = new Ver_4_CType();
			request.FutXferRec.Ver_5 = new Ver_5_CType();
			request.FutXferRec.TrnCodeCode = new TrnCodeCode_Type { Value = jxChangeComModel.TransactionCode };
			request.FutXferRec.FutXferDayOfMonth = new FutXferDayOfMonth_Type() { Value = jxChangeComModel.FutXferDayOfMonth };
			request.FutXferRec.FutXferFreqUnits = new FutXferFreqUnits_Type() { Value = jxChangeComModel.FutXferFreqUnits };
			request.FutXferRec.FutXferFreq = new FutXferFreq_Type() { Value = jxChangeComModel.FutXferFreq };
			request.FutXferRec.FutXferFirstDt = new FutXferFirstDt_Type() { Value = jxChangeComModel.FutXferFirstDt };
			request.FutXferRec.FutXferExpDt = new FutXferExpDt_Type() { Value = jxChangeComModel.FutXferExpDt };
			request.FutXferRec.Amt = new Amt_Type() { Value = jxChangeComModel.Amount };
		}

		/// <summary>
		/// Added by Invezza Team
		/// ValidateTransaction method to validate transaction to be add
		/// </summary>
		/// <param name="jxChangeComModel"></param>
		/// <returns></returns>
		public XferAddValidateResponse XferAddValidate(JXChangeComTransferModel jxChangeComModel)
		{
			XferAddValidateResponse response = null;

			//request header
			try
			{
				XferAddValidateRequest request = new XferAddValidateRequest();
				request.XferAdd = new XferAddRq_MType
				{
					MsgRqHdr = new MsgRqHdr_CType()
				};
				request.Ver_1 = new Ver_1_CType();
				request.XferAdd.MsgRqHdr.jXchangeHdr = CreateJXChangeTransactionHeader(jxChangeComModel);
				request.XferAdd.ErrOvrRdInfoArray = new ErrOvrRd_CType[] { };
				request.XferAdd.Ver_1 = new Ver_1_CType();
				request.XferAdd.Ver_2 = new Ver_2_CType();
				request.XferAdd.Ver_3 = new Ver_3_CType();
				request.XferAdd.AcctIdFrom = new AcctIdFrom_CType
				{
					FromAcctId = new AcctId_Type()
				};
				request.XferAdd.AcctIdFrom.FromAcctId.Value = jxChangeComModel.FromAccountId;
				request.XferAdd.AcctIdFrom.FromAcctType = new AcctType_Type
				{
					Value = jxChangeComModel.FromAccountType
				};

				request.XferAdd.AcctIdTo = new AcctIdTo_CType
				{
					ToAcctId = new AcctId_Type()
				};
				request.XferAdd.AcctIdTo.ToAcctId.Value = jxChangeComModel.ToAccountId;
				request.XferAdd.AcctIdTo.ToAcctType = new AcctType_Type
				{
					Value = jxChangeComModel.ToAccountType
				};

				request.XferAdd.XferType = new XferType_Type();
				request.XferAdd.XferType.Value = jxChangeComModel.XferType;

				request.XferAdd.ErrOvrRdInfoArray = new ErrOvrRd_CType[3];
				request.XferAdd.ErrOvrRdInfoArray[0] = new ErrOvrRd_CType();
				request.XferAdd.ErrOvrRdInfoArray[1] = new ErrOvrRd_CType();
				request.XferAdd.ErrOvrRdInfoArray[2] = new ErrOvrRd_CType();

				request.XferAdd.ErrOvrRdInfoArray[0].ErrCode = new ErrCode_Type();
				request.XferAdd.ErrOvrRdInfoArray[1].ErrCode = new ErrCode_Type();
				request.XferAdd.ErrOvrRdInfoArray[2].ErrCode = new ErrCode_Type();

				request.XferAdd.ErrOvrRdInfoArray[0].ErrCode.Value = "500019";
				request.XferAdd.ErrOvrRdInfoArray[1].ErrCode.Value = "500008";
				request.XferAdd.ErrOvrRdInfoArray[2].ErrCode.Value = "500025";
				//'500021, 500003

				request.XferAdd.ErrOvrRdInfoArray[0].Ver_1 = new Ver_1_CType();
				request.XferAdd.ErrOvrRdInfoArray[1].Ver_1 = new Ver_1_CType();
				request.XferAdd.ErrOvrRdInfoArray[2].Ver_1 = new Ver_1_CType();

				if (jxChangeComModel.XferType == "Xfer" || jxChangeComModel.XferType == "ACH")
				{
					request.XferAdd.XferRec = new XferRec_CType();
					request.XferAdd.XferRec.Ver_1 = new Ver_1_CType();
					request.XferAdd.XferRec.Ver_2 = new Ver_2_CType();
					request.XferAdd.XferRec.Ver_3 = new Ver_3_CType();
					request.XferAdd.XferRec.TrnCodeCode = new TrnCodeCode_Type();
					request.XferAdd.XferRec.TrnCodeCode.Value = jxChangeComModel.TransactionCode;
					request.XferAdd.XferRec.Amt = new Amt_Type() { Value = jxChangeComModel.Amount };
					request.XferAdd.XferRec.RedPrinc = new RedPrinc_Type() { Value = jxChangeComModel.RedPrinc };
				}

				if (jxChangeComModel.XferType == "ACH")
				{
					request.XferAdd.ACHXferRec = new ACHXferRec_CType();
					request.XferAdd.ACHXferRec.RedPrinc = new RedPrinc_Type() { Value = jxChangeComModel.RedPrinc };
					request.XferAdd.ACHXferRec.Ver_1 = new Ver_1_CType();
					request.XferAdd.ACHXferRec.Ver_2 = new Ver_2_CType();
					request.XferAdd.ACHXferRec.Ver_3 = new Ver_3_CType();
					request.XferAdd.ACHXferRec.Ver_4 = new Ver_4_CType();
					request.XferAdd.ACHXferRec.Ver_5 = new Ver_5_CType();
					request.XferAdd.ACHXferRec.XferBalType = new XferBalType_Type();
					request.XferAdd.ACHXferRec.ACHXferAmt = new ACHXferAmt_Type() { Value = jxChangeComModel.Amount };
					request.XferAdd.ACHXferRec.ACHDrName = new ACHDrName_Type() { Value = jxChangeComModel.ACHDrName };
					request.XferAdd.ACHXferRec.ACHNextXferDt = new ACHNextXferDt_Type() { Value = DateTime.Now };
					request.XferAdd.ACHXferRec.ACHDrAcctId = new ACHDrAcctId_Type() { Value = jxChangeComModel.FromAccountId };
					request.XferAdd.ACHXferRec.ACHDrAcctType = new ACHDrAcctType_Type() { Value = jxChangeComModel.FromAccountType };
					request.XferAdd.ACHXferRec.ACHCrAcctId = new ACHCrAcctId_Type() { Value = jxChangeComModel.ToAccountId };
					request.XferAdd.ACHXferRec.ACHCrAcctType = new ACHCrAcctType_Type() { Value = jxChangeComModel.ToAccountType };
				}
				else
				if (jxChangeComModel.XferType == "Fut")
				{
					request.XferAdd.FutXferRec = new FutXferRec_CType();
					request.XferAdd.FutXferRec.Amt = new Amt_Type() { Value = jxChangeComModel.Amount };
					request.XferAdd.FutXferRec.EftDescArray = new EftDescInfo_CType[] { };
					request.XferAdd.FutXferRec.Ver_1 = new Ver_1_CType();
					request.XferAdd.FutXferRec.Ver_2 = new Ver_2_CType();
					request.XferAdd.FutXferRec.Ver_3 = new Ver_3_CType();
					request.XferAdd.FutXferRec.Ver_4 = new Ver_4_CType();
					request.XferAdd.FutXferRec.Ver_5 = new Ver_5_CType();
					request.XferAdd.FutXferRec.TrnCodeCode = new TrnCodeCode_Type { Value = jxChangeComModel.TransactionCode };
					request.XferAdd.FutXferRec.FutXferDayOfMonth = new FutXferDayOfMonth_Type() { Value = jxChangeComModel.FutXferDayOfMonth };
					request.XferAdd.FutXferRec.FutXferFreqUnits = new FutXferFreqUnits_Type() { Value = jxChangeComModel.FutXferFreqUnits };
					request.XferAdd.FutXferRec.FutXferFreq = new FutXferFreq_Type() { Value = jxChangeComModel.FutXferFreq };
					request.XferAdd.FutXferRec.FutXferFirstDt = new FutXferFirstDt_Type() { Value = jxChangeComModel.FutXferFirstDt };
					request.XferAdd.FutXferRec.FutXferExpDt = new FutXferExpDt_Type() { Value = jxChangeComModel.FutXferExpDt };
				}

				//////proxy credentials
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.UserName))
					transactionProxy.ClientCredentials.UserName.UserName = jxChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.Password))
					transactionProxy.ClientCredentials.UserName.Password = jxChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ";     

				//////send request
				response = transactionProxy.XferAddValidate(request);
				string xmlResponse = ObjectToXml(response);
			}
			catch (Exception ex)
			{
				var exObject = ((System.ServiceModel.FaultException<HdrFault_MType>)ex).Detail.FaultRecInfoArray[0];
				response = new XferAddValidateResponse();
				response.RsStat = new RsStat_Type() { Value = "Fail" };
			}
			return response;
		}

		/// <summary>
		/// Added by Invezza Team
		/// ModifyTransaction method to modify transaction
		/// </summary>
		/// <param name="jxChangeComModel"></param>
		/// <returns></returns>
		public XferModResponse XferMod(JXChangeComTransferModel jxChangeComModel, ref JXChangeComReqRespModel jxChangeBankTrans)
		{
			XferModResponse response = null;

			try
			{
				//request header
				XferModRequest request = new XferModRequest
				{
					MsgRqHdr = new MsgRqHdr_CType()
				};
				request.MsgRqHdr.jXchangeHdr = CreateJXChangeTransactionHeader(jxChangeComModel);
				request.AcctIdFrom = new AcctIdFrom_CType
				{
					FromAcctId = new AcctId_Type()
					{
						Value = jxChangeComModel.FromAccountId
					},
					FromAcctType = new AcctType_Type()
					{
						Value = jxChangeComModel.FromAccountType
					}
				};
				request.AcctIdTo = new AcctIdTo_CType
				{
					ToAcctId = new AcctId_Type() { Value = jxChangeComModel.ToAccountId },
					ToAcctType = new AcctType_Type()
					{ Value = jxChangeComModel.ToAccountType } //"S"
				};
				request.XferKey = new XferKey_Type
				{
					Value = jxChangeComModel.XferKey
				};
				request.XferType = new XferType_Type() { Value = jxChangeComModel.XferType };
				request.Dlt = new Dlt_Type();
				request.Custom = new Custom_CType();
				request.Ver_1 = new Ver_1_CType();
				request.Ver_2 = new Ver_2_CType();
				request.Ver_3 = new Ver_3_CType();

				request.ErrOvrRdInfoArray = new ErrOvrRd_CType[3];
				request.ErrOvrRdInfoArray[0] = new ErrOvrRd_CType();
				request.ErrOvrRdInfoArray[1] = new ErrOvrRd_CType();
				request.ErrOvrRdInfoArray[2] = new ErrOvrRd_CType();

				request.ErrOvrRdInfoArray[0].ErrCode = new ErrCode_Type();
				request.ErrOvrRdInfoArray[1].ErrCode = new ErrCode_Type();
				request.ErrOvrRdInfoArray[2].ErrCode = new ErrCode_Type();

				request.ErrOvrRdInfoArray[0].ErrCode.Value = "500019";
				request.ErrOvrRdInfoArray[1].ErrCode.Value = "500008";
				request.ErrOvrRdInfoArray[2].ErrCode.Value = "500025";

				request.ErrOvrRdInfoArray[0].Ver_1 = new Ver_1_CType();
				request.ErrOvrRdInfoArray[1].Ver_1 = new Ver_1_CType();
				request.ErrOvrRdInfoArray[2].Ver_1 = new Ver_1_CType();

				if (jxChangeComModel.XferType == "Xfer")
				{
					request.XferRec = new XferRec_CType();
					request.XferRec.Ver_1 = new Ver_1_CType();
					request.XferRec.Ver_2 = new Ver_2_CType();
					request.XferRec.Ver_3 = new Ver_3_CType();
					request.XferRec.TrnCodeCode = new TrnCodeCode_Type();
					request.XferRec.TrnCodeCode.Value = jxChangeComModel.TransactionCode;
					request.XferRec.Amt = new Amt_Type() { Value = jxChangeComModel.Amount };
					request.XferRec.RedPrinc = new RedPrinc_Type() { Value = jxChangeComModel.RedPrinc };
				}
				else
				if (jxChangeComModel.XferType == "ACH")
				{
					request.ACHXferRec = new ACHXferRec_CType();
					request.ACHXferRec.RedPrinc = new RedPrinc_Type() { Value = jxChangeComModel.RedPrinc };
					request.ACHXferRec.Ver_1 = new Ver_1_CType();
					request.ACHXferRec.Ver_2 = new Ver_2_CType();
					request.ACHXferRec.Ver_3 = new Ver_3_CType();
					request.ACHXferRec.Ver_4 = new Ver_4_CType();
					request.ACHXferRec.Ver_5 = new Ver_5_CType();
					request.ACHXferRec.XferBalType = new XferBalType_Type();
					request.ACHXferRec.ACHXferAmt = new ACHXferAmt_Type() { Value = jxChangeComModel.Amount };
					request.ACHXferRec.ACHDrName = new ACHDrName_Type() { Value = jxChangeComModel.ACHDrName };
					request.ACHXferRec.ACHNextXferDt = new ACHNextXferDt_Type() { Value = DateTime.Now };
					request.ACHXferRec.ACHDrAcctId = new ACHDrAcctId_Type() { Value = jxChangeComModel.FromAccountId };
					request.ACHXferRec.ACHDrAcctType = new ACHDrAcctType_Type() { Value = jxChangeComModel.FromAccountType };
					request.ACHXferRec.ACHCrAcctId = new ACHCrAcctId_Type() { Value = jxChangeComModel.ToAccountId };
					request.ACHXferRec.ACHCrAcctType = new ACHCrAcctType_Type() { Value = jxChangeComModel.ToAccountType };
				}
				else
				if (jxChangeComModel.XferType == "Fut")
				{
					request.FutXferRec = new FutXferRec_CType();
					request.FutXferRec.Amt = new Amt_Type() { Value = jxChangeComModel.Amount };
					request.FutXferRec.EftDescArray = new EftDescInfo_CType[] { };
					request.FutXferRec.Ver_1 = new Ver_1_CType();
					request.FutXferRec.Ver_2 = new Ver_2_CType();
					request.FutXferRec.Ver_3 = new Ver_3_CType();
					request.FutXferRec.Ver_4 = new Ver_4_CType();
					request.FutXferRec.Ver_5 = new Ver_5_CType();
					request.FutXferRec.TrnCodeCode = new TrnCodeCode_Type { Value = jxChangeComModel.TransactionCode };
					request.FutXferRec.FutXferDayOfMonth = new FutXferDayOfMonth_Type() { Value = jxChangeComModel.FutXferDayOfMonth };
					request.FutXferRec.FutXferFreqUnits = new FutXferFreqUnits_Type() { Value = jxChangeComModel.FutXferFreqUnits };
					request.FutXferRec.FutXferFreq = new FutXferFreq_Type() { Value = jxChangeComModel.FutXferFreq };
					request.FutXferRec.FutXferFirstDt = new FutXferFirstDt_Type() { Value = jxChangeComModel.FutXferFirstDt };
					request.FutXferRec.FutXferExpDt = new FutXferExpDt_Type() { Value = jxChangeComModel.FutXferExpDt };
				}
				//proxy credentials
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.UserName))
					transactionProxy.ClientCredentials.UserName.UserName = jxChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";
				if (string.IsNullOrEmpty(transactionProxy.ClientCredentials.UserName.Password))
					transactionProxy.ClientCredentials.UserName.Password = jxChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ";   

				//send request
				response = transactionProxy.XferMod(request);


			}
			catch (Exception ex)
			{
				var exObject = ((System.ServiceModel.FaultException<HdrFault_MType>)ex).Detail.FaultRecInfoArray[0];

			}
			return response;
		}

		/// <summary>
		/// Added by Invezza Team
		/// Create Transaction Header.
		/// </summary>
		/// <param name="jxChangeComModel">JXChangeComModel jxChangeComModel</param>
		/// <returns>returns jXchangeHdr_CType</returns>
		private jXchangeHdr_CType CreateJXChangeTransactionHeader(JXChangeComTransferModel jxChangeComModel)
		{
			LogTrackingID = Guid.NewGuid().ToString();
			jXchangeHdr_CType jXchangeHdr = new jXchangeHdr_CType
			{
				AuditUsrId = new AuditUsrId_Type
				{
					Value = jxChangeComModel.AuditUserId//Test";
				},
				AuditWsId = new AuditWsId_Type()
			};
			jXchangeHdr.AuditWsId.Value = jxChangeComModel.AuditWsId;//"Test";
			jXchangeHdr.Ver_1 = new Ver_1_CType();
			jXchangeHdr.jXLogTrackingId = new jXLogTrackingId_Type
			{
				Value = LogTrackingID
			};
			jXchangeHdr.Ver_2 = new Ver_2_CType();
			jXchangeHdr.InstRtId = new InstRtId_Type
			{
				Value = jxChangeComModel.InstRtId//"011001276";
			};
			jXchangeHdr.InstEnv = new InstEnv_Type
			{
				Value = jxChangeComModel.InstEnv//"TEST";
			};
			jXchangeHdr.Ver_3 = new Ver_3_CType();
			jXchangeHdr.BusCorrelId = new BusCorrelId_Type
			{
				Value = Guid.NewGuid().ToString()
			};
			jXchangeHdr.Ver_4 = new Ver_4_CType();
			jXchangeHdr.Ver_5 = new Ver_5_CType();
			jXchangeHdr.ValidConsmName = new ValidConsmName_Type
			{
				Value = jxChangeComModel.ValidConsName//"AccountingIntegrators";
			};
			jXchangeHdr.ValidConsmProd = new ValidConsmProd_Type
			{
				Value = jxChangeComModel.ValidConsProd //"AccountingIntegrators";
			};

			return jXchangeHdr;
		}

		/// <summary>
		/// Added by Invezza Team
		/// Get Check Image from JxChange.
		/// </summary>
		/// <param name="jXChangeComCheckImage">JXChangeComCheckImageModel jXChangeComCheckImage</param>
		/// <param name="jxChangeBankTrans">JXChangeComReqRespModel jxChangeBankTrans</param>
		/// <returns></returns>
		public Image.ChkImgInqResponse DownloadCheckImage(JXChangeComCheckImageModel jXChangeComCheckImage, ref JXChangeComReqRespModel jxChangeBankTrans)
        {
            Image.ChkImgInqRequest request = new Image.ChkImgInqRequest();
            EndpointIdentity spn = EndpointIdentity.CreateSpnIdentity("host/mikev-ws");
            Uri uri = new Uri(this.jxChangeComModel.EndPointURL);
            var address = new EndpointAddress(uri, spn);
            var binding = new BasicHttpBinding("ImageServiceSoapBinding");
            Image.ImageServiceClient proxy = new Image.ImageServiceClient(binding, address);
            Image.ChkImgInqResponse response = null;
            try
            {
                //request header
                Image.MsgRqHdr_CType msgRqHdr = new Image.MsgRqHdr_CType();
                request.MsgRqHdr = msgRqHdr;
                msgRqHdr.jXchangeHdr = CreateJXChangeImageHeader();
                request.ChkImgFormat = new Image.ChkImgFormat_Type();
                request.ChkImgFormat.Value = jXChangeComCheckImage.CheckImageFormat; //PNG
                request.ChkImgId = new Image.ChkImgId_Type();
                request.ChkImgId.Value = jXChangeComCheckImage.CheckImageID;//"504300400000078";
                request.ChkImgSide = new Image.ChkImgSide_Type();
                request.ChkImgSide.Value = jXChangeComCheckImage.CheckSide;//front/back/both

                //proxy credentials
                if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.UserName))
                    proxy.ClientCredentials.UserName.UserName = jXChangeComCheckImage.JXChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";             
                if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.Password))
                    proxy.ClientCredentials.UserName.Password = jXChangeComCheckImage.JXChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ"; 

                //send request
                response = proxy.ChkImgInq(request);

                //return null;
                ////These fields are used to maintain Jack Henry Log Details
                jxChangeBankTrans.Request = ObjectToXml(request);
                if (response == null)
                {
                    jxChangeBankTrans.Response = String.Format("Missing Check Image Id, CheckImageId: {0}", request.ChkImgId);
                }
                else
                    jxChangeBankTrans.Response = ObjectToXml(response);

                jxChangeBankTrans.LogTrackingId = LogTrackingID;
                jxChangeBankTrans.LogType = "Info";
            }
            catch (Exception ex)
            {
                ////This field is used to maintain Jack Henry Exception Log Details
                jxChangeBankTrans.ExceptionMessage = ObjectToXml(ex);
                jxChangeBankTrans.LogType = "Error";
            }
            return response;
        }

        /// <summary>
		/// Added by Invezza Team
		/// Create Image Header.
		/// </summary>
		/// <param name="jxChangeComModel">JXChangeComModel jxChangeComModel</param>
		/// <returns>returns jXchangeHdr_CType</returns>
		public Image.jXchangeHdr_CType CreateJXChangeImageHeader()
        {
            string NewGUID = Guid.NewGuid().ToString();
            Image.jXchangeHdr_CType jXchangeHdr = new Image.jXchangeHdr_CType();
            jXchangeHdr.AuditUsrId = new Image.AuditUsrId_Type();
            jXchangeHdr.AuditUsrId.Value = jxChangeComModel.AuditUserId;//Test";
            jXchangeHdr.AuditWsId = new Image.AuditWsId_Type();
            jXchangeHdr.AuditWsId.Value = jxChangeComModel.AuditWsId;//"Test";
            jXchangeHdr.Ver_1 = new Image.Ver_1_CType();
            jXchangeHdr.jXLogTrackingId = new Image.jXLogTrackingId_Type();
            jXchangeHdr.jXLogTrackingId.Value = NewGUID;
            jXchangeHdr.Ver_2 = new Image.Ver_2_CType();
            jXchangeHdr.InstRtId = new Image.InstRtId_Type();
            jXchangeHdr.InstRtId.Value = jxChangeComModel.InstRtId;//"011001276";
            jXchangeHdr.InstEnv = new Image.InstEnv_Type();
            jXchangeHdr.InstEnv.Value = jxChangeComModel.InstEnv;//"TEST";
            jXchangeHdr.Ver_3 = new Image.Ver_3_CType();
            jXchangeHdr.BusCorrelId = new Image.BusCorrelId_Type();
            jXchangeHdr.BusCorrelId.Value = Guid.NewGuid().ToString();
            jXchangeHdr.Ver_4 = new Image.Ver_4_CType();
            jXchangeHdr.Ver_5 = new Image.Ver_5_CType();
            jXchangeHdr.ValidConsmName = new Image.ValidConsmName_Type();
            jXchangeHdr.ValidConsmName.Value = jxChangeComModel.ValidConsName;//"AccountingIntegrators";
            jXchangeHdr.ValidConsmProd = new Image.ValidConsmProd_Type();
            jXchangeHdr.ValidConsmProd.Value = jxChangeComModel.ValidConsProd; //"AccountingIntegrators";
            return jXchangeHdr;
        }

        /// <summary>
        /// Added by Invezza Team 
        /// InitializeAccountHistSrchForCheckImage method to Initlalize request
        /// </summary>
        /// <param name="jxChangeComAcctSrchModel"></param>
        public void InitializeAccountHistSrchForCheckImage(JXChangeComAcctSrchModel jxChangeComAcctSrchModel)
        {
            ////request header
            srchMsgRqHdr.MaxRec = new MaxRec_Type
            {
                Value = jxChangeComAcctSrchModel.MaxRecords
            };
            request.SrchMsgRqHdr = srchMsgRqHdr;
            request.SrchMsgRqHdr.jXchangeHdr = CreateJXChangeInquiryHeader();

            request.InAcctId = new InquiryMaster.AccountId_CType
            {
                AcctId = new InquiryMaster.AcctId_Type()
            };
            request.InAcctId.AcctId.Value = jxChangeComAcctSrchModel.AccountId;// "1881";
            request.InAcctId.AcctType = new InquiryMaster.AcctType_Type
            {
                Value = jxChangeComAcctSrchModel.AccountType
            };

            request.StartDt = new StartDt_Type
            {
                Value = jxChangeComAcctSrchModel.StartDate// new DateTime(2000, 02, 24);
            };
            request.EndDt = new EndDt_Type
            {
                Value = jxChangeComAcctSrchModel.EndDate//new DateTime(2018, 03, 19);
            };

            //proxy credentials
            if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.UserName))
                proxy.ClientCredentials.UserName.UserName = jxChangeComAcctSrchModel.JXChangeComModel.ClientCredentialsUserName;//"AccountingIntegrators@jxtest.local";

            if (string.IsNullOrEmpty(proxy.ClientCredentials.UserName.Password))
                proxy.ClientCredentials.UserName.Password = jxChangeComAcctSrchModel.JXChangeComModel.ClientCredentialsPassword;//"!Eto4N6CjQ";

            request.ChkNumStart = new InquiryMaster.ChkNumStart_Type
            {
                Value = jxChangeComAcctSrchModel.CheckNumber
            };
            request.ChkNumEnd = new InquiryMaster.ChkNumEnd_Type
            {
                Value = jxChangeComAcctSrchModel.CheckNumber
            };
            request.HighAmt = new InquiryMaster.HighAmt_Type
            {
                Value = jxChangeComAcctSrchModel.TransactionAmount
            };
            request.LowAmt = new InquiryMaster.LowAmt_Type
            {
                Value = jxChangeComAcctSrchModel.TransactionAmount
            };

            request.Ver_1 = new InquiryMaster.Ver_1_CType();
            request.Ver_2 = new InquiryMaster.Ver_2_CType();
            request.Ver_3 = new InquiryMaster.Ver_3_CType();
            request.Ver_4 = new InquiryMaster.Ver_4_CType();
            request.SrchMsgRqHdr.Cursor = new Cursor_Type();
        }
    }
}

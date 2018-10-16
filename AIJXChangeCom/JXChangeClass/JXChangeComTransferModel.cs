// ***********************************************************************
// Assembly         : AIJXChangeCom
// Author           : Invezza Team (Sagar Shinde)
// Created          : 07-18-2018
// Last Modified By : Invezza Team (Sagar Shinde)
// Last Modified On : 07-23-2018
// Last Modified Comments : 
// ***********************************************************************
// <copyright file="JXChangeComTransactionModel.cs" company="CINC Systems">
//     Copyright ©  2018
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;

namespace AIJXChangeCom.JXChangeClass
{
	public class JXChangeComTransferModel : JXChangeComCredentialsModel
	{
		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string FromAccountId { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string FromAccountType { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string ToAccountId { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string ToAccountType { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public decimal Amount { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string XferKey { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string RedPrinc { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string TransactionCode { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string drTransactionCode { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string XferType { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string XferSrcType { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public int ACHDrRtNum { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string ACHDrName { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public decimal ACHFeeAmt { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public int FutXferDayOfMonth { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string FutXferFreqUnits { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public int FutXferFreq { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public DateTime FutXferFirstDt { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public DateTime FutXferExpDt { get; set; }
	}
}

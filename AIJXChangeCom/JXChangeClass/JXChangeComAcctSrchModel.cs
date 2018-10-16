// ***********************************************************************
// Assembly         : AIJXChangeCom
// Author           : Invezza Team (Sagar Shinde)
// Created          : 07-18-2018
// Last Modified By : Invezza Team (Sagar Shinde)
// Last Modified On : 07-23-2018
// Last Modified Comments : 
// ***********************************************************************
// <copyright file="JXChangeComModel.cs" company="CINC Systems">
//     Copyright ©  2018
// </copyright>
// <summary></summary>
// ***********************************************************************

using System;

namespace AIJXChangeCom.JXChangeClass
{
	/// <summary>
	/// This model is created by Invezza Team to set Interface Type Credentials/Connection
	/// </summary>
	public class JXChangeComAcctSrchModel 
	{
        /// <summary>
        /// This field is required to Create JXChange Request
        /// </summary>
        public JXChangeComCredentialsModel JXChangeComModel { get; set; }
        /// <summar>
        /// This field is required to Create JXChange Request
        /// </summary>
        public string AccountId { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string AccountType { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public DateTime StartDate { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public DateTime EndDate { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public int MaxRecords { get; set; }

        /// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string AccountStatus { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Request
        /// </summary>
        public decimal TransactionAmount { get; set; }

        /// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string CheckNumber { get; set; }
    }
}

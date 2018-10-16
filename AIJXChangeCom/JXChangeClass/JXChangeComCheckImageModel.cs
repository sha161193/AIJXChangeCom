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

namespace AIJXChangeCom.JXChangeClass
{
	public class JXChangeComCheckImageModel
    {
        /// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public JXChangeComCredentialsModel JXChangeComModel { get; set; }

		/// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string CheckSide { get; set; }

        /// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string CheckImageID { get; set; }

        /// <summary>
		/// This field is required to Create JXChange Request
		/// </summary>
		public string CheckImageFormat { get; set; }
    }
}

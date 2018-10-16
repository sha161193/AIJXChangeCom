// ***********************************************************************
// Assembly         : AIJXChangeCom
// Author           : Invezza Team (Sagar Shinde)
// Created          : 07-18-2018
// Last Modified By : Invezza Team (Sagar Shinde)
// Last Modified On : 07-23-2018
// Last Modified Comments : 
// ***********************************************************************
// <copyright file="JXChangeBankTrans.cs" company="CINC Systems">
//     Copyright ©  2018
// </copyright>
// <summary></summary>
// ***********************************************************************

namespace AIJXChangeCom.JXChangeClass
{
	/// <summary>
	/// Added by Invezza Team 
	/// This Class is to pass ref object to maintain logs
	/// </summary>
	public class JXChangeComReqRespModel
	{
		/// <summary>
		/// This property contains request of proxy service
		/// </summary>
		public string Request { get; set; }

		/// <summary>
		/// This property contains response of proxy service
		/// </summary>
		public string Response { get; set; }

		/// <summary>
		/// This property contains exception of proxy service
		/// </summary>
		public string ExceptionMessage { get; set; }

		/// <summary>
		/// This property contains request of proxy service
		/// </summary>
		public string LogType { get; set; }

		/// <summary>
		/// This property used to maintain Log Tracking ID into database
		/// </summary>
		public string LogTrackingId { get; set; } 
	}
}

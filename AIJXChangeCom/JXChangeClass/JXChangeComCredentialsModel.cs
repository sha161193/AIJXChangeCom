// ***********************************************************************
// Assembly         : AIJXChangeCom
// Author           : Invezza Team (Sagar Shinde)
// Created          : 07-18-2018
// Last Modified By : Invezza Team (Sagar Shinde)
// Last Modified On : 07-23-2018
// Last Modified Comments : 
// ***********************************************************************
// <copyright file="BaseRequestModel.cs" company="CINC Systems">
//     Copyright ©  2018
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;

namespace AIJXChangeCom.JXChangeClass
{
    public class JXChangeComCredentialsModel
    {
        /// <summary>
        /// This EndPointURL is used to set address of JXChange API 
        /// </summary>
        public string EndPointURL { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string AuditUserId { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string AuditWsId { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string InstEnv { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string InstRtId { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string ValidConsName { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Inquiry Header
        /// </summary>
        public string ValidConsProd { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Request
        /// </summary>
        public string ClientCredentialsUserName { get; set; }

        /// <summary>
        /// This field is required to Create JXChange Request
        /// </summary>
        public string ClientCredentialsPassword { get; set; }

    }
}

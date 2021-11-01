using System;
using System.Collections.Generic;
using System.Text;

namespace SnmpSharpNet
{
	public class Snmp:UdpTransport
	{
        /// <summary>
        /// Internal event to send result of the async request to.
        /// </summary>
#pragma warning disable CS0414
        protected event SnmpAsyncResponse _response;
#pragma warning restore CS0414
		/// <summary>
		/// Internal storage for request target information.
		/// </summary>
		protected ITarget _target;

		#region Constructor(s)

		/// <summary>
		/// Constructor
		/// </summary>
		public Snmp()
			:base(useV6: false)
		{
			_response = null;
			_target = null;
		}

		#endregion Constructor(s)
	}
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Mono.VisualStudio.TextTemplating.VSHost
{
	public interface ITransformationRunFactoryService
	{
		IProcessTransformationRunFactory GetTransformationRunFactory (string guidID);
		/// <summary>
		/// Let the service now it can shutdown
		/// </summary>
		/// <returns></returns>
		bool Shutdown ();
	}
}
